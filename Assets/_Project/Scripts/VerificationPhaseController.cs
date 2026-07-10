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

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private Coroutine _routine;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;

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

                if (director.LastRunBreached) ReportFailure();
                else                          ReportSuccess();
            }
            else
            {
                // AI 미배선 스텁 — 무조건 방어 성공 처리
                yield return new WaitForSeconds(stubDuration);
                ReportSuccess();
            }
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

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
