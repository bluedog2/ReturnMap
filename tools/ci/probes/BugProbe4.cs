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
using UnityEngine.UI;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  BugProbe4 — BUG-4 재현 프로브 (배치모드 Play 검증)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// BUG-4: 빌드 HUD 에 연구소 열기 버튼과 핫바 접기/펼치기 버튼이 없다.
    /// 기대 동작: Build 페이즈에 <c>ResearchOpenButton</c>(클릭 시 연구소 패널 열림),
    /// <c>HotbarToggleButton</c>(클릭 시 핫바 숨김/표시, 예산 텍스트는 유지)이 활성 상태로 존재.
    /// 핫바는 스테이지 시작 시 기본 펼침.
    /// <para>수정 전에는 <c>[BUG]</c> 항목이 실패해야 하고, 수정 후에는 모두 통과해야 한다.
    /// <c>[BUG]</c> 로 시작하지 않는 항목은 준비 항목이며 이쪽이 실패하면 프로브 결함이다.</para>
    /// <para>호출: <c>Unity -batchmode -projectPath &lt;p&gt; -executeMethod ReTrap.EditorTools.BugProbe4.Run
    /// -probeOutput &lt;txt&gt;</c> (tools/ci/run-probe.ps1)</para>
    /// <para>Time.timeScale 은 VerificationSpeedController 소관이므로 건드리지 않고, 대기는 실시간(unscaled) 기준.</para>
    /// </summary>
    public static class BugProbe4
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
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "BugProbe4.txt");
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
            Debug.Log($"[BUGPROBE4] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
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
                _runnerGo = new GameObject("BugProbe4Runner") { hideFlags = HideFlags.HideAndDontSave };
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

        // ── 탐색 헬퍼 ────────────────────────────────────────────────────

        /// <summary>이름이 정확히 일치하는 씬 내 Button (비활성 포함). 없으면 null.</summary>
        static Button FindButton(string name) =>
            Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b != null && b.gameObject.scene.IsValid() && b.gameObject.name == name);

        /// <summary>비공개 필드 값을 리플렉션으로 읽는다. 실패 시 null.</summary>
        static T GetField<T>(object target, string field) where T : class
        {
            if (target == null) return null;
            var fi = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return fi != null ? fi.GetValue(target) as T : null;
        }

        static bool Active(GameObject go) => go != null && go.activeInHierarchy;

        // ── 시나리오 러너 (Play 중 코루틴) ───────────────────────────────

        sealed class Runner : MonoBehaviour
        {
            string _loadFailed;

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
                ResearchBoardPanel research = null;
                try
                {
                    yield return RunAll(p => research = p);
                }
                finally
                {
                    MapLoader.OnMapLoadFailed -= OnMapLoadFailed;
                    // 열려 있으면 닫는다
                    var root = GetField<GameObject>(research, "panelRoot");
                    if (research != null && root != null && root.activeSelf) research.TogglePanel();
                }
                _done = true;
                EditorApplication.ExitPlaymode();
            }

            IEnumerator RunAll(Action<ResearchBoardPanel> setResearch)
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
                yield return Wait(0.5f); // 페이즈 이벤트·HUD 안정화
                Check("Build 페이즈 도달", gpm.currentPhase == GamePhase.Build, $"현재 {gpm.currentPhase}");
                if (gpm.currentPhase != GamePhase.Build) yield break;

                // 3) HUD·연구소 패널 발견 (핫바 슬롯은 함정 로드 후 생성됨)
                var hud = FindFirstObjectByType<BuildHudController>();
                Check("BuildHud 발견", hud != null, hud != null ? hud.name : "BuildHudController 없음");
                var research = FindFirstObjectByType<ResearchBoardPanel>(FindObjectsInactive.Include);
                setResearch(research);
                Check("ResearchBoardPanel 발견", research != null, research != null ? research.name : "ResearchBoardPanel 없음");
                if (hud == null || research == null) yield break;

                var hotbar = GetField<Transform>(hud, "buttonContainer")?.gameObject;
                var budget = GetField<Text>(hud, "budgetText")?.gameObject;
                var researchRoot = GetField<GameObject>(research, "panelRoot");
                Check("핫바 컨테이너·예산 텍스트·연구소 루트 참조 확보",
                    hotbar != null && budget != null && researchRoot != null,
                    $"hotbar={hotbar != null} budget={budget != null} researchRoot={researchRoot != null}");

                // 4) 연구소 열기 버튼
                var openBtn = FindButton("ResearchOpenButton");
                Check("[BUG] ResearchOpenButton 존재·활성", Active(openBtn?.gameObject),
                    openBtn == null ? "버튼 없음" : $"activeInHierarchy={openBtn.gameObject.activeInHierarchy}");

                bool researchBefore = Active(researchRoot);
                bool opened = false;
                if (openBtn != null && researchRoot != null)
                {
                    openBtn.onClick.Invoke();
                    yield return null;
                    yield return null;
                    opened = Active(researchRoot);
                }
                Check("[BUG] ResearchOpenButton 클릭 → 연구소 열림", openBtn != null && !researchBefore && opened,
                    openBtn == null ? "버튼 없음" : $"클릭 전 열림={researchBefore} 클릭 후 열림={opened}");

                // 열린 연구소는 닫아 둔다 (토글 검사와 간섭 방지)
                if (researchRoot != null && researchRoot.activeSelf) research.TogglePanel();
                yield return null;

                // 5) 핫바 토글 버튼
                var toggleBtn = FindButton("HotbarToggleButton");
                Check("[BUG] HotbarToggleButton 존재·활성", Active(toggleBtn?.gameObject),
                    toggleBtn == null ? "버튼 없음" : $"activeInHierarchy={toggleBtn.gameObject.activeInHierarchy}");

                bool hotbarDefault = Active(hotbar);
                Check("[BUG] 기본 핫바 펼침", toggleBtn != null && hotbarDefault,
                    $"토글 버튼={(toggleBtn != null ? "있음" : "없음")} 핫바 활성={hotbarDefault}");

                bool hidden = false, budgetKept = false;
                if (toggleBtn != null && hotbar != null)
                {
                    toggleBtn.onClick.Invoke();
                    yield return null;
                    yield return null;
                    hidden = !Active(hotbar);
                    budgetKept = Active(budget);
                }
                Check("[BUG] 토글 → 핫바 숨김·예산 유지", toggleBtn != null && hidden && budgetKept,
                    toggleBtn == null ? "버튼 없음" : $"핫바 숨김={hidden} 예산 활성={budgetKept}");

                bool shown = false;
                if (toggleBtn != null && hotbar != null)
                {
                    toggleBtn.onClick.Invoke();
                    yield return null;
                    yield return null;
                    shown = Active(hotbar);
                }
                Check("[BUG] 재토글 → 핫바 표시", toggleBtn != null && shown,
                    toggleBtn == null ? "버튼 없음" : $"핫바 활성={shown}");
            }
        }
    }
}
