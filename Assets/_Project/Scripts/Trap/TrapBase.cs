using System.Collections.Generic;
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
    ///   <item>카르마 변이 4단계 시각 힌트 관리 (파티클·Light2D·스프라이트 틴트)</item>
    ///   <item>플레이어 충돌 감지 → Dud 상태 자동 차단</item>
    ///   <item>솔리드 셀 — 함정은 타일처럼 1칸을 차지하며 캐릭터가 밟고 설 수 있음
    ///         (<see cref="ActsAsSolidTile"/> 로 함정별 opt-out 가능)</item>
    ///   <item>Beneficial 변이 — anchor 바깥 방향으로 황금 솔리드 블록 +1칸 생성
    ///         (<see cref="SpawnsBeneficialBlock"/> 로 함정별 opt-out 가능)</item>
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

        [Header("Trap — 스펙 정의 (권장)")]
        [SerializeField]
        [Tooltip("함정 스펙(코스트·위험도·호환 anchor·HUD 아이콘·프리팹 참조)을 중앙화한 SO. " +
                 "지정하면 아래 dangerLevel/baseCost 필드 대신 이 값을 사용합니다.")]
        private TrapDefinition definition;

        [Header("Trap — 기본 설정 (TrapDefinition 미지정 시 fallback)")]
        [SerializeField, Range(0, 10)]
        [Tooltip("위험도. 높을수록 BaseCost 가 낮아진다 (역코스트). TrapDefinition 미지정 시 fallback.")]
        private int dangerLevel = 5;

        [SerializeField]
        [Tooltip("타일맵 그리드에서 차지하는 셀 크기 (TilemapGridManager 참조용).")]
        private Vector2Int cellSize = Vector2Int.one;

        [SerializeField]
        [Tooltip("이 함정이 가하는 피해 타입. AI 태그의 면역(DamageType 마스크) 판정에 사용됨. " +
                 "TrapDefinition 미지정 시 fallback.")]
        private DamageType damageType = DamageType.None;

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

        [Header("Visual Hints — 스프라이트 틴트")]
        [SerializeField]
        [Tooltip("Beneficial 황금 틴트를 적용할 SpriteRenderer 목록. 비워두면 자식 전체를 자동 수집.")]
        private SpriteRenderer[] tintTargets;

        [Header("Tile Background — 벽/바닥에 박힌 느낌")]
        [SerializeField]
        [Tooltip("함정 셀 뒤에 깔리는 타일 배경 SpriteRenderer. 설치된 anchor 의 부착면(바닥/벽/천장) " +
                 "타일과 같은 외형으로 자동 설정되어 함정이 지형에 박혀 있는 것처럼 보이게 합니다.")]
        private SpriteRenderer tileBackground;

        [SerializeField] [Tooltip("Floor anchor 부착면 타일 스프라이트.")]
        private Sprite bgFloorSprite;
        [SerializeField] [Tooltip("LeftWall/RightWall anchor 부착면 타일 스프라이트.")]
        private Sprite bgWallSprite;
        [SerializeField] [Tooltip("Ceiling anchor 부착면 타일 스프라이트.")]
        private Sprite bgCeilingSprite;

        [SerializeField, Min(0.01f)]
        [Tooltip("배경 타일이 채워야 할 셀 크기(유닛). 보통 1.")]
        private float backgroundCellSize = 1f;

        [Header("Trap — 코스트 (TrapDefinition 미지정 시 fallback)")]
        [SerializeField]
        [Tooltip("빌드 페이즈 소비 코스트. 역코스트 원칙: 위험할수록 낮게, 안전할수록 높게. TrapDefinition 미지정 시 fallback.")]
        private int baseCost = 10;

        // ── 조명 프리셋 ───────────────────────────────────────────────────────

        private static readonly Color LIGHT_DUD        = Color.white;
        private static readonly Color LIGHT_CRITICAL   = new Color(1f, 0.10f, 0.10f);
        private static readonly Color LIGHT_BENEFICIAL = new Color(1f, 0.85f, 0.20f);

        private Color[] _originalSpriteColors;

        // ── 솔리드 셀 / 황금 블록 ─────────────────────────────────────────────

        private BoxCollider2D _solidBody;       // 함정 칸 자체의 지형 콜라이더
        private GameObject    _beneficialBlock; // Beneficial 시 +1칸 황금 블록

        /// <summary>
        /// 함정이 지형 타일처럼 자기 칸을 솔리드로 채우는지.
        /// DropHammer 처럼 이동하는 함정은 false 로 오버라이드.
        /// <para>NavGrid(검증 AI 길찾기)가 "슬롯=지형" 불변식을 반영하려면 외부에서
        /// 이 값을 읽어야 하므로 public 으로 노출한다.</para>
        /// </summary>
        public virtual bool ActsAsSolidTile => true;

        /// <summary>
        /// Beneficial 변이 시 anchor 바깥 방향으로 황금 솔리드 블록(+1칸)을 만드는지.
        /// 자체 보너스가 있는 함정(DropHammer 엘리베이터 등)은 false 로 오버라이드.
        /// </summary>
        protected virtual bool SpawnsBeneficialBlock => true;

        /// <summary>설치된 슬롯의 anchor. ConfigureForAnchor 미호출 시 Floor.</summary>
        public TrapAnchor InstalledAnchor { get; private set; } = TrapAnchor.Floor;

        // ── 정적 레지스트리 — FindObjectsByType 씬 스캔 대체 ─────────────────

        private static readonly List<TrapBase> _activeTraps = new List<TrapBase>();

        /// <summary>
        /// 씬에서 활성화된 모든 함정. TrapMutationManager 등이 씬 전체 검색 없이 순회.
        /// <para><b>주의</b>: 라이브 목록이므로 순회 중 함정이 파괴/비활성화될 수 있는
        /// 작업은 역순 for 루프로 돌 것 (foreach 금지).</para>
        /// </summary>
        public static IReadOnlyList<TrapBase> ActiveTraps => _activeTraps;

        // ── 발동 이벤트 (사운드 등 표현 계층 구독) ────────────────────────────

        /// <summary>함정이 실제로 발동(가시 돌출·화살 발사·해머 낙하)할 때 발행. Dud 는 발행하지 않는다.</summary>
        public static event System.Action<TrapBase> OnTrapActivated;

        /// <summary>서브클래스가 발동 시점에 호출. Dud 상태면 내부에서 무시(이중 안전장치).</summary>
        protected void RaiseActivated()
        {
            if (CurrentState == TrapState.Dud) return;
            OnTrapActivated?.Invoke(this);
        }

        // ── 스펙 정의 (TrapDefinition) ────────────────────────────────────────

        /// <summary>중앙화된 함정 스펙 SO. 미지정이면 null — 이 경우 직렬화 필드로 fallback.</summary>
        public TrapDefinition Definition => definition;

        // ── ITrap 프로퍼티 ────────────────────────────────────────────────────

        public TrapState  CurrentState { get; private set; } = TrapState.Normal;
        public int        DangerLevel  => definition != null ? definition.DangerLevel : dangerLevel;
        public int        BaseCost     => definition != null ? definition.BaseCost    : baseCost;

        /// <summary>이 함정이 가하는 피해 타입 (AI 태그 면역 판정용).</summary>
        public DamageType DamageType   => definition != null ? definition.DamageType  : damageType;

        /// <summary>타일맵 그리드에서 차지하는 셀 크기.</summary>
        public Vector2Int CellSize     => cellSize;

        // ── 슬롯 호환 (Build Phase) ───────────────────────────────────────────

        /// <summary>
        /// 이 함정을 설치할 수 있는 슬롯 anchor 목록.
        /// Build UI 가 설치 가능 슬롯 필터링에 사용합니다.
        /// 기본 구현은 <see cref="Definition"/> 을 읽으며, 필요 시 서브클래스가 오버라이드 가능합니다.
        /// </summary>
        public virtual TrapAnchor[] CompatibleAnchors
            => definition != null ? definition.CompatibleAnchors : System.Array.Empty<TrapAnchor>();

        /// <summary>해당 anchor 슬롯에 설치 가능한지.</summary>
        public bool IsCompatibleWith(TrapAnchor anchor)
        {
            var list = CompatibleAnchors;
            for (int i = 0; i < list.Length; i++)
                if (list[i] == anchor) return true;
            return false;
        }

        /// <summary>
        /// 설치 슬롯의 anchor 에 맞게 함정을 구성합니다.
        /// anchor 저장(황금 블록 방향 결정) 후 서브클래스 훅을 호출합니다.
        /// Build UI 가 Instantiate 직후(Start 이전)에 호출합니다.
        /// </summary>
        public void ConfigureForAnchor(TrapAnchor anchor)
        {
            InstalledAnchor = anchor;
            OnConfigureAnchor(anchor);
            RefreshTileBackground();
        }

        /// <summary>
        /// 서브클래스 방향 설정 훅
        /// (예: 천장 스파이크 → isFlipped, 화살 슈터 → 발사 방향 _fireDir).
        /// </summary>
        protected virtual void OnConfigureAnchor(TrapAnchor anchor) { }

        // ── Unity ─────────────────────────────────────────────────────────────

        /// <summary>레지스트리 자기 등록. 서브클래스 오버라이드 시 base 호출 필수.</summary>
        protected virtual void OnEnable()  => _activeTraps.Add(this);

        /// <summary>레지스트리 등록 해제. 서브클래스 오버라이드 시 base 호출 필수.</summary>
        protected virtual void OnDisable() => _activeTraps.Remove(this);

        protected virtual void Awake()
        {
            // TrapDefinition 미지정 시 기존 직렬화 필드(dangerLevel/baseCost/CompatibleAnchors)로
            // fallback 하되, 튜닝 누락을 놓치지 않도록 경고 1회.
            if (definition == null)
                Debug.LogWarning($"[TrapBase] {name}: TrapDefinition 미지정 — " +
                                 "인스펙터 직렬화 필드(dangerLevel/baseCost)로 fallback합니다. " +
                                 "메뉴 'ReTrap → Setup → 3. 함정 프리팹 + Build UI 세팅' 재실행을 권장합니다.", this);

            // damageArea 가 지정되지 않으면 자신의 Collider2D 를 사용
            if (damageArea == null)
                damageArea = GetComponent<Collider2D>();

            // Beneficial 발판은 기본적으로 비활성화
            if (platformArea != null)
                platformArea.enabled = false;

            // 틴트 대상 수집 + 원본 색 저장
            if (tintTargets == null || tintTargets.Length == 0)
                tintTargets = GetComponentsInChildren<SpriteRenderer>(true);

            _originalSpriteColors = new Color[tintTargets.Length];
            for (int i = 0; i < tintTargets.Length; i++)
                if (tintTargets[i] != null)
                    _originalSpriteColors[i] = tintTargets[i].color;

            // 함정 칸 = 지형 타일 (캐릭터가 밟고 설 수 있음)
            if (ActsAsSolidTile)
                EnsureSolidBody();

            // 기본 배경 타일 적용 (ConfigureForAnchor 미호출 — 수동/프리뷰 배치 대비)
            RefreshTileBackground();

            ValidateTriggerRouting();
        }

        /// <summary>
        /// 자식 콜라이더의 트리거 이벤트는 부모 Rigidbody2D 가 있어야 이 컴포넌트의
        /// OnTriggerEnter2D 로 전달된다. 누락 시 함정이 데미지를 영영 못 주는데
        /// 에러도 없이 조용히 실패하므로, 여기서 즉시 경고를 띄운다.
        /// </summary>
        private void ValidateTriggerRouting()
        {
            if (damageArea == null)
            {
                Debug.LogWarning($"[TrapBase] {name}: DamageArea 콜라이더가 없습니다 — " +
                                 "피격 감지가 동작하지 않습니다.", this);
                return;
            }

            bool areaOnChild = damageArea.transform != transform;
            bool hasRootRb   = GetComponent<Rigidbody2D>() != null;

            if (areaOnChild && !hasRootRb)
                Debug.LogWarning($"[TrapBase] {name}: DamageArea 가 자식 오브젝트인데 루트에 " +
                                 "Rigidbody2D 가 없습니다 — 트리거 이벤트가 루트로 전달되지 않아 " +
                                 "데미지가 들어가지 않습니다. 루트에 Rigidbody2D(Kinematic) 를 추가하세요.", this);
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
                      $"(DangerLevel={DangerLevel}, BaseCost={BaseCost})");
