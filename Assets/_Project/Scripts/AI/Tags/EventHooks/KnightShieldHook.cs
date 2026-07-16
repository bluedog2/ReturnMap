using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  KnightShieldHook — 기사단(S-07): 사망 시 가장 가까운 아군에게 쉴드 1 부여
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="AgentContext.ActiveAgents"/> 정적 레지스트리를 순회해(자신·이미 사망한
    /// 개체 제외) 가장 가까운 생존 개체에 쉴드를 부여한다. 생존 판정은
    /// <see cref="AgentHealth.CurrentHP"/> &gt; 0.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Hook/기사단 (Knight Shield)", fileName = "Hook_KnightShield")]
    public class KnightShieldHook : TagEventHook
    {
        [SerializeField, Min(1)]
        [Tooltip("부여할 쉴드 수치.")]
        private int shieldAmount = 1;

        public override void OnDeath(AgentContext agent)
        {
            if (agent == null) return;

            Vector3 pos = agent.transform.position;
            var all = AgentContext.ActiveAgents;

            AgentContext nearest  = null;
            float        bestSqr  = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                AgentContext other = all[i];
                if (other == null || other == agent) continue;

                AgentHealth health = other.Health;
                if (health == null || health.CurrentHP <= 0) continue; // 사망 개체 제외

                float sqr = (other.transform.position - pos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = other;
                }
            }

            nearest?.Health?.AddShield(shieldAmount);
        }
    }
}
