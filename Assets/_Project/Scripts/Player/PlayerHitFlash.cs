using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PlayerHitFlash — 피격 시 캐릭터 빨간 점멸
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="PlayerHealth.OnDamageTaken"/> 을 구독해 피격 순간
    /// 캐릭터 스프라이트를 빨간색으로 점멸시킵니다. 공용 점멸 로직은
    /// <see cref="HitFlashBase"/> 참고.
    /// <para>Player 오브젝트에 PlayerHealth 와 함께 부착.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerHitFlash : HitFlashBase
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [SerializeField, Min(1)]
        [Tooltip("점멸 횟수.")]
        private int blinkCount = 2;

        // ── 내부 ─────────────────────────────────────────────────────────────

        private PlayerHealth _health;

        /// <summary>기존 깜빡임 총 시간(횟수 × 켜짐/꺼짐 1쌍)을 그대로 보존.</summary>
        protected override float FlashDuration => blinkCount * blinkInterval * 2f;

        // ── Unity ─────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _health = GetComponent<PlayerHealth>();
        }

        private void OnEnable() => _health.OnDamageTaken += HandleDamage;

        protected override void OnDisable()
        {
            _health.OnDamageTaken -= HandleDamage;
            base.OnDisable(); // 점멸 도중 비활성화돼도 색 원복
        }

        private void HandleDamage(int remainingHp, int damage) => StartFlash();
    }
}
