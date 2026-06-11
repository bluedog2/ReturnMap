using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PlayerHitFlash — 피격 시 캐릭터 빨간 점멸
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="PlayerHealth.OnDamageTaken"/> 을 구독해 피격 순간
    /// 캐릭터 스프라이트를 빨간색으로 점멸시킵니다.
    /// <para>Player 오브젝트에 PlayerHealth 와 함께 부착.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerHitFlash : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("점멸시킬 스프라이트. 비워두면 자식에서 자동 탐색 (Visual).")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("피격 점멸 색.")]
        [SerializeField] private Color flashColor = new Color(1f, 0.25f, 0.25f);

        [SerializeField, Min(1)]
        [Tooltip("점멸 횟수.")]
        private int blinkCount = 2;

        [SerializeField, Min(0.02f)]
        [Tooltip("점멸 1회 켜짐/꺼짐 시간(초).")]
        private float blinkInterval = 0.08f;

        // ── 내부 ─────────────────────────────────────────────────────────────

        private PlayerHealth _health;
        private Color        _originalColor = Color.white;
        private Coroutine    _routine;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();

            if (target == null)
                target = GetComponentInChildren<SpriteRenderer>();

            if (target != null)
                _originalColor = target.color;
        }

        private void OnEnable()  => _health.OnDamageTaken += HandleDamage;

        private void OnDisable()
        {
            _health.OnDamageTaken -= HandleDamage;
            RestoreColor(); // 점멸 도중 비활성화돼도 색 원복
        }

        // ── 점멸 ─────────────────────────────────────────────────────────────

        private void HandleDamage(int remainingHp, int damage)
        {
            if (target == null) return;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            var wait = new WaitForSeconds(blinkInterval);

            for (int i = 0; i < blinkCount; i++)
            {
                target.color = flashColor;
                yield return wait;
                target.color = _originalColor;
                yield return wait;
            }

            RestoreColor();
            _routine = null;
        }

        private void RestoreColor()
        {
            if (target != null)
                target.color = _originalColor;
        }
    }
}
