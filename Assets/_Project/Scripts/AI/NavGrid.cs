using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  NavGrid — 검증 AI 용 타일 기반 내비게이션 그리드 (중력 인지 세계 모델)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 내비게이션 노드 1칸. <b>지형(solid) / 설 자리(grounded) / 위험 비용</b>을 분리해
    /// 보관합니다. A* 는 <see cref="grounded"/> 인 칸만 노드로 취급하고, 공중 칸은
    /// 점프·낙하 링크의 통과 판정에만 쓰입니다(그 자체로 서 있을 수 없음).
    /// </summary>
    public struct NavNode
    {
        /// <summary>지형 타일(Empty 아님) 또는 설치된 함정이 점유한 칸인가 (밟고 서는 자리 / 통과 불가).</summary>
        public bool solid;

        /// <summary>!solid && 바로 아래가 solid — 캐릭터가 실제로 서 있을 수 있는 자리 (A* 노드 후보).</summary>
        public bool grounded;

        /// <summary>
        /// 함정 위험 + 학습 페널티가 합산된 추가 비용.
        /// A* 에서 이동 기본비용(1)에 더해집니다. 높을수록 우회 대상.
        /// </summary>
        public float dangerCost;
    }

    /// <summary>
    /// <see cref="MapData"/> 지형 + <see cref="TrapSlotRegistry"/> 의 설치 함정 +
    /// <see cref="AIMemory"/> 학습 기록을 합성해 만드는 길찾기용 스냅샷.
    /// 성향(신중함·학습력)과 개체 이동 능력(<see cref="TraversalProfile"/>)이 비용/지형
    /// 인식에 반영되므로 <b>시도마다 새로 Build</b> 합니다.
    ///
    /// <para><b>TODO(성능)</b>: 10마리가 각자 그리드를 새로 만들면 낭비이지만, dangerCost 에
    /// 개체별 <see cref="AIMemory"/> 학습 페널티가 섞여 있어 프로파일(<see cref="TraversalProfile.CacheKey"/>)
    /// 만으로는 그리드를 공유할 수 없습니다. 학습 페널티를 그리드에서 분리해 A* 비용 계산 시점에
    /// 별도로 조회하도록 리팩터링하면 프로파일 단위 캐시가 가능해집니다 — 성능 문제가 실측되면
    /// 그때 분리할 것 (현재는 개체별 <c>reuse</c> 배열 재사용만으로 GC 압박을 억제).</para>
    /// </summary>
    public class NavGrid
    {
        public int Width  { get; private set; }
        public int Height { get; private set; }

        private NavNode[] _nodes;

        public NavNode Get(int x, int y) => _nodes[y * Width + x];
        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        /// <summary>해당 칸이 서 있을 수 있는 자리인가. 범위 밖은 false.</summary>
        public bool IsGrounded(int x, int y) => InBounds(x, y) && _nodes[y * Width + x].grounded;

        /// <summary>해당 칸이 솔리드(통과 불가)인가. 범위 밖은 false — 맵 밖 경로가 생기지 않도록
        /// 호출부가 InBounds 검사를 우선해야 한다(여기서 true 로 취급하지 않는다).</summary>
        public bool IsSolid(int x, int y) => InBounds(x, y) && _nodes[y * Width + x].solid;

        // ── 빌드 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 맵/함정/기억/이동 능력 상태로 내비 그리드를 생성합니다.
        /// </summary>
        /// <param name="map">지형 소스 (MapLoader.Instance.CurrentMap)</param>
        /// <param name="p">성향에서 파생된 파라미터 (위험 배율·학습 가중치)</param>
        /// <param name="profile">개체의 이동 능력 프로파일 (면역 판정에 사용 — 점프/낙하 스펙 자체는
        /// A* 확장 단계에서 소비되고, 여기서는 면역 반영에만 쓰인다)</param>
        /// <param name="memory">이번 검증의 사망 기록 (null 허용 — 첫 시도)</param>
        /// <param name="reuse">이전 시도의 그리드. 크기가 같으면 노드 배열을 재사용해
        /// 시도마다 발생하는 할당(GC)을 제거합니다. 반환값을 다음 호출에 다시 넘길 것.</param>
        public static NavGrid Build(MapData map, in AIBehaviorParams p, in TraversalProfile profile,
                                    AIMemory memory, NavGrid reuse = null)
        {
            NavGrid g;
            if (reuse != null && reuse.Width == map.width && reuse.Height == map.height)
            {
                g = reuse; // 같은 크기 — 배열 재사용 (아래 루프가 전 칸을 덮어씀)
            }
            else
            {
                g = new NavGrid
                {
                    Width  = map.width,
                    Height = map.height,
                    _nodes = new NavNode[map.width * map.height],
                };
            }

            // 1) 지형 solid + 학습 페널티 초기화. grounded 는 함정 solid 마킹이 끝난 뒤
            //    2패스에서 계산한다(함정이 점유한 칸도 solid 로 반영되어야 그 위 칸이
            //    grounded 로 잡히기 때문 — "슬롯=지형" 불변식).
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                g._nodes[y * g.Width + x] = new NavNode
                {
                    solid      = map.GetTile(x, y) != TileType.Empty,
                    grounded   = false,
                    dangerCost = memory?.GetPenalty(x, y, p.memoryPenaltyWeight) ?? 0f,
                };
            }

            // 2) 설치된 함정 칸 → 솔리드 마킹 (슬롯=지형 불변식).
            //    이동형 함정(DropHammer 등, ActsAsSolidTile=false)은 opt-out.
            foreach (var slot in TrapSlotRegistry.GetOccupied())
            {
                TrapBase trap = slot.OccupiedBy != null ? slot.OccupiedBy.GetComponent<TrapBase>() : null;
                if (trap == null || !trap.ActsAsSolidTile) continue;
                if (!g.InBounds(slot.GridX, slot.GridY)) continue;

                g._nodes[slot.GridY * g.Width + slot.GridX].solid = true;
            }

            // 3) grounded 계산 — solid 확정 후 1패스. y==0 은 맵 바닥 밖이라 grounded 아님.
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                int  idx         = y * g.Width + x;
                bool solidBelow  = y > 0 && g._nodes[idx - g.Width].solid;
                g._nodes[idx].grounded = !g._nodes[idx].solid && solidBelow;
            }

            // 4) 설치된 함정의 위험 비용 — anchor 별 위험 칸(밟고 지나가는 바깥쪽 1칸)에 부여.
            //    Floor→(x,y+1) / Ceiling→(x,y-1) / LeftWall→(x+1,y) / RightWall→(x-1,y)
            //    (TrapBase.OutwardOf 와 동일 규약 — 중복 구현하지 않고 그대로 재사용)
            //    면역 반영: profile.Immunities 에 포함된 피해 타입의 함정은 위험 비용 0
            //    (예: 둥글둥글이 가시밭을 최단거리로 지나는 것이 올바른 행동).
            foreach (var slot in TrapSlotRegistry.GetOccupied())
            {
                TrapBase trap = slot.OccupiedBy != null ? slot.OccupiedBy.GetComponent<TrapBase>() : null;
                if (trap == null) continue;
                if ((trap.DamageType & profile.Immunities) != DamageType.None) continue;

                Vector2Int outward = TrapBase.OutwardOf(slot.Anchor);
                int x = slot.GridX + outward.x;
                int y = slot.GridY + outward.y;
                if (!g.InBounds(x, y)) continue;

                g._nodes[y * g.Width + x].dangerCost
                    += trap.GetNodeCostWeight() * p.dangerCostMultiplier;
            }

            return g;
        }

        // ── 착지점 탐색 ───────────────────────────────────────────────────────

        /// <summary>
        /// (x, startY) 에서 아래로 스캔해 첫 grounded 칸을 찾습니다. spawn/goal 을 실제
        /// 설 수 있는 자리로 스냅하거나, 낙하 링크의 착지 지점을 찾는 데 공용으로 씁니다.
        /// </summary>
        /// <param name="maxFall">허용 최대 낙차(칸). 이보다 멀리 내려가야 착지점이 있으면 실패.</param>
        /// <param name="landY">찾은 착지 y 좌표 (실패 시 startY).</param>
        public bool TryFindLanding(int x, int startY, int maxFall, out int landY)
        {
            landY = startY;
            if (!InBounds(x, startY)) return false;

            // 시작 칸 자체가 이미 grounded 면 낙차 0 으로 즉시 성공.
            for (int y = startY; y >= 0; y--)
            {
                if (!InBounds(x, y)) break;

                int fall = startY - y;
                if (fall > maxFall) return false;

                if (IsGrounded(x, y))
                {
                    landY = y;
                    return true;
                }

                // solid 칸 위에서 시작했는데 grounded 가 아니라면(=solid 자체) 더 내려갈 수 없음.
                if (IsSolid(x, y)) return false;
            }

            return false;
        }
    }
}
