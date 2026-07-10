using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  NavGrid — 검증 AI 용 타일 기반 내비게이션 그리드 (세계 모델)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>내비게이션 노드 1칸. 통행 가능 여부 + 위험 비용.</summary>
    public struct NavNode
    {
        /// <summary>AI 가 지나갈 수 있는 칸인가 (빈 공간).</summary>
        public bool walkable;

        /// <summary>
        /// 함정 위험 + 학습 페널티가 합산된 추가 비용.
        /// A* 에서 이동 기본비용(1)에 더해집니다. 높을수록 우회 대상.
        /// </summary>
        public float dangerCost;
    }

    /// <summary>
    /// <see cref="MapData"/> 지형 + <see cref="TrapSlotRegistry"/> 의 설치 함정 +
    /// <see cref="AIMemory"/> 학습 기록을 합성해 만드는 길찾기용 스냅샷.
    /// 성향(신중함·학습력)이 비용에 반영되므로 <b>시도마다 새로 Build</b> 합니다.
    /// </summary>
    public class NavGrid
    {
        public int Width  { get; private set; }
        public int Height { get; private set; }

        private NavNode[] _nodes;

        public NavNode Get(int x, int y) => _nodes[y * Width + x];
        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        // ── 빌드 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 맵/함정/기억 상태로 내비 그리드를 생성합니다.
        /// </summary>
        /// <param name="map">지형 소스 (MapLoader.Instance.CurrentMap)</param>
        /// <param name="p">성향에서 파생된 파라미터 (위험 배율·학습 가중치)</param>
        /// <param name="memory">이번 검증의 사망 기록 (null 허용 — 첫 시도)</param>
        /// <param name="reuse">이전 시도의 그리드. 크기가 같으면 노드 배열을 재사용해
        /// 시도마다 발생하는 할당(GC)을 제거합니다. 반환값을 다음 호출에 다시 넘길 것.</param>
        public static NavGrid Build(MapData map, in AIBehaviorParams p, AIMemory memory,
                                    NavGrid reuse = null)
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

            // 1) 지형: Empty 만 통행 가능. (슬롯=지형 불변식 — 함정이 설치된 칸은
            //    솔리드 지형이므로 Empty 가 아니게 관리되는지 여부는 MapLoader 쪽 규약을 따름)
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                g._nodes[y * g.Width + x] = new NavNode
                {
                    walkable   = map.GetTile(x, y) == TileType.Empty,
                    dangerCost = memory?.GetPenalty(x, y, p.memoryPenaltyWeight) ?? 0f,
                };
            }

            // 2) 설치된 함정 → 위험 비용. 함정 칸 자체는 솔리드(밟는 지형)이므로
            //    위험은 함정 '위 칸'(밟고 지나가는 자리)에 부여한다.
            //    TODO(함정별 위험 영역): ArrowShooter 사선, DropHammer 이동 경로 등
            //    함정마다 위험 영역이 다름 — ITrap 에 위험 셀 열거 API 를 추가해 대체할 것.
            foreach (var slot in TrapSlotRegistry.GetOccupied())
            {
                var trap = slot.OccupiedBy != null ? slot.OccupiedBy.GetComponent<ITrap>() : null;
                if (trap == null) continue;

                int x = slot.GridX, y = slot.GridY + 1;
                if (!g.InBounds(x, y)) continue;

                g._nodes[y * g.Width + x].dangerCost
                    += trap.GetNodeCostWeight() * p.dangerCostMultiplier;
            }

            // TODO(플랫포머 이동 모델): 2D 플랫포머는 상하좌우 인접 이동이 아니라
            // 점프 아크/낙하로 연결되는 칸이 있음. 노드 간 '점프 링크' 그래프 생성이
            // 실제 A* 품질의 핵심 — 구체 기획 확정 후 여기서 링크를 사전 계산할 것.

            return g;
        }
    }
}
