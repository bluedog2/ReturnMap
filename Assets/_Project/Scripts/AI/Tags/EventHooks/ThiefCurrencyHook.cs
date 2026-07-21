using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ThiefCurrencyHook — 도둑(S-04): 처치 시 재화 획득
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 개체 사망(= 함정에 의한 처치) 시 <see cref="CurrencyService"/> 에 재화(박살 난
    /// 지구본)를 지급한다.
    /// <para>TODO: 기획상 "처치 실패(골 도달/탈출) 시 재화 도난" 도 명시돼 있으나, 현재
    /// <see cref="TagEventHook"/> 에는 골 도달(OnGoalReached) 훅 포인트가 없다
    /// (AgentWaveController/AgentContext 병행 수정 중이라 이번 단계에서는 훅 포인트를
    /// 추가하지 않음 — 별도 작업으로 남긴다).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Hook/도둑(재화)", fileName = "Hook_ThiefCurrency")]
    public class ThiefCurrencyHook : TagEventHook
    {
        [SerializeField, Min(0)]
        [Tooltip("처치 시 지급할 재화(박살 난 지구본) 수량.")]
        private int rewardOnKill = 5;

        public override void OnDeath(AgentContext agent)
        {
            CurrencyService.Add(rewardOnKill);
        }
    }
}
