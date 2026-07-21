using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentHitFlash — 검증 AI 피격 시 빨간 점멸
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="AgentHealth.OnDamageTaken"/> 을 구독해 피격 순간 검증 AI 스프라이트를
    /// 빨간색으로 점멸시킵니다. <see cref="PlayerHitFlash"/> 의 AI 버전 — 공용 점멸 로직은
    /// <see cref="HitFlashBase"/> 참고.
    /// <para>
    /// 고정 횟수가 아니라 <see cref="AgentHealth.InvulnerabilityDuration"/> 만큼 지속 —
    /// 무적 시간이 끝나는 순간과 깜빡임이 함께 멈춘다.
    /// </para>
    /// <para>풀링 대응: <see cref="AgentContext"/> 가 <see cref="AgentHealth"/> 와 함께
    /// OnSpawned/OnDespawned 를 forwarding 해준다.</para>
    /// </summary>
    [RequireComponent(typeof(AgentHealth))]
    public class AgentHitFlash : HitFlashBase, IPoolable
    {
        // ── 내부 ─────────────────────────────────────────────────────────────

        private AgentHealth _health;

        protected override float FlashDuration => _health.InvulnerabilityDuration;

        // ── Unity ─────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _health = GetComponent<AgentHealth>();
        }

        private void OnEnable() => _health.OnDamageTaken += HandleDamage;

        protected override void OnDisable()
        {
            _health.OnDamageTaken -= HandleDamage;
            base.OnDisable(); // 점멸 도중 비활성화돼도 색 원복
        }

        private void HandleDamage(int remainingHp, int damage) => StartFlash();

        // ── IPoolable — ComponentPool 재사용 훅 (AgentContext 가 forwarding 호출) ────

        /// <summary>풀에서 꺼내질 때 이전 점멸 잔상 제거 — 색 원복.</summary>
        public void OnSpawned() => StopFlashAndRestore();

        /// <summary>
        /// 풀로 반납될 때 — 깜빡이던 중 반납되면 빨간 채로 다음 스폰에 재사용되는 것을 방지.
        /// </summary>
        public void OnDespawned() => StopFlashAndRestore();
    }
}
