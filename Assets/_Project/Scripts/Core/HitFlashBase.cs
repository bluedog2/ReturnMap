using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  HitFlashBase — 피격 점멸 공용 추상 기반 클래스
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 피격 순간 스프라이트를 지정 색으로 점멸시키는 공용 로직. <see cref="PlayerHitFlash"/>,
    /// <see cref="AgentHitFlash"/> 가 이 클래스를 상속해 대상별 구독/해제와
    /// <see cref="FlashDuration"/> 계산만 각자 구현합니다.
    /// <para>
    /// <b>서브클래스 규약</b>: <c>Awake</c> 를 오버라이드하면 반드시 <c>base.Awake()</c> 를
    /// 먼저 호출해 target 자동 탐색과 원본 색 저장이 이뤄지게 할 것.
    /// 피격 이벤트 구독 시 <see cref="StartFlash"/> 를, 무효화(비활성/반납) 시
    /// <see cref="StopFlashAndRestore"/> 를 호출한다.
    /// </para>
    /// </summary>
    public abstract class HitFlashBase : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("피격 점멸")]
        [Tooltip("점멸시킬 스프라이트. 비워두면 자식에서 자동 탐색.")]
        [SerializeField] protected SpriteRenderer target;

        [Tooltip("피격 점멸 색.")]
        [SerializeField] protected Color flashColor = new Color(1f, 0.35f, 0.35f);

        [SerializeField, Min(0.02f)]
        [Tooltip("점멸 1회 켜짐/꺼짐 시간(초).")]
        protected float blinkInterval = 0.08f;

        // ── 내부 ─────────────────────────────────────────────────────────────

        private Color     _originalColor = Color.white;
        private Coroutine _routine;

        /// <summary>이번 점멸이 총 몇 초 동안 지속될지. 서브클래스가 자기 사정에 맞게 계산.</summary>
        protected abstract float FlashDuration { get; }

        // ── Unity ─────────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            if (target == null)
                target = GetComponentInChildren<SpriteRenderer>();

            if (target != null)
                _originalColor = target.color;
        }

        protected virtual void OnDisable()
        {
            StopFlashAndRestore(); // 점멸 도중 비활성화돼도 색 원복
        }

        // ── 점멸 ─────────────────────────────────────────────────────────────

        /// <summary>점멸 시작. 이미 진행 중이던 점멸이 있으면 정지 후 다시 시작한다.</summary>
        protected void StartFlash()
        {
            if (target == null) return;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float duration = FlashDuration;
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

        /// <summary>진행 중인 점멸 코루틴을 즉시 정지하고 원본 색으로 복원한다.</summary>
        protected void StopFlashAndRestore()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            RestoreColor();
        }

        private void RestoreColor()
        {
            if (target != null)
                target.color = _originalColor;
        }
    }
}
