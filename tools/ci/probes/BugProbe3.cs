using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  BugProbe3 — BUG-3 재현 프로브 (배치모드 Play 검증)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// BUG-3: 빌드 페이즈에 플레이어 캐릭터가 보이고, 검증 페이즈에 대시 게이지("3/3")가 남는다.
    /// 기대 동작: 빌드·검증 페이즈에는 캐릭터와 게이지가 숨겨지고, 플레이 페이즈에서만 보인다.
    /// <para>수정 전에는 <c>[BUG]</c> 항목이 실패해야 하고, 수정 후에는 모두 통과해야 한다.
    /// <c>[BUG]</c> 로 시작하지 않는 항목은 준비/회귀 항목이며 이쪽이 실패하면 프로브 결함이다.</para>
    /// <para>호출: <c>Unity -batchmode -projectPath &lt;p&gt; -executeMethod ReTrap.EditorTools.BugProbe3.Run
    /// -probeOutput &lt;txt&gt;</c> (tools/ci/run-probe.ps1)</para>
    /// <para>Time.timeScale 은 VerificationSpeedController 소관이므로 건드리지 않고, 대기는 실시간(unscaled) 기준.</para>
    /// </summary>
    public static class BugProbe3
    {
        const string ScenePath     = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const string MapId         = "stage_01";
        const double GlobalTimeout = 150.0;
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
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "BugProbe3.txt");
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
            Debug.Log($"[BUGPROBE3] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
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
                _runnerGo = new GameObject("BugProbe3Runner") { hideFlags = HideFlags.HideAndDontSave };
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

        // ── 가시성 판정 헬퍼 ──────────────────────────────────────────────

        /// <summary>플레이어 SpriteRenderer 중 "켜져 있고 활성 계층인" 것의 수.</summary>
        static int VisibleSprites(PlayerController p) =>
            p.GetComponentsInChildren<SpriteRenderer>(true).Count(sr => sr.enabled && sr.gameObject.activeInHierarchy);

        /// <summary>
        /// 대시 게이지가 보이는가. 플레이어 하위 Canvas 중 enabled && activeInHierarchy 인 것이 있고,
        /// 그 안에 켜진 Graphic(Text/Image)이 있으며 CanvasGroup 알파가 0 이 아니면 보임.
        /// (캔버스 비활성 / 오브젝트 비활성 / 그래픽 비활성 / CanvasGroup 숨김 어느 방식의 수정이든 판정 가능)
        /// </summary>
        static bool GaugeVisible(PlayerController p, out string detail)
        {
            var canvases = p.GetComponentsInChildren<Canvas>(true);
            if (canvases.Length == 0) { detail = "Canvas 없음"; return false; }
            int visible = 0;
            foreach (var c in canvases)
            {
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                bool anyGraphic = c.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Any(g => g.enabled);
                if (!anyGraphic) continue;
                var groups = c.GetComponentsInParent<CanvasGroup>(false);
                if (groups.Any(g => g.alpha <= 0f)) continue;
                visible++;
            }
            detail = $"Canvas {canvases.Length}개 중 보임 {visible}개";
            return visible > 0;
        }

        // ── 시나리오 러너 (Play 중 코루틴) ───────────────────────────────

        sealed class Runner : MonoBehaviour
        {
            string _loadFailed;
            PlayerController _player;

            void Start() => StartCoroutine(Scenario());

            void OnMapLoadFailed(string a, string b) => _loadFailed = $"{a} / {b}";

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
                Check("초기 준비 (매니저·맵·함정 로드)", ready, ready ? "완료" : "매니저/맵/함정 준비 시간 초과");
                if (!ready) yield break;

                var loader = MapLoader.Instance;
                var gpm    = GamePhaseManager.Instance;

                // 2) 맵 로드 + Build 페이즈
                _loadFailed = null;
                if (loader.CurrentMap == null || loader.CurrentMap.mapId != MapId)
                {
                    yield return loader.LoadMapRoutine(MapId);
                    yield return WaitUntil(() => !loader.IsLoading && loader.IsLoaded
                        && loader.CurrentMap != null && loader.CurrentMap.mapId == MapId, WaitTimeout, _ => { });
                    yield return null;
                    TrapMutationManager.Instance.RollNewSeed();
                }
                bool loaded = _loadFailed == null && loader.IsLoaded && loader.CurrentMap != null && loader.CurrentMap.mapId == MapId;
                Check($"씬/맵 로드 ({MapId})", loaded, loaded ? "OK" : (_loadFailed ?? $"현재 맵 '{loader.CurrentMap?.mapId}' 로드={loader.IsLoaded}"));
                if (!loaded) yield break;

                gpm.SetPhase(GamePhase.Build, true);
                yield return Wait(0.5f); // 페이즈 이벤트·스폰 안정화
                Check("Build 페이즈 도달", gpm.currentPhase == GamePhase.Build, $"현재 {gpm.currentPhase}");
                if (gpm.currentPhase != GamePhase.Build) yield break;

                // 3) 플레이어 발견
                _player = null;
                yield return WaitUntil(() => (_player = FindFirstObjectByType<PlayerController>()) != null, 5f, _ => { });
                Check("플레이어 발견", _player != null, _player != null ? _player.name : "PlayerController 없음");
                if (_player == null) yield break;

                // 4) Build 페이즈 — 캐릭터/게이지 숨김 기대
                int buildSprites = VisibleSprites(_player);
                Check("[BUG] Build: 플레이어 SpriteRenderer 전부 숨김", buildSprites == 0, $"보이는 SpriteRenderer {buildSprites}개");
                Check("[BUG] Build: 플레이어 조작 비활성", !_player.enabled, $"PlayerController.enabled={_player.enabled}");
                bool buildGauge = GaugeVisible(_player, out string buildGaugeDetail);
                Check("[BUG] Build: 대시 게이지 숨김", !buildGauge, buildGaugeDetail);

                // 5) Verification 페이즈
                gpm.SetPhase(GamePhase.Verification, true);
                yield return Wait(0.5f);
                bool inVerif = gpm.currentPhase == GamePhase.Verification;
                Check("Verification 페이즈 도달", inVerif, $"현재 {gpm.currentPhase}");
                if (inVerif)
                {
                    _player = FindFirstObjectByType<PlayerController>() ?? _player;
                    int vSprites = VisibleSprites(_player);
                    Check("Verification: 플레이어 SpriteRenderer 숨김", vSprites == 0, $"보이는 SpriteRenderer {vSprites}개");
                    bool vGauge = GaugeVisible(_player, out string vGaugeDetail);
                    Check("[BUG] Verification: 대시 게이지 숨김", !vGauge, vGaugeDetail);
                }

                // 6) Play 페이즈 — 캐릭터/게이지 보임 (회귀 가드)
                gpm.SetPhase(GamePhase.Play, true);
                yield return Wait(0.5f);
                bool inPlay = gpm.currentPhase == GamePhase.Play;
                Check("Play 페이즈 도달", inPlay, $"현재 {gpm.currentPhase}");
                if (inPlay)
                {
                    _player = FindFirstObjectByType<PlayerController>() ?? _player;
                    int pSprites = VisibleSprites(_player);
                    Check("Play: 플레이어 SpriteRenderer 보임", pSprites > 0, $"보이는 SpriteRenderer {pSprites}개");
                    bool pGauge = GaugeVisible(_player, out string pGaugeDetail);
                    Check("Play: 대시 게이지 Canvas 보임", pGauge, pGaugeDetail);
                }

                gpm.SetPhase(GamePhase.Build, true);
                yield return null;
            }
        }
    }
}
