using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SettingsPanel — 설정 패널 (음량 3버스 + 해상도 + 전체화면), Build HUD 부속
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build 페이즈 중에만 Esc 키(또는 <see cref="TogglePanel"/>)로 열고 닫는 설정 화면.
    /// 음량 슬라이더 3개(21단계, 5%) · 해상도 드롭다운 · 전체화면 토글 · 닫기 버튼.
    /// 다른 페이즈로 넘어가면 자동으로 닫히고, 닫을 때 <see cref="SettingsService.Flush"/> 로 저장한다.
    /// 게임을 일시정지하지 않는다(timeScale 은 VerificationSpeedController 소유).
    /// <para>컴포넌트는 항상 활성인 컨트롤러 오브젝트에 두고 <c>panelRoot</c> 만 토글한다.
    /// 씬 배선은 <c>MetaUiSetup.BuildSettingsPanel</c> 이 수행한다.</para>
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private const int BusCount = 3;   // SoundBus: Master, Bgm, Sfx 순서

        [Header("표시 UI")]
        [SerializeField]
        [Tooltip("패널 전체 표시/숨김 대상 루트.")]
        private GameObject panelRoot;

        [SerializeField]
        [Tooltip("음량 슬라이더. 인덱스 = SoundBus(0 마스터, 1 BGM, 2 효과음). wholeNumbers 0~20.")]
        private Slider[] volumeSliders;

        [SerializeField]
        [Tooltip("음량 퍼센트 텍스트. volumeSliders 와 인덱스 대응.")]
        private Text[] volumeTexts;

        [SerializeField]
        [Tooltip("해상도 드롭다운.")]
        private Dropdown resolutionDropdown;

        [SerializeField]
        [Tooltip("전체화면 토글 (켬 = FullScreenWindow).")]
        private Toggle fullscreenToggle;

        [SerializeField]
        [Tooltip("닫기 버튼.")]
        private Button closeButton;

        private Vector2Int[] _options = new Vector2Int[0];
        private bool _refreshing;   // 갱신 중 콜백 재진입 차단

        // ── 프로브/외부 접근 ──────────────────────────────────────────────────

        /// <summary>표시/숨김 대상 루트 (열림 여부 = <c>PanelRoot.activeSelf</c>).</summary>
        public GameObject PanelRoot => panelRoot;
        public Slider GetVolumeSlider(SoundBus bus) => volumeSliders[(int)bus];
        public Text GetVolumeText(SoundBus bus) => volumeTexts[(int)bus];
        public Dropdown ResolutionDropdown => resolutionDropdown;
        public Toggle FullscreenToggle => fullscreenToggle;
        public Button CloseButton => closeButton;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (volumeSliders != null)
            {
                for (int i = 0; i < volumeSliders.Length && i < BusCount; i++)
                {
                    if (volumeSliders[i] == null) continue;
                    int index = i; // 클로저 캡처
                    volumeSliders[i].onValueChanged.AddListener(v => HandleVolumeChanged(index, v));
                }
            }
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(_ => HandleDisplayChanged());
            if (fullscreenToggle != null)   fullscreenToggle.onValueChanged.AddListener(_ => HandleDisplayChanged());
            if (closeButton != null)        closeButton.onClick.AddListener(ClosePanel);
        }

        private void Start()
        {
            // 기본 상태는 항상 닫힘 — 에디터 세팅이 활성으로 두더라도 방어적으로 강제.
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                TogglePanel();
        }

        // ── 열기/닫기 ─────────────────────────────────────────────────────────

        /// <summary>패널을 열거나 닫는다. Build 페이즈가 아니면 열지 않는다. 닫을 때 저장한다.</summary>
        public void TogglePanel()
        {
            if (panelRoot == null) return;

            if (panelRoot.activeSelf)
            {
                ClosePanel();
                return;
            }

            if (GamePhaseManager.Instance == null ||
                GamePhaseManager.Instance.currentPhase != GamePhase.Build)
                return;

            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();   // 런타임에 뒤늦게 생긴 UI 위로
            RefreshAll();
        }

        private void ClosePanel()
        {
            if (panelRoot != null && panelRoot.activeSelf)
                panelRoot.SetActive(false);

            // 닫을 때 세 버스 값을 모두 기록(슬라이더 값 기준) — 안 건드린 버스도 키가 생기도록.
            if (volumeSliders != null)
            {
                for (int i = 0; i < volumeSliders.Length && i < BusCount; i++)
                {
                    if (volumeSliders[i] == null) continue;
                    SettingsService.SetVolume((SoundBus)i,
                        SettingsService.LinearFromStep(Mathf.RoundToInt(volumeSliders[i].value)));
                }
            }
            SettingsService.Flush();
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Build && panelRoot != null && panelRoot.activeSelf)
                ClosePanel();
        }

        // ── 입력 핸들러 ───────────────────────────────────────────────────────

        private void HandleVolumeChanged(int index, float stepValue)
        {
            if (_refreshing) return;

            var bus = (SoundBus)index;
            float linear = SettingsService.LinearFromStep(Mathf.RoundToInt(stepValue));
            SettingsService.SetVolume(bus, linear);
            SoundManager.Instance?.SetBusVolume(bus, linear);
            SetPercentText(index, linear);
        }

        private void HandleDisplayChanged()
        {
            if (_refreshing || resolutionDropdown == null || fullscreenToggle == null) return;
            int sel = resolutionDropdown.value;
            if (sel < 0 || sel >= _options.Length) return;
            SettingsService.SetDisplay(_options[sel].x, _options[sel].y, fullscreenToggle.isOn);
        }

        // ── 갱신 ─────────────────────────────────────────────────────────────

        /// <summary>현재 저장/적용 값으로 UI 를 채운다(콜백 없이).</summary>
        private void RefreshAll()
        {
            _refreshing = true;

            if (volumeSliders != null)
            {
                for (int i = 0; i < volumeSliders.Length && i < BusCount; i++)
                {
                    if (volumeSliders[i] == null) continue;
                    var bus = (SoundBus)i;
                    float linear;
                    if (SoundManager.Instance != null) linear = SoundManager.Instance.GetBusVolume(bus);
                    else if (!SettingsService.TryGetVolume(bus, out linear)) linear = DefaultVolume(bus);

                    volumeSliders[i].SetValueWithoutNotify(SettingsService.StepFromLinear(linear));
                    SetPercentText(i, SettingsService.LinearFromStep(SettingsService.StepFromLinear(linear)));
                }
            }

            if (resolutionDropdown != null)
            {
                _options = SettingsService.GetResolutionOptions();
                var labels = new System.Collections.Generic.List<string>(_options.Length);
                for (int i = 0; i < _options.Length; i++) labels.Add($"{_options[i].x}x{_options[i].y}");
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(labels);

                int selected = -1;
                bool hasSaved = SettingsService.TryGetResolution(out Vector2Int saved);
                var want = hasSaved ? saved : new Vector2Int(1920, 1080);
                for (int i = 0; i < _options.Length; i++)
                    if (_options[i] == want) { selected = i; break; }
                if (selected < 0) selected = _options.Length - 1;   // 목록에 없거나 모니터 초과 → 최댓값
                resolutionDropdown.SetValueWithoutNotify(selected);
                resolutionDropdown.RefreshShownValue();
            }

            if (fullscreenToggle != null)
            {
                if (!SettingsService.TryGetFullscreen(out bool fs)) fs = true;   // 기본 FullScreenWindow
                fullscreenToggle.SetIsOnWithoutNotify(fs);
            }

            _refreshing = false;
        }

        private void SetPercentText(int index, float linear)
        {
            if (volumeTexts != null && index < volumeTexts.Length && volumeTexts[index] != null)
                volumeTexts[index].text = $"{Mathf.RoundToInt(linear * 100f)}%";
        }

        /// <summary>SoundManager 가 없을 때의 슬라이더 초기값 (AudioConfig 기본값과 동일).</summary>
        private static float DefaultVolume(SoundBus bus)
        {
            switch (bus)
            {
                case SoundBus.Master: return 1f;
                case SoundBus.Bgm:    return 0.6f;
                default:              return 0.8f;
            }
        }
    }
}
