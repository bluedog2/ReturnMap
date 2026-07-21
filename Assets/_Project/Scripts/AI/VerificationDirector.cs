using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationDirector — 검증 페이즈 AI 오케스트레이터 (얇은 어댑터)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 페이즈 AI 웨이브 시뮬레이션의 씬 진입점. 실제 시뮬레이션 본체는
    /// <see cref="AgentWaveController"/> 에 있고, 이 클래스는 로스터 확정/풀 준비/결과
    /// 노출만 담당하는 얇은 어댑터입니다. 파이프라인:
    /// <para>
    /// <see cref="StageAgentRoster"/>(웨이브 구성 확정) → <see cref="AgentWaveController"/>
    /// (스폰/시도/학습 오케스트레이션, 개체별 <see cref="AIPersonality"/> → <see cref="AIBehaviorParams"/>
    /// → <see cref="NavGrid.Build"/> → <see cref="IPathPlanner"/> → <see cref="VerificationAgent"/>)
    /// → 결과 집계(이 클래스).
    /// </para>
    ///
    /// <para><b>리소스 정책</b>: 에이전트는 <see cref="ComponentPool{T}"/> 로 스폰/반납
    /// (Instantiate/Destroy 금지). NavGrid 는 개체별로 시도 간 배열 재사용 —
    /// 프레임 할당이 발생하지 않게 유지합니다.</para>
    ///
    /// <para><b>중단 규약</b>: 검증 도중 페이즈가 바뀌면(B 키 빌드 복귀 등)
    /// 소유자(<see cref="VerificationPhaseController"/>)가 코루틴 정지 후 반드시
    /// <see cref="Cancel"/> 을 호출해 활성 에이전트를 회수해야 합니다.</para>
    ///
    /// <para><b>씬 배선</b>: Managers 오브젝트에 추가하고
    /// <see cref="VerificationPhaseController"/> 의 director 필드에 연결.
    /// <see cref="spawnTable"/> 을 꽂으면 웨이브 구성으로, 비워두면 archetype+debugTags
    /// 로 1마리만 스폰하는 폴백 경로로 동작합니다.</para>
    /// </summary>
    public class VerificationDirector : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("웨이브 구성")]
        [SerializeField]
        [Tooltip("이번 스테이지의 웨이브 구성(아키타입별 스폰 묶음 + 태그 가중치). " +
                 "미지정 시 archetype+debugTags 로 1마리만 스폰하는 폴백 경로로 동작한다.")]
        private StageSpawnTable spawnTable;

        [SerializeField]
        [Tooltip("로스터 확정 시드. 0 = 실행 시 랜덤 시드. 같은 시드는 같은 태그 구성을 재현한다.")]
        private int rosterSeed = 0;

        [SerializeField]
        [Tooltip("연구소(아웃게임) 가중치 보정을 반영할 연구 노드 목록. " +
                 "비워두면 스폰 테이블의 기본 가중치를 그대로 사용한다. " +
                 "진입 전 미리보기(StagePreviewPanel)와 같은 노드 목록을 꽂아야 표기 확률과 실제 스폰이 일치한다.")]
        private ResearchNodeDefinition[] researchNodes;

        [Header("AI 구성 (폴백 전용)")]
        [SerializeField]
        [Tooltip("씬에 스폰할 검증 AI 캐릭터 프리팹 (VerificationAgent 포함)")]
        private VerificationAgent agentPrefab;

        [SerializeField]
        [Tooltip("태그 적용 전 기본 스펙 (null 허용 — 기본값 사용). spawnTable 미지정 시 폴백 로스터의 아키타입.")]
        private AgentArchetype archetype;

        [SerializeField]
        [Tooltip("spawnTable 미지정 시 폴백 경로 전용 — 스폰되는 에이전트에 이 태그를 부여한다. " +
                 "면역 태그(둥글둥글 등)를 꽂으면 해당 함정에 데미지를 입지 않는 것이 정상 동작이다 — " +
                 "NavGrid 가 dangerCost 를 0으로 계산해 그 함정 위를 최단 경로로 그대로 통과한다.")]
        private AITagDefinition[] debugTags;

        [Header("판정")]
        [SerializeField]
        [Tooltip("이 시간(초) 안에 못 뚫으면 방어 성공 (GDD: 제한 시간 버티기)")]
        private float timeLimit = 60f;

        [Header("풀링")]
        [SerializeField, Min(1)]
        [Tooltip("에이전트 풀 예열 개수. 실제 예열 개수는 이 값과 로스터 총원 중 큰 쪽으로 확장된다.")]
        private int poolPrewarm = 1;

        [Header("연출")]
        [SerializeField, Min(0f)]
        [Tooltip("AI 사망 후 사망 애니메이션을 보여줄 시간(초). 0이면 즉시 회수")]
        private float deathLingerTime = 0.6f;

        // ── 결과 — VerificationPhaseController 가 읽음 ───────────────────────

        /// <summary>직전 Run 에서 AI 가 골에 도달했는가 (true = 방어 뚫림 → 재설계).</summary>
        public bool LastRunBreached { get; private set; }

        /// <summary>직전 Run 에서 골에 도달한 개체 수 (HUD/결과 리포트용).</summary>
        public int LastReachedCount { get; private set; }

        /// <summary>직전 Run 에서 영구 사망한 개체 수 (HUD/결과 리포트용).</summary>
        public int LastKilledCount { get; private set; }

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        // 교체 지점: 성향별로 다른 알고리즘을 쓰려면 여기서 분기/주입.
        private readonly IPathPlanner _planner = new AStarPathPlanner();

        private ComponentPool<VerificationAgent> _agentPool;
        private AgentWaveController              _wave;
        private StageAgentRoster                 _roster; // 스테이지 진입 시 1회 확정, 재검증에도 재사용

        // 연구 보정이 반영된 최종 가중치 버퍼 — 로스터 확정 시 1회 계산, 리스트 재사용
        private readonly List<StageSpawnTable.TagWeightEntry> _finalWeights
            = new List<StageSpawnTable.TagWeightEntry>();

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            Cancel();
            _agentPool?.Clear();
        }

        // ── 오케스트레이션 ────────────────────────────────────────────────────

        /// <summary>
        /// 검증 시뮬레이션 1회 실행. 완료 후 <see cref="LastRunBreached"/> 등 결과 프로퍼티에
        /// 값이 남습니다. 같은 스테이지에서 재검증해도 최초 확정된 로스터를 그대로 소비합니다
        /// (진입 전 UI 가 보여준 구성과 실제 스폰 구성이 항상 일치해야 하므로).
        /// </summary>
        public IEnumerator Run()
        {
            LastRunBreached  = false;
            LastReachedCount = 0;
            LastKilledCount  = 0;

            var loader = MapLoader.Instance;
            if (loader == null || !loader.IsLoaded || agentPrefab == null)
            {
                Debug.LogWarning("[VerificationDirector] 구성 미비(맵/프리팹) — 방어 성공으로 간주");
                yield break;
            }

            StageAgentRoster roster = EnsureRoster();
            if (roster == null || roster.Plans.Count == 0)
            {
                Debug.LogWarning("[VerificationDirector] 구성 미비(로스터 비어있음) — 방어 성공으로 간주");
                yield break;
            }

            EnsurePool(roster.Plans.Count);
            _wave ??= new AgentWaveController(_agentPool, _planner);

            yield return _wave.Run(roster, timeLimit, deathLingerTime, this);

            LastRunBreached  = _wave.Breached;
            LastReachedCount = _wave.ReachedCount;
            LastKilledCount  = _wave.KilledCount;

            if (LastRunBreached)
                Debug.Log($"[VerificationDirector] 방어 뚫림 — 도달 {LastReachedCount}, 영구사망 {LastKilledCount}");
            else
                Debug.Log($"[VerificationDirector] 방어 성공 — 도달 {LastReachedCount}, 영구사망 {LastKilledCount}");
        }

        /// <summary>
        /// 외부 중단 — 진행 중이던 웨이브를 즉시 회수합니다.
        /// 소유자가 Run 코루틴을 StopCoroutine 한 직후 반드시 호출할 것.
        /// </summary>
        public void Cancel() => _wave?.Cancel();

        /// <summary>
        /// 확정된 로스터 캐시를 무효화합니다. 다음 <see cref="Run"/> 때 새로 굴립니다.
        /// TODO(스테이지 진입 연동): 새 스테이지/새 판 진입 시 GamePhaseManager 이벤트를
        /// 구독해 자동 호출하도록 연결할 것 (지금은 수동 호출 전용).
        /// </summary>
        public void RerollRoster() => _roster = null;

        // ── 내부 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 최초 Run 때 1회 로스터를 확정하고 캐시한다. spawnTable 이 있으면 정식 웨이브,
        /// 없으면 archetype+debugTags 로 1마리만 구성하는 폴백 경로.
        /// </summary>
        private StageAgentRoster EnsureRoster()
        {
            if (_roster != null) return _roster;

            if (spawnTable != null)
            {
                int seed = rosterSeed != 0 ? rosterSeed : System.Environment.TickCount;

                if (researchNodes != null && researchNodes.Length > 0)
                {
                    // 아웃게임 연구 보정이 반영된 최종 가중치로 굴린다.
                    // TagWeightService 가 공식의 유일한 진실 소스이므로
                    // 진입 전 UI 의 표기 확률과 실제 스폰 확률이 항상 일치한다.
                    TagWeightService.ComputeFinalWeights(spawnTable, researchNodes, _finalWeights);
                    _roster = StageAgentRoster.Build(spawnTable, seed, _finalWeights);
                }
                else
                {
                    _roster = StageAgentRoster.Build(spawnTable, seed);
                }
            }
            else
            {
                TagSet tags = (debugTags == null || debugTags.Length == 0)
                    ? TagSet.Empty
                    : new TagSet(debugTags);
                _roster = StageAgentRoster.BuildSingle(archetype, tags);
            }

            return _roster;
        }

        private void EnsurePool(int rosterTotal)
        {
            if (_agentPool == null)
            {
                // 부모를 디렉터 밑에 두어 하이어라키 오염 방지 (Managers 는 스케일 1 가정)
                _agentPool = new ComponentPool<VerificationAgent>(agentPrefab, transform);
            }

            _agentPool.Prewarm(Mathf.Max(poolPrewarm, rosterTotal));
        }
    }
}
