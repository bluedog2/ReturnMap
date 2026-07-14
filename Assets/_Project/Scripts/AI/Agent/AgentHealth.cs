using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentHealth — 검증 AI 개체의 HP/쉴드
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 AI 개체의 체력·쉴드를 관리합니다. <see cref="AgentDamageSystem"/> 이 판정을
    /// 마친 뒤에만 호출하는 순수 데이터 계층 — 면역/플래그 판정은 여기서 하지 않습니다.
    /// </summary>
    public class AgentHealth : MonoBehaviour, IPoolable
    {
        /// <summary>현재 체력.</summary>
        public int CurrentHP { get; private set; } = 1;

        /// <summary>현재 쉴드 — 체력보다 먼저 소모된다 (기사단 태그 등).</summary>
        public int Shield { get; private set; }

        /// <summary>이미 사망 처리되었는가.</summary>
        public bool IsDead { get; private set; }

        // GetComponent 캐시 (사망 시점에 매번 조회하지 않도록)
        private AgentContext      _ctx;
        private VerificationAgent _body;

        private void Awake()
        {
            _ctx  = GetComponent<AgentContext>();
            _body = GetComponent<VerificationAgent>();
        }

        /// <summary>스폰/Initialize 시 호출 — HP 를 maxHP 로, 쉴드를 0으로 리셋합니다.</summary>
        public void ResetHealth(int maxHP)
        {
            CurrentHP = Mathf.Max(1, maxHP);
            Shield    = 0;
            IsDead    = false;
        }

        /// <summary>쉴드를 부여합니다 (기사단 태그 등 훅에서 호출 예정).</summary>
        public void AddShield(int amount)
        {
            if (amount <= 0) return;
            Shield += amount;
        }

        /// <summary>
        /// 피해를 적용합니다. 쉴드를 우선 차감하고 남은 만큼 HP 를 깎습니다.
        /// HP 가 0 이하가 되면 사망 처리(사망 훅 → VerificationAgent.Kill()) 후 true 를 반환합니다.
        /// 이미 사망 상태면 아무 것도 하지 않고 false 를 반환합니다.
        /// </summary>
        public bool TakeDamage(int amount)
        {
            if (IsDead) return false; // 이미 사망 처리된 개체 — 중복 처리 방지

            // 쉴드 우선 차감
            if (Shield > 0)
            {
                int absorbedByShield = Mathf.Min(Shield, amount);
                Shield -= absorbedByShield;
                amount -= absorbedByShield;
            }

            if (amount > 0)
                CurrentHP -= amount;

            if (CurrentHP <= 0)
            {
                IsDead = true;

                // 캐시가 비어 있으면(컴포넌트 추가 순서 등) 안전하게 재조회
                if (_ctx  == null) _ctx  = GetComponent<AgentContext>();
                if (_body == null) _body = GetComponent<VerificationAgent>();

                _ctx?.DispatchDeathHooks();
                _body?.Kill();
                return true;
            }

            return false;
        }

        // ── IPoolable — ComponentPool 재사용 훅 (AgentContext 가 forwarding 호출) ────

        /// <summary>풀에서 꺼내질 때 기본값(HP 1/쉴드 0)으로 리셋. Initialize 가 실제 값으로 덮어씀.</summary>
        public void OnSpawned()
        {
            CurrentHP = 1;
            Shield    = 0;
            IsDead    = false;
        }

        /// <summary>풀로 반납될 때 — 별도 정리 없음.</summary>
        public void OnDespawned() { }
    }
}
