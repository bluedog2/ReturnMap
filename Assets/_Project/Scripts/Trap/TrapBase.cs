using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapBase — 모든 함정의 추상 기반 클래스
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <para><b>역할 분담</b></para>
    /// <list type="bullet">
    ///   <item>ITrap 계약 구현 (상태·코스트·변이·NodeCost)</item>
    ///   <item>카르마 변이 4단계 시각 힌트 관리 (파티클·Light2D)</item>
    ///   <item>플레이어 충돌 감지 → Dud 상태 자동 차단</item>
    ///   <item>Beneficial 상태의 발판 Collider 자동 토글</item>
    /// </list>
    ///
    /// <para><b>서브클래스 필수 구현</b></para>
    /// <see cref="OnNormal"/>, <see cref="OnDud"/>, <see cref="OnCritical"/>,
    /// <see cref="OnBeneficial"/>, <see cref="OnPlayerContact"/>
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class TrapBase : MonoBehaviour, ITrap
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Trap — 기본 설정")]
        [SerializeField, Range(0, 10)]
        [Tooltip("위험도. 높을수록 BaseCost 가 낮아진다 (역코스트).")]
        private int dangerLevel = 5;

        [SerializeField]
        [Tooltip("타일맵 그리드에서 차지하는 셀 크기 (TilemapGridManager 참조용).")]
        private Vector2Int cellSize = Vector2Int.one;

        [Header("Colliders")]
        [SerializeField]
        [Tooltip("플레이어 피격을 감지하는 Trigger Collider. " +
                 "미지정 시 이 컴포넌트의 첫 번째 Collider2D 를 사용.")]
        private Collider2D damageArea;

        [SerializeField]
        [Tooltip("Beneficial 상태에서 발판으로 사용될 Solid Collider. 선택 사항.")]
        private Collider2D platformArea;

        [Header("Visual Hints — 파티클")]
        [SerializeField]
        [Tooltip("Dud 상태: 스파크·불규칙 깜빡임 이펙트.")]
        private ParticleSystem dudParticles;

        [SerializeField]
        [Tooltip("Critical 상태: 붉은·보라색 이펙트.")]
        private ParticleSystem criticalParticles;

        [Header("Visual Hints — 조명")]
        [SerializeField]
        [Tooltip("상태별 색상·강도가 자동 변경되는 Point Light 2D.")]
        private Light2D stateLight;

        [Header("Trap — 코스트")]
        [SerializeField]
        [Tooltip("빌드 페이즈 소비 코스트. 역코스트 원칙: 위험할수록 낮게, 안전할수록 높게.")]
        private int baseCost = 10;

        // ── 조명 프리셋 ───────────────────────────────────────────────────────

        private static readonly Color LIGHT_DUD        = Color.white;
        private static readonly Color LIGHT_CRITICAL   = new Color(1f, 0.10f, 0.10f);
        private static readonly Color LIGHT_BENEFICIAL = new Color(1f, 0.85f, 0.20f);

        // ── ITrap 프로퍼티 ────────────────────────────────────────────────────

        public TrapState  CurrentState { get; private set; } = TrapState.Normal;
        public int        DangerLevel  => dangerLevel;
        public int        BaseCost     => baseCost;

        /// <summary>타일맵 그리드에서 차지하는 셀 크기.</summary>
        public Vector2Int CellSize     => cellSize;

        // ── Unity ─────────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            // damageArea 가 지정되지 않으면 자신의 Collider2D 를 사용
            if (damageArea == null)
                damageArea = GetComponent<Collider2D>();

            // Beneficial 발판은 기본적으로 비활성화
            if (platformArea != null)
                platformArea.enabled = false;
        }

        /// <summary>
        /// 모든 Awake 완료 후 Normal 상태로 완전 초기화.
        /// 서브클래스에서 오버라이드 시 반드시 base.Start() 를 먼저 호출해야
        /// OnNormal() 이 올바른 순서로 실행된다.
        /// </summary>
        protected virtual void Start()
        {
            // Awake 에서 서브클래스 필드가 모두 초기화된 뒤에 호출되어야
            // OnNormal() 이 안전하게 실행된다 (예: SpikeTrap.originalScale).
            Mutate(TrapState.Normal);
        }

        // ── ITrap 구현 ────────────────────────────────────────────────────────

        /// <summary>
        /// TrapMutationManager 가 플레이 페이즈 진입 시 호출.
        /// 시각·콜라이더·동작 모두 한 번에 변경된다.
        /// </summary>
        public void Mutate(TrapState newState)
        {
            CurrentState = newState;
            RefreshColliders(newState);
            RefreshVisuals(newState);
            DispatchBehaviourChange(newState);

#if UNITY_EDITOR
            Debug.Log($"[TrapBase] {name} → {newState}  " +
                      $"(DangerLevel={dangerLevel}, BaseCost={BaseCost})");
#endif
        }

        /// <summary>빌드 페이즈로 복귀 시 Normal 리셋.</summary>
        public void ResetToNormal() => Mutate(TrapState.Normal);

        /// <summary>
        /// A* NodeCost 가중치.
        /// 기본값 = DangerLevel. 특수 동작이 필요한 함정은 오버라이드.
        /// </summary>
        public virtual float GetNodeCostWeight() => dangerLevel;

        // ── 충돌 감지 ─────────────────────────────────────────────────────────

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (CurrentState == TrapState.Dud) return;
            if (other.TryGetComponent<PlayerController>(out var player))
                OnPlayerContact(player);
        }

        // ── 추상 메서드 ───────────────────────────────────────────────────────

        /// <summary>Normal 상태 — 기본 스펙으로 초기화.</summary>
        protected abstract void OnNormal();

        /// <summary>
        /// Dud 상태 — 트랩 메커니즘 완전 정지.
        /// (예: 회전 정지, 투사체 비활성화)
        /// </summary>
        protected abstract void OnDud();

        /// <summary>
        /// Critical 상태 — 속도·범위 증가, 독 추가 등 치명적 변화.
        /// </summary>
        protected abstract void OnCritical();

        /// <summary>
        /// Beneficial 상태 — 발판 고정 or 둔화 처리.
        /// (platformArea 는 TrapBase 가 자동 처리하므로 추가 로직만 작성)
        /// </summary>
        protected abstract void OnBeneficial();

        /// <summary>
        /// Dud 가 아닌 상태에서 플레이어가 damageArea 트리거에 진입했을 때.
        /// Beneficial 일 때도 호출됨 — 둔화 등 긍정 효과를 여기서 적용.
        /// </summary>
        protected abstract void OnPlayerContact(PlayerController player);

        // ── protected 헬퍼 (서브클래스에서 상태별 세부 제어용) ──────────────────

        /// <summary>
        /// 서브클래스가 상태 내부 동작(낙하 중 데미지 활성화 등)을 위해 직접 제어.
        /// Mutate() 가 RefreshColliders 를 먼저 실행한 뒤 OnXxx() 를 호출하므로
        /// OnXxx() 안에서 호출하면 TrapBase 의 기본값을 덮어쓸 수 있다.
        /// </summary>
        protected void SetDamageAreaEnabled(bool active)
        {
            if (damageArea != null) damageArea.enabled = active;
        }

        protected void SetPlatformAreaEnabled(bool active)
        {
            if (platformArea != null) platformArea.enabled = active;
        }

        // ── 내부 — 콜라이더 토글 ─────────────────────────────────────────────

        private void RefreshColliders(TrapState state)
        {
            bool isBeneficial = (state == TrapState.Beneficial);
            if (damageArea   != null) damageArea.enabled   = !isBeneficial;
            if (platformArea != null) platformArea.enabled  =  isBeneficial;
        }

        // ── 내부 — 시각 힌트 ──────────────────────────────────────────────────

        private void RefreshVisuals(TrapState state)
        {
            StopAllParticles();

            switch (state)
            {
                case TrapState.Dud:
                    Play(dudParticles);
                    SetLight(LIGHT_DUD, 0.5f);
                    break;

                case TrapState.Critical:
                    Play(criticalParticles);
                    SetLight(LIGHT_CRITICAL, 1.5f);
                    break;

                case TrapState.Beneficial:
                    SetLight(LIGHT_BENEFICIAL, 1.0f);
                    break;

                default: // Normal
                    SetLight(Color.white, 0f);
                    break;
            }
        }

        private void StopAllParticles()
        {
            if (dudParticles)      dudParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (criticalParticles) criticalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void Play(ParticleSystem ps)
        {
            if (ps != null) ps.Play();
        }

        private void SetLight(Color color, float intensity)
        {
            if (stateLight == null) return;
            stateLight.color     = color;
            stateLight.intensity = intensity;
        }

        // ── 내부 — 동작 디스패치 ──────────────────────────────────────────────

        private void DispatchBehaviourChange(TrapState state)
        {
            switch (state)
            {
                case TrapState.Normal:     OnNormal();     break;
                case TrapState.Dud:        OnDud();        break;
                case TrapState.Critical:   OnCritical();   break;
                case TrapState.Beneficial: OnBeneficial(); break;
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            // 상태 & 코스트 정보 표시
            string info = $"Cost:{BaseCost}  Danger:{dangerLevel}  [{CurrentState}]";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, info);

            // 차지하는 그리드 셀 영역 표시 (TilemapGridManager 와 맞추기 위해 1셀 = 1unit 가정)
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
            Gizmos.DrawCube(transform.position,
                            new Vector3(cellSize.x, cellSize.y, 0.05f));
        }
#endif
    }
}
