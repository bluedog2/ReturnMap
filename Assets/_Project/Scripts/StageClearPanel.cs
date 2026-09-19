using UnityEngine;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageClearPanel — 스테이지 클리어 결과 패널 (뷰)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="StageFlowController"/> 가 발행하는 이벤트를 구독해 클리어 결과를 표시하는
    /// 얇은 뷰입니다. 버튼 클릭은 전부 <see cref="StageFlowController"/> 의 공개 API 만
    /// 호출하고 직접 페이즈/맵 로직을 수행하지 않습니다(<see cref="PhaseControlsGuide"/> 와
    /// 동일 패턴).
    ///
    /// <para><b>씬 배선</b>: <see cref="ReTrap.EditorTools.MetaUiSetup"/> 이 UICanvas 아래에
    /// 생성/배선합니다. 이 컴포넌트는 항상 활성 상태인 루트에 붙고, 실제 표시/숨김은 자식
    /// <see cref="panelRoot"/>(콘텐츠 컨테이너)로 제어합니다 — 루트 자체를 SetActive(false)로
    /// 끄면 OnDisable 이 <see cref="StageFlowController"/> 의 전역 이벤트 구독을 해제해
    /// 이후 클리어를 영영 못 받는다(ResearchBoardPanel/StagePreviewPanel 과 동일한 이유).</para>
    /// </summary>
    public class StageClearPanel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("표시 콘텐츠")]
        [Tooltip("패널 표시/숨김 대상 컨테이너 (컴포넌트가 붙은 루트 자신은 항상 활성 유지).")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("제목 텍스트 (\"스테이지 클리어\").")]
        [SerializeField] private Text titleText;

        [Tooltip("보상 지급 텍스트. rewardGranted>0 이면 지급량, 0이면 \"보상 이미 수령\"(재클리어).")]
        [SerializeField] private Text rewardText;

        [Tooltip("다음 스테이지 없음(전 스테이지 완주) 등 상태 안내 텍스트. 평시 비어 있음.")]
        [SerializeField] private Text statusText;

        [Tooltip("전환 실패 사유 표시. 평시 비활성.")]
        [SerializeField] private Text errorText;

        [Header("버튼")]
        [Tooltip("다음 스테이지로 진행. hasNext=true 일 때만 표시.")]
        [SerializeField] private Button nextButton;

        [Tooltip("처음 스테이지부터 다시 시작. hasNext=false(마지막 스테이지) 일 때만 표시.")]
        [SerializeField] private Button restartButton;

        [Tooltip("패널을 닫고 프리즈만 해제(페이즈 전환 없음). hasNext=false(마지막 스테이지) 일 때만 표시.")]
        [SerializeField] private Button closeButton;

        // ── 버튼 기본 라벨 (전환 실패 시 "다시 시도"로 바뀌었다가 다음 클리어 표시 때 복원) ──

        private const string NextLabel    = "다음 스테이지";
        private const string RestartLabel = "처음부터";
        private const string RetryLabel   = "다시 시도";

        /// <summary>가장 최근 전환을 트리거한 버튼. 실패 시 이 버튼만 재활성화 + 라벨을 "다시 시도"로 교체.</summary>
        private Button _pendingButton;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // 중복 리스너 방지를 위해 Awake 에서 1회만 연결 (PhaseControlsGuide 와 동일 패턴).
            if (nextButton    != null) nextButton.onClick.AddListener(HandleNextClicked);
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestartClicked);
            if (closeButton   != null) closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void Start()
        {
            // 기본 상태는 항상 닫힘 — 에디터 세팅이 이미 비활성으로 두더라도 방어적으로 강제.
            if (panelRoot != null)
                panelRoot.SetActive(false);
            HideError();
        }

        private void OnEnable()
        {
            StageFlowController.OnStageClearPresented += HandleClearPresented;
            StageFlowController.OnTransitionFailed     += HandleTransitionFailed;
            GamePhaseManager.OnPhaseChanged            += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            StageFlowController.OnStageClearPresented -= HandleClearPresented;
            StageFlowController.OnTransitionFailed     -= HandleTransitionFailed;
            GamePhaseManager.OnPhaseChanged            -= HandlePhaseChanged;
        }

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        /// <summary>
        /// 다음/처음부터 전환이 성공하면 GamePhaseManager 가 Build 로 전환하며
        /// OnPhaseChanged 를 발행한다 — 그 시점에 패널을 자동으로 닫는다
        /// (StageFlowController 는 UI 를 직접 모르므로 별도 "닫아라" 이벤트를 주지 않는다).
        /// </summary>
        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Build && panelRoot != null && panelRoot.activeSelf)
                panelRoot.SetActive(false);
        }

        // ── StageFlowController 이벤트 핸들러 ────────────────────────────────

        private void HandleClearPresented(string mapId, int rewardGranted, bool hasNext)
        {
            _pendingButton = null;
            HideError();

            if (titleText != null)
                titleText.text = "스테이지 클리어";

            if (rewardText != null)
                rewardText.text = rewardGranted > 0
                    ? $"박살 난 지구본 +{rewardGranted}"
                    : "보상 이미 수령";

            if (statusText != null)
                statusText.text = hasNext ? string.Empty : "전 스테이지 완주";

            SetButtonState(nextButton,    hasNext,  NextLabel);
            SetButtonState(restartButton, !hasNext, RestartLabel);
            SetButtonState(closeButton,   !hasNext, null); // 닫기 라벨은 에디터 세팅값 고정

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                // 화면 중앙에서 연구소 패널과 겹칠 수 있으니 항상 더 위에 보이도록 보장.
                panelRoot.transform.SetAsLastSibling();
            }
        }

        private void HandleTransitionFailed(string mapId, string reason)
        {
            if (errorText != null)
            {
                errorText.text = $"전환 실패: {reason}";
                errorText.gameObject.SetActive(true);
            }

            // 실패를 유발한 버튼만 "다시 시도"로 재활성화한다 (다른 버튼 상태는 그대로 둔다).
            if (_pendingButton != null)
            {
                _pendingButton.interactable = true;
                SetButtonLabel(_pendingButton, RetryLabel);
            }

            // 닫기는 비동기 작업이 없으므로(프리즈 해제만) 실패와 무관하게 항상 다시 눌릴 수
            // 있게 해, 사용자가 재시도 대신 닫기를 선택할 길을 막지 않는다.
            if (closeButton != null && closeButton.gameObject.activeSelf)
                closeButton.interactable = true;
        }

        // ── 버튼 클릭 ─────────────────────────────────────────────────────────

        private void HandleNextClicked()
        {
            if (nextButton != null) nextButton.interactable = false; // 중복 클릭 2중 방어(UI 측)
            _pendingButton = nextButton;
            HideError();
            StageFlowController.Instance?.RequestNextStage();
        }

        private void HandleRestartClicked()
        {
            if (restartButton != null) restartButton.interactable = false;
            if (closeButton   != null) closeButton.interactable   = false;
            _pendingButton = restartButton;
            HideError();
            StageFlowController.Instance?.RestartFromFirst();
        }

        private void HandleCloseClicked()
        {
            if (closeButton != null) closeButton.interactable = false;
            StageFlowController.Instance?.DismissClear();

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        // ── 내부 유틸 ─────────────────────────────────────────────────────────

        private void HideError()
        {
            if (errorText != null)
                errorText.gameObject.SetActive(false);
        }

        private static void SetButtonState(Button button, bool visible, string label)
        {
            if (button == null) return;
            button.gameObject.SetActive(visible);
            button.interactable = true;
            if (label != null)
                SetButtonLabel(button, label);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }
    }
}
