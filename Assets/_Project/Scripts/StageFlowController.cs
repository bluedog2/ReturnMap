using System;
using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageFlowController — 스테이지 클리어 → 다음 스테이지 전환 로직
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="StageGoalTrigger.OnStageCleared"/> 를 구독해 클리어 결과를 알리고
    /// (뷰는 <see cref="StageClearPanel"/>이 담당), 사용자의 선택에 따라 다음 스테이지로
    /// 전환하거나 처음부터 다시 시작합니다. 로직만 전담하며 UI 참조를 직접 갖지 않습니다.
    ///
    /// <para><b>전환 순서 불변식</b>: 맵 로드 완료 → 1프레임 대기 → <c>SetPhase(Build, immediate:true)</c>.
    /// 로드 <i>전에</i> Build 로 돌리면 <see cref="BuildPhaseController.HandlePhaseChanged"/> 가
    /// IsActive=true 로 바뀌는데 예산 리셋은 <see cref="MapLoader.OnMapLoaded"/> 에서 일어나므로,
    /// 직전 스테이지 잔여 예산으로 곧 파괴될 슬롯에 설치 가능한 창이 열린다. 1프레임 대기는
    /// Destroy 가 프레임 끝 처리라 구 함정이 신맵 위에 겹쳐 보이는 것을 막는다.</para>
    ///
    /// <para><b>Time.timeScale 사용 금지</b>: timeScale 은 <see cref="VerificationSpeedController"/> 가
    /// 소유하며 "1 로 복원이 최우선 불변식"이다. 전환 중 플레이어 정지는
    /// <see cref="PlayerPhaseVisibility.SetFrozen"/> 으로 한다 — <see cref="MapLoader.UnloadRoutine"/>
    /// 이 지형을 지우고 최소 1프레임 기다리는 동안 Play 페이즈 + rb.simulated=true 면
    /// 플레이어가 월드 밖으로 낙하한다.</para>
    /// </summary>
    public class StageFlowController : MonoBehaviour
    {
        // ── 싱글턴 ───────────────────────────────────────────────────────────

        public static StageFlowController Instance { get; private set; }

        /// <summary>
        /// 전환(클리어 결과 표시 ~ 다음 맵 로드 ~ Build 복귀) 진행 중 여부.
        /// <see cref="BuildPhaseController"/> 가 Update 선두에서 이 값을 확인해 B/Enter/설치
        /// 입력을 일괄 차단한다.
        /// </summary>
        public static bool IsTransitioning { get; private set; }

        // ── 이벤트 ────────────────────────────────────────────────────────────

        /// <summary>클리어 결과 표시 시점에 발행: (mapId, 지급된 보상, 다음 스테이지 존재 여부).</summary>
        public static event Action<string, int, bool> OnStageClearPresented;

        /// <summary>
        /// 전환 실패(다음 맵 로드 실패 등) 시 발행: (mapId, 실패 사유). 발행 후에도 프리즈와
        /// <see cref="IsTransitioning"/> 은 그대로 유지된다 — 빈 월드로 페이즈가 넘어가는
        /// 소프트락을 막고, 재시도(같은 API 재호출)를 허용하기 위함이다.
        /// </summary>
        public static event Action<string, string> OnTransitionFailed;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        /// <summary>가장 최근 클리어된 mapId. 다음 스테이지 계산의 기준.</summary>
        private string _clearedMapId;

        /// <summary>진행 중인 전환 코루틴 — 중복 요청(다음/재시작 버튼 연타) 가드.</summary>
        private Coroutine _transitionRoutine;

        /// <summary>카탈로그 미할당 경고를 1회만 찍기 위한 플래그.</summary>
        private bool _warnedNoCatalog;

        // ── 공개 상태 ─────────────────────────────────────────────────────────

        /// <summary>가장 최근 클리어한 스테이지 기준으로 다음 스테이지가 존재하는가.</summary>
        public bool HasNext => !string.IsNullOrEmpty(ComputeNextMapId(_clearedMapId));

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnEnable()  => StageGoalTrigger.OnStageCleared += HandleStageCleared;
        private void OnDisable() => StageGoalTrigger.OnStageCleared -= HandleStageCleared;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 클리어 감지 ───────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 클리어 즉시: 플레이어 프리즈 + 입력 차단(<see cref="IsTransitioning"/>) +
        /// 결과 표시 이벤트 발행. 실제 다음 맵 로드는 사용자가 <see cref="RequestNextStage"/>
        /// 등을 호출해야 시작된다(자동 전환 아님 — 클리어 패널에서 선택을 기다린다).
        /// </summary>
        private void HandleStageCleared(string mapId, int rewardGranted)
        {
            _clearedMapId = mapId;

            var visibility = FindFirstObjectByType<PlayerPhaseVisibility>();
            visibility?.SetFrozen(true);

            IsTransitioning = true;

            OnStageClearPresented?.Invoke(mapId, rewardGranted, HasNext);
        }

        // ── 공개 API — 뷰(StageClearPanel)가 호출 ───────────────────────────

        /// <summary>다음 스테이지로 전환. 다음 스테이지가 없거나 이미 전환 중이면 아무 것도 하지 않는다.</summary>
        public void RequestNextStage()
        {
            if (_transitionRoutine != null) return; // 중복 가드(이미 로딩 중)

            string nextId = ComputeNextMapId(_clearedMapId);
            if (string.IsNullOrEmpty(nextId))
            {
                Debug.LogWarning("[StageFlowController] 다음 스테이지가 없습니다.");
                return;
            }

            _transitionRoutine = StartCoroutine(TransitionRoutine(nextId));
        }

        /// <summary>카탈로그 첫 스테이지부터 다시 시작.</summary>
        public void RestartFromFirst()
        {
            if (_transitionRoutine != null) return; // 중복 가드(이미 로딩 중)

            var catalog = MapLoader.Instance != null ? MapLoader.Instance.Catalog : null;
            string firstId = catalog != null ? catalog.GetByIndex(0)?.mapId : null;
            if (string.IsNullOrEmpty(firstId))
            {
                Debug.LogWarning("[StageFlowController] 카탈로그에 스테이지가 없어 처음부터 시작할 수 없습니다.");
                return;
            }

            _transitionRoutine = StartCoroutine(TransitionRoutine(firstId));
        }

        /// <summary>
        /// 페이즈 전환 없이 프리즈만 해제한다. <b>마지막 스테이지 클리어([닫기]) 전용</b> —
        /// Build 로 전환하는 경로(<see cref="RequestNextStage"/>/<see cref="RestartFromFirst"/>)는
        /// <c>GamePhaseManager.OnPhaseChanged</c> 를 받는 <see cref="PlayerPhaseVisibility"/> 가
        /// 자동으로 프리즈를 푼다.
        /// </summary>
        public void DismissClear()
        {
            IsTransitioning = false;

            var visibility = FindFirstObjectByType<PlayerPhaseVisibility>();
            visibility?.SetFrozen(false);
        }

        // ── 전환 코루틴 ───────────────────────────────────────────────────────

        /// <summary>
        /// ⚠️ 이 컨트롤러 자신은 <c>StopAllCoroutines()</c> 를 호출하지 않는다 — 이 코루틴이
        /// 바깥 iterator 로 <see cref="MapLoader.LoadMapRoutine"/> 를 직접 구동하고 있으므로,
        /// 자기 자신을 멈추면 전환이 중간에 끊긴 채 <see cref="IsTransitioning"/> 이 영영 true로
        /// 남는 소프트락이 된다.
        /// </summary>
        private IEnumerator TransitionRoutine(string nextId)
        {
            yield return MapLoader.Instance.LoadMapRoutine(nextId);

            // 성공 판정은 이벤트 구독이 아니라 로드 완료 후 상태를 직접 확인 — 더 단순·정확하다.
            bool success = MapLoader.Instance.IsLoaded
                        && MapLoader.Instance.CurrentMap != null
                        && MapLoader.Instance.CurrentMap.mapId == nextId;

            if (!success)
            {
                _transitionRoutine = null;
                // 빈 월드로 페이즈가 넘어가는 소프트락 방지 — 페이즈 전환 금지.
                // 프리즈·IsTransitioning 은 유지해 재시도를 허용한다.
                OnTransitionFailed?.Invoke(nextId, $"맵 로드 실패: {nextId}");
                yield break;
            }

            StageProgressService.SetLastPlayedMapId(nextId);

            // 1프레임 대기 → 시드 리롤 → Build 복귀 → 전환 종료 (순서 불변식, 클래스 헤더 참고)
            yield return null;
            TrapMutationManager.Instance?.RollNewSeed();
            GamePhaseManager.Instance?.SetPhase(GamePhase.Build, true);

            IsTransitioning    = false;
            _transitionRoutine = null;
            // 프리즈 해제는 위 SetPhase(Build) 가 유발하는 OnPhaseChanged 를
            // PlayerPhaseVisibility.HandlePhaseChanged 가 받아 자동으로 수행한다.
        }

        // ── 내부 유틸 ─────────────────────────────────────────────────────────

        private string ComputeNextMapId(string fromMapId)
        {
            var catalog = MapLoader.Instance != null ? MapLoader.Instance.Catalog : null;
            if (catalog == null)
            {
                if (!_warnedNoCatalog)
                {
                    Debug.LogWarning("[StageFlowController] StageCatalog 미할당 — 다음 스테이지 없음으로 처리합니다.");
                    _warnedNoCatalog = true;
                }
                return null;
            }

            return catalog.GetNextMapId(fromMapId);
        }
    }
}
