using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ThiefCurrencyHook — 도둑(S-04): 처치 시 재화 획득, 탈출 시 재화 도난
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 개체 사망(= 함정에 의한 처치) 시 <see cref="CurrencyService"/> 에 재화(박살 난
    /// 지구본)를 지급하고, 골에 도달(탈출)하면 보유 재화를 도난한다(잔액 한도까지).
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Hook/도둑(재화)", fileName = "Hook_ThiefCurrency")]
    public class ThiefCurrencyHook : TagEventHook
    {
        [SerializeField, Min(0)]
        [Tooltip("처치 시 지급할 재화(박살 난 지구본) 수량.")]
        private int rewardOnKill = 5;

        [SerializeField, Min(0)]
        [Tooltip("골 도달(탈출) 시 도난당할 재화 수량. 잔액이 부족하면 0 까지만 차감.")]
        private int stealOnEscape = 5;

        public override void OnDeath(AgentContext agent)
        {
            CurrencyService.Add(rewardOnKill);
        }

        public override void OnGoalReached(AgentContext agent)
        {
            CurrencyService.Steal(stealOnEscape);
        }
    }
}
