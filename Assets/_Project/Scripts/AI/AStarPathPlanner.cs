using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AStarPathPlanner — A* 경로 탐색
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 타일 그리드 표준 A* 구현체.
    /// <para>
    /// 이동 비용 = 기본 1 + <see cref="NavNode.dangerCost"/>
    /// (위험 배율·학습 페널티는 NavGrid.Build 에서 이미 반영됨 — 여기서 재가공하지 않음).
    /// 휴리스틱 = 맨해튼 거리, 이웃 확장 = 상하좌우 4방향.
    /// </para>
    /// <para><b>리소스 정책</b>: 탐색 버퍼(g비용/부모/상태)와 결과 리스트는 멤버로
    /// 재사용해 시도 루프에서 반복 할당이 생기지 않게 합니다. 반환된 경로 리스트는
    /// <b>다음 FindPath 호출 시 덮어써지므로</b> 호출자가 보관하려면 복사할 것.</para>
    /// <para>TODO(플랫포머 이동 모델): 4방향 이동은 공중을 자유 통행하는 근사 모델.
    /// NavGrid 의 점프 링크가 생기면 이웃 확장을 링크 기반으로 대체할 것.</para>
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

        public List<GridCoord> FindPath(NavGrid grid, GridCoord start, GridCoord goal, in AIBehaviorParams p)
        {
            int w = grid.Width, h = grid.Height, count = w * h;

            if (!grid.InBounds(start.x, start.y) || !grid.InBounds(goal.x, goal.y))
            {
                Debug.LogWarning($"[AStarPathPlanner] 시작/골 좌표가 그리드 밖: {start} → {goal}");
                return null;
            }
            if (!grid.Get(start.x, start.y).walkable || !grid.Get(goal.x, goal.y).walkable)
            {
                Debug.LogWarning($"[AStarPathPlanner] 시작/골 칸이 통행 불가: {start} → {goal}");
                return null;
            }

            EnsureBuffers(count);

            int startIdx = start.y * w + start.x;
            int goalIdx  = goal.y  * w + goal.x;

            _gCost[startIdx] = 0f;
            _fCost[startIdx] = Manhattan(start.x, start.y, goal.x, goal.y);
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
                ExpandNeighbor(grid, cx + 1, cy, current, goal, w);
                ExpandNeighbor(grid, cx - 1, cy, current, goal, w);
                ExpandNeighbor(grid, cx, cy + 1, current, goal, w);
                ExpandNeighbor(grid, cx, cy - 1, current, goal, w);
            }

            return null; // 골까지 이어지는 경로 없음
        }

        // ── 내부 ──────────────────────────────────────────────────────────────

        private void ExpandNeighbor(NavGrid grid, int nx, int ny, int current, GridCoord goal, int w)
        {
            if (!grid.InBounds(nx, ny)) return;

            int idx = ny * w + nx;
            if (_state[idx] == Closed) return;

            var node = grid.Get(nx, ny);
            if (!node.walkable) return;

            // 이동 비용: 기본 1 + 위험/학습 비용 (NavGrid.Build 에서 합산 완료)
            float g = _gCost[current] + 1f + node.dangerCost;
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
