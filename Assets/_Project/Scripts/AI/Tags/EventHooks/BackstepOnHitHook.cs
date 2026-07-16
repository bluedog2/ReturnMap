using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BackstepOnHitHook — 백스텝(S-09): 함정 피격 시 뒤로 2칸 강제 반동
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 피격 시 <see cref="VerificationAgent.RequestStepBack"/> 을 호출해 다음 셀 루프
    /// 처리 시점에 안전하게 2칸 후퇴하도록 요청한다 (코루틴을 물리 콜백에서 직접 건드리지
    /// 않고 플래그로 위임).
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Hook/백스텝 (Backstep On Hit)", fileName = "Hook_BackstepOnHit")]
    public class BackstepOnHitHook : TagEventHook
    {
        [SerializeField, Min(1)]
        [Tooltip("피격 시 후퇴할 칸수.")]
        private int stepBackCells = 2;

        public override void OnTrapHit(AgentContext agent, TrapBase trap)
        {
            if (agent == null) return;
            agent.GetComponent<VerificationAgent>()?.RequestStepBack(stepBackCells);
        }
    }
}
