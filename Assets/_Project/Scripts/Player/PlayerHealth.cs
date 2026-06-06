using UnityEngine;
using System;

namespace ReTrap
{
    /// <summary>
    /// 플레이어 체력과 무적 프레임을 관리합니다.
    /// <para>
    /// - 체력(HP)은 캐릭터·스테이지에 따라 Inspector 에서 자유롭게 설정.<br/>
    /// - 피격 후 <see cref="iFrameDuration"/> 동안 무적. 무적 중 재피격 차단.<br/>
    /// - <see cref="SetInvincible(float)"/> 로 외부에서도 무적 부여 가능 (화살 축복 등).
    /// </para>
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("체력")]
        [SerializeField, Min(1)]
        [Tooltip("최대 HP. 캐릭터·스테이지 설정에 따라 조정.")]
        private int maxHp = 3;

        [Header("무적 프레임")]
        [SerializeField, Min(0f)]
        [Tooltip("피격 후 무적 지속 시간(초).")]
        private float iFrameDuration = 0.5f;

        // ── 공개 상태 ─────────────────────────────────────────────────────────

        public int  MaxHp       => maxHp;
        public int  CurrentHp   { get; private set; }
        public bool IsInvincible { get; private set; }
        public bool IsDead      => CurrentHp <= 0;

        // ── 이벤트 ────────────────────────────────────────────────────────────

        /// <summary>피격 후 남은 HP 전달. (남은HP, 받은데미지)</summary>
        public event Action<int, int> OnDamageTaken;

        /// <summary>HP 가 0 이 되는 순간 발행.</summary>
        public event Action OnDeath;

        /// <summary>무적 상태가 변경될 때 발행.</summary>
        public event Action<bool> OnInvincibilityChanged;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake() => CurrentHp = maxHp;

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>데미지 적용. 무적 중이면 무시.</summary>
        public void TakeDamage(int amount)
        {
            if (IsInvincible || IsDead) return;

            int prev  = CurrentHp;
            CurrentHp = Mathf.Max(0, CurrentHp - amount);
            OnDamageTaken?.Invoke(CurrentHp, prev - CurrentHp);

            if (CurrentHp <= 0)
            {
                OnDeath?.Invoke();
                return;
            }

            // 피격 후 무적 프레임 자동 부여
            SetInvincible(iFrameDuration);
        }

        /// <summary>
        /// 외부에서 무적 부여 (화살 Beneficial 등).
        /// 이미 무적 중이면 남은 시간이 더 길 때만 갱신합니다.
        /// </summary>
        public void SetInvincible(float duration)
        {
            StopAllCoroutines();
            StartCoroutine(InvincibilityRoutine(duration));
        }

        /// <summary>HP 전체 회복.</summary>
        public void RestoreFullHp()
        {
            CurrentHp = maxHp;
        }

        // ── 내부 ──────────────────────────────────────────────────────────────

        private System.Collections.IEnumerator InvincibilityRoutine(float duration)
        {
            IsInvincible = true;
            OnInvincibilityChanged?.Invoke(true);

            yield return new WaitForSeconds(duration);

            IsInvincible = false;
            OnInvincibilityChanged?.Invoke(false);
        }
    }
}
