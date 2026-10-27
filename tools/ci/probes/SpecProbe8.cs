using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  SpecProbe8 — SPEC-8 배치모드 Play 검증 (저주부르미 · 도둑 재화 · 넉백). 임시, 커밋하지 않음
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 실제 게임 경로로 수용 기준 A2~A10 을 측정한다.
    /// <list type="bullet">
    ///   <item>검증 AI 개체: 실제 프리팹 + AgentContext.Initialize(태그) + VerificationAgent.FollowPath. 경로는 맵 밖 빈 공중의
    ///   평평한 일직선(타 물체와 충돌 없음)이고, 피격은 실제 ArrowShooter 화살 / SpikeTrap 이 만든다.</item>
    ///   <item>변이 굴림 고정: TrapMutationManager 의 직렬화 확률 필드(Normal/Dud/Critical)를 리플렉션으로 바꾼다
    ///   (Normal 100% / 전부 0% = Beneficial). 시드를 바꾸는 것이 아니라 설정값을 바꾸는 정식 이음새. 끝나면 복원.</item>
    ///   <item>도둑 탈출(A7): 실제 맵 위에서 실제 AgentWaveController 로 도둑 1마리를 돌린다.</item>
    ///   <item>재화(박살 난 지구본)는 PlayerPrefs 에 저장되므로 시작 잔액을 기억했다가 끝에 복원한다.</item>
    /// </list>
    /// 넉백 후퇴 측정: 후퇴는 피격 프레임이 아니라 이어지는 셀 루프에서 일어난다(진행 중이던 한 칸을 마친 뒤 후퇴).
    /// 그래서 피격 후 "도달한 최대 셀(피크) − 이후 최소 셀"과, 같은 구간의 x 위치 변위(피크 x − 최소 x)÷tileUnit 을 잰다.
    /// 참고로 "피격 시점 셀 − 최소 셀"(naive) 도 상세에 함께 적는다.
    /// </summary>
    public static class SpecProbe8
    {
        const string ScenePath     = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const string ArrowPath     = "Assets/_Project/ResourcceEX/Prefabs/Traps/ArrowShooter.prefab";
        const string SpikePath     = "Assets/_Project/ResourcceEX/Prefabs/Traps/SpikeTrap.prefab";
        const string ArchetypePath = "Assets/_Project/Settings/AI/Archetypes/Archetype_Default.asset";
        const double GlobalTimeout = 420.0;

        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors  = new List<string>();
        static int    _fail;
        static string _out;
        static double _wallStart;
        static bool   _done;
        static GameObject _runnerGo;

        // 복원 대상 (Finish 에서도 한 번 더 시도 — 멱등)
        static bool  _envSaved, _envRestored;
        static int   _origGlobes;
        static TrapMutationManager _tmm;
        static float _origN, _origD, _origC;
        static bool  _origHard;
        static object _origBalance;
        static VerificationPhaseController _vpc;

        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "probe8.txt");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Application.logMessageReceived += OnLog;
            _wallStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>콘솔 Error·Exception 수집. 에디터 검색 인덱서의 배치모드 예외(무해)만 제외한다.</summary>
        static void OnLog(string m, string s, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception) return;
            if (s != null && s.Contains("UnityEditor.Search.")) return;
            if (_errors.Count < 30) _errors.Add($"[{t}] {m.Split('\n')[0]}");
        }

        static string Arg(string n) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

        static void Check(string name, bool ok, string detail)
        {
            if (!ok) _fail++;
            _results.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
            Debug.Log($"[PROBE8] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static void Skip(string name, string reason)
        {
            _results.Add($"SKIP | {name} | {reason}");
            Debug.Log($"[PROBE8] SKIP {name} — {reason}");
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > GlobalTimeout)
            {
                Check("timeout", false, $"{GlobalTimeout:F0}초 초과");
                Finish();
                return;
            }
            if (_runnerGo == null && !_done && EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _runnerGo = new GameObject("SpecProbe8Runner") { hideFlags = HideFlags.HideAndDontSave };
                _runnerGo.AddComponent<Runner>();
                return;
            }
            if (_done && !EditorApplication.isPlaying) Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            RestoreEnv();
            Check("콘솔 오류 0개 (무해 목록 제외)", _errors.Count == 0, $"{_errors.Count}개");
            var sb = new StringBuilder();
            sb.AppendLine($"RESULT: {(_fail == 0 ? "PASS" : "FAIL")} (fail {_fail} / total {_results.Count})");
            foreach (var r in _results) sb.AppendLine(r);
            sb.AppendLine($"CONSOLE ERRORS ({_errors.Count}):");
            foreach (var e in _errors) sb.AppendLine("  " + e);
            string dir = Path.GetDirectoryName(_out);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_out, sb.ToString(), new UTF8Encoding(false));
            EditorApplication.Exit(_fail == 0 ? 0 : 1);
        }

        // ── 환경 복원 / 설정 ──────────────────────────────────────────────

        const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        static FieldInfo TmmField(string n) => typeof(TrapMutationManager).GetField(n, Priv);

        static void SetRoll(float n, float d, float c)
        {
            TmmField("normalChance").SetValue(_tmm, n);
            TmmField("dudChance").SetValue(_tmm, d);
            TmmField("criticalChance").SetValue(_tmm, c);
        }

        static void SetGlobes(int v)
        {
            int cur = CurrencyService.Globes;
            if (cur > v) CurrencyService.Steal(cur - v);
            else if (cur < v) CurrencyService.Add(v - cur);
        }

        /// <summary>시작 상태로 복원 (멱등). 재화 · 변이 확률/난이도/밸런스 참조 · 검증 컨트롤러.</summary>
        static void RestoreEnv()
        {
            if (!_envSaved || _envRestored) return;
            _envRestored = true;
            try
            {
                SetGlobes(_origGlobes);
                if (_tmm != null)
                {
                    TmmField("normalChance").SetValue(_tmm, _origN);
                    TmmField("dudChance").SetValue(_tmm, _origD);
                    TmmField("criticalChance").SetValue(_tmm, _origC);
                    TmmField("hardMode").SetValue(_tmm, _origHard);
                    TmmField("balance").SetValue(_tmm, _origBalance);
                }
                if (_vpc != null) _vpc.enabled = true;
            }
            catch (Exception e) { Debug.LogWarning("[PROBE8] 복원 중 예외: " + e.Message); }
        }

        // ── 시나리오 러너 (Play 중 코루틴) ───────────────────────────────

        sealed class Runner : MonoBehaviour
        {
            enum Kind { ArrowSingle, ArrowContinuous, Spike }

            /// <summary>시나리오 1건의 관측값.</summary>
            sealed class Sc
            {
                public int hp0, hpEnd, hits, consumed, crossed;
                public bool started, killed, reachedGoal, isDone;
                public int hitCell, peakCell, minCell, dropCell;
                public float dropX;
                public int pending = -1;
                public float xAtDeath, xEnd; public int cellAtDeath, cellEnd;
                public ArrowShooter shooter;
                public string tail = "";
            }

            const int PathEnd  = 24;
            const int ShooterX = 26;
            const int SpikeX   = 12;

            MapData _map; Vector2 _origin; float _tu; int _rowY;
            AgentArchetype _arch; GameObject _arrowPrefab, _spikePrefab;
            ComponentPool<VerificationAgent> _pool;
            readonly Dictionary<string, AITagDefinition> _tags = new Dictionary<string, AITagDefinition>();

            float _want = 1f;
            ArrowShooter _watch; bool _fired, _single;
            bool _respawned;

            static readonly FieldInfo PendingField = typeof(VerificationAgent).GetField("_pendingStepBackCells", Priv);

            void Start() => StartCoroutine(Scenario());
            void OnEnable()  { TrapBase.OnTrapActivated += OnAct; RespawnManager.OnRespawn += OnResp; }
            void OnDisable() { TrapBase.OnTrapActivated -= OnAct; RespawnManager.OnRespawn -= OnResp; }
            void OnAct(TrapBase t) { if (_watch != null && t == _watch) _fired = true; }
            void OnResp() => _respawned = true;

            IEnumerator Scenario()
            {
                try { yield return RunAll(); }
                finally { RestoreEnv(); Time.timeScale = 1f; }
                _done = true;
                EditorApplication.ExitPlaymode();
            }

            // ── 공용 대기/제어 ─────────────────────────────────────────

            /// <summary>timeScale 유지 + 단발 사격 모드면 첫 화살이 나온 즉시 슈터를 Dud 로 정지(화살은 정확히 1발).</summary>
            void Pump()
            {
                if (Mathf.Abs(Time.timeScale - _want) > 0.01f) Time.timeScale = _want;
                if (_single && _fired && _watch != null && _watch.CurrentState != TrapState.Dud)
                    _watch.Mutate(TrapState.Dud);
            }

            IEnumerator Frames(int n) { for (int i = 0; i < n; i++) { Pump(); yield return null; } }

            IEnumerator Secs(float s)
            {
                float end = Time.unscaledTime + s;
                while (Time.unscaledTime < end) { Pump(); yield return null; }
            }

            void SetPhase(GamePhase p)
            {
                GamePhaseManager.Instance.SetPhase(p, true);
                _want = 1f; Time.timeScale = 1f;
            }

            Vector2 CellW(int x, int y) => _map.CellToWorld(x, y, _origin);

            AITagDefinition Tag(string id) => _tags.TryGetValue(id, out var t) ? t : null;

            // ── 메인 ───────────────────────────────────────────────────

            IEnumerator RunAll()
            {
                // 준비 대기
                BuildPhaseController build = null;
                float end = Time.unscaledTime + 30f;
                while (Time.unscaledTime < end)
                {
                    if (build == null) build = FindFirstObjectByType<BuildPhaseController>();
                    if (GamePhaseManager.Instance != null && MapLoader.Instance != null && MapLoader.Instance.IsLoaded
                        && build != null && build.TrapsReady && TrapMutationManager.Instance != null
                        && RespawnManager.Instance != null) break;
                    yield return null;
                }
                bool ready = GamePhaseManager.Instance != null && MapLoader.Instance != null && MapLoader.Instance.IsLoaded
                             && TrapMutationManager.Instance != null && RespawnManager.Instance != null;
                Check("초기 준비 (매니저·맵 로드)", ready, ready ? "완료" : "시간 초과");
                if (!ready) yield break;

                _map = MapLoader.Instance.CurrentMap; _origin = MapLoader.Instance.MapOrigin;
                _tu = _map.tileUnit; _rowY = _map.height + 40;

                // 에셋
                _arch        = AssetDatabase.LoadAssetAtPath<AgentArchetype>(ArchetypePath);
                _arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath);
                _spikePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpikePath);
                foreach (var g in AssetDatabase.FindAssets("t:AITagDefinition"))
                {
                    var t = AssetDatabase.LoadAssetAtPath<AITagDefinition>(AssetDatabase.GUIDToAssetPath(g));
                    if (t != null && !string.IsNullOrEmpty(t.TagId)) _tags[t.TagId] = t;
                }
                string[] need = { "D-12", "D-02", "D-07", "S-02", "S-03", "S-04", "S-09" };
                string missing = string.Join(",", need.Where(n => Tag(n) == null));
                bool assetsOk = _arch != null && _arch.AgentPrefab != null && _arrowPrefab != null && _spikePrefab != null && missing.Length == 0;
                Check("프로브 에셋 로드", assetsOk, assetsOk ? $"태그 {need.Length}종·아키타입·프리팹 OK" : $"archetype={_arch != null} arrow={_arrowPrefab != null} spike={_spikePrefab != null} 누락 태그=[{missing}]");
                if (!assetsOk) yield break;

                _pool = new ComponentPool<VerificationAgent>(_arch.AgentPrefab, transform);
                _pool.Prewarm(1);

                // 환경 저장 + 설정
                _tmm = TrapMutationManager.Instance;
                _origGlobes  = CurrencyService.Globes;
                _origN = (float)TmmField("normalChance").GetValue(_tmm);
                _origD = (float)TmmField("dudChance").GetValue(_tmm);
                _origC = (float)TmmField("criticalChance").GetValue(_tmm);
                _origHard = (bool)TmmField("hardMode").GetValue(_tmm);
                _origBalance = TmmField("balance").GetValue(_tmm);
                _vpc = FindFirstObjectByType<VerificationPhaseController>();
                _envSaved = true;
                // 밸런스 SO 가 있으면 확률을 덮으므로 참조를 비워 직렬화 필드를 쓰게 한다 (복원됨)
                TmmField("balance").SetValue(_tmm, null);
                TmmField("hardMode").SetValue(_tmm, true);
                // 검증 컨트롤러가 켜져 있으면 Verification 진입 시 실제 웨이브가 돌아 재화·함정을 오염시킨다 → 끈다
                if (_vpc != null) _vpc.enabled = false;

                var gpm = GamePhaseManager.Instance;
                SetPhase(GamePhase.Build);
                SetPhase(GamePhase.Verification);
                yield return Frames(3);

                // ═════ A9 넉백 칸수 ═════
                SetRoll(1f, 0f, 0f);
                yield return KnockbackCase("무태그", new AITagDefinition[0], 1);
                yield return KnockbackCase("S-02 경량화", new[] { Tag("S-02") }, 2);
                yield return KnockbackCase("S-03 유리몸", new[] { Tag("S-03") }, 3);
                yield return KnockbackCase("S-02+S-09 경량화+백스텝", new[] { Tag("S-02"), Tag("S-09") }, 2);

                // ═════ A10 넉백 없음 ═════
                yield return A10Spike();
                yield return A10Sponge();
                yield return A10Death();

                // ═════ A8 도둑 처치 보상 ═════
                yield return A8ThiefKill();

                // ═════ A7 도둑 탈출 도난 ═════
                yield return A7ThiefEscape(10, 5);
                yield return A7ThiefEscape(3, 0);

                // ═════ A2 / A5 / A6 / A4 / A3 저주 ═════
                yield return CurseGroup();

                SetPhase(GamePhase.Build);
            }

            // ── 시나리오: 에이전트 1마리 ───────────────────────────────

            IEnumerator Scenario(Sc r, AITagDefinition[] tags, Kind kind, bool keepShooter)
            {
                var path = new List<GridCoord>();
                for (int x = 0; x <= PathEnd; x++) path.Add(new GridCoord(x, _rowY));

                ArrowShooter sh = null; SpikeTrap sp = null;
                if (kind == Kind.Spike)
                {
                    sp = Instantiate(_spikePrefab, CellW(SpikeX, _rowY - 1), Quaternion.identity).GetComponent<SpikeTrap>();
                    sp.ConfigureForAnchor(TrapAnchor.Floor);
                }
                else
                {
                    sh = Instantiate(_arrowPrefab, CellW(ShooterX, _rowY), Quaternion.identity).GetComponent<ArrowShooter>();
                    sh.ConfigureForAnchor(TrapAnchor.LeftWall); // 왼쪽(경로 쪽)으로 발사
                    _watch = sh; _fired = false; _single = kind == Kind.ArrowSingle;
                    r.shooter = sh;
                }

                VerificationAgent agent = _pool.Get(CellW(0, _rowY), Quaternion.identity);
                var ctx = agent.GetComponent<AgentContext>();
                ctx.Initialize(_arch, new TagSet(tags));
                var health = ctx.Health;
                r.hp0 = health.CurrentHP;
                agent.StartCoroutine(agent.FollowPath(path, _map, _origin, AIBehaviorParams.Default));

                yield return Frames(2); // 함정 Start(Normal) 이 지나간 뒤에 상태 지정
                if (kind == Kind.ArrowContinuous) sh.Mutate(TrapState.Critical);
                if (kind == Kind.Spike) sp.Mutate(TrapState.Critical);

                var lastDist = new Dictionary<Arrow, float>();
                var lastDx   = new Dictionary<Arrow, float>();
                var cur      = new HashSet<Arrow>();
                int lastHp = health.CurrentHP;
                float winStart = 0f, peakX = 0f;
                float t0 = Time.unscaledTime, deathAt = -1f;
                const float Window = 2.5f;

                void StartWin(int cell, float x)
                {
                    if (r.started) return;
                    r.started = true; winStart = Time.unscaledTime;
                    r.hitCell = r.peakCell = r.minCell = cell; peakX = x;
                }

                while (true)
                {
                    Pump();
                    float ax = agent.transform.position.x;
                    int cell = agent.CurrentCell.x;

                    // 화살 추적: 몸에 닿아 사라짐(흡수/피격) · 몸을 스쳐 지나감(면역)
                    cur.Clear();
                    foreach (var a in FindObjectsByType<Arrow>(FindObjectsSortMode.None))
                    {
                        if (!a.gameObject.activeInHierarchy) continue;
                        cur.Add(a);
                        Vector3 ap = a.transform.position, gp = agent.transform.position;
                        float dx = ap.x - gp.x;
                        if (lastDx.TryGetValue(a, out float pdx) && pdx > 0f && dx <= 0f && Mathf.Abs(ap.y - gp.y) < 1f)
                        { r.crossed++; StartWin(cell, ax); }
                        lastDx[a] = dx; lastDist[a] = Vector2.Distance(ap, gp);
                    }
                    foreach (var a in lastDist.Keys.ToList())
                    {
                        if (cur.Contains(a)) continue;
                        if (lastDist[a] < 1.5f) { r.consumed++; StartWin(cell, ax); }
                        lastDist.Remove(a); lastDx.Remove(a);
                    }

                    int hp = health.CurrentHP;
                    if (hp < lastHp) { r.hits++; StartWin(cell, ax); }
                    lastHp = hp;

                    if (r.started)
                    {
                        r.peakCell = Mathf.Max(r.peakCell, cell); peakX = Mathf.Max(peakX, ax);
                        r.dropCell = Mathf.Max(r.dropCell, r.peakCell - cell);
                        r.dropX    = Mathf.Max(r.dropX, (peakX - ax) / _tu);
                        r.minCell  = Mathf.Min(r.minCell, cell);
                    }

                    if (kind == Kind.ArrowContinuous)
                    {
                        if (agent.IsDone && deathAt < 0f)
                        {
                            deathAt = Time.unscaledTime; r.xAtDeath = ax; r.cellAtDeath = cell;
                            r.killed = health.IsDead; r.reachedGoal = agent.ReachedGoal;
                        }
                        if (deathAt >= 0f && Time.unscaledTime - deathAt >= 1.5f) break;
                    }
                    else if (r.started && Time.unscaledTime - winStart >= Window) break;
                    else if (agent.IsDone && !r.started) { break; }

                    if (Time.unscaledTime - t0 > 20f) break;
                    yield return null;
                }

                r.isDone      = agent.IsDone;
                r.reachedGoal = r.reachedGoal || agent.ReachedGoal;
                r.killed      = r.killed || health.IsDead;
                r.hpEnd       = health.CurrentHP;
                r.xEnd        = agent.transform.position.x; r.cellEnd = agent.CurrentCell.x;
                if (PendingField != null) r.pending = (int)PendingField.GetValue(agent);

                _pool.Release(agent);
                if (sp != null) Destroy(sp.gameObject);
                if (sh != null && !keepShooter) Destroy(sh.gameObject);
                _single = false;
                if (!keepShooter) _watch = null;
                yield return Frames(2);
            }

            string Fmt(Sc r) =>
                $"hp {r.hp0}→{r.hpEnd} 피격 {r.hits}회 흡수/소멸 {r.consumed} 관통 {r.crossed} | 피격시 셀 {r.hitCell} 피크 {r.peakCell} 최소 {r.minCell} → 후퇴 {r.dropCell}칸(피크기준), naive(피격시셀−최소) {r.hitCell - r.minCell}, x변위 {r.dropX:F2}칸 | pendingStepBack {r.pending}";

            // ── A9 ─────────────────────────────────────────────────────

            IEnumerator KnockbackCase(string label, AITagDefinition[] tags, int expect)
            {
                var r = new Sc();
                yield return Scenario(r, tags, Kind.ArrowSingle, false);
                bool ok = r.hits >= 1 && !r.killed && r.dropCell == expect && Mathf.Abs(r.dropX - expect) <= 0.25f;
                Check($"[A9] 넉백 {expect}칸 — {label}", ok, (r.hits >= 1 ? "" : "화살 피격 없음(측정 무효) | ") + Fmt(r));
            }

            // ── A10 ────────────────────────────────────────────────────

            IEnumerator A10Spike()
            {
                var r = new Sc();
                yield return Scenario(r, new AITagDefinition[0], Kind.Spike, false);
                bool ok = r.hits >= 1 && r.dropCell == 0 && r.dropX <= 0.25f && r.pending == 0;
                Check("[A10] 가시 피격 → 후퇴 0칸", ok, (r.hits >= 1 ? "" : "가시 피격 없음(측정 무효) | ") + Fmt(r));
            }

            IEnumerator A10Sponge()
            {
                var r = new Sc();
                yield return Scenario(r, new[] { Tag("D-07") }, Kind.ArrowSingle, false);
                bool ok = r.consumed >= 1 && r.hits == 0 && r.hpEnd == r.hp0 && r.dropCell == 0 && r.dropX <= 0.25f && r.pending == 0;
                Check("[A10] 스펀지 몸(D-07) 화살 흡수 → 후퇴 0칸", ok, (r.consumed >= 1 ? "" : "흡수 관측 없음(측정 무효) | ") + Fmt(r));
            }

            IEnumerator A10Death()
            {
                var r = new Sc();
                yield return Scenario(r, new AITagDefinition[0], Kind.ArrowContinuous, false);
                float moved = Mathf.Abs(r.xEnd - r.xAtDeath) / _tu;
                bool ok = r.killed && !r.reachedGoal && moved <= 0.05f && r.cellEnd == r.cellAtDeath && r.pending == 0;
                Check("[A10] 사망(치명타) → 후퇴 0칸", ok,
                    $"killed={r.killed} 골도달={r.reachedGoal} 피격 {r.hits}회 | 사망 후 1.5초 x변위 {moved:F2}칸 셀 {r.cellAtDeath}→{r.cellEnd} pendingStepBack {r.pending}");
            }

            // ── A8 ─────────────────────────────────────────────────────

            IEnumerator A8ThiefKill()
            {
                SetGlobes(0);
                var r = new Sc();
                yield return Scenario(r, new[] { Tag("S-04") }, Kind.ArrowContinuous, false);
                int g = CurrencyService.Globes;
                Check("[A8] 도둑 처치 → 지구본 0→5", r.killed && g == 5,
                    $"killed={r.killed} 골도달={r.reachedGoal} 피격 {r.hits}회 지구본 0→{g}");
            }

            // ── A7 ─────────────────────────────────────────────────────

            IEnumerator A7ThiefEscape(int start, int expect)
            {
                SetGlobes(start);
                var roster = StageAgentRoster.BuildSingle(_arch, new TagSet(new[] { Tag("S-04") }));
                var wave   = new AgentWaveController(_pool, new AStarPathPlanner());
                _want = 3f; Time.timeScale = 3f; // 맵 횡단이 길다 — 배속 (결과 무관)
                yield return wave.Run(roster, 120f, 0.6f, this);
                _want = 1f; Time.timeScale = 1f;
                int g = CurrencyService.Globes;
                Check($"[A7] 도둑 탈출 → 지구본 {start}→{expect}", wave.ReachedCount == 1 && g == expect,
                    $"도달 {wave.ReachedCount} 영구사망 {wave.KilledCount} 지구본 {start}→{g} (맵 {_map.mapId})");
                yield return Frames(2);
            }

            // ── 저주 (A2~A6) ───────────────────────────────────────────

            ArrowShooter SpawnShooter(int rowOffset)
            {
                var s = Instantiate(_arrowPrefab, CellW(ShooterX, _rowY + rowOffset), Quaternion.identity).GetComponent<ArrowShooter>();
                s.ConfigureForAnchor(TrapAnchor.LeftWall);
                return s;
            }

            static int GoldenBlocks(TrapBase t)
            {
                int n = 0;
                foreach (var c in t.GetComponentsInChildren<Transform>(false))
                    if (c.name == "BeneficialBlock" && c.gameObject.activeInHierarchy) n++;
                return n;
            }

            int GroundAt(Vector2 p)
            {
                Physics2D.SyncTransforms();
                return Physics2D.OverlapPointAll(p, 1 << LayerMask.NameToLayer("Ground")).Length;
            }

            Vector2 OutwardPoint(ArrowShooter s) // LeftWall 슬롯: 바깥 +1칸 = +x 방향
                => (Vector2)s.transform.position + new Vector2(1f, 0f) * _tu;

            /// <summary>Build→Verification(저주 해제) 후 슈터(+대조 슈터)를 세우고, 주어진 태그의 개체가 슈터 화살에 맞게 한다.</summary>
            IEnumerator CurseSetup(AITagDefinition[] tags, Sc r, Action<ArrowShooter, ArrowShooter> outShooters)
            {
                SetPhase(GamePhase.Build);
                SetPhase(GamePhase.Verification);
                yield return Frames(2);
                var control = SpawnShooter(10); // 저주와 무관한 대조군 (다른 행 — 화살이 개체와 만나지 않음)
                yield return Frames(2);
                control.Mutate(TrapState.Dud);  // 대조 슈터 사격은 불필요 — Play 진입 때 변이로 덮어쓰임
                yield return Scenario(r, tags, Kind.ArrowSingle, true);
                outShooters(r.shooter, control);
            }

            IEnumerator CurseGroup()
            {
                var gpm = GamePhaseManager.Instance;
                ArrowShooter s = null, c = null;

                // ── A2 : 굴림 Normal → 저주 → Critical ──
                SetRoll(1f, 0f, 0f);
                var r2 = new Sc();
                yield return CurseSetup(new[] { Tag("D-12") }, r2, (a, b) => { s = a; c = b; });
                bool hit2 = r2.hits >= 1;
                SetPhase(GamePhase.Play);
                yield return Frames(3);
                TrapState a2 = s.CurrentState, a2c = c.CurrentState;
                Check("[A2] 저주 Normal→Critical", hit2 && a2 == TrapState.Critical && a2c == TrapState.Normal,
                    $"D-12 피격 {r2.hits}회(hp {r2.hp0}→{r2.hpEnd}) | 굴림 Normal(확률 고정) → 저주 슈터 {a2}, 대조 슈터(비저주) {a2c}");

                // ── A5 : 하드모드 사망 리롤 후에도 저주 유지 ──
                int seedBefore = (int)TmmField("currentSeed").GetValue(_tmm);
                var ph = FindFirstObjectByType<PlayerHealth>();
                bool a5ok = false; string a5d;
                if (ph == null) a5d = "PlayerHealth 없음";
                else
                {
                    float we = Time.unscaledTime + 6f;
                    while ((ph.IsInvincible || ph.IsDead) && Time.unscaledTime < we) { Pump(); yield return null; }
                    int deathBefore = RespawnManager.Instance.DeathCount;
                    _respawned = false;
                    ph.TakeDamage(ph.MaxHp); // 실제 사망 경로 → RespawnManager → TrapMutationManager.OnPlayerRespawn(하드모드 리롤)
                    we = Time.unscaledTime + 8f;
                    while (!_respawned && Time.unscaledTime < we) { Pump(); yield return null; }
                    yield return Frames(3);
                    int seedAfter = (int)TmmField("currentSeed").GetValue(_tmm);
                    TrapState s5 = s.CurrentState, c5 = c.CurrentState;
                    bool died = RespawnManager.Instance.DeathCount > deathBefore && _respawned;
                    a5ok = hit2 && died && s5 == TrapState.Critical && c5 == TrapState.Normal;
                    a5d = $"사망·리스폰 {died} (DeathCount {deathBefore}→{RespawnManager.Instance.DeathCount}) 시드 {(seedBefore != seedAfter ? "리롤됨" : "동일")} | 리롤(Normal) 후 저주 슈터 {s5}, 대조 {c5}";
                }
                Check("[A5] 하드모드 사망 리롤 후에도 저주 유지(Critical)", a5ok, a5d);

                // ── A6 : Build → Verification(무피격) → Play(Normal) → Normal ──
                SetPhase(GamePhase.Build);
                SetPhase(GamePhase.Verification);
                yield return Secs(0.5f);
                SetPhase(GamePhase.Play);
                yield return Frames(3);
                TrapState a6 = s.CurrentState, a6c = c.CurrentState;
                Check("[A6] Build/Verification 진입 시 저주 해제", hit2 && a6 == TrapState.Normal && a6c == TrapState.Normal,
                    $"(A2 에서 저주된 같은 슈터) Build→Verification(피격 없음)→Play 굴림 Normal → 슈터 {a6}, 대조 {a6c}");

                CleanShooters(s, c);
                yield return Frames(2);

                // ── A4 : 화살 면역 + D-12 → 저주 없음 ──
                SetRoll(1f, 0f, 0f);
                var r4 = new Sc();
                yield return CurseSetup(new[] { Tag("D-02"), Tag("D-12") }, r4, (a, b) => { s = a; c = b; });
                SetPhase(GamePhase.Play);
                yield return Frames(3);
                TrapState a4 = s.CurrentState;
                Check("[A4] 화살 면역+D-12 피격 → 저주 없음(Normal 유지)",
                    r4.crossed >= 1 && r4.hits == 0 && r4.hpEnd == r4.hp0 && a4 == TrapState.Normal,
                    (r4.crossed >= 1 ? "" : "화살이 몸을 스친 관측 없음(측정 무효) | ") + $"관통 {r4.crossed} 피격 {r4.hits} hp {r4.hp0}→{r4.hpEnd} | 굴림 Normal → 슈터 {a4}");
                CleanShooters(s, c);
                yield return Frames(2);

                // ── A3 : 굴림 Beneficial + 저주 → 황금 블록 0개, Normal ──
                SetRoll(0f, 0f, 0f); // Normal/Dud/Critical 0% → 나머지 100% = Beneficial
                var r3 = new Sc();
                yield return CurseSetup(new[] { Tag("D-12") }, r3, (a, b) => { s = a; c = b; });
                bool hit3 = r3.hits >= 1;
                SetPhase(GamePhase.Play);
                yield return Frames(3);
                TrapState a3 = s.CurrentState, a3c = c.CurrentState;
                int blocks = GoldenBlocks(s), ground = GroundAt(OutwardPoint(s));
                int blocksC = GoldenBlocks(c), groundC = GroundAt(OutwardPoint(c));
                Check("[A3] 저주 Beneficial→황금 블록 0개·Normal",
                    hit3 && a3 == TrapState.Normal && blocks == 0 && ground == 0 && a3c == TrapState.Beneficial && blocksC == 1 && groundC >= 1,
                    $"D-12 피격 {r3.hits}회 | 굴림 Beneficial(확률 고정) → 저주 슈터 {a3} 황금블록 {blocks}개(바깥칸 지형 콜라이더 {ground}) | 대조(비저주) {a3c} 황금블록 {blocksC}개(바깥칸 콜라이더 {groundC})");

                CleanShooters(s, c);
                SetRoll(1f, 0f, 0f);
                yield return Frames(2);
            }

            void CleanShooters(ArrowShooter a, ArrowShooter b)
            {
                _watch = null; _single = false;
                if (a != null) Destroy(a.gameObject);
                if (b != null) Destroy(b.gameObject);
            }
        }
    }
}
