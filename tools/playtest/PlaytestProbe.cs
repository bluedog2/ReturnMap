using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  PlaytestProbe — 자동 플레이테스트 (fixture 배치 → 검증 페이즈 N 시드)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 기획자가 쓴 스테이지별 함정 배치 fixture(tools/playtest/fixtures/*.json)로 검증 페이즈(AI 돌파)를
    /// 시드별로 돌려 결과를 Playtest.json 으로 남긴다. 판정(PASS/FAIL)은 "실행 성공" 여부만이며
    /// 돌파율은 판정이 아니라 데이터다.
    /// tools/ci/run-probe.ps1 이 Assets/_Project/Editor/CI/ 로 복사해 실행합니다 (-nographics 없음).
    ///
    /// <para>호출: <c>Unity -batchmode -projectPath &lt;p&gt; -executeMethod ReTrap.EditorTools.PlaytestProbe.Run
    /// -probeOutput &lt;txt&gt;</c></para>
    /// <para>Time.captureDeltaTime 을 1/60 로 고정해 같은 머신에서 재현 가능하게 한다. timeScale 은
    /// VerificationSpeedController 소관이므로 건드리지 않는다(1x).</para>
    /// </summary>
    public static class PlaytestProbe
    {
        const string ScenePath      = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const double GlobalTimeout  = 1620.0; // run-probe -TimeoutMinutes 30 보다 먼저 끝나 결과를 남기게
        const float  WaitTimeout    = 30f;
        const float  FixedDelta     = 1f / 60f;
        const float  DefaultTimeLimit = 60f;
        const float  TimeoutMargin  = 30f;

        // ── fixture 스키마 ──────────────────────────────────────────

        [Serializable] class Placement { public string trap; public int x; public int y; }

        [Serializable] class Fixture
        {
            public string mapId;
            public int seedCount;
            public int seedBase;
            public Placement[] placements;
        }

        // ── 결과 ────────────────────────────────────────────────────

        class SeedResult { public int seed; public bool breached; public int reached; public int killed; public float simSec; }

        class FixtureResult
        {
            public string mapId;
            public string spawnTable = "";
            public readonly List<SeedResult> seeds = new List<SeedResult>();
            public float BreachRate => seeds.Count == 0 ? 0f : seeds.Count(s => s.breached) / (float)seeds.Count;
        }

        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors  = new List<string>();
        static readonly List<FixtureResult> _fixtureResults = new List<FixtureResult>();
        static int    _errorTotal;
        static int    _fail;
        static string _out;
        static double _wallStart;
        static bool   _done;
        static GameObject _runnerGo;

        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "playtest.txt");
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
            _errorTotal++;
            if (_errors.Count < 30) _errors.Add($"[{t}] {m.Split('\n')[0]}");
        }

        static string Arg(string n)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1];
            return null;
        }

        static void Check(string name, bool ok, string detail)
        {
            if (!ok) _fail++;
            _results.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
            Debug.Log($"[PLAYTEST] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > GlobalTimeout)
            {
                Check("timeout", false, $"{GlobalTimeout:F0}초 초과");
                Time.captureDeltaTime = 0f;
                Finish();
                return;
            }
            if (_runnerGo == null && !_done && EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _runnerGo = new GameObject("PlaytestProbeRunner") { hideFlags = HideFlags.HideAndDontSave };
                _runnerGo.AddComponent<Runner>();
                return;
            }
            if (_done && !EditorApplication.isPlaying) Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Check("콘솔 오류 0개 (무해 목록 제외)", _errorTotal == 0, $"{_errorTotal}개");

            var sb = new StringBuilder();
            sb.AppendLine($"RESULT: {(_fail == 0 ? "PASS" : "FAIL")} (fail {_fail} / total {_results.Count})");
            foreach (var r in _results) sb.AppendLine(r);
            sb.AppendLine($"CONSOLE ERRORS ({_errorTotal}):");
            foreach (var e in _errors) sb.AppendLine("  " + e);
            string dir = Path.GetDirectoryName(_out);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_out, sb.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, "Playtest.json"), BuildJson(), new UTF8Encoding(false));
            EditorApplication.Exit(_fail == 0 ? 0 : 1);
        }

        // ── JSON 출력 ───────────────────────────────────────────────

        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static string BuildJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"ref\":\"").Append(Esc(GetRef())).Append("\",\"results\":[");
            for (int i = 0; i < _fixtureResults.Count; i++)
            {
                var r = _fixtureResults[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"mapId\":\"").Append(Esc(r.mapId)).Append("\",\"spawnTable\":\"").Append(Esc(r.spawnTable)).Append("\",\"seeds\":[");
                for (int k = 0; k < r.seeds.Count; k++)
                {
                    var s = r.seeds[k];
                    if (k > 0) sb.Append(',');
                    sb.Append("{\"seed\":").Append(s.seed)
                      .Append(",\"breached\":").Append(s.breached ? "true" : "false")
                      .Append(",\"reached\":").Append(s.reached)
                      .Append(",\"killed\":").Append(s.killed)
                      .Append(",\"simSec\":").Append(F(s.simSec)).Append('}');
                }
                sb.Append("],\"breachRate\":").Append(F(r.BreachRate)).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// 검증 대상 ref 식별용. run-probe 의 임시 커밋(HEAD)은 ref 를 부모로 가지므로 HEAD^ 를 우선 쓰고,
        /// 실패하면 HEAD, 그것도 실패하면 "unknown".
        /// </summary>
        static string GetRef()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Git(root, "rev-parse HEAD^") ?? Git(root, "rev-parse HEAD") ?? "unknown";
        }

        static string Git(string workDir, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = workDir, RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(5000);
                    return p.ExitCode == 0 && o.Length > 0 ? o : null;
                }
            }
            catch { return null; }
        }

        // ── fixture 로드 ────────────────────────────────────────────

        static string FindFixtureDir()
        {
            string outDir = Path.GetDirectoryName(_out);
            if (!string.IsNullOrEmpty(outDir))
            {
                string a = Path.Combine(outDir, "fixtures");
                if (Directory.Exists(a)) return a;
            }
            string b = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "tools", "playtest", "fixtures");
            return Directory.Exists(b) ? b : null;
        }

        // ── 시나리오 러너 (Play 중 코루틴) ───────────────────────────────

        sealed class Runner : MonoBehaviour
        {
            string _loadFailed;

            void Start() => StartCoroutine(Scenario());

            void OnMapLoadFailed(string a, string b) => _loadFailed = $"{a} / {b}";

            /// <summary>조건이 참이 될 때까지 실시간(wall) 기준으로 대기.</summary>
            IEnumerator WaitUntil(Func<bool> cond, float timeout, Action<bool> result)
            {
                float end = Time.realtimeSinceStartup + timeout;
                while (Time.realtimeSinceStartup < end)
                {
                    if (cond()) { result(true); yield break; }
                    yield return null;
                }
                result(cond());
            }

            IEnumerator Scenario()
            {
                MapLoader.OnMapLoadFailed += OnMapLoadFailed;
                Time.captureDeltaTime = FixedDelta;
                try
                {
                    yield return RunAll();
                }
                finally
                {
                    MapLoader.OnMapLoadFailed -= OnMapLoadFailed;
                    Time.captureDeltaTime = 0f;
                    Time.timeScale = 1f;
                }
                _done = true;
                EditorApplication.ExitPlaymode();
            }

            IEnumerator RunAll()
            {
                BuildPhaseController build = null;
                bool ready = false;
                yield return WaitUntil(() =>
                {
                    if (build == null) build = FindFirstObjectByType<BuildPhaseController>();
                    return GamePhaseManager.Instance != null && MapLoader.Instance != null
                        && MapLoader.Instance.IsLoaded && build != null && build.TrapsReady;
                }, WaitTimeout, ok => ready = ok);
                Check("초기 준비 (매니저·맵·함정 로드)", ready, ready ? "완료" : "매니저/맵/함정 로드 시간 초과");
                if (!ready) yield break;

                var loader  = MapLoader.Instance;
                var catalog = loader.Catalog;
                if (catalog == null || catalog.Count == 0)
                {
                    Check("스테이지 카탈로그", false, "카탈로그 없음 또는 비어있음");
                    yield break;
                }

                // fixture 로드
                string dir = FindFixtureDir();
                if (dir == null)
                {
                    Check("fixture 폴더", false, "fixtures 폴더 없음 (probeOutput 폴더/fixtures 또는 tools/playtest/fixtures)");
                    yield break;
                }
                var files = Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList();
                Check("fixture 폴더", files.Count > 0, $"{dir} — {files.Count}개");
                if (files.Count == 0) yield break;

                Fixture firstValid = null;
                SeedResult firstResult = null;

                foreach (string file in files)
                {
                    string label = Path.GetFileName(file);
                    Fixture fx = null;
                    string err = null;
                    try { fx = JsonUtility.FromJson<Fixture>(File.ReadAllText(file, Encoding.UTF8)); }
                    catch (Exception e) { err = "JSON 파싱 실패: " + e.Message; }
                    if (err == null) err = Validate(fx, catalog);
                    if (err != null) { Check($"[{label}] fixture 검증", false, err); continue; }

                    var fr = new FixtureResult { mapId = fx.mapId };
                    _fixtureResults.Add(fr);
                    string failReason = null;
                    for (int i = 0; i < fx.seedCount && failReason == null; i++)
                    {
                        SeedResult sr = null;
                        yield return RunSeed(fx, fx.seedBase + i, fr, r => sr = r, r => failReason = r);
                        if (sr != null)
                        {
                            fr.seeds.Add(sr);
                            if (firstValid == null) { firstValid = fx; firstResult = sr; }
                        }
                    }
                    Check($"[{fx.mapId}] 실행 ({label})", failReason == null,
                        failReason ?? $"{fr.seeds.Count}시드 완료, 돌파율 {F(fr.BreachRate)}, 스폰테이블 '{fr.spawnTable}'");
                }

                // 결정성: 첫 fixture 첫 시드 재실행
                if (firstValid == null)
                {
                    Check("결정성 (첫 시드 재실행)", false, "완료된 시드 없음");
                    yield break;
                }
                SeedResult again = null;
                string againFail = null;
                var scratch = new FixtureResult { mapId = firstValid.mapId };
                yield return RunSeed(firstValid, firstResult.seed, scratch, r => again = r, r => againFail = r);
                bool same = again != null && again.breached == firstResult.breached
                    && again.reached == firstResult.reached && again.killed == firstResult.killed;
                Check("결정성 (첫 시드 재실행)", same,
                    again == null ? $"재실행 실패: {againFail}"
                    : $"[{firstValid.mapId}] 시드 {firstResult.seed}: 1차 breached={firstResult.breached}/reached={firstResult.reached}/killed={firstResult.killed}"
                      + $" vs 2차 breached={again.breached}/reached={again.reached}/killed={again.killed}");
            }

            static string Validate(Fixture fx, StageCatalog catalog)
            {
                if (fx == null) return "fixture 비어있음";
                if (string.IsNullOrEmpty(fx.mapId) || !Regex.IsMatch(fx.mapId, @"^stage_\d+$")) return $"mapId 형식 위반: '{fx.mapId}'";
                if (catalog.IndexOf(fx.mapId) < 0) return $"카탈로그에 없는 mapId: {fx.mapId}";
                if (fx.seedCount < 1 || fx.seedCount > 20) return $"seedCount 범위(1~20) 위반: {fx.seedCount}";
                if (fx.seedBase < 1) return $"seedBase 는 1 이상이어야 함(0 은 랜덤 시드): {fx.seedBase}";
                if (fx.placements == null) return "placements 누락";
                if (fx.placements.Length > 64) return $"placements 64개 초과: {fx.placements.Length}";
                foreach (var p in fx.placements) if (p == null || string.IsNullOrEmpty(p.trap)) return "placements 항목에 trap 이름 없음";
                return null;
            }

            /// <summary>시드 1회: 맵 재로드 → Build → 설치 → 시드 → Verification → 종료 대기. 실패 시 onFail(사유).</summary>
            IEnumerator RunSeed(Fixture fx, int seed, FixtureResult fr, Action<SeedResult> onOk, Action<string> onFail)
            {
                var loader = MapLoader.Instance;
                var gpm    = GamePhaseManager.Instance;
                var build  = FindFirstObjectByType<BuildPhaseController>();
                string tag = $"시드 {seed}: ";

                // 맵 재로드 (매 시드 깨끗한 상태)
                _loadFailed = null;
                yield return loader.LoadMapRoutine(fx.mapId);
                yield return WaitUntil(() => !loader.IsLoading && loader.IsLoaded
                    && loader.CurrentMap != null && loader.CurrentMap.mapId == fx.mapId, WaitTimeout, _ => { });
                yield return null;
                if (_loadFailed != null || !loader.IsLoaded || loader.CurrentMap == null || loader.CurrentMap.mapId != fx.mapId)
                {
                    onFail(tag + "맵 로드 실패 " + (_loadFailed ?? ""));
                    yield break;
                }
                TrapMutationManager.Instance.RollNewSeed();
                gpm.SetPhase(GamePhase.Build, true);
                yield return null;
                yield return WaitUntil(() => build != null && build.TrapsReady, WaitTimeout, _ => { });
                yield return null; // 슬롯 마커/예산 안정화

                // 설치
                var tryPlace = typeof(BuildPhaseController).GetMethod("TryPlace", BindingFlags.Instance | BindingFlags.NonPublic);
                if (tryPlace == null) { onFail(tag + "TryPlace 리플렉션 실패"); yield break; }
                foreach (var p in fx.placements)
                {
                    int idx = -1;
                    for (int k = 0; k < build.TrapDefinitions.Count; k++)
                        if (build.TrapDefinitions[k].name == p.trap) { idx = k; break; }
                    if (idx < 0) { onFail(tag + $"함정 정의 '{p.trap}' 없음 (로드된 정의: {string.Join(",", build.TrapDefinitions.Select(d => d.name))})"); yield break; }
                    if (!TrapSlotRegistry.TryGet(p.x, p.y, out var slot)) { onFail(tag + $"({p.x},{p.y}) 슬롯 없음"); yield break; }
                    build.SelectTrap(idx);
                    tryPlace.Invoke(build, new object[] { p.x, p.y });
                    if (slot.IsEmpty)
                    {
                        onFail(tag + $"{p.trap} @({p.x},{p.y}) 설치 실패 (슬롯 앵커 {slot.Anchor}, 잔여 예산 {build.RemainingBudget})");
                        yield break;
                    }
                }
                Physics2D.SyncTransforms();

                // 시드 지정 + 검증 페이즈
                var director = FindFirstObjectByType<VerificationDirector>();
                if (director == null) { onFail(tag + "VerificationDirector 없음"); yield break; }
                director.SetRosterSeed(seed);

                var tableField = typeof(VerificationDirector).GetField("spawnTable", BindingFlags.Instance | BindingFlags.NonPublic);
                var tbl = tableField?.GetValue(director) as UnityEngine.Object;
                fr.spawnTable = tbl != null ? tbl.name : "(없음: 폴백 1마리)";

                var limitProp = typeof(VerificationDirector).GetProperty("TimeLimit", BindingFlags.Instance | BindingFlags.NonPublic);
                float limit = limitProp != null ? (float)limitProp.GetValue(director) : DefaultTimeLimit;
                float timeout = limit + TimeoutMargin;

                gpm.SetPhase(GamePhase.Verification, true);
                float simStart = Time.time;
                bool timedOut = false;
                while (gpm.currentPhase == GamePhase.Verification)
                {
                    if (Time.time - simStart > timeout) { timedOut = true; break; }
                    yield return null;
                }
                float simSec = Time.time - simStart;
                if (timedOut)
                {
                    gpm.SetPhase(GamePhase.Build, true);
                    yield return null;
                    onFail(tag + $"검증 페이즈 타임아웃 (시뮬 {simSec:F1}초 > {timeout:F0}초)");
                    yield break;
                }

                onOk(new SeedResult
                {
                    seed = seed,
                    breached = director.LastRunBreached,
                    reached = director.LastReachedCount,
                    killed = director.LastKilledCount,
                    simSec = simSec
                });
                gpm.SetPhase(GamePhase.Build, true);
                yield return null;
            }
        }
    }
}
