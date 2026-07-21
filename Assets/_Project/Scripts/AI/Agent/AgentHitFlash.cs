using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentHitFlash — 검증 AI 피격 시 빨간 점멸
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="AgentHealth.OnDamageTaken"/> 을 구독해 피격 순간 검증 AI 스프라이트를
    /// 빨간색으로 점멸시킵니다. <see cref="PlayerHitFlash"/> 의 AI 버전.
    /// <para>
    /// 고정 횟수가 아니라 <see cref="AgentHealth.InvulnerabilityDuration"/> 만큼 지속 —
    /// 무적 시간이 끝나는 순간과 깜빡임이 함께 멈춘다.
    /// </para>
    /// <para>풀링 대응: <see cref="AgentContext"/> 가 <see cref="AgentHealth"/> 와 함께
    /// OnSpawned/OnDespawned 를 forwarding 해준다.</para>
    /// </summary>
    [RequireComponent(typeof(AgentHealth))]
    public class AgentHitFlash : MonoBehaviour, IPoolable
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("점멸시킬 스프라이트. 비워두면 자식에서 자동 탐색.")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("피격 점멸 색.")]
        [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.35f);

        [SerializeField, Min(0.02f)]
        [Tooltip("점멸 1회 켜짐/꺼짐 시간(초).")]
        private float blinkInterval = 0.08f;

        // ── 내부 ─────────────────────────────────────────────────────────────

        private AgentHealth _health;
        private Color       _originalColor = Color.white;
        private Coroutine   _routine;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _health = GetComponent<AgentHealth>();

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
            float duration = _health.InvulnerabilityDuration;
            var   wait     = new WaitForSeconds(blinkInterval);
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                target.color = flashColor;
                yield return wait;
                elapsed += blinkInterval;

                target.color = _originalColor;
                yield return wait;
                elapsed += blinkInterval;
            }

            RestoreColor();
            _routine = null;
        }

        private void RestoreColor()
        {
            if (target != null)
                target.color = _originalColor;
        }

        // ── IPoolable — ComponentPool 재사용 훅 (AgentContext 가 forwarding 호출) ────

        /// <summary>풀에서 꺼내질 때 이전 점멸 잔상 제거 — 색 원복.</summary>
        public void OnSpawned()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            RestoreColor();
        }

        /// <summary>
        /// 풀로 반납될 때 — 깜빡이던 중 반납되면 빨간 채로 다음 스폰에 재사용되는 것을 방지.
        /// </summary>
        public void OnDespawned()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            RestoreColor();
        }
    }
}
