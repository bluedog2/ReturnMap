using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

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
    /// <para><b>HUD</b>: <see cref="hudRef"/> (어드레서블 uGUI 프리팹) 을 Awake 에서
    /// 비동기 인스턴스화합니다. HUD 로직은 <see cref="BuildHudController"/> 가 담당.</para>
    /// </summary>
    public class BuildPhaseController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("설치 가능한 함정 (TrapDefinition · 스펙+프리팹 참조 중앙화)")]
        [Tooltip("함정 스펙 SO 목록. 각 정의의 prefabRef 로 Addressable 프리팹을 비동기 로드합니다 " +
                 "(빌드 중복 제거). Awake 에서 비동기 로드 → 완료 시 OnTrapsReady 발행.")]
        [SerializeField] private List<TrapDefinition> trapDefinitions = new List<TrapDefinition>();

        [Header("UI")]
        [Tooltip("Build HUD 패널 프리팹 (Addressable). 씬에 임베드되지 않고 UI 번들에서 로드 후 " +
                 "uiRoot 아래에 인스턴스화합니다.")]
        [SerializeField] private AssetReferenceGameObject hudRef;

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

        /// <summary>철거 모드 — 좌클릭으로 설치된 함정을 제거(전액 환불). HUD 가 읽음.</summary>
        public bool IsRemoveMode    { get; private set; }

        /// <summary>설치 가능한 (로드 완료된) 함정 프리팹 목록 (읽기 전용).
        /// 비동기 로드 전에는 비어 있습니다 — <see cref="TrapsReady"/> / <see cref="OnTrapsReady"/> 참고.</summary>
        public IReadOnlyList<GameObject> TrapPrefabs => _loadedTrapPrefabs;

        /// <summary>설치 가능한 (로드 완료된) 함정의 스펙 정의 목록 (읽기 전용).
        /// <see cref="TrapPrefabs"/> 와 인덱스가 1:1 로 대응합니다.
        /// 비동기 로드 전에는 비어 있습니다 — <see cref="TrapsReady"/> / <see cref="OnTrapsReady"/> 참고.</summary>
        public IReadOnlyList<TrapDefinition> TrapDefinitions => _loadedTrapDefinitions;

        /// <summary>어드레서블 함정 프리팹 로드 완료 여부.</summary>
        public bool TrapsReady { get; private set; }

        /// <summary>함정 프리팹 비동기 로드가 끝나 <see cref="TrapPrefabs"/> 가 채워졌을 때 발행.
        /// HUD 가 버튼을 이 시점에 생성합니다.</summary>
        public event Action OnTrapsReady;

        /// <summary><see cref="RemainingBudget"/> 이 외부(예: 가성비 타파 훅)에 의해 변경됐을 때 발행.
        /// 인자는 변경 후 잔여 예산. HUD 는 매 프레임 폴링하므로 구독은 선택.</summary>
        public event Action<int> OnBudgetChanged;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private int            _selected;
        private Camera         _cam;

        // 어드레서블에서 로드한 함정 프리팹 캐시 + 해제용 핸들
        private readonly List<GameObject> _loadedTrapPrefabs = new List<GameObject>();
        // TrapPrefabs 와 인덱스가 1:1 대응하는 definition 캐시 (null 필터링 후)
        private readonly List<TrapDefinition> _loadedTrapDefinitions = new List<TrapDefinition>();
        private readonly List<AsyncOperationHandle<GameObject>> _trapHandles =
            new List<AsyncOperationHandle<GameObject>>();

        // HUD 어드레서블 인스턴스 핸들 (해제용)
        private AsyncOperationHandle<GameObject> _hudHandle;
        private bool _hudHandleValid;

        private GameObject     _ghost;
        private SpriteRenderer _ghostSR;

        private int  _hoverX = int.MinValue, _hoverY;
        private bool _hoverValid;

        private static readonly Color GhostOk  = new Color(0.3f, 1f, 0.4f, 0.55f);
        private static readonly Color GhostBad = new Color(1f, 0.25f, 0.25f, 0.55f);

        /// <summary>선택된 함정을 설치할 수 있는 슬롯의 강조 색 (초록).</summary>
        private static readonly Color SlotHighlight = new Color(0.3f, 1f, 0.4f, 0.85f);

        /// <summary>철거 모드에서 호버한 설치 함정 위 표시 색 (빨강 반투명).</summary>
        private static readonly Color RemoveHover = new Color(1f, 0.25f, 0.25f, 0.45f);

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam = Camera.main;
            EnsureTrapRoot();

            LoadTraps();
            LoadHud();
        }

        // ── 어드레서블 HUD 로드 ───────────────────────────────────────────────

        /// <summary>
        /// <see cref="hudRef"/> 를 비동기 인스턴스화해 씬 공유 Canvas 아래에 붙입니다.
        /// InstantiateAsync 는 핸들 해제 시 인스턴스도 함께 파괴되므로 OnDestroy 에서 정리합니다.
        /// </summary>
        private void LoadHud()
        {
            if (hudRef == null || !hudRef.RuntimeKeyIsValid())
            {
                Debug.LogWarning("[BuildPhaseController] HUD 어드레서블 참조 미할당/무효 — " +
                                 "메뉴 'ReTrap → Setup → 3. 함정 프리팹 + Build UI 세팅' 실행 필요");
                return;
            }

            Transform parent = uiRoot;
            if (parent == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                parent = canvas != null ? canvas.transform : null;
            }

            if (parent == null)
            {
                Debug.LogWarning("[BuildPhaseController] 씬에 Canvas 가 없습니다 — " +
                                 "메뉴 'ReTrap → Setup → 3. 함정 프리팹 + Build UI 세팅' 실행 필요");
                return;
            }

            _hudHandle      = hudRef.InstantiateAsync(parent, false);
            _hudHandleValid = true;
            _hudHandle.Completed += h =>
            {
                if (h.Status != AsyncOperationStatus.Succeeded)
                    Debug.LogError("[BuildPhaseController] HUD 어드레서블 로드 실패");
            };
        }

        private void OnEnable()
        {
            GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
            MapLoader.OnMapLoaded           += HandleMapLoaded;
            TrapSlotRegistry.OnChanged      += RefreshSlotHighlights;
        }

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
            MapLoader.OnMapLoaded           -= HandleMapLoaded;
            TrapSlotRegistry.OnChanged      -= RefreshSlotHighlights;
        }

        private void OnDestroy()
        {
            // 로드한 함정 프리팹 핸들 해제 (씬 종료 시) — 인스턴스는 이미 파괴된 뒤이므로 안전
            foreach (var h in _trapHandles)
                if (h.IsValid()) Addressables.Release(h);
            _trapHandles.Clear();
            _loadedTrapPrefabs.Clear();
            _loadedTrapDefinitions.Clear();

            // HUD 인스턴스 핸들 해제 (InstantiateAsync → ReleaseInstance 로 인스턴스까지 정리)
            if (_hudHandleValid && _hudHandle.IsValid())
                Addressables.ReleaseInstance(_hudHandle);
            _hudHandleValid = false;
        }

        // ── 어드레서블 함정 로드 ──────────────────────────────────────────────

        /// <summary>
        /// <see cref="trapDefinitions"/> 각각의 prefabRef 로 함정 프리팹을 비동기 로드합니다.
        /// 완료되면 <see cref="_loadedTrapPrefabs"/> 를 채우고 <see cref="OnTrapsReady"/> 를 발행해
        /// HUD 가 버튼을 생성하도록 합니다. 직접 씬 참조가 없어 빌드 중복이 제거됩니다.
        /// </summary>
        private void LoadTraps()
        {
            int total = trapDefinitions != null ? trapDefinitions.Count : 0;
            if (total == 0)
            {
                FinalizeTrapLoad(new GameObject[0]);
                return;
            }

            var results = new GameObject[total];
            int pending = total;

            for (int i = 0; i < total; i++)
            {
                var def   = trapDefinitions[i];
                var aref  = def != null ? def.PrefabRef : null;
                if (aref == null || !aref.RuntimeKeyIsValid())
                {
                    Debug.LogWarning($"[BuildPhaseController] trapDefinitions[{i}] 의 prefabRef 가 " +
                                     "비어있거나 유효하지 않은 어드레서블 참조입니다.");
                    if (--pending == 0) FinalizeTrapLoad(results);
                    continue;
                }

                int index   = i; // 클로저 캡처
                var handle  = aref.LoadAssetAsync<GameObject>();
                _trapHandles.Add(handle);
                handle.Completed += h =>
                {
                    if (h.Status == AsyncOperationStatus.Succeeded)
                        results[index] = h.Result;
                    else
                        Debug.LogError($"[BuildPhaseController] 함정 프리팹 로드 실패: trapDefinitions[{index}]");

                    if (--pending == 0) FinalizeTrapLoad(results);
                };
            }
        }

        /// <summary>
        /// 로드 결과를 캐시에 반영하고 준비 완료를 알립니다 (null 항목은 제외).
        /// <see cref="_loadedTrapDefinitions"/> 도 같은 기준으로 필터링해 <see cref="TrapPrefabs"/> 와
        /// 인덱스가 항상 1:1 로 맞도록 유지합니다 (HUD·설치 로직이 인덱스로 양쪽을 참조).
        /// </summary>
        private void FinalizeTrapLoad(GameObject[] results)
        {
            _loadedTrapPrefabs.Clear();
            _loadedTrapDefinitions.Clear();

            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] == null) continue;
                _loadedTrapPrefabs.Add(results[i]);
                _loadedTrapDefinitions.Add(trapDefinitions[i]);
            }

            TrapsReady = true;
            RefreshGhostSprite();
            RefreshSlotHighlights();
            OnTrapsReady?.Invoke();

            Debug.Log($"[BuildPhaseController] 함정 프리팹 로드 완료: {_loadedTrapPrefabs.Count}개");
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

            // Enter → 빌드 완료 (검증 페이즈 사용 여부는 GamePhaseManager 가 결정)
            if (kb != null && kb.enterKey.wasPressedThisFrame)
                GamePhaseManager.Instance?.AdvanceFromBuild();
        }

        // ── 공개 API — HUD 버튼이 호출 ───────────────────────────────────────

        /// <summary>함정 선택. HUD 버튼 / 숫자 키 양쪽에서 사용. 철거 모드는 자동 해제.</summary>
        public void SelectTrap(int index)
        {
            if (index < 0 || index >= _loadedTrapPrefabs.Count) return;
            _selected     = index;
            IsRemoveMode  = false;
            RefreshGhostSprite();
            RefreshSlotHighlights();
        }

        /// <summary>철거 모드 토글. HUD 버튼 / X 키 양쪽에서 사용.</summary>
        public void ToggleRemoveMode() => SetRemoveMode(!IsRemoveMode);

        public void SetRemoveMode(bool on)
        {
            IsRemoveMode = on;
            RefreshGhostSprite();
            RefreshSlotHighlights();
            SetGhostVisible(false); // 다음 UpdateHover 에서 모드에 맞게 다시 표시
        }

        /// <summary>
        /// 외부(가성비 타파 S-08 훅 등)에서 빌드 예산을 차감합니다. 0 미만으로는 내려가지
        /// 않으며(클램프), 변경 시 <see cref="OnBudgetChanged"/> 를 발행합니다.
        /// </summary>
        public void DrainBudget(int amount)
        {
            if (amount <= 0) return;

            RemainingBudget = Mathf.Max(0, RemainingBudget - amount);
            RefreshSlotHighlights();
            OnBudgetChanged?.Invoke(RemainingBudget);
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            IsActive = phase == GamePhase.Build;
            if (!IsActive)
            {
                SetGhostVisible(false);
                IsRemoveMode = false; // 빌드 이탈 시 철거 모드 해제
            }
            RefreshSlotHighlights();
        }

        /// <summary>
        /// 선택된 함정을 설치할 수 있는 슬롯(호환 anchor + 빈 슬롯 + 예산 내)을
        /// 초록색으로 강조합니다. 선택·예산·점유·페이즈가 바뀔 때마다 갱신.
        /// 프리팹 로드 여부와 무관하게 definition 값만으로 판단합니다.
        /// </summary>
        private void RefreshSlotHighlights()
        {
            var def = SelectedDefinition();

            foreach (var slot in TrapSlotRegistry.All)
            {
                bool canInstall = IsActive && !IsRemoveMode && def != null && slot.IsEmpty
                               && def.IsCompatibleWith(slot.Anchor)
                               && def.BaseCost <= RemainingBudget;
                slot.SetGuideHighlight(canInstall, SlotHighlight);
            }
        }

        private void HandleMapLoaded(MapData map)
        {
            RemainingBudget = map.buildBudget;
            for (int i = trapRoot.childCount - 1; i >= 0; i--)
                Destroy(trapRoot.GetChild(i).gameObject);

            // 슬롯 마커는 맵 빌드 중 먼저 등록되므로 예산 확정 후 한 번 더 갱신
            RefreshSlotHighlights();
        }

        // ── 입력 — 함정 선택 (키보드 단축키) ─────────────────────────────────

        private void HandleSelectionInput(Keyboard kb)
        {
            if (kb == null) return;

            for (int i = 0; i < _loadedTrapPrefabs.Count && i < 9; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                    SelectTrap(i);
            }

            // X → 철거 모드 토글
            if (kb.xKey.wasPressedThisFrame)
                ToggleRemoveMode();
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

            // ── 철거 모드: 설치된 함정 위에서만 빨간 표시 ────────────────────
            if (IsRemoveMode)
            {
                _hoverValid = hasSlot && !slot.IsEmpty;
                if (_hoverValid)
                {
                    EnsureGhost();
                    _ghost.transform.position = slot.transform.position;
                    _ghostSR.color = RemoveHover;
                    SetGhostVisible(true);
                }
                else
                {
                    SetGhostVisible(false);
                }
                return;
            }

            // ── 설치 모드 ─────────────────────────────────────────────────────
            _hoverValid = hasSlot && CanPlaceAt(slot, out _);

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

            // 철거 모드: 1×1 색상 블록 (빨간 오버레이용)
            if (IsRemoveMode)
            {
                _ghostSR.sprite = TrapBase.GetUnitSprite();
                return;
            }

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
            {
                if (IsRemoveMode) TryRemove(_hoverX, _hoverY);
                else              TryPlace(_hoverX, _hoverY);
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                TryRemove(_hoverX, _hoverY); // 우클릭 철거는 모드 무관 (기존 동작 유지)
            }
        }

        /// <summary>
        /// 설치 가능 여부 + 불가 사유.
        /// 코스트/anchor 호환 검사는 프리팹 로드 여부와 무관하게 definition 에서 직접 읽습니다.
        /// </summary>
        private bool CanPlaceAt(TrapSlotMarker slot, out string reason)
        {
            reason = null;

            var def = SelectedDefinition();
            if (def == null)                   { reason = "선택된 함정 없음";  return false; }
            if (slot == null)                  { reason = "슬롯 아님";        return false; }
            if (!slot.IsEmpty)                 { reason = "이미 설치됨";      return false; }

            if (!def.IsCompatibleWith(slot.Anchor))
                                               { reason = $"{slot.Anchor} 슬롯과 비호환"; return false; }
            if (def.BaseCost > RemainingBudget)
                                               { reason = "예산 부족";        return false; }

            var prefab = SelectedPrefab();
            if (prefab == null)                { reason = "함정 프리팹 로드 중"; return false; }
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

            var def    = SelectedDefinition();
            var prefab = SelectedPrefab();
            var inst   = Instantiate(prefab, slot.transform.position, Quaternion.identity, trapRoot);
            inst.name  = $"{prefab.name}_{x}_{y}";

            // 방향 자동 설정 — Start(OnNormal) 이전이므로 안전
            var trap = inst.GetComponent<TrapBase>();
            trap.ConfigureForAnchor(slot.Anchor);

            slot.TryOccupy(inst);   // 가이드 숨김은 마커가 처리
            RemainingBudget -= def.BaseCost;
            RefreshSlotHighlights(); // 예산 변동 반영 (TryOccupy 알림 시점엔 옛 예산)

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
            RefreshSlotHighlights(); // 환불 반영 (Vacate 알림 시점엔 옛 예산)

            Debug.Log($"[Build] 철거 @({x},{y})  환불 {refund} → 잔여 예산 {RemainingBudget}");
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────

        private GameObject SelectedPrefab()
            => (_selected >= 0 && _selected < _loadedTrapPrefabs.Count) ? _loadedTrapPrefabs[_selected] : null;

        /// <summary>현재 선택된 함정의 스펙 정의. 프리팹 로드 여부와 무관하게 조회 가능.</summary>
        private TrapDefinition SelectedDefinition()
            => (_selected >= 0 && _selected < _loadedTrapDefinitions.Count) ? _loadedTrapDefinitions[_selected] : null;

        private void EnsureTrapRoot()
        {
            if (trapRoot != null) return;
            var go = new GameObject("PlacedTraps");
            trapRoot = go.transform;
        }
    }
}
