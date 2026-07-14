using System.Collections;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationDirector — 검증 페이즈 AI 오케스트레이터
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 페이즈 AI 시뮬레이션의 총괄자. 파이프라인:
    /// <para>
    /// <see cref="AIPersonality"/>(성향 데이터) → <see cref="AIBehaviorParams"/>(변환)
    /// → <see cref="NavGrid.Build"/>(세계 모델) → <see cref="IPathPlanner"/>(계획)
    /// → <see cref="VerificationAgent"/>(실행) → 결과 집계(이 클래스).
    /// </para>
    /// 시도 사이에는 <see cref="AIMemory"/> 로 사망 경험을 학습시킵니다.
    ///
    /// <para><b>리소스 정책</b>: 에이전트는 <see cref="ComponentPool{T}"/> 로 스폰/반납
    /// (Instantiate/Destroy 금지), NavGrid 는 시도 간 배열 재사용 — 시도 루프에서
    /// 프레임 할당이 발생하지 않게 유지합니다.</para>
    ///
    /// <para><b>중단 규약</b>: 검증 도중 페이즈가 바뀌면(B 키 빌드 복귀 등)
    /// 소유자(<see cref="VerificationPhaseController"/>)가 코루틴 정지 후 반드시
    /// <see cref="Cancel"/> 을 호출해 활성 에이전트를 회수해야 합니다.</para>
    ///
    /// <para><b>씬 배선</b>: Managers 오브젝트에 추가하고
    /// <see cref="VerificationPhaseController"/> 의 director 필드에 연결.
    /// 성향 에셋(Create → ReTrap → AI 성향)과 에이전트 프리팹을 꽂아야 동작합니다.</para>
    ///
    /// <para>TODO(다중 에이전트): 성향이 다른 여러 AI 를 웨이브로 보내려면
    /// personality 를 배열로 확장하고 시도 루프를 에이전트별로 도는 구조로 확장.</para>
    /// </summary>
    public class VerificationDirector : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("AI 구성")]
        [SerializeField]
        [Tooltip("이번 스테이지에서 돌파를 시도할 AI 의 성향 에셋")]
        private AIPersonality personality;

        [SerializeField]
        [Tooltip("씬에 스폰할 검증 AI 캐릭터 프리팹 (VerificationAgent 포함)")]
        private VerificationAgent agentPrefab;

        [SerializeField]
        [Tooltip("태그 적용 전 기본 스펙 (null 허용 — 기본값 사용)")]
        private AgentArchetype archetype;

        [SerializeField]
        [Tooltip("임시 테스트용 — 4단계 StageAgentRoster 도입 시 제거 예정. " +
                 "스폰되는 에이전트에 이 태그를 부여한다.")]
        private AITagDefinition[] debugTags;

        [Header("판정")]
        [SerializeField]
        [Tooltip("이 시간(초) 안에 못 뚫으면 방어 성공 (GDD: 제한 시간 버티기)")]
        private float timeLimit = 60f;

        [Header("풀링")]
        [SerializeField, Min(1)]
        [Tooltip("에이전트 풀 예열 개수. 다중 에이전트/웨이브 확장 시 늘릴 것")]
        private int poolPrewarm = 1;

        [Header("연출")]
        [SerializeField, Min(0f)]
        [Tooltip("AI 사망 후 사망 애니메이션을 보여줄 시간(초). 0이면 즉시 회수")]
        private float deathLingerTime = 0.6f;

        // ── 결과 — VerificationPhaseController 가 읽음 ───────────────────────

        /// <summary>직전 Run 에서 AI 가 골에 도달했는가 (true = 방어 뚫림 → 재설계).</summary>
        public bool LastRunBreached { get; private set; }

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        // 교체 지점: 성향별로 다른 알고리즘을 쓰려면 여기서 분기/주입.
        private readonly IPathPlanner _planner = new AStarPathPlanner();

        private ComponentPool<VerificationAgent> _agentPool;
        private VerificationAgent                _activeAgent; // 회수 보장용 추적
        private NavGrid                          _navGrid;     // 시도 간 배열 재사용

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            Cancel();
            _agentPool?.Clear();
        }

        // ── 오케스트레이션 ────────────────────────────────────────────────────

        /// <summary>
        /// 검증 시뮬레이션 1회 실행. 완료 후 <see cref="LastRunBreached"/> 에 결과가 남습니다.
        /// </summary>
        public IEnumerator Run()
        {
            LastRunBreached = false;

            var loader = MapLoader.Instance;
            if (loader == null || !loader.IsLoaded || personality == null || agentPrefab == null)
            {
                Debug.LogWarning("[VerificationDirector] 구성 미비(맵/성향/프리팹) — 방어 성공으로 간주");
                yield break;
            }

            EnsurePool();

            MapData map    = loader.CurrentMap;
            Vector2 origin = loader.MapOrigin;
            var p          = personality.ToBehaviorParams();
            var memory     = new AIMemory(map.width);
            float deadline = Time.time + timeLimit;

            for (int attempt = 1; attempt <= p.maxAttempts && Time.time < deadline; attempt++)
            {
                // 1) 계획 — 시도마다 학습이 반영된 그리드로 다시 세운다 (배열 재사용)
                _navGrid = NavGrid.Build(map, p, memory, _navGrid);
                var path = _planner.FindPath(_navGrid, map.spawnPoint, map.goalPoint, p);

                if (path == null || path.Count == 0)
                {
                    // 경로 자체가 없으면 남은 시도도 결과가 같다 (기억만으론 길이 안 생김)
                    Debug.Log($"[VerificationDirector] 시도 {attempt}: 경로 없음 — 방어 성공");
                    yield break;
                }

                // 2) 실행 — 풀에서 에이전트를 꺼내 경로 수행을 지켜본다
                Vector2 spawnPos = map.CellToWorld(map.spawnPoint.x, map.spawnPoint.y, origin);
                _activeAgent = _agentPool.Get(spawnPos, Quaternion.identity);

                // 태그 배선 (임시 테스트용 — 4단계 StageAgentRoster 도입 시 debugTags 는 제거)
                if (_activeAgent.TryGetComponent<AgentContext>(out var ctx))
                {
                    TagSet tags = (debugTags == null || debugTags.Length == 0)
                        ? TagSet.Empty
                        : new TagSet(debugTags);
                    ctx.Initialize(archetype, tags);
                }

                _activeAgent.StartCoroutine(_activeAgent.FollowPath(path, map, origin, p));
                while (_activeAgent != null && !_activeAgent.IsDone && Time.time < deadline)
                    yield return null;

                // Cancel 로 외부 중단된 경우 (페이즈 이탈) — 루프 종료
                if (_activeAgent == null) yield break;

                if (!_activeAgent.IsDone) _activeAgent.Kill(); // 시간 초과 — 방어 성공 쪽

                bool breached  = _activeAgent.ReachedGoal;
                var  deathCell = _activeAgent.CurrentCell;

                // 사망 애니메이션이 보이도록 잠시 유지 후 회수 (Cancel 시 즉시 회수됨)
                if (!breached && deathLingerTime > 0f)
                {
                    yield return new WaitForSeconds(deathLingerTime);
                    if (_activeAgent == null) yield break; // 유지 중 외부 중단
                }
                ReleaseActiveAgent();

                if (breached)
                {
                    LastRunBreached = true;
                    Debug.Log($"[VerificationDirector] 시도 {attempt}: AI 돌파 성공 — 방어 뚫림");
                    yield break;
                }

                // 3) 학습 — 죽은 자리를 기억하고 다음 시도로
                memory.RecordDeath(deathCell);
                Debug.Log($"[VerificationDirector] 시도 {attempt}: AI 사망 (누적 {memory.TotalDeaths}회)");
            }

            // 모든 시도 실패 또는 시간 초과 → 방어 성공 (LastRunBreached == false)
        }

        /// <summary>
        /// 외부 중단 — 진행 중이던 에이전트를 풀로 회수합니다.
        /// 소유자가 Run 코루틴을 StopCoroutine 한 직후 반드시 호출할 것.
        /// </summary>
        public void Cancel() => ReleaseActiveAgent();

        // ── 내부 ──────────────────────────────────────────────────────────────

        private void EnsurePool()
        {
            if (_agentPool != null) return;

            // 부모를 디렉터 밑에 두어 하이어라키 오염 방지 (Managers 는 스케일 1 가정)
            _agentPool = new ComponentPool<VerificationAgent>(agentPrefab, transform);
            _agentPool.Prewarm(poolPrewarm);
        }

        private void ReleaseActiveAgent()
        {
            if (_activeAgent == null) return;
            _agentPool.Release(_activeAgent); // OnDespawned 가 잔여 코루틴 정리
            _activeAgent = null;
        }
    }
}
