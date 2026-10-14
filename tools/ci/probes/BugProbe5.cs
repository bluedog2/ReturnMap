using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  BugProbe5 — BUG-5 재현 프로브 (배치모드 Play 검증)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// BUG-5: 설정 UI 에서 해상도 드롭다운 목록을 펼치면 항목 글자가 보이지 않는다.
    /// 기대 동작: 목록의 각 항목 라벨이 비어있지 않고, 잘리지 않으며, 글자가 실제로 렌더되고,
    /// 배경과 충분한 명암 대비를 가진다.
    /// <para>수정 전에는 <c>[BUG]</c> 항목이 실패해야 하고, 수정 후에는 모두 통과해야 한다.
    /// <c>[BUG]</c> 로 시작하지 않는 항목은 준비 항목이며 이쪽이 실패하면 프로브 결함이다.</para>
    /// <para>패널은 Build 페이즈에서 <c>SettingsPanel.TogglePanel()</c> (Esc 와 동일 경로)로 열고,
    /// 드롭다운은 공개 프로퍼티 <c>ResolutionDropdown</c> 으로 접근해 <c>Show()</c> 를 호출한다.</para>
    /// <para>호출: <c>Unity -batchmode -projectPath &lt;p&gt; -executeMethod ReTrap.EditorTools.BugProbe5.Run
    /// -probeOutput &lt;txt&gt;</c> (tools/ci/run-probe.ps1)</para>
    /// <para>Time.timeScale 은 VerificationSpeedController 소관이므로 건드리지 않고, 대기는 실시간(unscaled) 기준.</para>
    /// </summary>
    public static class BugProbe5
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
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "BugProbe5.txt");
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
            Debug.Log($"[BUGPROBE5] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
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
                _runnerGo = new GameObject("BugProbe5Runner") { hideFlags = HideFlags.HideAndDontSave };
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

        // ── 측정 헬퍼 ────────────────────────────────────────────────────

        /// <summary>단순 밝기(감마 공간 가중합) — 대비 비교용.</summary>
        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static string Hex(Color c) => $"#{ColorUtility.ToHtmlStringRGBA(c)}";

        /// <summary>항목 Toggle 안의 라벨 Text (비활성 포함). 없으면 null.</summary>
        static Text ItemLabel(Toggle item) => item.GetComponentInChildren<Text>(true);

        /// <summary>항목 배경 Image — 이름이 "Item Background" 인 것을 우선, 없으면 Toggle 의 targetGraphic.</summary>
        static Image ItemBackground(Toggle item)
        {
            var bg = item.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.gameObject.name == "Item Background");
            return bg != null ? bg : item.targetGraphic as Image;
        }

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
                SettingsPanel panel = null;
                Dropdown dd = null;
                try
                {
                    yield return RunAll((p, d) => { panel = p; dd = d; });
                }
                finally
                {
                    MapLoader.OnMapLoadFailed -= OnMapLoadFailed;
                    // 드롭다운·패널을 닫아 둔다
                    if (dd != null) dd.Hide();
                    if (panel != null && panel.PanelRoot != null && panel.PanelRoot.activeSelf) panel.TogglePanel();
                }
                _done = true;
                EditorApplication.ExitPlaymode();
            }

            IEnumerator RunAll(Action<SettingsPanel, Dropdown> setRefs)
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

                // 3) 설정 패널 발견·열기 (Esc 와 같은 TogglePanel 경로)
                var panel = FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
                Check("SettingsPanel 발견", panel != null, panel != null ? panel.name : "SettingsPanel 없음");
                if (panel == null) yield break;
                var dd = panel.ResolutionDropdown;
                setRefs(panel, dd);

                if (panel.PanelRoot != null && !panel.PanelRoot.activeSelf) panel.TogglePanel();
                yield return null;
                yield return null;
                bool opened = panel.PanelRoot != null && panel.PanelRoot.activeInHierarchy;
                Check("설정 패널 열림", opened, $"panelRoot 활성={opened}");
                if (!opened) yield break;

                // 4) 해상도 드롭다운 확인
                int optCount = dd != null ? dd.options.Count : 0;
                Check("해상도 드롭다운 발견 (옵션 1개 이상)", dd != null && optCount >= 1,
                    dd == null ? "ResolutionDropdown 없음" : $"옵션 {optCount}개: " + string.Join(", ", dd.options.Select(o => o.text)));
                if (dd == null || optCount < 1) yield break;

                // 5) 목록 펼치기
                dd.Show();
                yield return null;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();

                var listTr = dd.transform.Find("Dropdown List");
                Check("Dropdown List 생성", listTr != null, listTr != null ? "OK" : "Dropdown List 없음");
                if (listTr == null) yield break;
                LayoutRebuilder.ForceRebuildLayoutImmediate(listTr as RectTransform);
                Canvas.ForceUpdateCanvases();

                // 복제된 항목만 (템플릿 원본은 비활성)
                var items = listTr.GetComponentsInChildren<Toggle>(false).Where(t => t.gameObject.activeInHierarchy).ToList();
                Check("목록 항목 수 == 옵션 수", items.Count == optCount, $"항목 {items.Count} / 옵션 {optCount}");
                if (items.Count == 0) yield break;

                var labels = new List<(Toggle item, Text text, int idx)>();
                for (int i = 0; i < items.Count; i++) labels.Add((items[i], ItemLabel(items[i]), i));

                // 6) [BUG] 라벨 텍스트
                {
                    var bad = new List<string>();
                    var det = new List<string>();
                    foreach (var (item, text, idx) in labels)
                    {
                        string expect = idx < optCount ? dd.options[idx].text : "?";
                        string actual = text != null ? text.text : null;
                        det.Add($"[{idx}]'{actual}'");
                        if (text == null || string.IsNullOrEmpty(actual) || actual != expect)
                            bad.Add($"[{idx}] 기대='{expect}' 실제='{actual}'");
                    }
                    Check("[BUG] 해상도 항목 라벨 텍스트가 비어있지 않음 (옵션 텍스트와 일치)", bad.Count == 0,
                        bad.Count == 0 ? string.Join(" ", det) : "불일치: " + string.Join("; ", bad));
                }

                // 7) [BUG] 라벨이 잘리지 않음
                {
                    var bad = new List<string>();
                    var det = new List<string>();
                    foreach (var (item, text, idx) in labels)
                    {
                        if (text == null) { bad.Add($"[{idx}] 라벨 없음"); continue; }
                        float h = text.rectTransform.rect.height;
                        float pref = text.preferredHeight;
                        bool truncate = text.verticalOverflow == VerticalWrapMode.Truncate;
                        det.Add($"[{idx}] rect.h={h:F1} preferred.h={pref:F1} fontSize={text.fontSize} vOverflow={text.verticalOverflow}");
                        if (truncate && h + 0.01f < pref) bad.Add($"[{idx}] rect.h={h:F1} < preferred.h={pref:F1} (fontSize={text.fontSize}, Truncate)");
                    }
                    Check("[BUG] 해상도 항목 라벨이 잘리지 않음", bad.Count == 0,
                        bad.Count == 0 ? string.Join(" | ", det) : string.Join("; ", bad));
                }

                // 8) [BUG] 글자 렌더됨
                {
                    Canvas.ForceUpdateCanvases();
                    var bad = new List<string>();
                    var det = new List<string>();
                    foreach (var (item, text, idx) in labels)
                    {
                        if (text == null) { bad.Add($"[{idx}] 라벨 없음"); continue; }
                        int visible = text.cachedTextGenerator.characterCountVisible;
                        int verts = text.cachedTextGenerator.vertexCount;
                        det.Add($"[{idx}] visible={visible} verts={verts} len={text.text?.Length ?? 0}");
                        if (visible <= 0 && verts <= 0) bad.Add($"[{idx}] visible={visible} verts={verts} (len={text.text?.Length ?? 0})");
                    }
                    Check("[BUG] 해상도 항목 글자 렌더됨", bad.Count == 0,
                        bad.Count == 0 ? string.Join(" | ", det) : "렌더된 글자 없음: " + string.Join("; ", bad));
                }

                // 9) [BUG] 가시성 (알파·폰트·배경 대비)
                {
                    var bad = new List<string>();
                    var det = new List<string>();
                    foreach (var (item, text, idx) in labels)
                    {
                        if (text == null) { bad.Add($"[{idx}] 라벨 없음"); continue; }
                        var bg = ItemBackground(item);
                        Color fg = text.color;
                        float contrast = bg != null ? Mathf.Abs(Luma(fg) - Luma(bg.color)) : -1f;
                        det.Add($"[{idx}] color={Hex(fg)} font={(text.font != null ? text.font.name : "null")} bg={(bg != null ? Hex(bg.color) : "없음")} 대비={contrast:F2}");
                        if (fg.a <= 0.5f) bad.Add($"[{idx}] 알파 {fg.a:F2} <= 0.5");
                        if (text.font == null) bad.Add($"[{idx}] font null");
                        if (bg == null) bad.Add($"[{idx}] 배경 Image 없음");
                        else if (contrast < 0.3f) bad.Add($"[{idx}] 대비 {contrast:F2} < 0.30 (글자 {Hex(fg)} / 배경 {Hex(bg.color)})");
                    }
                    Check("[BUG] 항목 라벨 가시성 (알파>0.5·폰트·배경 대비>=0.3)", bad.Count == 0,
                        bad.Count == 0 ? string.Join(" | ", det) : string.Join("; ", bad) + " || " + string.Join(" | ", det));
                }

                // 10) 정리 — 드롭다운 닫기 (패널은 finally 에서 닫음)
                dd.Hide();
                yield return null;
                yield return null;
            }
        }
    }
}
