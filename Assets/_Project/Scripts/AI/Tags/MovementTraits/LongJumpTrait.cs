using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  LongJumpTrait — 멀리뛰기(M-09): 점프 높이 낮고 가로 4칸 주파
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 모든 이동을 낮고 넓은 아크로 표현한다(점프군(GROUP_JUMP) 배타 그룹). 실제 가로
    /// 4칸 도약은 <see cref="AStarPathPlanner"/> 의 점프 링크가 ModifyTraversal 로 넓힌
    /// MaxJumpDistance 를 소비해 만든다 — 이 클래스는 그 링크의 아크 높이만 보정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/멀리뛰기 (Long Jump)", fileName = "Trait_LongJump")]
    public class LongJumpTrait : MovementTrait
    {
        public override void ModifyArc(ref ArcSpec arc)
        {
            arc.useArc = true;
            arc.height = Mathf.Max(arc.height, 0.4f); // 덮어쓰기 금지 — 상향만(규약 참고)
        }

        /// <summary>가로 4칸까지 점프로 건널 수 있게 한다 (점프 높이·낙하는 손대지 않음).</summary>
        public override void ModifyTraversal(ref int maxJumpHeight, ref int maxJumpDistance, ref int maxFallHeight)
        {
            maxJumpDistance = Mathf.Max(maxJumpDistance, 4);
        }
    }
}
