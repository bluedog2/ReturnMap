using System.Collections.Generic;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  IPathPlanner — 검증 AI 경로 탐색 전략 인터페이스
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 경로 탐색 알고리즘의 교체 지점.
    /// 성향 스텟으로 알고리즘 자체를 다양화할 때(예: 그리디 직진형 vs 완전 탐색형)
    /// 이 인터페이스의 구현체를 갈아끼웁니다. 기본 구현은 <see cref="AStarPathPlanner"/>.
    /// </summary>
    public interface IPathPlanner
    {
        /// <summary>
        /// start → goal 경로를 탐색합니다. 반환되는 경로는 <b>grounded 칸만</b> 포함하며,
        /// 점프·낙하 링크로 인접하지 않은 칸이 연속으로 나올 수 있습니다(소비자가 처리).
        /// </summary>
        /// <param name="profile">개체의 이동 능력(점프 높이/거리, 낙하 허용치, 지름길 중독 등).</param>
        /// <returns>셀 좌표 웨이포인트 목록. 경로가 없으면 null.</returns>
        List<GridCoord> FindPath(NavGrid grid, GridCoord start, GridCoord goal, in AIBehaviorParams p,
                                 in TraversalProfile profile);
    }
}
