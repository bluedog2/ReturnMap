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
    //  SmokeProbe — 회귀 스모크 프로브 (배치모드 Play 검증)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 모든 스테이지를 순회하며 맵 로드 → 플레이어 스폰 → 함정 설치(UI 없이) → 설치 칸 솔리드 →
    /// 검증/플레이 페이즈 진입을 점검하고 콘솔 오류 유무를 확인합니다.
    /// tools/ci/run-probe.ps1 이 Assets/_Project/Editor/CI/ 로 복사해 실행합니다 (-nographics 없음).
    ///
    /// <para>호출: <c>Unity -batchmode -projectPath &lt;p&gt; -executeMethod ReTrap.EditorTools.SmokeProbe.Run
    /// -probeOutput &lt;txt&gt;</c></para>
    /// <para>결과 파일 첫 줄: <c>RESULT: PASS|FAIL (fail n / total m)</c>. 종료 코드 0=PASS / 1=FAIL.</para>
    /// <para>Time.timeScale 은 VerificationSpeedController 소관이므로 건드리지 않는다.</para>
    /// </summary>
    public static class SmokeProbe
    {
        const string ScenePath    = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const double GlobalTimeout = 300.0;
        const float  WaitTimeout   = 30f;

        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors  = new List<string>();
        static int    _errorTotal;
        static int    _fail;
        static string _out;
        static double _wallStart;
        static bool   _done;
        static GameObject _runnerGo;

        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "smoke.txt");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Application.logMessageReceived += OnLog;
            _wallStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// 콘솔 Error·Exception 수집. 에디터 검색 인덱서의 배치모드 예외(무해)만 제외한다.
        /// </summary>
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
            Debug.Log($"[SMOKE] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > GlobalTimeout)
            {
                Check("timeout", false, $"{GlobalTimeout:F0}초 초과");
                Finish();
                return;
            }
            // 플레이 진입 후 러너를 한 번만 생성
            if (_runnerGo == null && !_done && EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _runnerGo = new GameObject("SmokeProbeRunner") { hideFlags = HideFlags.HideAndDontSave };
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
            EditorApplication.Exit(_fail == 0 ? 0 : 1);
        }

        // ── 시나리오 러너 (Play 중 코루틴) ───────────────────────────────

        sealed class Runner : MonoBehaviour
        {
            string _loadFailed;
            PlayerController _player;

            void Start() => StartCoroutine(Scenario());

            void OnMapLoadFailed(string a, string b) => _loadFailed = $"{a} / {b}";

            /// <summary>조건이 참이 될 때까지 실시간 기준으로 대기. 시간 내 성공 여부 반환은 result 로.</summary>
            IEnumerator WaitUntil(Func<bool> cond, float timeout, Action<bool> result)
            {
                float end = Time.unscaledTime + timeout;
                while (Time.unscaledTime < end)
                {
                    if (cond()) { result(true); yield break; }
                    yield return null;
                }
                result(cond());
            }

            IEnumerator Wait(float seconds)
            {
                float end = Time.unscaledTime + seconds;
                while (Time.unscaledTime < end) yield return null;
            }

            IEnumerator Scenario()
            {
                MapLoader.OnMapLoadFailed += OnMapLoadFailed;
                try
                {
                    yield return RunAll();
                }
                finally
                {
                    MapLoader.OnMapLoadFailed -= OnMapLoadFailed;
                }
                _done = true;
                EditorApplication.ExitPlaymode();
            }

            IEnumerator RunAll()
            {
                // 1) 초기 준비 대기
                BuildPhaseController build = null;
                bool ready = false;
                yield return WaitUntil(() =>
                {
                    if (build == null) build = FindFirstObjectByType<BuildPhaseController>();
                    return GamePhaseManager.Instance != null && MapLoader.Instance != null
                        && MapLoader.Instance.IsLoaded && build != null && build.TrapsReady;
                }, WaitTimeout, ok => ready = ok);
                Check("초기 준비 (매니저·맵·함정 로드)", ready,
                    ready ? "완료" : $"GPM={(GamePhaseManager.Instance != null)} MapLoader={(MapLoader.Instance != null)} 로드={(MapLoader.Instance != null && MapLoader.Instance.IsLoaded)} build={(build != null)}");
                if (!ready) yield break;

                var loader  = MapLoader.Instance;
                var catalog = loader.Catalog;
                if (catalog == null || catalog.Count == 0)
                {
                    Check("스테이지 카탈로그", false, "카탈로그 없음 또는 비어있음");
                    yield break;
                }

                var ids = new List<string>();
                for (int i = 0; i < catalog.Count; i++) ids.Add(catalog.GetByIndex(i).mapId);

                foreach (string id in ids)
                    yield return RunStage(id, loader, build);
            }

            IEnumerator RunStage(string id, MapLoader loader, BuildPhaseController build)
            {
                var gpm = GamePhaseManager.Instance;

                // a) 맵 로드
                _loadFailed = null;
                if (loader.CurrentMap == null || loader.CurrentMap.mapId != id)
                {
                    yield return loader.LoadMapRoutine(id);
                    yield return WaitUntil(() => !loader.IsLoading && loader.IsLoaded
                        && loader.CurrentMap != null && loader.CurrentMap.mapId == id, WaitTimeout, _ => { });
                    yield return null;
                    TrapMutationManager.Instance.RollNewSeed();
                    gpm.SetPhase(GamePhase.Build, true);
                }
                else
                {
                    yield return null;
                    gpm.SetPhase(GamePhase.Build, true);
                }
                bool loaded = _loadFailed == null && loader.IsLoaded && loader.CurrentMap != null && loader.CurrentMap.mapId == id;
                Check($"[{id}] 맵 로드", loaded, loaded ? "OK" : (_loadFailed ?? $"현재 맵 '{loader.CurrentMap?.mapId}' 로드={loader.IsLoaded}"));
                if (!loaded) yield break;
                yield return Wait(0.5f); // 스폰/리스폰 포인트 안정화

                // b) 플레이어 스폰
                _player = null;
                yield return WaitUntil(() => (_player = FindFirstObjectByType<PlayerController>()) != null, 5f, _ => { });
                Rigidbody2D rb = _player != null ? _player.GetComponent<Rigidbody2D>() : null;
                string spawnDetail;
                bool spawnOk = false;
                if (_player == null || rb == null) spawnDetail = $"player={(_player != null)} rb={(rb != null)}";
                else if (RespawnManager.Instance == null) spawnDetail = "RespawnManager 없음";
                else
                {
                    float dist = Vector2.Distance(_player.transform.position, RespawnManager.Instance.RespawnPoint);
                    spawnOk = rb.simulated && dist <= 1.5f;
                    spawnDetail = $"simulated={rb.simulated} 리스폰점 거리 {dist:F2}";
                }
                Check($"[{id}] 플레이어 스폰", spawnOk, spawnDetail);

                // c) 함정 설치 (UI 없이)
                var tryPlace = typeof(BuildPhaseController).GetMethod("TryPlace", BindingFlags.Instance | BindingFlags.NonPublic);
                int trapKinds = build.TrapDefinitions.Count;
                var emptySlots = TrapSlotRegistry.GetEmpty().Take(4).ToList();
                int totalSlots = emptySlots.Count;
                int placed = 0;
                if (tryPlace == null) Check($"[{id}] 함정 설치", false, "TryPlace 리플렉션 실패");
                else
                {
                    foreach (var slot in emptySlots)
                    {
                        for (int k = 0; k < trapKinds && slot.IsEmpty; k++)
                        {
                            build.SelectTrap(k);
                            tryPlace.Invoke(build, new object[] { slot.GridX, slot.GridY });
                        }
                        if (!slot.IsEmpty) placed++;
                    }
                    bool placeOk = totalSlots == 0 || placed >= 1;
                    Check($"[{id}] 함정 설치", placeOk,
                        totalSlots == 0 ? "빈 슬롯 0개" : $"{placed}/{totalSlots}개 설치 (함정 종류 {trapKinds}, 잔여 예산 {build.RemainingBudget})"
                        + (placeOk ? "" : " — 예산/호환 문제로 설치 실패"));
                }

                // d) 설치 칸 솔리드
                Physics2D.SyncTransforms();
                int solidTotal = 0, solidOk = 0;
                int groundMask = 1 << LayerMask.NameToLayer("Ground");
                foreach (var slot in TrapSlotRegistry.GetOccupied().ToList())
                {
                    var trap = slot.OccupiedTrap;
                    if (trap == null || !trap.ActsAsSolidTile) continue;
                    solidTotal++;
                    if (Physics2D.OverlapPoint(slot.transform.position, groundMask) != null) solidOk++;
                }
                Check($"[{id}] 설치 칸 솔리드 ({solidOk}/{solidTotal})", solidOk == solidTotal, $"{solidOk}/{solidTotal}");

                // e) 검증 페이즈
                var gpm2 = GamePhaseManager.Instance;
                int errBefore = _errorTotal;
                gpm2.SetPhase(GamePhase.Verification, true);
                float vEnd = Time.unscaledTime + 8f;
                while (Time.unscaledTime < vEnd && gpm2.currentPhase == GamePhase.Verification) yield return null;
                GamePhase endPhase = gpm2.currentPhase;
                int errDelta = _errorTotal - errBefore;
                Check($"[{id}] 검증 페이즈 진행", errDelta == 0, $"종료 페이즈 {endPhase}, 예외·오류 {errDelta}개");

                // f) 플레이 페이즈
                gpm2.SetPhase(GamePhase.Play, true);
                yield return Wait(3f);
                _player = FindFirstObjectByType<PlayerController>();
                rb = _player != null ? _player.GetComponent<Rigidbody2D>() : null;
                bool playOk = rb != null && rb.simulated;
                Check($"[{id}] 플레이 페이즈", playOk, rb == null ? "플레이어/Rigidbody2D 없음" : $"simulated={rb.simulated}");

                // g) Build 복귀
                gpm2.SetPhase(GamePhase.Build, true);
                yield return null;
            }
        }
    }
}
