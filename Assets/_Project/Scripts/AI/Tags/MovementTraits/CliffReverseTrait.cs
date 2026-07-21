using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CliffReverseTrait — 안전제일(M-04): 낭떠러지 감지 시 반전
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>1차 방어(플래너)</b>: <see cref="ModifyTraversal"/> 로 <see cref="TraversalProfile.MaxFallHeight"/>
    /// 를 1로 클램프해 A* 가 애초에 낙차 2+ 링크를 생성하지 않게 한다(1칸 내려서기는
    /// 허용). 경로가 없으면 러너가 영구히 멈춰 검증 시간 초과 = 방어 성공 — "낙사 메타
    /// 차단"이라는 태그 기획 그대로다.
    /// <para><b>2차 방어(실행부 안전망)</b>: 그럼에도(다른 이동 태그 조합·프로파일 예외
    /// 등으로) 2칸 이상 하강하는 세그먼트가 실행에 들어오면 이동을 거부(veto)하고 2칸
    /// 후퇴한다. <see cref="VerificationAgent"/> 가 같은 지점에서 veto 가 3연속 발생하면
    /// 무한 왕복 방지를 위해 강행 통과시킨다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/안전제일 (Cliff Reverse)", fileName = "Trait_CliffReverse")]
    public class CliffReverseTrait : MovementTrait
    {
        public override TraitAction OnBeforeMove(ref TraitState s, in MoveQuery q)
        {
            if (q.IsDescending && q.DropHeight >= 2)
                return new TraitAction { vetoMove = true, stepBackCells = 2 };

            return TraitAction.None;
        }

        /// <summary>낙하 허용치를 1칸으로 하향 클램프 — RecklessDrop(int.MaxValue)과 동시
        /// 부여돼도 Mathf.Min 이므로 항상 안전제일이 이긴다(<see cref="TraversalProfile.From"/> 참고).</summary>
        public override void ModifyTraversal(ref int maxJumpHeight, ref int maxJumpDistance, ref int maxFallHeight)
        {
            maxFallHeight = Mathf.Min(maxFallHeight, 1);
        }
    }
}
