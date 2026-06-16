using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BuildHudController — Build Phase uGUI HUD (프리팹 루트에 부착)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build HUD Canvas 프리팹의 로직.
    /// <list type="bullet">
    ///   <item>함정 버튼을 <see cref="BuildPhaseController.TrapPrefabs"/> 로부터 동적 생성</item>
    ///   <item>버튼 클릭 → <see cref="BuildPhaseController.SelectTrap"/></item>
    ///   <item>예산·선택 하이라이트 실시간 갱신</item>
    ///   <item>Build 페이즈에만 패널 표시, Play 중엔 복귀 힌트만</item>
    /// </list>
    /// </summary>
    public class BuildHudController : MonoBehaviour
    {
        // ── Inspector (프리팹 빌더가 할당) ───────────────────────────────────

        [SerializeField] private GameObject panel;            // Build 패널 루트
        [SerializeField] private Text       budgetText;
        [SerializeField] private Transform  buttonContainer;  // 함정 버튼 부모
        [SerializeField] private Text       hintText;
        [SerializeField] private GameObject playHint;         // Play 중 "[B] 빌드 복귀"

        // ── 내부 ─────────────────────────────────────────────────────────────

        private BuildPhaseController _ctrl;
        private readonly List<Image> _buttonFrames = new List<Image>();
        private Image                _removeFrame;   // 철거 모드 버튼 프레임

        // 핫바 슬롯 룩
        private const float SlotSize   = 56f;   // 정사각 슬롯 한 변
        private const float FrameInset = 3f;    // 바깥 테두리 두께

        private static readonly Color FrameSelected   = new Color(1f, 0.85f, 0.2f, 1f);     // 선택 = 금색 테두리
        private static readonly Color FrameUnselected = new Color(0.32f, 0.33f, 0.40f, 1f); // 평소 = 회색 테두리
        private static readonly Color FrameRemove     = new Color(1f, 0.3f, 0.3f, 1f);      // 철거 = 빨간 테두리
        private static readonly Color SlotBackground  = new Color(0.09f, 0.09f, 0.12f, 0.95f);

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Start()
        {
            _ctrl = FindFirstObjectByType<BuildPhaseController>();
            if (_ctrl == null)
            {
                Debug.LogWarning("[BuildHud] BuildPhaseController 를 찾지 못했습니다.");
                enabled = false;
                return;
            }

            // 함정 프리팹은 어드레서블로 비동기 로드되므로, 준비된 뒤 버튼을 생성한다.
            if (_ctrl.TrapsReady)
                BuildTrapButtons();
            else
                _ctrl.OnTrapsReady += BuildTrapButtons;

            if (hintText != null)
                hintText.text = "버튼/1~3: 선택 · X: 철거 모드 · 슬롯 클릭: 설치 · 우클릭: 철거 · Enter: 시작";
        }

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;

        private void OnDestroy()
        {
            if (_ctrl != null) _ctrl.OnTrapsReady -= BuildTrapButtons;
        }

        private void Update()
        {
            if (_ctrl == null) return;

            // 예산
            if (budgetText != null)
                budgetText.text = $"예산  {_ctrl.RemainingBudget}";

            // 선택 하이라이트 — 철거 모드 중엔 함정 선택 강조 해제
            for (int i = 0; i < _buttonFrames.Count; i++)
                _buttonFrames[i].color =
                    (!_ctrl.IsRemoveMode && i == _ctrl.SelectedIndex) ? FrameSelected : FrameUnselected;

            // 철거 모드 버튼 하이라이트
            if (_removeFrame != null)
                _removeFrame.color = _ctrl.IsRemoveMode ? FrameRemove : FrameUnselected;
        }

        // ── 페이즈 ────────────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            bool build = phase == GamePhase.Build;
            if (panel    != null) panel.SetActive(build);
            if (playHint != null) playHint.SetActive(!build);
        }

        // ── 함정 버튼 동적 생성 ──────────────────────────────────────────────

        private bool _buttonsBuilt;

        private void BuildTrapButtons()
        {
            if (_buttonsBuilt) return;          // 중복 생성 방지 (이벤트 재진입 대비)
            if (buttonContainer == null) return;
            _buttonsBuilt = true;

            if (_ctrl != null) _ctrl.OnTrapsReady -= BuildTrapButtons;

            var prefabs = _ctrl.TrapPrefabs;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int index = i; // 클로저 캡처
                var prefab = prefabs[i];
                if (prefab == null) continue;

                var trap  = prefab.GetComponent<TrapBase>();
                var srcSR = ResolveIconRenderer(prefab);

                // 키번호는 1~9 까지만 표시 (그 이상은 숫자 키 매핑 없음)
                string keyLabel = index < 9 ? (index + 1).ToString() : "";

                var slot = CreateSlot($"TrapSlot_{prefab.name}", keyLabel, out var frame);
                _buttonFrames.Add(frame);
                slot.GetComponent<Button>().onClick.AddListener(() => _ctrl.SelectTrap(index));

                AddSpriteIcon(slot.transform,
                              srcSR != null ? srcSR.sprite : null,
                              srcSR != null ? srcSR.color  : Color.white);

                AddCornerCost(slot.transform,
                              trap != null ? trap.BaseCost.ToString() : "?",
                              new Color(1f, 0.9f, 0.4f));
            }

            BuildRemoveModeButton();
        }

        /// <summary>
        /// 핫바 아이콘으로 쓸 함정의 대표 SpriteRenderer 를 찾습니다.
        /// <para>"Visual" 자식(함정 본체 아트)을 우선 사용합니다. 함정 셀 뒤에 깔리는
        /// "TileBackground"(지형 타일)는 아이콘에서 제외해야 함정 그림이 보입니다.</para>
        /// </summary>
        private static SpriteRenderer ResolveIconRenderer(GameObject prefab)
        {
            // 1순위: "Visual" 명명 자식
            var visual = prefab.transform.Find("Visual");
            if (visual != null && visual.TryGetComponent<SpriteRenderer>(out var vsr) && vsr.sprite != null)
                return vsr;

            // 2순위: TileBackground 가 아닌 첫 스프라이트 (스프라이트가 있는 것 우선)
            SpriteRenderer fallback = null;
            foreach (var sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.gameObject.name == "TileBackground") continue;
                if (sr.sprite != null) return sr;
                fallback ??= sr;
            }
            return fallback;
        }

        // ── 철거 모드 버튼 ───────────────────────────────────────────────────

        /// <summary>핫바 끝에 ✕ 철거 모드 토글 슬롯을 추가합니다 (X 키와 동일).</summary>
        private void BuildRemoveModeButton()
        {
            var slot = CreateSlot("RemoveModeSlot", "X", out _removeFrame);
            slot.GetComponent<Button>().onClick.AddListener(() => _ctrl.ToggleRemoveMode());

            AddTextIcon(slot.transform, "✕", 30, new Color(1f, 0.45f, 0.45f));
            AddCornerCost(slot.transform, "철거", new Color(0.9f, 0.9f, 0.9f));
        }

        // ── 슬롯 빌딩 헬퍼 ────────────────────────────────────────────────────

        /// <summary>
        /// MMO 핫바 슬롯 한 칸 생성: 바깥 테두리(frame) + 안쪽 배경(Inner) 2겹,
        /// 좌상단 키번호. 아이콘·코스트는 호출 측에서 덧붙입니다.
        /// </summary>
        private GameObject CreateSlot(string name, string keyLabel, out Image frame)
        {
            var slotGo = new GameObject(name,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            slotGo.transform.SetParent(buttonContainer, false);

            frame       = slotGo.GetComponent<Image>();
            frame.color = FrameUnselected;

            var layout = slotGo.GetComponent<LayoutElement>();
            layout.preferredWidth  = SlotSize;
            layout.preferredHeight = SlotSize;

            var button = slotGo.GetComponent<Button>();
            button.targetGraphic = frame;

            // 안쪽 배경 — 테두리 두께만큼 inset 해서 베벨 느낌
            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(slotGo.transform, false);
            var innerRect = (RectTransform)innerGo.transform;
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(FrameInset, FrameInset);
            innerRect.offsetMax = new Vector2(-FrameInset, -FrameInset);

            var inner = innerGo.GetComponent<Image>();
            inner.color         = SlotBackground;
            inner.raycastTarget = false;

            // 좌상단 키번호
            if (!string.IsNullOrEmpty(keyLabel))
            {
                var keyGo = new GameObject("Key", typeof(RectTransform), typeof(Text));
                keyGo.transform.SetParent(slotGo.transform, false);
                var keyRect = (RectTransform)keyGo.transform;
                keyRect.anchorMin        = new Vector2(0f, 1f);
                keyRect.anchorMax        = new Vector2(0f, 1f);
                keyRect.pivot            = new Vector2(0f, 1f);
                keyRect.anchoredPosition = new Vector2(4f, -2f);
                keyRect.sizeDelta        = new Vector2(20f, 16f);

                var key = keyGo.GetComponent<Text>();
                key.text          = keyLabel;
                key.font          = BuiltinFont();
                key.fontSize      = 13;
                key.fontStyle     = FontStyle.Bold;
                key.alignment     = TextAnchor.UpperLeft;
                key.color         = new Color(1f, 1f, 1f, 0.85f);
                key.raycastTarget = false;
            }

            return slotGo;
        }

        /// <summary>슬롯 중앙에 스프라이트 아이콘을 채웁니다.</summary>
        private static void AddSpriteIcon(Transform slot, Sprite sprite, Color tint)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slot, false);
            FillCenter((RectTransform)iconGo.transform, 0.14f);

            var icon = iconGo.GetComponent<Image>();
            icon.sprite         = sprite;
            icon.color          = tint;
            icon.preserveAspect = true;
            icon.raycastTarget  = false;
        }

        /// <summary>슬롯 중앙에 텍스트 아이콘(✕ 등)을 채웁니다.</summary>
        private static void AddTextIcon(Transform slot, string glyph, int size, Color color)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Text));
            iconGo.transform.SetParent(slot, false);
            FillCenter((RectTransform)iconGo.transform, 0.1f);

            var icon = iconGo.GetComponent<Text>();
            icon.text          = glyph;
            icon.font          = BuiltinFont();
            icon.fontSize      = size;
            icon.fontStyle     = FontStyle.Bold;
            icon.alignment     = TextAnchor.MiddleCenter;
            icon.color         = color;
            icon.raycastTarget = false;
        }

        /// <summary>슬롯 우하단 코스트(또는 라벨) 표시.</summary>
        private static void AddCornerCost(Transform slot, string text, Color color)
        {
            var costGo = new GameObject("Cost", typeof(RectTransform), typeof(Text));
            costGo.transform.SetParent(slot, false);
            var costRect = (RectTransform)costGo.transform;
            costRect.anchorMin        = new Vector2(1f, 0f);
            costRect.anchorMax        = new Vector2(1f, 0f);
            costRect.pivot            = new Vector2(1f, 0f);
            costRect.anchoredPosition = new Vector2(-4f, 2f);
            costRect.sizeDelta        = new Vector2(40f, 16f);

            var cost = costGo.GetComponent<Text>();
            cost.text          = text;
            cost.font          = BuiltinFont();
            cost.fontSize      = 13;
            cost.fontStyle     = FontStyle.Bold;
            cost.alignment     = TextAnchor.LowerRight;
            cost.color         = color;
            cost.raycastTarget = false;
        }

        /// <summary>부모를 꽉 채우되 percent 만큼 사방 여백을 둔 RectTransform 설정.</summary>
        private static void FillCenter(RectTransform r, float marginPercent)
        {
            r.anchorMin = new Vector2(marginPercent, marginPercent);
            r.anchorMax = new Vector2(1f - marginPercent, 1f - marginPercent);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        /// <summary>Unity 내장 기본 폰트.</summary>
        public static Font BuiltinFont()
            => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
