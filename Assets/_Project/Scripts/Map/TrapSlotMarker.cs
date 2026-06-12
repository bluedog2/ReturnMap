using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapSlotMarker — 런타임 함정 슬롯 데이터 보관
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MapLoader 가 슬롯 마커 오브젝트에 부착합니다.
    /// Build Phase UI 가 이 컴포넌트를 탐색해 설치 가능 위치를 파악합니다.
    ///
    /// <para><b>5단계</b>에서 TrapSlotRegistry 가 이 컴포넌트를 수집·관리합니다.</para>
    /// </summary>
    public class TrapSlotMarker : MonoBehaviour
    {
        // ── 슬롯 정보 (읽기 전용) ────────────────────────────────────────────

        public int        GridX      { get; private set; }
        public int        GridY      { get; private set; }
        public TrapAnchor Anchor     { get; private set; }

        /// <summary>현재 이 슬롯에 설치된 함정 GameObject. null 이면 비어있음.</summary>
        public GameObject OccupiedBy { get; private set; }

        /// <summary>슬롯이 비어있는지 여부.</summary>
        public bool IsEmpty => OccupiedBy == null;

        // ── 초기화 / 레지스트리 등록 ─────────────────────────────────────────

        private bool           _registered;
        private SpriteRenderer _guideSR;       // 슬롯 가이드 이미지
        private Color          _guideBaseTint = Color.white; // 가이드 원본 색 (하이라이트 복원용)
        private GameObject     _seal;          // Play 중 빈 슬롯을 막는 봉인 타일

        /// <summary>
        /// MapLoader 에서 생성 직후 호출. 좌표 확정과 동시에
        /// <see cref="TrapSlotRegistry"/> 에 등록됩니다.
        /// (OnEnable 등록은 AddComponent 시점에 좌표가 0,0 이라 사용 불가)
        /// </summary>
        public void Init(int x, int y, TrapAnchor anchor)
        {
            GridX  = x;
            GridY  = y;
            Anchor = anchor;

            _guideSR = GetComponent<SpriteRenderer>();
            if (_guideSR != null)
                _guideBaseTint = _guideSR.color;

            TrapSlotRegistry.Register(this);
            _registered = true;

            RefreshVisualState();
        }

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;

        private void OnDestroy()
        {
            if (_registered)
                TrapSlotRegistry.Unregister(this);
        }

        // ── 가이드 / 봉인 표시 규칙 ──────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase _) => RefreshVisualState();

        private bool IsBuildPhase
            => GamePhaseManager.Instance == null
            || GamePhaseManager.Instance.currentPhase == GamePhase.Build;

        private void RefreshVisualState()
        {
            RefreshGuideVisible();
            RefreshSealActive();
        }

        /// <summary>
        /// 가이드 이미지 표시 규칙:
        /// <b>Build 페이즈 + 빈 슬롯</b>일 때만 보임.
        /// 함정이 설치되면 가이드는 사라지고 함정만 남는다. Play 중엔 전부 숨김.
        /// </summary>
        private void RefreshGuideVisible()
        {
            if (_guideSR == null) return;
            _guideSR.enabled = IsBuildPhase && IsEmpty;
        }

        /// <summary>
        /// 봉인 타일 활성 규칙:
        /// <b>Build 가 아닌 페이즈 + 빈 슬롯</b> = 일반 타일로 막힘.
        /// 함정이 설치된 슬롯은 함정 자신의 솔리드 셀이 지형 역할을 한다.
        /// </summary>
        private void RefreshSealActive()
        {
            if (_seal == null) return;
            _seal.SetActive(!IsBuildPhase && IsEmpty);
        }

        // ── 봉인 타일 구성 (MapLoader 가 생성 직후 호출) ─────────────────────

        /// <summary>
        /// 빈 슬롯을 Play 중 솔리드 타일로 막는 봉인 오브젝트를 구성합니다.
        /// 콜라이더는 맵 루트의 CompositeCollider2D 로 병합되어 이음새 없이 동작합니다.
        /// </summary>
        public void CreateSeal(Sprite sprite, Color color, Material material,
                               string sortingLayerName, int sortingOrder,
                               int layerIndex, float size)
        {
            if (_seal != null) return;

            _seal = new GameObject("SlotSeal");
            _seal.transform.SetParent(transform, false);
            _seal.layer = layerIndex;

            var sr    = _seal.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color  = color;
            if (material != null)
                sr.sharedMaterial = material; // 일반 타일과 동일 조명·배칭
            if (!string.IsNullOrEmpty(sortingLayerName))
                sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            var col  = _seal.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * size;
            col.compositeOperation = Collider2D.CompositeOperation.Merge;

            RefreshSealActive();
        }

        // ── 설치 가능 하이라이트 (Build UI 가 호출) ──────────────────────────

        /// <summary>
        /// 현재 선택된 함정을 설치할 수 있는 슬롯이면 강조 색,
        /// 아니면 원본 가이드 색으로 복원합니다.
        /// </summary>
        public void SetGuideHighlight(bool on, Color highlightColor)
        {
            if (_guideSR == null) return;
            _guideSR.color = on ? highlightColor : _guideBaseTint;
        }

        // ── 5단계 Build Phase 연동 ────────────────────────────────────────────

        /// <summary>함정 설치. Build Phase UI 에서 호출합니다.</summary>
        public bool TryOccupy(GameObject trapObject)
        {
            if (!IsEmpty) return false;
            OccupiedBy = trapObject;
            RefreshVisualState();    // 설치 → 가이드 숨김, 함정만 남음
            TrapSlotRegistry.NotifyOccupancyChanged();
            return true;
        }

        /// <summary>함정 제거. Build Phase UI 에서 호출합니다.</summary>
        public void Vacate()
        {
            if (OccupiedBy == null) return;
            OccupiedBy = null;
            RefreshVisualState();    // 철거 → 가이드 복원
            TrapSlotRegistry.NotifyOccupancyChanged();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsEmpty
                ? new Color(1f, 0.5f, 0f, 0.6f)
                : new Color(0.2f, 0.9f, 0.3f, 0.6f);

            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
        }
#endif
    }
}
