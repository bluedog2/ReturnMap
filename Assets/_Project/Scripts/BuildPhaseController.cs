using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BuildPhaseController — Build Phase 함정 설치 컨트롤러
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build Phase 동안 함정을 슬롯에 설치/철거합니다.
    ///
    /// <para><b>조작</b></para>
    /// <list type="bullet">
    ///   <item>HUD 버튼 클릭 또는 1~9 키: 함정 선택</item>
    ///   <item>슬롯 가이드 이미지 좌클릭: 설치 (설치되면 가이드는 숨고 함정만 남음)</item>
    ///   <item>설치된 함정 우클릭: 철거 (전액 환불, 가이드 복원)</item>
    ///   <item>Enter: 플레이 시작 / (Play 중) B: 빌드로 복귀</item>
    /// </list>
    ///
    /// <para><b>HUD</b>: <see cref="hudPrefab"/> (uGUI Canvas 프리팹) 을 Awake 에서
    /// 인스턴스화합니다. HUD 로직은 <see cref="BuildHudController"/> 가 담당.</para>
    /// </summary>
    public class BuildPhaseController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("설치 가능한 함정 프리팹 (TrapBase 필수)")]
        [SerializeField] private List<GameObject> trapPrefabs = new List<GameObject>();

        [Header("UI")]
        [Tooltip("Build HUD 패널 프리팹 (Canvas 없음). Awake 에서 uiRoot 아래에 인스턴스화.")]
        [SerializeField] private GameObject hudPrefab;

        [Tooltip("HUD 를 붙일 씬의 Canvas 루트. 비워두면 씬에서 Canvas 자동 탐색.")]
        [SerializeField] private Transform uiRoot;

        [Tooltip("설치된 함정의 부모. 비워두면 'PlacedTraps' 오브젝트 자동 생성.")]
        [SerializeField] private Transform trapRoot;

        // ── 공개 상태 (HUD 가 읽음) ──────────────────────────────────────────

        /// <summary>남은 예산. 맵 로드 시 buildBudget 으로 초기화.</summary>
        public int  RemainingBudget { get; private set; }

        /// <summary>Build Phase 활성 여부.</summary>
        public bool IsActive        { get; private set; }

        /// <summary>현재 선택된 함정 인덱스.</summary>
        public int  SelectedIndex   => _selected;

        /// <summary>설치 가능한 함정 프리팹 목록 (읽기 전용).</summary>
        public IReadOnlyList<GameObject> TrapPrefabs => trapPrefabs;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private int            _selected;
        private Camera         _cam;

        private GameObject     _ghost;
        private SpriteRenderer _ghostSR;

        private int  _hoverX = int.MinValue, _hoverY;
        private bool _hoverValid;

        private static readonly Color GhostOk  = new Color(0.3f, 1f, 0.4f, 0.55f);
        private static readonly Color GhostBad = new Color(1f, 0.25f, 0.25f, 0.55f);

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam = Camera.main;
            EnsureTrapRoot();

            if (hudPrefab != null)
            {
                // 씬의 공유 Canvas 아래에 패널을 로드 (UI 전부 한 캔버스 공유)
                Transform parent = uiRoot;
                if (parent == null)
                {
                    var canvas = FindFirstObjectByType<Canvas>();
                    parent = canvas != null ? canvas.transform : null;
                }

                if (parent != null)
                    Instantiate(hudPrefab, parent, false);
                else
                    Debug.LogWarning("[BuildPhaseController] 씬에 Canvas 가 없습니다 — " +
                                     "메뉴 'ReTrap → Setup → 함정 프리팹 + Build UI 세팅' 실행 필요");
            }
            else
            {
                Debug.LogWarning("[BuildPhaseController] HUD 프리팹 미할당 — " +
                                 "메뉴 'ReTrap → Setup → 함정 프리팹 + Build UI 세팅' 실행 필요");
            }
        }

        private void OnEnable()
        {
            GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
            MapLoader.OnMapLoaded           += HandleMapLoaded;
        }

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
            MapLoader.OnMapLoaded           -= HandleMapLoaded;
        }

        private void Start()
        {
            if (MapLoader.Instance != null && MapLoader.Instance.CurrentMap != null)
                HandleMapLoaded(MapLoader.Instance.CurrentMap);

            if (GamePhaseManager.Instance != null)
                HandlePhaseChanged(GamePhaseManager.Instance.currentPhase);
        }

        private void Update()
        {
            var kb = Keyboard.current;

            // Play 중 B 키 → 빌드 복귀 (개발 편의)
            if (!IsActive)
            {
                if (kb != null && kb.bKey.wasPressedThisFrame)
                    GamePhaseManager.Instance?.SetPhase(GamePhase.Build);
                return;
            }

            HandleSelectionInput(kb);

            // 마우스가 UI 위에 있으면 월드 설치 입력 차단 (버튼 클릭이 설치로 새는 것 방지)
            bool overUI = EventSystem.current != null &&
                          EventSystem.current.IsPointerOverGameObject();

            if (overUI)
            {
                SetGhostVisible(false);
            }
            else
            {
                UpdateHover();
                HandlePlacementInput();
            }

            // Enter → 플레이 시작
            if (kb != null && kb.enterKey.wasPressedThisFrame)
                GamePhaseManager.Instance?.SetPhase(GamePhase.Play);
        }

        // ── 공개 API — HUD 버튼이 호출 ───────────────────────────────────────

        /// <summary>함정 선택. HUD 버튼 / 숫자 키 양쪽에서 사용.</summary>
        public void SelectTrap(int index)
        {
            if (index < 0 || index >= trapPrefabs.Count) return;
            _selected = index;
            RefreshGhostSprite();
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            IsActive = phase == GamePhase.Build;
            if (!IsActive) SetGhostVisible(false);
        }

        private void HandleMapLoaded(MapData map)
        {
            RemainingBudget = map.buildBudget;
            for (int i = trapRoot.childCount - 1; i >= 0; i--)
                Destroy(trapRoot.GetChild(i).gameObject);
        }

        // ── 입력 — 함정 선택 (키보드 단축키) ─────────────────────────────────

        private void HandleSelectionInput(Keyboard kb)
        {
            if (kb == null) return;

            for (int i = 0; i < trapPrefabs.Count && i < 9; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                    SelectTrap(i);
            }
        }

        // ── 호버 / 고스트 ─────────────────────────────────────────────────────

        private void UpdateHover()
        {
            var mouse = Mouse.current;
            if (mouse == null || _cam == null) return;

            Vector3 world  = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 origin = MapLoader.Instance != null ? MapLoader.Instance.MapOrigin : Vector2.zero;

            _hoverX = Mathf.FloorToInt(world.x - origin.x);
            _hoverY = Mathf.FloorToInt(world.y - origin.y);

            bool hasSlot = TrapSlotRegistry.TryGet(_hoverX, _hoverY, out var slot);
            _hoverValid  = hasSlot && CanPlaceAt(slot, out _);

            // 빈 슬롯(가이드 보이는 곳) 위에서만 고스트 표시
            if (hasSlot && slot.IsEmpty)
            {
                EnsureGhost();
                _ghost.transform.position = slot.transform.position;
                _ghostSR.color = _hoverValid ? GhostOk : GhostBad;
                SetGhostVisible(true);
            }
            else
            {
                SetGhostVisible(false);
            }
        }

        private void EnsureGhost()
        {
            if (_ghost != null) return;

            _ghost = new GameObject("TrapGhost");
            _ghost.transform.SetParent(transform, false);
            _ghostSR = _ghost.AddComponent<SpriteRenderer>();
            _ghostSR.sortingOrder = 100;
            RefreshGhostSprite();
        }

        private void RefreshGhostSprite()
        {
            if (_ghostSR == null) return;
            var prefab = SelectedPrefab();
            _ghostSR.sprite = prefab != null
                ? prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite
                : null;
        }

        private void SetGhostVisible(bool visible)
        {
            if (_ghost != null) _ghost.SetActive(visible);
        }

        // ── 입력 — 설치 / 철거 ───────────────────────────────────────────────

        private void HandlePlacementInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
                TryPlace(_hoverX, _hoverY);
            else if (mouse.rightButton.wasPressedThisFrame)
                TryRemove(_hoverX, _hoverY);
        }

        /// <summary>설치 가능 여부 + 불가 사유.</summary>
        private bool CanPlaceAt(TrapSlotMarker slot, out string reason)
        {
            reason = null;

            var prefab = SelectedPrefab();
            if (prefab == null)                { reason = "선택된 함정 없음";  return false; }
            if (slot == null)                  { reason = "슬롯 아님";        return false; }
            if (!slot.IsEmpty)                 { reason = "이미 설치됨";      return false; }

            var trap = prefab.GetComponent<TrapBase>();
            if (trap == null)                  { reason = "TrapBase 없는 프리팹"; return false; }
            if (!trap.IsCompatibleWith(slot.Anchor))
                                               { reason = $"{slot.Anchor} 슬롯과 비호환"; return false; }
            if (trap.BaseCost > RemainingBudget)
                                               { reason = "예산 부족";        return false; }
            return true;
        }

        private void TryPlace(int x, int y)
        {
            if (!TrapSlotRegistry.TryGet(x, y, out var slot)) return;
            if (!CanPlaceAt(slot, out string reason))
            {
                if (reason != null) Debug.Log($"[Build] 설치 불가 ({x},{y}): {reason}");
                return;
            }

            var prefab = SelectedPrefab();
            var inst   = Instantiate(prefab, slot.transform.position, Quaternion.identity, trapRoot);
            inst.name  = $"{prefab.name}_{x}_{y}";

            // 방향 자동 설정 — Start(OnNormal) 이전이므로 안전
            var trap = inst.GetComponent<TrapBase>();
            trap.ConfigureForAnchor(slot.Anchor);

            slot.TryOccupy(inst);   // 가이드 숨김은 마커가 처리
            RemainingBudget -= trap.BaseCost;

            Debug.Log($"[Build] 설치: {prefab.name} @({x},{y})  잔여 예산 {RemainingBudget}");
        }

        private void TryRemove(int x, int y)
        {
            if (!TrapSlotRegistry.TryGet(x, y, out var slot) || slot.IsEmpty) return;

            var trap   = slot.OccupiedBy.GetComponent<TrapBase>();
            int refund = trap != null ? trap.BaseCost : 0;

            Destroy(slot.OccupiedBy);
            slot.Vacate();          // 가이드 복원은 마커가 처리
            RemainingBudget += refund;

            Debug.Log($"[Build] 철거 @({x},{y})  환불 {refund} → 잔여 예산 {RemainingBudget}");
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        private GameObject SelectedPrefab()
            => (_selected >= 0 && _selected < trapPrefabs.Count) ? trapPrefabs[_selected] : null;

        private void EnsureTrapRoot()
        {
            if (trapRoot != null) return;
            var go = new GameObject("PlacedTraps");
            trapRoot = go.transform;
        }
    }
}
