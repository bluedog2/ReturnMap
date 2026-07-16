using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentWaveController — 웨이브 스폰 오케스트레이션 본체 (순수 C#)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="StageAgentRoster"/> 로 확정된 전체 개체를 스폰 간격에 맞춰 하나씩 투입하고,
    /// 개체별로 독립된 시도 루프(계획 → 실행 → 학습)를 돌리는 웨이브 시뮬레이션 본체.
    /// <b>MonoBehaviour 가 아닙니다</b> — 검증/방어 페이즈가 공유해서 쓸 수 있도록 순수
    /// C# 클래스로 두고, 코루틴 구동(StartCoroutine)은 호출부가 넘겨주는
    /// <c>coroutineHost</c>(보통 <see cref="VerificationDirector"/>)에게 위임합니다.
    ///
    /// <para><b>개체별 학습</b>: 각 개체(<see cref="AgentRunner"/>)는 자기 소유의
    /// <see cref="AIMemory"/> 를 갖고 자기가 죽은 자리만 기억합니다. 재도전 횟수는
    /// 아키타입의 성향(<see cref="AIPersonality.persistence"/> → maxAttempts)을 따릅니다.</para>
    ///
    /// <para><b>중단 규약</b>: 소유자가 <see cref="Run"/> 코루틴을 중단하면(페이즈 이탈 등)
    /// 반드시 <see cref="Cancel"/> 을 호출해야 합니다. 개별 러너 코루틴을 먼저 정지시킨 뒤
    /// 활성 에이전트를 풀로 회수하는 순서를 지켜 누수를 막습니다.</para>
    /// </summary>
    public sealed class AgentWaveController
    {
        // ── 개체 러너 — 로스터 계획 1건을 소비하는 개별 시도 루프의 상태 ─────────

        private enum RunnerState { Pending, Active, Reached, Dead }

        /// <summary>개체 1명의 진행 상태. plan/기억/남은 시도 횟수는 개체별로 독립.</summary>
        private sealed class AgentRunner
        {
            public readonly StageAgentRoster.AgentSpawnPlan Plan;
            public readonly AIMemory Memory;

            public int AttemptsLeft;
            public NavGrid Grid;               // 개체별 재사용 — 시도마다 배열 재할당 방지
            public VerificationAgent ActiveAgent;
            public RunnerState State;
            public Coroutine Handle;           // coroutineHost 가 돌리는 RunAgent 코루틴 핸들

            public AgentRunner(StageAgentRoster.AgentSpawnPlan plan, int mapWidth)
            {
                Plan   = plan;
                Memory = new AIMemory(mapWidth);
                State  = RunnerState.Pending;

                AIPersonality personality = plan.Archetype != null ? plan.Archetype.Personality : null;
                AttemptsLeft = personality != null ? personality.ToBehaviorParams().maxAttempts : 1;
            }

            public bool Finished => State == RunnerState.Reached || State == RunnerState.Dead;
        }

        private readonly ComponentPool<VerificationAgent> _pool;
        private readonly IPathPlanner _planner;

        // 감시 루프에서 재사용하는 목록 — Run() 시작 시 1회만 다시 채운다 (프레임 할당 금지).
        private readonly List<AgentRunner> _runners = new List<AgentRunner>();

        private MonoBehaviour _coroutineHost; // Cancel 이 StopCoroutine 을 걸 대상

        public AgentWaveController(ComponentPool<VerificationAgent> pool, IPathPlanner planner)
        {
            _pool    = pool;
            _planner = planner;
        }

        // ── 결과 — 소유자(Director)가 읽음 ───────────────────────────────────

        /// <summary>도달 수가 방어 실패 기준(breachThreshold)에 도달했는가.</summary>
        public bool Breached { get; private set; }

        /// <summary>골에 도달한 개체 수.</summary>
        public int ReachedCount { get; private set; }

        /// <summary>영구 사망(재시도 소진 또는 경로 없음) 개체 수.</summary>
        public int KilledCount { get; private set; }

        /// <summary>현재 진행 중인(스폰되어 아직 결판나지 않은) 개체 수 — HUD 용.</summary>
        public int AliveCount { get; private set; }

        // ── 실행 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 웨이브 1회 실행. 로스터의 모든 개체가 결판나거나(도달/영구사망), 방어 실패가
        /// 확정되거나, 제한 시간이 지날 때까지 대기합니다. 종료 후 결과 프로퍼티를 읽습니다.
        /// </summary>
        public IEnumerator Run(StageAgentRoster roster, float timeLimit, float deathLingerTime,
                               MonoBehaviour coroutineHost)
        {
            Breached      = false;
            ReachedCount  = 0;
            KilledCount   = 0;
            AliveCount    = 0;
            _runners.Clear();
            _coroutineHost = coroutineHost;

            var loader = MapLoader.Instance;
            if (loader == null || !loader.IsLoaded || roster == null || roster.Plans.Count == 0)
            {
                Debug.LogWarning("[AgentWaveController] 구성 미비(맵/로스터) — 방어 성공으로 간주");
                yield break;
            }

            MapData map    = loader.CurrentMap;
            Vector2 origin = loader.MapOrigin;

            // Table 이 null 인 폴백 로스터(BuildSingle)는 합리적 기본값을 쓴다.
            int   breachThreshold = roster.Table != null ? roster.Table.BreachThreshold : 1;
            float spawnInterval   = roster.Table != null ? roster.Table.SpawnInterval : 1.5f;

            for (int i = 0; i < roster.Plans.Count; i++)
                _runners.Add(new AgentRunner(roster.Plans[i], map.width));

            float deadline      = Time.time + timeLimit;
            int   nextSpawn     = 0;
            float nextSpawnTime = Time.time; // 첫 개체는 즉시 스폰

            while (true)
            {
                // ── 스폰 루프: 간격마다 다음 러너를 개별 코루틴으로 가동 ────────
                if (nextSpawn < _runners.Count && Time.time >= nextSpawnTime && Time.time < deadline)
                {
                    AgentRunner runner = _runners[nextSpawn];
                    runner.State  = RunnerState.Active;
                    runner.Handle = coroutineHost.StartCoroutine(
                        RunAgent(runner, map, origin, deadline, deathLingerTime));

                    nextSpawn++;
                    nextSpawnTime = Time.time + spawnInterval;
                }

                RecomputeAliveCount();

                // ── 방어 실패 확정 — 나머지 진행은 무의미하므로 즉시 회수 후 종료 ──
                if (ReachedCount >= breachThreshold)
                {
                    Breached = true;
                    Cancel();
                    yield break;
                }

                // ── 전원 결판(스폰 완료 + 전부 종료) — 방어 성공 ─────────────────
                bool allSpawned = nextSpawn >= _runners.Count;
                if (allSpawned && AllRunnersFinished())
                    yield break;

                // ── 제한 시간 초과 — 방어 성공 쪽, 진행 중이던 개체 전원 회수 ────
                if (Time.time >= deadline)
                {
                    Cancel();
                    yield break;
                }

                yield return null;
            }
        }

        private bool AllRunnersFinished()
        {
            for (int i = 0; i < _runners.Count; i++)
                if (!_runners[i].Finished) return false;
            return true;
        }

        private void RecomputeAliveCount()
        {
            int alive = 0;
            for (int i = 0; i < _runners.Count; i++)
                if (_runners[i].State == RunnerState.Active) alive++;
            AliveCount = alive;
        }

        /// <summary>
        /// 외부 중단 — 아직 결판나지 않은 러너 전원을 회수합니다. 코루틴을 먼저 정지시킨
        /// 뒤 활성 에이전트를 풀로 반납하는 순서를 지켜 누수를 방지합니다.
        /// <see cref="Run"/> 내부(방어 실패 확정·시간 초과)와 소유자(페이즈 이탈)
        /// 양쪽에서 호출됩니다.
        /// </summary>
        public void Cancel()
        {
            for (int i = 0; i < _runners.Count; i++)
            {
                AgentRunner runner = _runners[i];
                if (runner.Finished) continue;

                if (_coroutineHost != null && runner.Handle != null)
                    _coroutineHost.StopCoroutine(runner.Handle);
                runner.Handle = null;

                ReleaseRunnerAgent(runner);
                runner.State = RunnerState.Dead; // 강제 중단 — 재시도/도달 어느 쪽도 아님
            }

            AliveCount = 0;
        }

        // ── 개체 러너 코루틴 ──────────────────────────────────────────────────

        /// <summary>
        /// 개체 1명의 시도 루프. 경로 계획 → 실행 → (사망 시) 학습 → 재시도를
        /// 시도 소진/시간 초과/골 도달 중 하나가 될 때까지 반복한다.
        /// </summary>
        private IEnumerator RunAgent(AgentRunner runner, MapData map, Vector2 origin,
                                     float deadline, float deathLingerTime)
        {
            AIBehaviorParams p = ResolveParams(runner.Plan.Archetype);

            while (runner.AttemptsLeft > 0 && Time.time < deadline)
            {
                // 1) 계획 — 시도마다 이 개체의 학습(memory)이 반영된 그리드로 다시 세운다
                runner.Grid = NavGrid.Build(map, p, runner.Memory, runner.Grid);
                var path = _planner.FindPath(runner.Grid, map.spawnPoint, map.goalPoint, p);

                if (path == null || path.Count == 0)
                {
                    // 경로 자체가 없으면 남은 시도도 결과가 같다 (기억만으론 길이 안 생김)
                    runner.State = RunnerState.Dead;
                    KilledCount++;
                    yield break;
                }

                // 2) 실행 — 풀에서 에이전트를 꺼내 경로 수행을 지켜본다
                Vector2 spawnPos = map.CellToWorld(map.spawnPoint.x, map.spawnPoint.y, origin);
                VerificationAgent agent = _pool.Get(spawnPos, Quaternion.identity);
                runner.ActiveAgent = agent;

                if (agent.TryGetComponent<AgentContext>(out var ctx))
                    ctx.Initialize(runner.Plan.Archetype, runner.Plan.Tags);

                agent.StartCoroutine(agent.FollowPath(path, map, origin, p));
                while (runner.ActiveAgent != null && !runner.ActiveAgent.IsDone && Time.time < deadline)
                    yield return null;

                // Cancel 로 외부 중단된 경우 — 회수는 Cancel 쪽이 이미 처리했으므로 종료만
                if (runner.ActiveAgent == null) yield break;

                if (!runner.ActiveAgent.IsDone) runner.ActiveAgent.Kill(); // 시간 초과

                bool      reached   = runner.ActiveAgent.ReachedGoal;
                GridCoord deathCell = runner.ActiveAgent.CurrentCell;

                // 사망 애니메이션이 보이도록 잠시 유지 후 회수 (Cancel 시 즉시 회수됨)
                if (!reached && deathLingerTime > 0f)
                {
                    yield return new WaitForSeconds(deathLingerTime);
                    if (runner.ActiveAgent == null) yield break; // 유지 중 외부 중단
                }
                ReleaseRunnerAgent(runner);

                if (reached)
                {
                    runner.State = RunnerState.Reached;
                    ReachedCount++;
                    yield break;
                }

                // 3) 학습 — 죽은 자리를 기억하고 다음 시도로
                runner.Memory.RecordDeath(deathCell);
                runner.AttemptsLeft--;
            }

            // 시도 소진 또는 시간 초과 — 영구 사망
            runner.State = RunnerState.Dead;
            KilledCount++;
        }

        private void ReleaseRunnerAgent(AgentRunner runner)
        {
            if (runner.ActiveAgent == null) return;
            _pool.Release(runner.ActiveAgent); // OnDespawned 가 잔여 코루틴 정리
            runner.ActiveAgent = null;
        }

        /// <summary>아키타입의 성향이 있으면 변환 값, 없으면 합리적 기본값.</summary>
        private static AIBehaviorParams ResolveParams(AgentArchetype archetype)
        {
            AIPersonality personality = archetype != null ? archetype.Personality : null;
            return personality != null ? personality.ToBehaviorParams() : AIBehaviorParams.Default;
        }
    }
}
