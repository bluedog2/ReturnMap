using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AStarPathPlanner — A* 경로 탐색 (중력 인지 · 링크 기반)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 타일 그리드 A* 구현체. <b>핵심 원칙: 노드는 grounded 칸만.</b> 공중 칸은 노드가
    /// 아니며, 점프/낙하는 grounded → grounded 를 잇는 "링크"로 취급합니다. 이것이
    /// 과거 4방향 확장이 공중을 사다리처럼 오르내리던 문제의 근본 해결입니다.
    /// <para>
    /// 이웃 확장 3종:
    /// <list type="bullet">
    ///   <item><b>걷기</b>: 좌우 인접 칸이 grounded 면 비용 1 + dangerCost.</item>
    ///   <item><b>낙하</b>: 좌우 인접 칸이 공중이면 <see cref="NavGrid.TryFindLanding"/> 으로
    ///         착지점을 찾아 링크. 비용 = 1 + 낙차*0.5(지름길 중독은 0) + 착지칸 dangerCost.</item>
    ///   <item><b>점프</b>: <see cref="TraversalProfile.MaxJumpDistance"/>(가로) × <see cref="TraversalProfile.MaxJumpHeight"/>
    ///         (상승) ~ <see cref="TraversalProfile.MaxFallHeight"/>(하강, dx≥2 만 — dx=1 하강은
    ///         낙하 링크가 이미 커버) 범위의 grounded 후보에 대해 ㄱ자 3구간 통과 검사(벽 뚫기
    ///         방지) 후 링크. 비용 = 1 + dx*0.5 + 수직분*0.5(상승은 그대로, 하강은 낙하 링크와
    ///         동일하게 지름길 중독이면 0) + 도착칸 dangerCost (걷기보다 비싸 불필요한 점프 억제).</item>
    /// </list>
    /// </para>
    /// <para>휴리스틱 = 맨해튼 거리.</para>
    /// <para><b>리소스 정책</b>: 탐색 버퍼(g비용/부모/상태)와 결과 리스트는 멤버로
    /// 재사용해 시도 루프에서 반복 할당이 생기지 않게 합니다. 반환된 경로 리스트는
    /// <b>다음 FindPath 호출 시 덮어써지므로</b> 호출자가 보관하려면 복사할 것.</para>
    /// <para><b>결과 경로의 형태</b>: grounded 칸들의 목록이며, 점프·낙하 링크로 인해
    /// 인접하지 않은 칸이 연속으로 나올 수 있습니다 — 소비자(<see cref="VerificationAgent"/>)가
    /// From→To 다중 칸 이동으로 처리합니다.</para>
    /// </summary>
    public class AStarPathPlanner : IPathPlanner
    {
        // 노드 탐색 상태 (byte 로 오픈/클로즈드 겸용)
        private const byte Unvisited = 0;
        private const byte Open      = 1;
        private const byte Closed    = 2;

        // ── 재사용 버퍼 — 그리드 크기가 같으면 시도 간 재할당 없음 ───────────

        private float[] _gCost;    // 시작점부터의 누적 비용
        private float[] _fCost;    // g + 휴리스틱
        private int[]   _parent;   // 경로 복원용 부모 인덱스 (-1 = 없음)
        private byte[]  _state;    // Unvisited/Open/Closed
        private readonly List<int>       _openList = new List<int>(128);
        private readonly List<GridCoord> _path     = new List<GridCoord>(64);

        public List<GridCoord> FindPath(NavGrid grid, GridCoord start, GridCoord goal, in AIBehaviorParams p,
                                        in TraversalProfile profile)
        {
            int w = grid.Width, h = grid.Height, count = w * h;

            if (!grid.InBounds(start.x, start.y) || !grid.InBounds(goal.x, goal.y))
            {
                Debug.LogWarning($"[AStarPathPlanner] 시작/골 좌표가 그리드 밖: {start} → {goal}");
                return null;
            }

            // ── 시작/골 정규화 — grounded 가 아니면 아래 착지점으로 스냅 ──────────
            // 스폰 지점이 공중일 수 있고(맵 제작 편의), goal 이 정확히 발판이 아닐 수도
            // 있다. 규약: 원래 goal 이 공중이면 그 아래 착지점 도달 = 골 도달로 간주한다
            // (낙차 제한 없이 스냅 — 방어 판정을 낙하 가능 여부로 좌우하지 않기 위함).
            if (!TrySnapToGround(grid, start, out GridCoord snappedStart))
            {
                Debug.LogWarning($"[AStarPathPlanner] 시작 지점이 고립됨(착지 불가): {start}");
                return null;
            }
            if (!TrySnapToGround(grid, goal, out GridCoord snappedGoal))
            {
                Debug.LogWarning($"[AStarPathPlanner] 골 지점이 고립됨(착지 불가): {goal}");
                return null;
            }

            EnsureBuffers(count);

            int startIdx = snappedStart.y * w + snappedStart.x;
            int goalIdx  = snappedGoal.y  * w + snappedGoal.x;

            _gCost[startIdx] = 0f;
            _fCost[startIdx] = Manhattan(snappedStart.x, snappedStart.y, snappedGoal.x, snappedGoal.y);
            _state[startIdx] = Open;
            _openList.Clear();
            _openList.Add(startIdx);

            while (_openList.Count > 0)
            {
                // 오픈 리스트에서 f 최소 노드 선택 (그리드가 작아 선형 탐색으로 충분.
                // 맵이 커지면 이진 힙으로 교체 지점)
                int best = 0;
                for (int i = 1; i < _openList.Count; i++)
                    if (_fCost[_openList[i]] < _fCost[_openList[best]]) best = i;

                int current = _openList[best];
                _openList[best] = _openList[_openList.Count - 1];
                _openList.RemoveAt(_openList.Count - 1);

                if (current == goalIdx)
                    return ReconstructPath(current, w);

                _state[current] = Closed;

                int cx = current % w, cy = current / w;
                ExpandNeighbors(grid, in profile, cx, cy, current, snappedGoal, w);
            }

            return null; // 골까지 이어지는 경로 없음
        }

        // ── 내부 — 시작/골 스냅 ──────────────────────────────────────────────

        /// <summary>이미 grounded 면 그대로, 아니면 낙차 제한 없이 아래 착지점으로 스냅.</summary>
        private static bool TrySnapToGround(NavGrid grid, GridCoord cell, out GridCoord snapped)
        {
            if (grid.IsGrounded(cell.x, cell.y))
            {
                snapped = cell;
                return true;
            }

            if (grid.TryFindLanding(cell.x, cell.y, int.MaxValue, out int landY))
            {
                snapped = new GridCoord(cell.x, landY);
                return true;
            }

            snapped = cell;
            return false;
        }

        // ── 내부 — 이웃 확장 (걷기 / 낙하 / 점프) ────────────────────────────

        private void ExpandNeighbors(NavGrid grid, in TraversalProfile profile, int cx, int cy,
                                     int current, GridCoord goal, int w)
        {
            // 1) 걷기 — 좌우 인접 grounded 칸
            ExpandWalk(grid, cx + 1, cy, current, goal, w);
            ExpandWalk(grid, cx - 1, cy, current, goal, w);

            // 2) 낙하 — 좌우 인접이 공중이면 착지점까지 링크
            ExpandFall(grid, in profile, cx + 1, cy, current, goal, w);
            ExpandFall(grid, in profile, cx - 1, cy, current, goal, w);

            // 3) 점프 — dx: 1..MaxJumpDistance, dy: -maxDescend..MaxJumpHeight.
            //    dy<0(하강 점프)은 dx≥2 원거리 도약만 대상으로 한다 — dx=1 하강은 위의
            //    낙하 링크(2)가 이미 커버하므로 여기서 중복 생성하지 않는다.
            //    maxDescend 는 RecklessDrop(MaxFallHeight=int.MaxValue) 폭주를 막기 위해
            //    그리드 높이로 클램프한다.
            int maxDescend = Mathf.Min(profile.MaxFallHeight, grid.Height);
            for (int dx = 1; dx <= profile.MaxJumpDistance; dx++)
            for (int dy = -maxDescend; dy <= profile.MaxJumpHeight; dy++)
            {
                if (dx == 1 && dy == 0) continue; // 걷기와 완전히 동일한 링크라 스킵
                if (dx == 1 && dy < 0)  continue; // 낙하 링크(ExpandFall)와 중복 방지
                ExpandJump(grid, in profile, cx, cy, cx + dx, cy + dy, current, goal, w);
                ExpandJump(grid, in profile, cx, cy, cx - dx, cy + dy, current, goal, w);
            }
        }

        private void ExpandWalk(NavGrid grid, int nx, int ny, int current, GridCoord goal, int w)
        {
            if (!grid.InBounds(nx, ny) || !grid.IsGrounded(nx, ny)) return;

            int idx = ny * w + nx;
            if (_state[idx] == Closed) return;

            float cost = 1f + grid.Get(nx, ny).dangerCost;
            RelaxNeighbor(idx, current, cost, nx, ny, goal, w);
        }

        private void ExpandFall(NavGrid grid, in TraversalProfile profile, int nx, int cy,
                                int current, GridCoord goal, int w)
        {
            if (!grid.InBounds(nx, cy)) return;
            if (grid.IsSolid(nx, cy)) return;     // 옆이 막혀 있으면 낙하 불가
            if (grid.IsGrounded(nx, cy)) return;  // 이미 grounded 면 걷기 링크가 담당

            if (!grid.TryFindLanding(nx, cy, profile.MaxFallHeight, out int landY)) return;

            int idx = landY * w + nx;
            if (_state[idx] == Closed) return;

            int   fallCells = cy - landY;
            float cost = 1f + fallCells * (profile.RecklessDrop ? 0f : 0.5f) + grid.Get(nx, landY).dangerCost;

            RelaxNeighbor(idx, current, cost, nx, landY, goal, w);
        }

        private void ExpandJump(NavGrid grid, in TraversalProfile profile, int cx, int cy, int tx, int ty,
                                int current, GridCoord goal, int w)
        {
            if (!grid.InBounds(tx, ty) || !grid.IsGrounded(tx, ty)) return;

            int idx = ty * w + tx;
            if (_state[idx] == Closed) return;

            if (!IsJumpPathClear(grid, cx, cy, tx, ty)) return;

            int dx = Mathf.Abs(tx - cx);
            int dy = ty - cy; // 상승(+)/평행(0)/하강(-) — 호출부가 -maxDescend..MaxJumpHeight 범위로 넘김

            // 하강분은 낙하 링크(ExpandFall)와 동일한 계수 규칙을 적용(지름길 중독이면 0).
            float verticalCost = dy >= 0 ? dy * 0.5f : Mathf.Abs(dy) * (profile.RecklessDrop ? 0f : 0.5f);
            float cost = 1f + dx * 0.5f + verticalCost + grid.Get(tx, ty).dangerCost;

            RelaxNeighbor(idx, current, cost, tx, ty, goal, w);
        }

        /// <summary>
        /// 점프 궤적이 지형을 뚫지 않는지 검사하는 안전한 근사(ㄱ자 3구간):
        /// (1) 출발 열에서 정점 높이까지 수직, (2) 정점 높이에서 도착 열까지 수평,
        /// (3) 도착 열에서 도착 높이까지 수직 하강. 한 칸이라도 solid 면 무효.
        /// <para>정점(apexY = Max(cy, ty) + 1)이 출발/도착 두 높이보다 항상 높으므로 하강
        /// 점프(ty &lt; cy)에도 그대로 유효하다 — 별도 분기 불필요.</para>
        /// </summary>
        private static bool IsJumpPathClear(NavGrid grid, int cx, int cy, int tx, int ty)
        {
            int apexY = Mathf.Max(cy, ty) + 1;

            // (1) 출발점 머리 위 수직 구간: cx 열, cy+1 ~ apexY
            for (int y = cy + 1; y <= apexY; y++)
                if (!grid.InBounds(cx, y) || grid.IsSolid(cx, y)) return false;

            // (2) 정점 높이 수평 구간: apexY 행, cx ~ tx
            int xLo = Mathf.Min(cx, tx), xHi = Mathf.Max(cx, tx);
            for (int x = xLo; x <= xHi; x++)
                if (!grid.InBounds(x, apexY) || grid.IsSolid(x, apexY)) return false;

            // (3) 도착점 수직 하강 구간: tx 열, apexY-1 ~ ty
            for (int y = apexY - 1; y >= ty; y--)
                if (!grid.InBounds(tx, y) || grid.IsSolid(tx, y)) return false;

            return true;
        }

        private void RelaxNeighbor(int idx, int current, float stepCost, int nx, int ny, GridCoord goal, int w)
        {
            if (_state[idx] == Closed) return;

            float g = _gCost[current] + stepCost;
            if (_state[idx] == Open && g >= _gCost[idx]) return;

            _gCost[idx]  = g;
            _fCost[idx]  = g + Manhattan(nx, ny, goal.x, goal.y);
            _parent[idx] = current;

            if (_state[idx] != Open)
            {
                _state[idx] = Open;
                _openList.Add(idx);
            }
        }

        private List<GridCoord> ReconstructPath(int goalIdx, int w)
        {
            _path.Clear();
            for (int idx = goalIdx; idx != -1; idx = _parent[idx])
                _path.Add(new GridCoord(idx % w, idx / w));
            _path.Reverse();
            return _path;
        }

        private void EnsureBuffers(int count)
        {
            if (_gCost == null || _gCost.Length != count)
            {
                _gCost  = new float[count];
                _fCost  = new float[count];
                _parent = new int[count];
                _state  = new byte[count];
            }

            for (int i = 0; i < count; i++)
            {
                _gCost[i]  = float.MaxValue;
                _fCost[i]  = float.MaxValue;
                _parent[i] = -1;
                _state[i]  = Unvisited;
            }
        }

        private static float Manhattan(int x0, int y0, int x1, int y1)
            => Mathf.Abs(x1 - x0) + Mathf.Abs(y1 - y0);
    }
}
