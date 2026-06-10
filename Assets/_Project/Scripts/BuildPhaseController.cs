using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BuildPhaseController — Build Phase 함정 설치 컨트롤러
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build Phase 동안 함정을 슬롯에 설치/철거합니다.
    ///
    /// <para><b>조작 (v1 — IMGUI HUD)</b></para>
    /// <list type="bullet">
    ///   <item>1~9: 함정 선택</item>
    ///   <item>좌클릭: 슬롯에 설치 / 우클릭: 철거 (전액 환불)</item>
    ///   <item>Enter: 플레이 시작 / (Play 중) B: 빌드로 복귀</item>
    /// </list>
    ///
    /// <para><b>의존</b>: TrapSlotRegistry(슬롯 조회), MapLoader(예산),
    /// GamePhaseManager(페이즈), TrapBase.CompatibleAnchors(호환 필터).</para>
    ///
    /// <para>HUD 는 임시 IMGUI 입니다 — 아트 확정 후 uGUI Canvas 로 교체 예정.</para>
    /// </summary>
    public class BuildPhaseController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("설치 가능한 함정 프리팹 (TrapBase 필수)")]
        [SerializeField] private List<GameObject> trapPrefabs = new List<GameObject>();

        [Tooltip("설치된 함정의 부모. 비워두면 'PlacedTraps' 오브젝트 자동 생성.")]
        [SerializeField] private Transform trapRoot;

        // ── 공개 상태 ─────────────────────────────────────────────────────────

        /// <summary>남은 예산. 맵 로드 시 buildBudget 으로 초기화.</summary>
        public int  RemainingBudget { get; private set; }

        /// <summary>Build Phase 활성 여부.</summary>
        public bool IsActive        { get; private set; }

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private int            _selected;             // trapPrefabs 인덱스
        private Camera         _cam;

        private GameObject     _ghost;                // 마우스 추적 미리보기
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
            // 컨트롤러보다 먼저 맵이 로드된 경우 대비
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
            UpdateHover();
            HandlePlacementInput();

            // Enter → 플레이 시작
            if (kb != null && kb.enterKey.wasPressedThisFrame)
                GamePhaseManager.Instance?.SetPhase(GamePhase.Play);
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
            // 설치물은 맵 언로드 시 trapRoot 정리로 함께 제거
            for (int i = trapRoot.childCount - 1; i >= 0; i--)
                Destroy(trapRoot.GetChild(i).gameObject);
        }

        // ── 입력 — 함정 선택 ─────────────────────────────────────────────────

        private void HandleSelectionInput(Keyboard kb)
        {
            if (kb == null) return;

            for (int i = 0; i < trapPrefabs.Count && i < 9; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                {
                    _selected = i;
                    RefreshGhostSprite();
                }
            }
        }

        // ── 호버 / 고스트 ─────────────────────────────────────────────────────

        private void UpdateHover()
        {
            var mouse = Mouse.current;
            if (mouse == null || _cam == null) return;

            Vector3 world = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 origin = MapLoader.Instance != null ? MapLoader.Instance.MapOrigin : Vector2.zero;

            _hoverX = Mathf.FloorToInt(world.x - origin.x);
            _hoverY = Mathf.FloorToInt(world.y - origin.y);

            bool hasSlot = TrapSlotRegistry.TryGet(_hoverX, _hoverY, out var slot);
            _hoverValid  = hasSlot && CanPlaceAt(slot, out _);

            // 고스트 위치/색 갱신
            if (hasSlot)
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
            _ghostSR.sortingOrder = 100; // 항상 위
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

            slot.TryOccupy(inst);
            RemainingBudget -= trap.BaseCost;

            Debug.Log($"[Build] 설치: {prefab.name} @({x},{y})  잔여 예산 {RemainingBudget}");
        }

        private void TryRemove(int x, int y)
        {
            if (!TrapSlotRegistry.TryGet(x, y, out var slot) || slot.IsEmpty) return;

            var trap = slot.OccupiedBy.GetComponent<TrapBase>();
            int refund = trap != null ? trap.BaseCost : 0;

            Destroy(slot.OccupiedBy);
            slot.Vacate();
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

        // ── 임시 HUD (IMGUI) — 아트 확정 후 uGUI 로 교체 ─────────────────────

        private void OnGUI()
        {
            if (!IsActive)
            {
                if (GamePhaseManager.Instance != null &&
                    GamePhaseManager.Instance.currentPhase == GamePhase.Play)
                    GUI.Label(new Rect(10, 10, 300, 22), "[B] 빌드 페이즈로 돌아가기");
                return;
            }

            const float W = 250f;
            float h = 86f + trapPrefabs.Count * 22f;
            GUI.Box(new Rect(10, 10, W, h), "🔨 Build Phase");

            float y = 34f;
            GUI.Label(new Rect(20, y, W - 20, 20), $"예산: {RemainingBudget}");
            y += 24f;

            for (int i = 0; i < trapPrefabs.Count; i++)
            {
                var trap = trapPrefabs[i] != null ? trapPrefabs[i].GetComponent<TrapBase>() : null;
                string cost = trap != null ? trap.BaseCost.ToString() : "?";
                string mark = i == _selected ? "▶" : "  ";
                GUI.Label(new Rect(20, y, W - 20, 20),
                    $"{mark} [{i + 1}] {(trapPrefabs[i] != null ? trapPrefabs[i].name : "—")}  ({cost})");
                y += 22f;
            }

            GUI.Label(new Rect(20, y, W - 20, 20), "좌클릭 설치 · 우클릭 철거 · Enter 시작");
        }
    }
}
