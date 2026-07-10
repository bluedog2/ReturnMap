using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AStarPathPlanner — A* 경로 탐색 (골격)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 타일 그리드 A* 구현체의 <b>골격(스텁)</b>. 아직 탐색을 수행하지 않고 null 을 반환합니다.
    ///
    /// <para>TODO(A* 본체) 구현 계획:
    /// 1) 오픈 리스트(우선순위 큐) + 클로즈드 셋으로 표준 A*.
    /// 2) 이동 비용 = 기본 1 + <see cref="NavNode.dangerCost"/>
    ///    (위험 배율·학습 페널티는 NavGrid.Build 에서 이미 반영됨 — 여기서 재가공 금지).
    /// 3) 휴리스틱 = 맨해튼 거리.
    /// 4) 이웃 확장은 당장은 상하좌우 4방향, 이후 NavGrid 의 점프 링크로 대체.
    /// 5) 성향 활용 예: p.trapWaitTolerance 가 크면 위험 노드를 '대기 후 통과' 후보로
    ///    남기는 등 성향별 변형은 이 클래스를 상속/교체(IPathPlanner)로 다양화.</para>
    /// </summary>
    public class AStarPathPlanner : IPathPlanner
    {
        public List<GridCoord> FindPath(NavGrid grid, GridCoord start, GridCoord goal, in AIBehaviorParams p)
        {
            // TODO(A* 본체): 위 구현 계획 참고. 지금은 미구현 — 경로 없음으로 처리.
            Debug.LogWarning("[AStarPathPlanner] A* 미구현 스텁 — 경로 없음(null) 반환");
            return null;
        }
    }
}
