using System;
using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationPhaseController — 검증 페이즈 AI 돌파 시뮬레이션 (골격)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verification Phase 진입 시 AI 돌파 시도를 실행하는 컨트롤러의 <b>골격(스텁)</b>.
    /// 실제 A* 길찾기 AI 본체는 이번 범위가 아니며, 아래 <c>TODO</c> 지점에서
    /// 향후 통합됩니다. 지금은 <see cref="stubDuration"/> 만큼 대기 후 자동으로
    /// <see cref="ReportSuccess"/> 를 호출해 Play 페이즈로 즉시 통과시킵니다.
    ///
    /// <para><b>씬 배선</b>: 이 컴포넌트는 씬에 자동 생성되지 않습니다.
    /// Managers 오브젝트에 수동으로 추가해야 동작합니다.</para>
    /// </summary>
    public class VerificationPhaseController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("AI 연동")]
        [SerializeField]
        [Tooltip("검증 AI 오케스트레이터. 비워두면 아래 스텁으로 동작")]
        private VerificationDirector director;

        [Header("AI 스텁")]
        [SerializeField]
        [Tooltip("director 미연결 시 스텁: 이 시간 후 자동 통과")]
        private float stubDuration = 1f;

        [Header("방어 성공 연출")]
        [SerializeField]
        [Tooltip("방어 성공 배너. 비워두면 이 오브젝트에 자동 추가")]
        private PhaseResultBanner resultBanner;

        [SerializeField]
        [Tooltip("방어 성공 배너 문구")]
        private string successMessage = "방어 성공!";

        [SerializeField, Min(0f)]
        [Tooltip("방어 성공 배너를 보여준 뒤 Play 로 넘어가기까지 대기 시간(초, 실시간). 0이면 즉시 전환")]
        private float successHoldTime = 1.2f;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private Coroutine _routine;
        private bool      _resultConfirmed;

        /// <summary>검증은 끝났고 결과 패널에서 「계속」 확인을 기다리는 중인가.</summary>
        public bool IsAwaitingResult { get; private set; }

        /// <summary>결과 대기에 들어갈 때 발행 — 결과 패널이 구독해 표시한다 (판정은 director 의 LastReport/LastRunBreached).</summary>
        public static event Action OnResultPresented;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // ?? 는 UnityEngine.Object 의 가짜 null 을 못 걸러내므로 명시 비교
            if (resultBanner == null) resultBanner = GetComponent<PhaseResultBanner>();
            if (resultBanner == null) resultBanner = gameObject.AddComponent<PhaseResultBanner>();
        }

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
            IsAwaitingResult = false;
        }

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            // 검증 페이즈를 벗어나면 진행 중이던 시뮬레이션은 즉시 중단.
            // StopCoroutine 은 디렉터가 스폰한 에이전트까지 정리해 주지 않으므로
            // 반드시 Cancel 로 회수한다 (중단 규약 — VerificationDirector 참조).
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
                director?.Cancel();
            }
            IsAwaitingResult = false; // 결과 대기 중 강제 전환 시 대기 해제

            if (phase == GamePhase.Verification)
                _routine = StartCoroutine(RunVerification());
        }

        /// <summary>
        /// 검증 시뮬레이션 본체. <see cref="director"/> 가 연결돼 있으면
        /// AI 시뮬레이션(계획→실행→학습 루프)에 위임하고 결과만 판정합니다.
        /// <para><b>판정 규약 (GDD 4장)</b>: AI 가 죽거나 제한 시간을 버티면 <b>방어 성공</b>
        /// → Play 진행. AI 가 골에 도달하면 <b>방어 실패(뚫림)</b> → Build 재설계.</para>
        /// </summary>
        private IEnumerator RunVerification()
        {
            if (director != null)
            {
                yield return director.Run();

                // 결과 패널에서 「계속」 을 누를 때까지 대기한 뒤 판정대로 전환
                yield return AwaitResult(director.LastRunBreached);
            }
            else
            {
                // AI 미배선 스텁 — 무조건 방어 성공 처리
                yield return new WaitForSeconds(stubDuration);
                ReportSuccess();
            }
        }

        /// <summary>
        /// 결과 표시 후 <see cref="ConfirmResult"/> 가 불릴 때까지 대기하고, 확인되면 판정대로
        /// 실패(Build) / 성공(Play) 전환을 한다. 검증 페이즈가 중간에 바뀌면
        /// <see cref="HandlePhaseChanged"/> 가 코루틴을 중단해 이 대기는 자연히 해제된다.
        /// </summary>
        private IEnumerator AwaitResult(bool breached)
        {
            _resultConfirmed = false;

            // 결과 패널이 씬에 없으면(미배선) 「계속」 을 누를 곳이 없어 영영 멈추므로 대기 없이 바로 전환한다.
            if (FindFirstObjectByType<VerificationResultPanel>() == null)
            {
                Debug.LogWarning("[VerificationPhaseController] VerificationResultPanel 미배선 — 결과 대기 없이 전환합니다. " +
                                 "ReTrap/Setup/6. 메타 UI 세팅을 실행하세요.");
                _resultConfirmed = true;
            }

            IsAwaitingResult = true;
            OnResultPresented?.Invoke();

            while (!_resultConfirmed) yield return null;

            IsAwaitingResult = false;
            if (breached) ReportFailure();
            else          ReportSuccess();
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>결과 패널의 「계속」 확인 — 결과 대기 중일 때만 유효. 판정대로 페이즈를 전환한다.</summary>
        public void ConfirmResult()
        {
            if (IsAwaitingResult) _resultConfirmed = true;
        }

        /// <summary>방어 성공(AI 저지/시간 초과) — Play 페이즈로 진행.</summary>
        public void ReportSuccess()
        {
            _routine = null;
            GamePhaseManager.Instance?.SetPhase(GamePhase.Play);
        }

        /// <summary>방어 실패(AI 가 골 도달, 방어 뚫림) — Build 페이즈로 복귀해 재설계.</summary>
        public void ReportFailure()
        {
            _routine = null;
            GamePhaseManager.Instance?.SetPhase(GamePhase.Build);
        }
    }
}
