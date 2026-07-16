using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  LongJumpTrait — 멀리뛰기(M-09): 점프 높이 낮고 가로 4칸 주파
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 모든 이동을 낮고 넓은 아크로 표현한다. horizontalCells 는 현재 연출 메타데이터로만
    /// 사용되며(실제 4칸 도약은 점프 링크가 도입되는 5단계 과제), 이번 단계에서는 셀 1칸
    /// 거리 이동의 포물선 형태에만 반영된다. 점프군(GROUP_JUMP) 배타 그룹.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/멀리뛰기 (Long Jump)", fileName = "Trait_LongJump")]
    public class LongJumpTrait : MovementTrait
    {
        public override void ModifyArc(ref ArcSpec arc)
        {
            arc.useArc          = true;
            arc.height          = 0.4f;
            arc.horizontalCells = 4;
        }
    }
}
