using System;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentHealth — 검증 AI 개체의 HP/쉴드
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 AI 개체의 체력·쉴드를 관리합니다. <see cref="AgentDamageSystem"/> 이 판정을
    /// 마친 뒤에만 호출하는 순수 데이터 계층 — 면역/플래그 판정은 여기서 하지 않습니다.
    /// <para>
    /// 무적 시간(<see cref="IsInvulnerable"/>)의 <b>소유</b>만 이 컴포넌트가 담당하고,
    /// 무적 <b>판정</b>은 게이트웨이(<see cref="AgentDamageSystem"/>)가 한다.
    /// </para>
    /// </summary>
    public class AgentHealth : MonoBehaviour, IPoolable
    {
        [Header("무적 시간")]
        [SerializeField, Min(0f)]
        [Tooltip("피격 후 무적 지속 시간(초). 가시 진동 사이클마다 트리거가 재발동해 연타사하는 것을 막는다.")]
        private float invulnerabilityDuration = 0.6f;

        /// <summary>현재 체력.</summary>
        public int CurrentHP { get; private set; } = 1;

        /// <summary>현재 쉴드 — 체력보다 먼저 소모된다 (기사단 태그 등).</summary>
        public int Shield { get; private set; }

        /// <summary>이미 사망 처리되었는가.</summary>
        public bool IsDead { get; private set; }

        /// <summary>직전 피격의 무적 시간 중인가.</summary>
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        /// <summary>부여되는 무적 지속 시간(초) — 피격 연출(AgentHitFlash)이 깜빡임 길이를 맞추는 데 참조.</summary>
        public float InvulnerabilityDuration => invulnerabilityDuration;

        /// <summary>피격 시 발행. (남은 HP, 받은 피해) — 사망 시엔 발행하지 않는다(사망 훅 → Kill() 흐름 유지).</summary>
        public event Action<int, int> OnDamageTaken;

        private float _invulnerableUntil;

        // GetComponent 캐시 (사망 시점에 매번 조회하지 않도록)
        private AgentContext      _ctx;
        private VerificationAgent _body;

        private void Awake()
        {
            _ctx  = GetComponent<AgentContext>();
            _body = GetComponent<VerificationAgent>();
        }

        /// <summary>스폰/Initialize 시 호출 — HP 를 maxHP 로, 쉴드를 0으로, 무적을 해제합니다.</summary>
        public void ResetHealth(int maxHP)
        {
            CurrentHP          = Mathf.Max(1, maxHP);
            Shield             = 0;
            IsDead             = false;
            _invulnerableUntil = 0f;
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

            // 무적 부여/이벤트 발행 판단 기준 — 쉴드가 전량 흡수해 HP 변화가 없어도 이 값을 쓴다.
            int originalAmount = amount;

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

            // 피해 시도가 (쉴드로 전량 차단되었든 HP 에 실제로 적용되었든) 처리되고 생존한
            // 모든 경우에 무적 부여 + 피격 이벤트 발행. 쉴드가 전부 흡수해 HP 변화가 없다고
            // 무적을 켜지 않으면 진동 가시 등이 매 사이클 쉴드를 연속으로 깎아 연타 방지 취지가
            // 무력화된다. damage 인자는 쉴드 흡수분+HP 감소분을 합친 "총 피해 시도량"
            // (originalAmount) — HitFlash 등 연출이 쉴드 흡수 히트에도 반응하도록 한다.
            if (originalAmount > 0)
            {
                _invulnerableUntil = Time.time + invulnerabilityDuration;
                OnDamageTaken?.Invoke(CurrentHP, originalAmount);
            }

            return false;
        }

        // ── IPoolable — ComponentPool 재사용 훅 (AgentContext 가 forwarding 호출) ────

        /// <summary>
        /// 풀에서 꺼내질 때 기본값(HP 1/쉴드 0)으로 리셋. Initialize 가 실제 값으로 덮어씀.
        /// OnDamageTaken 구독은 건드리지 않는다 — 풀 재사용 시 구독자(AgentHitFlash 등)가 유지돼야 한다.
        /// </summary>
        public void OnSpawned()
        {
            CurrentHP          = 1;
            Shield             = 0;
            IsDead             = false;
            _invulnerableUntil = 0f;
        }

        /// <summary>풀로 반납될 때 — 별도 정리 없음.</summary>
        public void OnDespawned() { }
    }
}
