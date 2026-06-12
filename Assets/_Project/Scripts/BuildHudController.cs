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

        private static readonly Color FrameSelected   = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color FrameUnselected = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color FrameRemove     = new Color(1f, 0.3f, 0.3f, 1f);

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

                var trap   = prefab.GetComponent<TrapBase>();
                var srcSR  = prefab.GetComponentInChildren<SpriteRenderer>(true);

                // ── 버튼 루트 (프레임 배경 = 선택 하이라이트) ────────────────
                var btnGo = new GameObject($"TrapButton_{prefab.name}",
                    typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                btnGo.transform.SetParent(buttonContainer, false);

                var frame = btnGo.GetComponent<Image>();
                frame.color = FrameUnselected;
                _buttonFrames.Add(frame);

                var layout = btnGo.GetComponent<LayoutElement>();
                layout.preferredWidth  = 64f;
                layout.preferredHeight = 64f;

                var button = btnGo.GetComponent<Button>();
                button.targetGraphic = frame;
                button.onClick.AddListener(() => _ctrl.SelectTrap(index));

                // ── 함정 아이콘 ──────────────────────────────────────────────
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(btnGo.transform, false);
                var iconRect = (RectTransform)iconGo.transform;
                iconRect.anchorMin = new Vector2(0.1f, 0.25f);
                iconRect.anchorMax = new Vector2(0.9f, 0.95f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;

                var icon = iconGo.GetComponent<Image>();
                icon.sprite         = srcSR != null ? srcSR.sprite : null;
                icon.color          = srcSR != null ? srcSR.color  : Color.white;
                icon.preserveAspect = true;
                icon.raycastTarget  = false;

                // ── 코스트 라벨 ──────────────────────────────────────────────
                var costGo = new GameObject("Cost", typeof(RectTransform), typeof(Text));
                costGo.transform.SetParent(btnGo.transform, false);
                var costRect = (RectTransform)costGo.transform;
                costRect.anchorMin = new Vector2(0f, 0f);
                costRect.anchorMax = new Vector2(1f, 0.28f);
                costRect.offsetMin = Vector2.zero;
                costRect.offsetMax = Vector2.zero;

                var cost = costGo.GetComponent<Text>();
                cost.text          = trap != null ? trap.BaseCost.ToString() : "?";
                cost.font          = BuiltinFont();
                cost.fontSize      = 14;
                cost.fontStyle     = FontStyle.Bold;
                cost.alignment     = TextAnchor.MiddleCenter;
                cost.color         = new Color(1f, 0.9f, 0.4f);
                cost.raycastTarget = false;
            }

            BuildRemoveModeButton();
        }

        // ── 철거 모드 버튼 ───────────────────────────────────────────────────

        /// <summary>함정 버튼 뒤에 ✕ 철거 모드 토글 버튼을 추가합니다 (X 키와 동일).</summary>
        private void BuildRemoveModeButton()
        {
            var btnGo = new GameObject("RemoveModeButton",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(buttonContainer, false);

            _removeFrame       = btnGo.GetComponent<Image>();
            _removeFrame.color = FrameUnselected;

            var layout = btnGo.GetComponent<LayoutElement>();
            layout.preferredWidth  = 64f;
            layout.preferredHeight = 64f;

            var button = btnGo.GetComponent<Button>();
            button.targetGraphic = _removeFrame;
            button.onClick.AddListener(() => _ctrl.ToggleRemoveMode());

            // ✕ 아이콘 텍스트
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Text));
            iconGo.transform.SetParent(btnGo.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(0f, 0.25f);
            iconRect.anchorMax = new Vector2(1f, 0.95f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var icon = iconGo.GetComponent<Text>();
            icon.text          = "✕";
            icon.font          = BuiltinFont();
            icon.fontSize      = 32;
            icon.fontStyle     = FontStyle.Bold;
            icon.alignment     = TextAnchor.MiddleCenter;
            icon.color         = new Color(1f, 0.4f, 0.4f);
            icon.raycastTarget = false;

            // "철거" 라벨 (코스트 라벨 자리)
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0.28f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<Text>();
            label.text          = "철거";
            label.font          = BuiltinFont();
            label.fontSize      = 13;
            label.fontStyle     = FontStyle.Bold;
            label.alignment     = TextAnchor.MiddleCenter;
            label.color         = new Color(0.9f, 0.9f, 0.9f);
            label.raycastTarget = false;
        }

        /// <summary>Unity 내장 기본 폰트.</summary>
        public static Font BuiltinFont()
            => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
