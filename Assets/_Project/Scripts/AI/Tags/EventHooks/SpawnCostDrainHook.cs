using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SpawnCostDrainHook — 가성비 타파(S-08): 스폰 시 유저 빌드 코스트 차감
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 개체 스폰 직후 씬의 <see cref="BuildPhaseController"/> 를 찾아 빌드 예산을
    /// 차감한다. <see cref="BuildPhaseController"/> 는 싱글톤이 아니라 매 호출마다
    /// <see cref="Object.FindFirstObjectByType{T}"/> 로 조회한다(<see cref="BuildHudController"/>
    /// 와 동일한 접근 방식).
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Hook/가성비 타파 (코스트 차감)", fileName = "Hook_SpawnCostDrain")]
    public class SpawnCostDrainHook : TagEventHook
    {
        [SerializeField, Min(0)]
        [Tooltip("스폰 시 차감할 빌드 코스트.")]
        private int costDrain = 5;

        public override void OnSpawn(AgentContext agent)
        {
            var ctrl = Object.FindFirstObjectByType<BuildPhaseController>();
            if (ctrl == null) return;

            ctrl.DrainBudget(costDrain);
        }
    }
}
