using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationResultPanel — 검증 결과 패널 (뷰)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증이 끝나면 <see cref="VerificationPhaseController.OnResultPresented"/> 를 받아 판정과
    /// 리포트를 보여주고, 「계속」 버튼으로 <see cref="VerificationPhaseController.ConfirmResult"/> 를
    /// 호출하는 얇은 뷰입니다 (<see cref="StageClearPanel"/> 과 같은 패턴).
    ///
    /// <para><b>구성</b>: 제목("방어 성공!"/"방어 실패") + 두 탭. 「결과」 탭은 판정 문구,
    /// 「리포트」 탭은 요약(A: 돌파/처치/저지) 과 「상세」 토글로 펼치는 B(함정별 처치 상위 5 |
    /// 사망 지점 상위 5). 「계속」 은 항상 표시됩니다.</para>
    ///
    /// <para><b>씬 배선</b>: <c>ReTrap.EditorTools.MetaUiSetup</c> 이 UICanvas 아래에 생성/배선합니다.
    /// 이 컴포넌트는 항상 활성인 루트에 붙고 표시/숨김은 자식 <see cref="panelRoot"/> 로만 제어합니다
    /// (루트를 끄면 OnDisable 이 전역 이벤트 구독을 해제해 이후 결과를 못 받는다).</para>
    ///
    /// <para><b>입력 가드</b>: 결과 표시 직후 <see cref="inputGuardTime"/> 동안은 「계속」 클릭을 무시한다
    /// (검증 중 누른 Enter 스킵 키가 submit 으로 이어지는 것 방지). 버튼은 자동 선택하지 않는다.</para>
    /// </summary>
    public class VerificationResultPanel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("연동")]
        [Tooltip("결과 대기/확인을 담당하는 검증 페이즈 컨트롤러.")]
        [SerializeField] private VerificationPhaseController phaseController;

        [Tooltip("판정과 리포트(LastRunBreached/LastReport)를 읽을 검증 디렉터.")]
        [SerializeField] private VerificationDirector director;

        [Header("표시 콘텐츠")]
        [Tooltip("패널 표시/숨김 대상 컨테이너 (컴포넌트가 붙은 루트 자신은 항상 활성 유지).")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("제목 텍스트 (\"방어 성공!\" / \"방어 실패\").")]
        [SerializeField] private Text titleText;

        [Header("탭")]
        [Tooltip("「결과」 탭 버튼.")]
        [SerializeField] private Button resultTabButton;

        [Tooltip("「리포트」 탭 버튼.")]
        [SerializeField] private Button reportTabButton;

        [Tooltip("「결과」 탭 페이지 컨테이너.")]
        [SerializeField] private GameObject resultPage;

        [Tooltip("「리포트」 탭 페이지 컨테이너.")]
        [SerializeField] private GameObject reportPage;

        [Header("결과 탭")]
        [Tooltip("판정 문구.")]
        [SerializeField] private Text verdictText;

        [Header("리포트 탭")]
        [Tooltip("요약 A: 돌파 n/라이프 · 처치 · 저지 n/투입 n.")]
        [SerializeField] private Text summaryText;

        [Tooltip("「상세」 토글 버튼 (B 펼침/접힘).")]
        [SerializeField] private Button detailButton;

        [Tooltip("상세 B 컨테이너 (함정별 처치 | 사망 지점). 시작 시 접힘.")]
        [SerializeField] private GameObject detailRoot;

        [Tooltip("B 왼쪽 열: 함정별 처치 상위 5.")]
        [SerializeField] private Text killsText;

        [Tooltip("B 오른쪽 열: 사망 지점 상위 5.")]
        [SerializeField] private Text cellsText;

        [Header("버튼")]
        [Tooltip("계속 — 판정대로 Build/Play 로 넘어간다. 항상 표시.")]
        [SerializeField] private Button continueButton;

        [Header("입력 가드")]
        [SerializeField, Min(0f)]
        [Tooltip("결과 표시 직후 「계속」 클릭을 무시할 시간(초, unscaled).")]
        private float inputGuardTime = 0.3f;

        // ── 상수 ──────────────────────────────────────────────────────────────

        private const int TopRows = 5;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private float _inputGuardUntil;
        private bool  _detailOpen;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // 중복 리스너 방지를 위해 Awake 에서 1회만 연결 (StageClearPanel 과 동일 패턴).
            if (resultTabButton != null) resultTabButton.onClick.AddListener(ShowResultPage);
            if (reportTabButton != null) reportTabButton.onClick.AddListener(ShowReportPage);
            if (detailButton    != null) detailButton.onClick.AddListener(ToggleDetail);
            if (continueButton  != null) continueButton.onClick.AddListener(HandleContinueClicked);
        }

        private void Start()
        {
            // 기본 상태는 항상 닫힘 — 에디터 세팅이 열려 있어도 방어적으로 강제.
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            VerificationPhaseController.OnResultPresented += HandleResultPresented;
            GamePhaseManager.OnPhaseChanged               += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            VerificationPhaseController.OnResultPresented -= HandleResultPresented;
            GamePhaseManager.OnPhaseChanged               -= HandlePhaseChanged;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────────

        /// <summary>어떤 페이즈 전환이든 결과 패널은 닫는다 (계속·강제 전환 공통).</summary>
        private void HandlePhaseChanged(GamePhase phase)
        {
            if (panelRoot != null && panelRoot.activeSelf)
                panelRoot.SetActive(false);
        }

        private void HandleResultPresented()
        {
            bool breached = director != null && director.LastRunBreached;
            VerificationReport report = director != null ? director.LastReport : null;

            if (titleText != null)
                titleText.text = breached ? "방어 실패" : "방어 성공!";

            if (verdictText != null)
                verdictText.text = BuildVerdict(breached, report);

            if (summaryText != null)
                summaryText.text = BuildSummary(report);

            _detailOpen = false;
            RefreshDetail(report);

            ShowResultPage();

            _inputGuardUntil = Time.unscaledTime + inputGuardTime;

            // 버튼 자동 선택 금지 — 선택돼 있으면 Enter/Space submit 이 곧바로 「계속」 이 된다.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                // 다른 패널(연구소 등)과 겹치지 않도록 항상 위로.
                panelRoot.transform.SetAsLastSibling();
            }
        }

        // ── 버튼 ──────────────────────────────────────────────────────────────

        private void HandleContinueClicked()
        {
            if (Time.unscaledTime < _inputGuardUntil) return;
            phaseController?.ConfirmResult();
        }

        private void ShowResultPage() => SetPage(true);

        private void ShowReportPage() => SetPage(false);

        private void SetPage(bool result)
        {
            if (resultPage != null) resultPage.SetActive(result);
            if (reportPage != null) reportPage.SetActive(!result);
        }

        private void ToggleDetail()
        {
            _detailOpen = !_detailOpen;
            RefreshDetail(director != null ? director.LastReport : null);
        }

        // ── 텍스트 구성 ───────────────────────────────────────────────────────

        private void RefreshDetail(VerificationReport report)
        {
            if (detailRoot != null) detailRoot.SetActive(_detailOpen);
            if (!_detailOpen || report == null) return;

            if (killsText != null) killsText.text = BuildRows("함정별 처치", report.TopKills(TopRows));
            if (cellsText != null) cellsText.text = BuildRows("사망 지점", report.TopCells(TopRows));
        }

        private static string BuildVerdict(bool breached, VerificationReport report)
        {
            if (report == null)
                return breached ? "적이 방어선을 뚫었습니다." : "방어선을 지켜냈습니다.";

            return breached
                ? $"적이 방어선을 뚫었습니다.\n돌파 {report.Reached}/{report.Life}"
                : "방어선을 지켜냈습니다.";
        }

        private static string BuildSummary(VerificationReport report)
        {
            if (report == null) return string.Empty;

            return $"돌파 {report.Reached}/{report.Life}\n" +
                   $"처치 {report.DeathEvents}\n" +
                   $"저지 {report.StoppedAgents}/투입 {report.Spawned}";
        }

        private static string BuildRows(string header, List<VerificationReport.Row> rows)
        {
            var sb = new StringBuilder(header);
            if (rows.Count == 0)
                return sb.Append("\n(없음)").ToString();

            for (int i = 0; i < rows.Count; i++)
                sb.Append('\n').Append(rows[i].Label).Append(" x").Append(rows[i].Count);
            return sb.ToString();
        }
    }
}
