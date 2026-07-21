using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  HighJumpTrait — 높이뛰기(M-08): 가로 짧고 세로 3칸 비상
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 모든 이동을 높은 아크(세로 3칸 느낌)로 도약하듯 표현한다. 점프군(GROUP_JUMP)
    /// 배타 그룹이라 멀리뛰기와 동시에 부여되지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/높이뛰기 (High Jump)", fileName = "Trait_HighJump")]
    public class HighJumpTrait : MovementTrait
    {
        public override void ModifyArc(ref ArcSpec arc)
        {
            arc.useArc = true;
            arc.height = Mathf.Max(arc.height, 1.5f); // 덮어쓰기 금지 — 상향만(규약 참고)
        }

        /// <summary>세로 3칸까지 점프로 오를 수 있게 한다 (가로 사거리·낙하는 손대지 않음).</summary>
        public override void ModifyTraversal(ref int maxJumpHeight, ref int maxJumpDistance, ref int maxFallHeight)
        {
            maxJumpHeight = Mathf.Max(maxJumpHeight, 3);
        }
    }
}