#endif
        }

        /// <summary>빌드 페이즈로 복귀 시 Normal 리셋.</summary>
        public void ResetToNormal() => Mutate(TrapState.Normal);

        /// <summary>
        /// A* NodeCost 가중치.
        /// 기본값 = DangerLevel. 특수 동작이 필요한 함정은 오버라이드.
        /// </summary>
        public virtual float GetNodeCostWeight() => DangerLevel;

        // ── 충돌 감지 ─────────────────────────────────────────────────────────

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (CurrentState == TrapState.Dud) return;
            if (other.TryGetComponent<PlayerController>(out var player))
                OnPlayerContact(player);
            // 검증 AI 피격 — AgentDamageSystem 게이트웨이 경유 (면역/플래그 판정).
            // Beneficial 은 무해하므로 통과.
            else if (CurrentState != TrapState.Beneficial &&
                     other.TryGetComponent<VerificationAgent>(out var agentBody))
            {
                // GetComponent 는 트리거 진입 시 1회뿐이라 프레임 비용 허용.
                var ctx = agentBody.GetComponent<AgentContext>(); // null 허용 (미배선 프리팹 하위 호환)
                if (ctx != null)
                    AgentDamageSystem.TryDamage(ctx, DamageType, transform.position, 1, this);
                else
                    agentBody.Kill(); // 미배선 프리팹 — 기존 즉사 동작 보존
            }
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

        /// <summary>
        /// 데미지 트리거 콜라이더 (읽기 전용 접근).
        /// 서브클래스가 표면 돌출 구역으로 위치·크기를 조정할 때 사용.
        /// </summary>
        protected Collider2D DamageArea => damageArea;

        // ── 내부 — 콜라이더 토글 ─────────────────────────────────────────────

        private void RefreshColliders(TrapState state)
        {
            bool isBeneficial = (state == TrapState.Beneficial);
            if (damageArea   != null) damageArea.enabled   = !isBeneficial;
            if (platformArea != null) platformArea.enabled  =  isBeneficial;

            // Beneficial = anchor 바깥쪽 +1칸 황금 솔리드 블록 (Dud 는 0칸 — 발사만 정지)
            SetBeneficialBlockActive(isBeneficial);
        }

        // ── 내부 — 솔리드 셀 / 황금 블록 ─────────────────────────────────────

        /// <summary>Ground 레이어 인덱스. 없으면 0(Default).</summary>
        private static int GroundLayerIndex()
        {
            int ground = LayerMask.NameToLayer("Ground");
            return ground >= 0 ? ground : 0;
        }

        /// <summary>
        /// 함정 칸 자체의 지형 콜라이더(1칸 솔리드 박스)를 생성합니다.
        /// 플레이어 ground check 가 인식하도록 Ground 레이어 사용.
        /// </summary>
        private void EnsureSolidBody()
        {
            if (_solidBody != null) return;

            var go = new GameObject("SolidBody");
            go.transform.SetParent(transform, false);
            go.layer = GroundLayerIndex();

            _solidBody      = go.AddComponent<BoxCollider2D>();
            _solidBody.size = new Vector2(cellSize.x, cellSize.y);
        }

        /// <summary>
        /// anchor 의 부착면 반대(바깥) 방향 단위 벡터.
        /// NavGrid 가 함정별 위험 셀(설치 칸 바깥 1칸)을 계산할 때도 재사용하므로
        /// internal 로 노출한다(같은 어셈블리인 NavGrid 에서 참조).
        /// </summary>
        internal static Vector2Int OutwardOf(TrapAnchor anchor) => anchor switch
        {
            TrapAnchor.Ceiling   => new Vector2Int( 0, -1),
            TrapAnchor.LeftWall  => new Vector2Int( 1,  0),
            TrapAnchor.RightWall => new Vector2Int(-1,  0),
            _                    => new Vector2Int( 0,  1), // Floor
        };

        private void SetBeneficialBlockActive(bool active)
        {
            if (!SpawnsBeneficialBlock) return;

            if (active)
            {
                EnsureBeneficialBlock();
                Vector2Int o = OutwardOf(InstalledAnchor);
                _beneficialBlock.transform.localPosition = new Vector3(o.x, o.y, 0f);
            }

            if (_beneficialBlock != null)
                _beneficialBlock.SetActive(active);
        }

        private void EnsureBeneficialBlock()
        {
            if (_beneficialBlock != null) return;

            _beneficialBlock = new GameObject("BeneficialBlock");
            _beneficialBlock.transform.SetParent(transform, false);
            _beneficialBlock.layer = GroundLayerIndex();

            var col  = _beneficialBlock.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            var sr              = _beneficialBlock.AddComponent<SpriteRenderer>();
            sr.sprite           = GetUnitSprite();
            sr.color            = LIGHT_BENEFICIAL;
            sr.sortingLayerName = "Map";  // 타일과 같은 레이어라야 가려지지 않음
            sr.sortingOrder     = 15;     // 타일(0)·함정(10) 위, 캐릭터(20) 아래

            _beneficialBlock.SetActive(false);
        }

        /// <summary>황금 블록·철거 모드 표시 등에 쓰이는 공용 1×1 흰 스프라이트. <see cref="SpriteUtil"/> 위임.</summary>
        internal static Sprite GetUnitSprite() => SpriteUtil.UnitWhite();

        // ── 내부 — 타일 배경 (벽/바닥에 박힌 느낌) ────────────────────────────

        /// <summary>
        /// 설치된 anchor 의 부착면(바닥/벽/천장)에 맞는 타일 스프라이트를 배경에 깔고
        /// 1셀 크기에 맞게 스케일을 보정합니다. 함정이 지형에 박혀 보이도록 합니다.
        /// </summary>
        private void RefreshTileBackground()
        {
            if (tileBackground == null) return;

            Sprite sp = InstalledAnchor switch
            {
                TrapAnchor.Ceiling   => bgCeilingSprite,
                TrapAnchor.LeftWall  => bgWallSprite,
                TrapAnchor.RightWall => bgWallSprite,
                _                    => bgFloorSprite,
            };

            // 해당 anchor 스프라이트가 비어 있으면 사용 가능한 것으로 폴백
            if (sp == null) sp = bgFloorSprite ?? bgWallSprite ?? bgCeilingSprite;

            tileBackground.sprite = sp;
            tileBackground.enabled = sp != null;
            if (sp == null) return;

            // PPU·해상도가 제각각이어도 정확히 1셀(backgroundCellSize)을 채우도록 스케일 보정
            Vector2 b = sp.bounds.size;
            if (b.x > 0.0001f && b.y > 0.0001f)
                tileBackground.transform.localScale =
                    new Vector3(backgroundCellSize / b.x, backgroundCellSize / b.y, 1f);
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
                    RestoreTint();
                    break;

                case TrapState.Critical:
                    Play(criticalParticles);
                    SetLight(LIGHT_CRITICAL, 1.5f);
                    RestoreTint();
                    break;

                case TrapState.Beneficial:
                    SetLight(LIGHT_BENEFICIAL, 1.2f);
                    ApplyTint(LIGHT_BENEFICIAL);
                    break;

                default: // Normal
                    SetLight(Color.white, 0f);
                    RestoreTint();
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

        /// <summary>모든 틴트 대상에 동일한 색을 적용 (Beneficial 황금 등).</summary>
        private void ApplyTint(Color color)
        {
            if (tintTargets == null) return;
            for (int i = 0; i < tintTargets.Length; i++)
                if (tintTargets[i] != null)
                    tintTargets[i].color = color;
        }

        /// <summary>각 렌더러를 Awake 시점에 저장한 원본 색으로 복원.</summary>
        private void RestoreTint()
        {
            if (tintTargets == null || _originalSpriteColors == null) return;
            for (int i = 0; i < tintTargets.Length; i++)
                if (tintTargets[i] != null)
                    tintTargets[i].color = _originalSpriteColors[i];
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
            string info = $"Cost:{BaseCost}  Danger:{DangerLevel}  [{CurrentState}]";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, info);

            // 차지하는 그리드 셀 영역 표시 (TilemapGridManager 와 맞추기 위해 1셀 = 1unit 가정)
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
            Gizmos.DrawCube(transform.position,
                            new Vector3(cellSize.x, cellSize.y, 0.05f));
        }
#endif
    }
}
