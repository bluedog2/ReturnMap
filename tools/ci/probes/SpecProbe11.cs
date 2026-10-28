using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  SpecProbe11 — SPEC-11(검증 페이즈 규칙·밸런스) 배치모드 Play 검증. 임시, 커밋하지 않음
    //
    //  대상: 수용 기준 #1~#7, #8(프로브 부분), #11(실제 스폰 수), #12, #13, #14, #16, #18.
    //  제외: #9 #10 #15 #17(EditMode·수동), #19(CI·스모크).
    //
    //  환경: SampleScene 에는 결과 패널·researchNodes 배선이 없다 → Play 진입 전에 MetaUiSetup 의 공개 빌더
    //        (BuildVerificationResultPanel / BuildSettingsPanel)와 배선 로직(WireDirectorResearchNodes)을
    //        메모리상에서만 거친다. 씬은 저장하지 않는다.
    //
    //  자연 이벤트 유도 방법(결과는 모두 실제 검증 페이즈 Run 이 낸 것 — 판정·리포트를 강제 주입하지 않는다):
    //   · 방어 실패: stage_02 에 함정을 하나도 설치하지 않고 검증 → 라이프 1(테이블 breach 1)이라 첫 돌파 즉시 실패.
    //   · 함정 킬(≥1): stage_01 에 tools/playtest/fixtures 의 설치 배치(가시·화살)로 시드 1001~ 순차 시도, 킬 ≥1 인 첫 시드 사용.
    //   · 방어 성공: stage_01 + 방어선 보강 Lv1(#16)을 시드 1001~1008 로 반복해 돌파 수가 라이프 미만인 판을 찾는다.
    //                그래도 성공이 없으면 stage_04(라이프 3·5마리) + 방어선 보강 Lv2(라이프 5) + 슬롯 전부 설치로 시도.
    //  연구 레벨은 공개 경로(CurrencyService.Add → ResearchBoardState.TryUpgrade)로 올린다. 비용만큼 먼저 적립한 뒤
    //  바로 쓰므로 지구본 잔액은 변하지 않고, research_board.json 은 시작 시 백업·종료 시 복원한다.
    // ═══════════════════════════════════════════════════════════════
    public static class SpecProbe11
    {
        const string ScenePath = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const float  WallLimit = 540f;   // 전체 실제 시간 상한(초)
        const float  RunScale  = 5f;     // 검증 진행 구간 배속(결과 대기에 들어가면 풀어 둔다)
        const float  To720     = 720f / 1080f;

        // tools/playtest/fixtures/stage_01.json 과 동일한 설치 배치
        static readonly (string trap, int x, int y)[] Stage01Fixture =
        {
            ("TrapDefinition_Spike", 10, 3), ("TrapDefinition_Spike", 5, 0),
            ("TrapDefinition_ArrowShooter", 18, 0), ("TrapDefinition_ArrowShooter", 0, 5),
        };

        enum Place { None, Fixture01, Greedy }

        sealed class Res
        {
            public string tag, map, table;
            public int seed;
            public bool ok;                       // 결과 대기까지 도달했는가
            public bool breached;
            public int reached, rLife, rCount, spawned, stopped, deaths, timedOut, repLife;
            public float rTime;
            public List<VerificationReport.Row> kills, cells;
            public GamePhase next = GamePhase.Verification; public float nextAfter = -1f; public bool continued;
            public bool simulated;
        }

        // ── 상태 ──
        static double _wallStart;
        static bool _done, _aborted;
        static readonly Stack<IEnumerator> _st = new Stack<IEnumerator>();
        static bool _hold; static float _holdScale = 1f;
        static string _out, _dir;
        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors = new List<string>();
        static int _fail;

        static GamePhaseManager _gpm;
        static VerificationDirector _director;
        static VerificationPhaseController _phaseCtl;
        static VerificationResultPanel _panel;
        static SettingsPanel _settings;
        static BuildPhaseController _build;
        static Canvas _canvas;
        static int _mapLoaded;

        static byte[] _researchBackup; static bool _researchExisted;
        static bool _addedKeyboard;
        static readonly List<Res> _all = new List<Res>();
        static bool _shotDone;

        // ═══════════════ 진입 ═══════════════
        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "probe11.txt");
            _dir = Path.GetDirectoryName(_out);
            Application.logMessageReceived += OnLog;
            _wallStart = EditorApplication.timeSinceStartup;
            try
            {
                BackupResearch();
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var canvasGo = GameObject.Find("UICanvas");
                if (canvasGo == null) { Check("[준비] UICanvas 발견", false, "씬에 UICanvas 없음"); Finish(); return; }

                // 메모리상으로만 UI 를 만든다(씬 저장 금지). 결과 패널 → 연구 노드 배선 → 설정 패널(항상 마지막 sibling) 순.
                MetaUiSetup.BuildVerificationResultPanel(canvasGo.transform);
                var tMeta = typeof(MetaUiSetup);
                var load = tMeta.GetMethod("LoadResearchNodes", BindingFlags.Static | BindingFlags.NonPublic);
                var wire = tMeta.GetMethod("WireDirectorResearchNodes", BindingFlags.Static | BindingFlags.NonPublic);
                if (load == null || wire == null) Check("[준비] MetaUiSetup 연구 노드 배선 로직 발견", false, "LoadResearchNodes/WireDirectorResearchNodes 리플렉션 실패");
                else wire.Invoke(null, new object[] { load.Invoke(null, null) });
                MetaUiSetup.BuildSettingsPanel(canvasGo.transform);

                EditorApplication.update += Tick;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Check("[준비] Run 예외", false, e.GetType().Name + ": " + e.Message);
                Finish();
            }
        }

        // ═══════════════ 공통 유틸 ═══════════════
        static void OnLog(string m, string s, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception) return;
            if (s != null && s.Contains("UnityEditor.Search.")) return; // SearchInit.IndexationOnStartup (무해)
            if (_errors.Count < 30) _errors.Add($"[{t}] {m.Split('\n')[0]}");
        }

        static string Arg(string n) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

        static void Check(string name, bool ok, string detail)
        {
            if (!ok) _fail++;
            _results.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
            Debug.Log($"[PROBE11] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static T Fld<T>(object o, string n) where T : class
        {
            if (o == null) return null;
            var f = o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f?.GetValue(o) as T;
        }

        static IEnumerator WaitReal(float sec)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < sec) yield return null;
        }

        static IEnumerator WaitFrames(int n)
        {
            int f0 = Time.frameCount;
            while (Time.frameCount - f0 < n) yield return null;
        }

        /// <summary>조건이 참이 될 때까지(실제 시간 상한) 기다린다. 결과는 _waitOk 에.</summary>
        static bool _waitOk;
        static IEnumerator WaitUntil(Func<bool> cond, float timeoutReal)
        {
            float t0 = Time.realtimeSinceStartup;
            _waitOk = false;
            while (Time.realtimeSinceStartup - t0 < timeoutReal)
            {
                if (cond()) { _waitOk = true; yield break; }
                yield return null;
            }
        }

        static void Abort(string why)
        {
            Check("[중단] 프로브 진행 불가", false, why);
            _aborted = true;
        }

        // ═══════════════ 상태머신 구동 ═══════════════
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > WallLimit) { Check("timeout", false, $"{WallLimit:F0}초 초과"); Finish(); return; }
            if (_done) { if (!EditorApplication.isPlaying) Finish(); return; }
            if (!EditorApplication.isPlaying) return;

            if (_st.Count == 0)
            {
                if (GamePhaseManager.Instance == null) return;
                _st.Push(Main());
            }
            // 검증 진행 중에만 배속을 다시 건다. 결과 대기에 들어가면(IsAwaitingResult) 재적용을 멈춰
            // 제품(VerificationSpeedController)이 복원한 값을 그대로 관찰한다.
            bool awaiting = _phaseCtl != null && _phaseCtl.IsAwaitingResult;
            if (_hold && !awaiting && Mathf.Abs(Time.timeScale - _holdScale) > 0.01f) Time.timeScale = _holdScale;

            var top = _st.Peek();
            bool more;
            try { more = top.MoveNext(); }
            catch (Exception e) { Check("[중단] 프로브 예외", false, e.GetType().Name + ": " + e.Message); _st.Clear(); _aborted = true; more = false; }

            if (more) { if (top.Current is IEnumerator sub) _st.Push(sub); }
            else if (_st.Count > 0 && _st.Peek() == top) _st.Pop();

            if (_aborted || _st.Count == 0)
            {
                _hold = false; Time.timeScale = 1f;
                _done = true;
                EditorApplication.ExitPlaymode();
            }
        }

        // ═══════════════ 연구 레벨(공개 경로) ═══════════════
        static string ResearchPath => Path.Combine(Application.persistentDataPath, "research_board.json");

        static void BackupResearch()
        {
            _researchExisted = File.Exists(ResearchPath);
            _researchBackup = _researchExisted ? File.ReadAllBytes(ResearchPath) : null;
        }

        static void RestoreResearch()
        {
            try
            {
                if (_researchExisted) File.WriteAllBytes(ResearchPath, _researchBackup);
                else if (File.Exists(ResearchPath)) File.Delete(ResearchPath);
                var f = typeof(ResearchBoardState).GetField("_levels", BindingFlags.Static | BindingFlags.NonPublic);
                f?.SetValue(null, null); // 다음 접근 때 디스크에서 다시 읽는다
            }
            catch (Exception e) { Debug.LogWarning("[PROBE11] 연구 상태 복원 실패: " + e.Message); }
        }

        static ResearchNodeDefinition FindNode(ResearchEffect eff)
        {
            return AssetDatabase.FindAssets("t:ResearchNodeDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<ResearchNodeDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(n => n != null && n.Effect == eff).OrderBy(n => n.name).FirstOrDefault();
        }

        /// <summary>연구 레벨을 전부 0 으로 만든 뒤 지정한 노드만 올린다. 지구본은 비용만큼 적립 후 즉시 소비(잔액 불변).</summary>
        static bool SetResearch(ResearchNodeDefinition node, int level)
        {
            ResearchBoardState.ResetAll();
            if (node == null || level <= 0) return true;
            for (int l = 1; l <= level; l++)
            {
                CurrencyService.Add(node.GetCost(l));
                if (!ResearchBoardState.TryUpgrade(node)) return false;
            }
            return ResearchBoardState.GetLevel(node) == level;
        }

        // ═══════════════ 입력 주입 ═══════════════
        static Keyboard Kb()
        {
            var kb = Keyboard.current;
            if (kb == null) { kb = InputSystem.AddDevice<Keyboard>(); _addedKeyboard = true; }
            return kb;
        }

        // ═══════════════ 시나리오 실행 ═══════════════
        static IEnumerator RunScenario(Res r, Place place, int seed, int limit = int.MaxValue)
        {
            r.seed = seed;
            _hold = false; Time.timeScale = 1f;
            _gpm.SetPhase(GamePhase.Build, true);
            yield return WaitFrames(2);

            int before = _mapLoaded;
            MapLoader.Instance.LoadMap(r.map);
            yield return WaitUntil(() => _mapLoaded > before && !MapLoader.Instance.IsLoading && MapLoader.Instance.IsLoaded
                && MapLoader.Instance.CurrentMap != null && MapLoader.Instance.CurrentMap.mapId == r.map, 60f);
            if (!_waitOk) { Check($"[{r.tag}] 맵 로드 {r.map}", false, "로드 시간 초과"); yield break; }

            TrapMutationManager.Instance.RollNewSeed();
            _gpm.SetPhase(GamePhase.Build, true);
            yield return WaitFrames(2);
            yield return WaitUntil(() => _build != null && _build.TrapsReady, 30f);
            yield return WaitFrames(2);

            // 설치
            var tryPlace = typeof(BuildPhaseController).GetMethod("TryPlace", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tryPlace == null) { Check($"[{r.tag}] TryPlace 리플렉션", false, "메서드 없음"); yield break; }
            int placed = 0;
            if (place == Place.Fixture01)
            {
                foreach (var p in Stage01Fixture)
                {
                    int idx = -1;
                    for (int k = 0; k < _build.TrapDefinitions.Count; k++) if (_build.TrapDefinitions[k].name == p.trap) { idx = k; break; }
                    if (idx < 0 || !TrapSlotRegistry.TryGet(p.x, p.y, out var slot)) { Check($"[{r.tag}] 설치 준비 {p.trap}@({p.x},{p.y})", false, "정의 또는 슬롯 없음"); yield break; }
                    _build.SelectTrap(idx); tryPlace.Invoke(_build, new object[] { p.x, p.y });
                    if (slot.IsEmpty) { Check($"[{r.tag}] 설치 {p.trap}@({p.x},{p.y})", false, $"설치 실패(잔여 예산 {_build.RemainingBudget})"); yield break; }
                    placed++;
                }
            }
            else if (place == Place.Greedy)
            {
                foreach (var slot in TrapSlotRegistry.All.ToList())
                    for (int k = 0; k < _build.TrapDefinitions.Count && slot.IsEmpty && placed < limit; k++)
                    {
                        _build.SelectTrap(k); tryPlace.Invoke(_build, new object[] { slot.GridX, slot.GridY });
                        if (!slot.IsEmpty) placed++;
                    }
            }
            Physics2D.SyncTransforms();

            var seedM = typeof(VerificationDirector).GetMethod("SetRosterSeed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (seedM == null) { Check($"[{r.tag}] SetRosterSeed 리플렉션", false, "메서드 없음"); yield break; }
            seedM.Invoke(_director, new object[] { seed });
            r.table = (Fld<UnityEngine.Object>(_director, "spawnTable"))?.name ?? "(없음)";

            // 검증 페이즈 진입 → 결과 대기까지 배속으로 진행
            _gpm.SetPhase(GamePhase.Verification, true);
            _hold = true; _holdScale = RunScale;
            yield return WaitUntil(() => _phaseCtl.IsAwaitingResult, 150f);
            _hold = false;
            if (!_waitOk)
            {
                Check($"[{r.tag}] 검증 종료 후 결과 대기 진입", false, $"150초 내 IsAwaitingResult 안 됨 (phase {_gpm.currentPhase})");
                _gpm.SetPhase(GamePhase.Build, true);
                yield break;
            }
            yield return null; // OnResultPresented 가 같은 프레임에 패널을 열도록 한 프레임 둔다

            var rep = _director.LastReport;
            r.ok = true;
            r.breached = _director.LastRunBreached; r.reached = _director.LastReachedCount;
            r.rLife = _director.LastRules.Life; r.rCount = _director.LastRules.AgentCount; r.rTime = _director.LastRules.TimeLimit;
            r.spawned = rep.Spawned; r.stopped = rep.StoppedAgents; r.deaths = rep.DeathEvents; r.timedOut = rep.TimedOutAttempts; r.repLife = rep.Life;
            r.kills = rep.TopKills(5); r.cells = rep.TopCells(5);
            _all.Add(r);
            Debug.Log($"[PROBE11] 시나리오 {r.tag}: map={r.map} seed={seed} 설치 {placed} breached={r.breached} reached={r.reached} life={r.rLife} count={r.rCount} time={r.rTime} spawned={r.spawned} deaths={r.deaths} timedOut={r.timedOut} table={r.table}");
        }

        /// <summary>「계속」 클릭 → 다음 페이즈 관찰(#3·#4 자료).</summary>
        static IEnumerator ContinueFlow(Res r)
        {
            yield return WaitReal(0.5f); // 입력 가드(0.3초 unscaled) 경과
            var btn = Fld<Button>(_panel, "continueButton");
            if (btn == null) { r.continued = false; yield break; }
            float t0 = Time.realtimeSinceStartup;
            btn.onClick.Invoke();
            r.continued = true;
            yield return WaitUntil(() => _gpm.currentPhase != GamePhase.Verification, 1.0f);
            r.next = _gpm.currentPhase; r.nextAfter = Time.realtimeSinceStartup - t0;
            if (!r.breached && r.next == GamePhase.Play)
            {
                var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                Rigidbody2D rb = null; if (pc != null) pc.TryGetComponent(out rb);
                yield return WaitUntil(() => rb != null && rb.simulated, 4f);
                r.simulated = rb != null && rb.simulated;
            }
        }

        // ═══════════════ 결과 패널 측정 ═══════════════
        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>글자가 실제로 그려진 영역(버텍스 경계). 버텍스가 없으면 rect 로 대체.</summary>
        static Rect TextInk(Text t)
        {
            // 탭 전환 직후 cachedTextGenerator 가 낡았을 수 있어 현재 텍스트·rect 로 새로 생성해 쓴다.
            var gen = new TextGenerator();
            gen.Populate(t.text, t.GetGenerationSettings(t.rectTransform.rect.size));
            var verts = gen.verts;
            int n = gen.vertexCount;
            if (verts == null || n <= 0) return ScreenRect(t.rectTransform);
            float u = 1f / Mathf.Max(0.0001f, t.pixelsPerUnit);
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            for (int i = 0; i < n; i++)
            {
                Vector3 w = t.transform.TransformPoint(verts[i].position * u);
                Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, w);
                mn = Vector2.Min(mn, s); mx = Vector2.Max(mx, s);
            }
            return Rect.MinMaxRect(mn.x, mn.y, mx.x, mx.y);
        }

        static float EffectiveFont(Text t)
        {
            float size = t.resizeTextForBestFit ? t.cachedTextGenerator.fontSizeUsedForBestFit : t.fontSize;
            float k = _canvas.transform.lossyScale.x > 0f ? t.transform.lossyScale.x / _canvas.transform.lossyScale.x : 1f;
            return size * k; // 기준(1080) 단위
        }

        static bool Overlap(Rect a, Rect b) => a.xMin < b.xMax - 0.5f && b.xMin < a.xMax - 0.5f && a.yMin < b.yMax - 0.5f && b.yMin < a.yMax - 0.5f;

        /// <summary>#2 — 현재 보이는 화면(view)의 글자 크기·화면 밖·겹침. (1080 기준 → 720p 환산 ×0.667)</summary>
        static IEnumerator MeasureLayout(string tag)
        {
            // 탭 전환·표시 직후 레이아웃·메시가 갱신되도록 갱신 + 프레임 대기 후 측정
            Canvas.ForceUpdateCanvases();
            yield return WaitFrames(2);
            Canvas.ForceUpdateCanvases();
            var rootGo = Fld<GameObject>(_panel, "panelRoot");
            var title = Fld<Text>(_panel, "titleText");
            var sw = (float)Screen.width; var sh = (float)Screen.height;
            var screen = new Rect(0, 0, sw, sh);

            var clicks = new List<KeyValuePair<string, Rect>>();
            foreach (var n in new[] { "resultTabButton", "reportTabButton", "detailButton", "continueButton" })
            {
                var b = Fld<Button>(_panel, n);
                if (b != null && b.gameObject.activeInHierarchy) clicks.Add(new KeyValuePair<string, Rect>(n, ScreenRect((RectTransform)b.transform)));
            }
            var texts = new List<KeyValuePair<string, Rect>>();
            float minBody = float.MaxValue; string minName = ""; int nTxt = 0;
            foreach (var t in rootGo.GetComponentsInChildren<Text>(false))
            {
                if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                nTxt++;
                if (t != title)
                {
                    float f720 = EffectiveFont(t) * To720;
                    if (f720 < minBody) { minBody = f720; minName = t.name; }
                }
                if (t.GetComponentInParent<Selectable>() == null) texts.Add(new KeyValuePair<string, Rect>("글자'" + t.name + "'", TextInk(t)));
            }
            var all = new List<KeyValuePair<string, Rect>>(clicks); all.AddRange(texts);

            var outside = new List<string>();
            foreach (var kv in all)
                if (kv.Value.xMin < screen.xMin - 1f || kv.Value.xMax > screen.xMax + 1f || kv.Value.yMin < screen.yMin - 1f || kv.Value.yMax > screen.yMax + 1f) outside.Add(kv.Key);
            Rect pr = ScreenRect((RectTransform)rootGo.transform);
            bool panelIn = pr.xMin >= -1f && pr.yMin >= -1f && pr.xMax <= sw + 1f && pr.yMax <= sh + 1f;

            var overlaps = new List<string>();
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    if (Overlap(all[i].Value, all[j].Value)) overlaps.Add(all[i].Key + "×" + all[j].Key);

            Check($"#2 [{tag}] 본문 글자 720p 환산 ≥24px", nTxt > 0 && minBody >= 24f - 0.01f, $"활성 글자 {nTxt}개, 제목 외 최소 {(nTxt > 0 ? minBody : 0):F2}px@720p ('{minName}'), 패널 activeInHierarchy={rootGo.activeInHierarchy}");
            Check($"#2 [{tag}] 화면 밖 0", outside.Count == 0 && panelIn, $"Screen {Screen.width}x{Screen.height}, 패널 {pr.width:F0}x{pr.height:F0} 화면 안={panelIn}, 밖으로 나간 요소 [{string.Join(", ", outside)}]");
            Check($"#2 [{tag}] 덮인 UI 0", overlaps.Count == 0, $"검사 {all.Count}개(버튼 {clicks.Count}+글자 {texts.Count}), 겹침 {overlaps.Count} [{string.Join(", ", overlaps)}]");
        }

        /// <summary>#2 픽셀 — 패널 on/off 두 번 렌더 차이로 패널 영역과 글자(흰색·금색) 픽셀을 본다. 끝나면 모두 원복.</summary>
        static void MeasurePixels(string tag)
        {
            var cam = Camera.main;
            var rootGo = Fld<GameObject>(_panel, "panelRoot");
            if (cam == null) { Check($"#2 [{tag}] 픽셀", false, "Camera.main 없음"); return; }
            const int W = 1920, H = 1080;
            var prevMode = _canvas.renderMode; var prevCam = _canvas.worldCamera; float prevDist = _canvas.planeDistance;
            var prevTarget = cam.targetTexture; int prevMask = cam.cullingMask;
            var rt = new RenderTexture(W, H, 24);
            int n = 0, white = 0, gold = 0, w = 0, h = 0; string note = ""; Rect expect = default;
            try
            {
                // 오버레이/ScreenSpaceCamera 는 이 환경에서 cam.Render 에 안 잡혀(SpecProbe16 도 0px) 세계 공간 캔버스로 바꿔 렌더한다.
                // (SpecProbe9b 의 함정 게이지 월드 캔버스가 cam.Render 로 잡히는 것과 같은 경로) 캔버스 높이가 화면 높이와 같도록 배치.
                var crt = (RectTransform)_canvas.transform;
                Vector2 prevSize = crt.sizeDelta; Vector3 prevPos = crt.position, prevScl = crt.localScale; Quaternion prevRot = crt.rotation;
                Vector2 cSize = crt.rect.size;
                float dist = 10f;
                float viewH = cam.orthographic ? cam.orthographicSize * 2f : 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                _canvas.renderMode = RenderMode.WorldSpace; _canvas.worldCamera = cam;
                crt.sizeDelta = cSize;
                crt.rotation = cam.transform.rotation;
                crt.position = cam.transform.position + cam.transform.forward * dist;
                crt.localScale = Vector3.one * (viewH / cSize.y);
                _canvas.overrideSorting = true; _canvas.sortingLayerName = "Map"; _canvas.sortingOrder = 1000;
                _restoreCanvas = () => { crt.sizeDelta = prevSize; crt.position = prevPos; crt.localScale = prevScl; crt.rotation = prevRot; _canvas.overrideSorting = false; };
                cam.cullingMask |= 1 << _canvas.gameObject.layer;
                Texture2D on = null, off = null;
                for (int attempt = 0; attempt < 2 && n == 0; attempt++)
                {
                    bool req = attempt == 1;
                    on = RenderOnce(cam, rt, req);
                    rootGo.SetActive(false);
                    off = RenderOnce(cam, rt, req);
                    rootGo.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    {
                        // 패널 rect 를 RT(1920×1080) 좌표로 투영한 기대 영역
                        var cs = new Vector3[4]; ((RectTransform)rootGo.transform).GetWorldCorners(cs);
                        Vector3 v0 = cam.WorldToViewportPoint(cs[0]), v2 = cam.WorldToViewportPoint(cs[2]);
                        expect = Rect.MinMaxRect(Mathf.Min(v0.x, v2.x) * W, Mathf.Min(v0.y, v2.y) * H, Mathf.Max(v0.x, v2.x) * W, Mathf.Max(v0.y, v2.y) * H);
                    }
                    var pa = on.GetPixels(); var pb = off.GetPixels();
                    int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue; n = 0; white = gold = 0;
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            Color A = pa[y * W + x], B = pb[y * W + x];
                            if (Mathf.Abs(A.r - B.r) + Mathf.Abs(A.g - B.g) + Mathf.Abs(A.b - B.b) < 0.08f) continue;
                            n++; minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                            if (A.r > 0.9f && A.g > 0.9f && A.b > 0.9f) white++;
                            else if (A.r > 0.85f && A.g > 0.65f && A.g < 0.95f && A.b < 0.35f) gold++;
                        }
                    w = n > 0 ? maxX - minX + 1 : 0; h = n > 0 ? maxY - minY + 1 : 0;
                    note += $"[{(req ? "URP RenderRequest" : "cam.Render")}: 차이 {n}px] ";
                    if (n > 0 || attempt == 1) File.WriteAllBytes(Path.Combine(_dir, $"probe11_{tag}.png"), on.EncodeToPNG());
                    UnityEngine.Object.Destroy(on); UnityEngine.Object.Destroy(off);
                }
                bool fits = n > 0 && w <= expect.width + 4f && h <= expect.height + 4f;           // 패널이 자기 rect 를 넘치지 않음
                bool cover = n > 0 && w >= expect.width * 0.9f && h >= expect.height * 0.9f;       // 패널 배경이 실제로 그려짐
                Check($"#2 [{tag}] 픽셀: 패널이 그려지고 rect 안에 있음", fits && cover, $"차이 영역 {w}x{h}px, rect {expect.width:F0}x{expect.height:F0}px(±4 / ≥90%) {note}");
                Check($"#2 [{tag}] 픽셀: 흰 글자·금색 제목이 보임", white >= 400 && gold >= 300, $"흰 픽셀 {white}(≥400) 금 픽셀 {gold}(≥300) — 보조 가정 임계");
            }
            catch (Exception e) { Check($"#2 [{tag}] 픽셀", false, "예외: " + e.Message); }
            finally
            {
                cam.targetTexture = prevTarget; cam.cullingMask = prevMask;
                if (!rootGo.activeSelf) rootGo.SetActive(true);
                _canvas.renderMode = prevMode; _canvas.worldCamera = prevCam; _canvas.planeDistance = prevDist;
                _restoreCanvas?.Invoke(); _restoreCanvas = null;
                Canvas.ForceUpdateCanvases();
                rt.Release();
            }
        }

        static Texture2D RenderOnce(Camera cam, RenderTexture rt, bool useRequest)
        {
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            if (useRequest)
            {
                var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
            }
            else cam.Render();
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = null; return t;
        }

        // ── 탭·상세 조작 + 텍스트 파싱 ──
        static IEnumerator ClickButton(string field)
        {
            var b = Fld<Button>(_panel, field);
            if (b != null) b.onClick.Invoke();
            yield return WaitFrames(2);
            Canvas.ForceUpdateCanvases();
        }

        static List<(string label, int count)> ParseRows(string text)
        {
            var rows = new List<(string, int)>();
            if (string.IsNullOrEmpty(text)) return rows;
            var lines = text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var m = Regex.Match(lines[i].Trim(), @"^(.*)\s+x(\d+)$");
                if (m.Success) rows.Add((m.Groups[1].Value.Trim(), int.Parse(m.Groups[2].Value)));
            }
            return rows;
        }

        /// <summary>#5·#6·#7·#8(일관성) — 리포트 탭 A, 「상세」 B.</summary>
        static IEnumerator CheckReportTabs(Res r, bool needKills)
        {
            yield return ClickButton("reportTabButton");
            var sum = Fld<Text>(_panel, "summaryText")?.text ?? "";
            var mB = Regex.Match(sum, @"돌파\s*(\d+)\s*/\s*(\d+)");
            var mK = Regex.Match(sum, @"처치\s*(\d+)");
            var mS = Regex.Match(sum, @"저지\s*(\d+)\s*/\s*투입\s*(\d+)");
            int aReached = mB.Success ? int.Parse(mB.Groups[1].Value) : -1;
            int aKills = mK.Success ? int.Parse(mK.Groups[1].Value) : -1;
            Check($"#5 [{r.tag}] 리포트 탭 A: 돌파 수 = LastReachedCount", mB.Success && aReached == _director.LastReachedCount,
                $"A '돌파 {aReached}/{(mB.Success ? mB.Groups[2].Value : "?")}' vs LastReachedCount {_director.LastReachedCount} (텍스트 \"{sum.Replace("\n", " / ")}\")");
            Check($"#5 [{r.tag}] 리포트 탭 A: 저지/투입 표기", mS.Success && int.Parse(mS.Groups[1].Value) == r.stopped && int.Parse(mS.Groups[2].Value) == r.spawned,
                $"A '{(mS.Success ? mS.Value : "없음")}' vs 저지 {r.stopped} 투입 {r.spawned}");
            yield return MeasureLayout($"{r.tag}·리포트");

            var detailRoot = Fld<GameObject>(_panel, "detailRoot");
            bool openedBefore = detailRoot != null && detailRoot.activeSelf;
            yield return ClickButton("detailButton");
            bool opened = detailRoot != null && detailRoot.activeSelf;
            Check($"#6 [{r.tag}] 「상세」 클릭 → B 펼침(처음엔 접힘)", !openedBefore && opened, $"클릭 전 {openedBefore} → 후 {opened}");
            yield return MeasureLayout($"{r.tag}·상세");

            var kills = ParseRows(Fld<Text>(_panel, "killsText")?.text);
            var cells = ParseRows(Fld<Text>(_panel, "cellsText")?.text);
            int kSum = kills.Sum(x => x.count), cSum = cells.Sum(x => x.count);
            if (needKills) // 처치가 실제로 발생한 판에서만 B 를 판정한다(처치 0 판은 측정 대상 아님)
            {
                bool kRowsOk = kills.Count >= 1 && kills.Count <= 6 && (kills.Count <= 5 || kills[kills.Count - 1].label.StartsWith("기타"));
                Check($"#6 [{r.tag}] B 함정별 처치 행 수 1–5(+기타)", kRowsOk, $"행 {kills.Count}: {string.Join(" | ", kills.Select(k => k.label + " x" + k.count))}");
                Check($"#6 [{r.tag}] B 합계 = A 처치", kSum == aKills && cSum == aKills && aKills == r.deaths,
                    $"함정별 합 {kSum}, 사망 지점 합 {cSum}, A 처치 {aKills}, 리포트 DeathEvents {r.deaths}");
                // 함정 이름 = 실제 TrapDefinition 에셋들의 표시 이름 중 하나
                var trapNames = AssetDatabase.FindAssets("t:TrapDefinition")
                    .Select(g => AssetDatabase.LoadAssetAtPath<TrapDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(d => d != null && !string.IsNullOrEmpty(d.DisplayName)).Select(d => d.DisplayName).Distinct().ToList();
                bool named = kills.Count >= 1 && trapNames.Any(n => kills[0].label.Contains(n)) && !kills[0].label.StartsWith("기타") && kills[0].count >= 1;
                Check($"#7 [{r.tag}] 함정 킬 기록: B 1행 라벨에 함정 이름·킬 ≥1", r.deaths >= 1 && named, kills.Count > 0 ? $"1행 '{kills[0].label}' x{kills[0].count}, 총 처치 {r.deaths}" : $"행 없음, 총 처치 {r.deaths}");
            }
            // #8: 시간 초과 시도는 처치(A·B)에 편입되지 않고 별도 계상 — 합계 일치 + 시간초과분이 B 에 따로 섞이지 않음
            Check($"#8 [{r.tag}] 시간 초과 사망은 처치 아님", kSum == r.deaths, $"B 합 {kSum} = DeathEvents {r.deaths}, 시간 초과 시도(별도 계상) {r.timedOut}");
            yield return ClickButton("resultTabButton");
        }

        // ═══════════════ 본 시나리오 ═══════════════
        static IEnumerator Main()
        {
            // ── 준비 ──
            _gpm = GamePhaseManager.Instance;
            yield return WaitUntil(() => MapLoader.Instance != null && MapLoader.Instance.IsLoaded
                && (_build = UnityEngine.Object.FindFirstObjectByType<BuildPhaseController>()) != null && _build.TrapsReady, 60f);
            if (!_waitOk) { Abort("초기 매니저·맵·함정 로드 시간 초과"); yield break; }
            MapLoader.OnMapLoaded += _ => _mapLoaded++;

            _director = UnityEngine.Object.FindFirstObjectByType<VerificationDirector>();
            _phaseCtl = UnityEngine.Object.FindFirstObjectByType<VerificationPhaseController>();
            _panel = UnityEngine.Object.FindFirstObjectByType<VerificationResultPanel>(FindObjectsInactive.Include);
            _settings = UnityEngine.Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            var rootGo = Fld<GameObject>(_panel, "panelRoot");
            var nodesWired = Fld<ResearchNodeDefinition[]>(_director, "researchNodes");
            Check("[준비] 디렉터·페이즈 컨트롤러·결과 패널·설정 패널 존재",
                _director != null && _phaseCtl != null && _panel != null && rootGo != null && _settings != null && _settings.PanelRoot != null,
                $"director={_director != null} phaseCtl={_phaseCtl != null} panel={_panel != null} settings={_settings != null}");
            Check("[준비] 결과 패널이 director·phaseController 로 배선됨(빌더 산출)",
                _panel != null && Fld<VerificationDirector>(_panel, "director") != null && Fld<VerificationPhaseController>(_panel, "phaseController") != null,
                _panel == null ? "패널 없음" : $"director={Fld<VerificationDirector>(_panel, "director") != null} phaseController={Fld<VerificationPhaseController>(_panel, "phaseController") != null}");
            Check("[준비] director.researchNodes 자동 배선(MetaUiSetup)", nodesWired != null && nodesWired.Length > 0, $"{nodesWired?.Length ?? 0}개");
            if (_director == null || _phaseCtl == null || _panel == null || rootGo == null || _settings == null) { Abort("필수 오브젝트 없음"); yield break; }
            _canvas = rootGo.GetComponentInParent<Canvas>().rootCanvas;

            var nodeLife = FindNode(ResearchEffect.BreachLife);
            var nodeTime = FindNode(ResearchEffect.VerificationTime);
            var nodeCount = FindNode(ResearchEffect.AgentCount);
            Check("[준비] 새 연구 노드 3종 에셋(BreachLife·VerificationTime·AgentCount)", nodeLife != null && nodeTime != null && nodeCount != null,
                $"life={nodeLife?.name} time={nodeTime?.name} count={nodeCount?.name}");
            if (nodeLife == null || nodeCount == null) { Abort("연구 노드 에셋 없음"); yield break; }

            // ═══ S1: stage_02 무방비 → 자연 실패. #1 #2 #5 #12 #14 #18 + 「계속」 #3 ═══
            if (!SetResearch(null, 0)) { Abort("연구 초기화 실패"); yield break; }
            var s1 = new Res { tag = "S1 stage_02 무방비", map = "stage_02" };
            yield return RunScenario(s1, Place.None, 2001);
            if (!s1.ok) { Abort("S1 결과 대기 실패"); yield break; }
            float tAw = Time.realtimeSinceStartup;
            Check("#14 stage_02 전용 스폰 테이블", s1.table.Contains("Stage02"), $"테이블 '{s1.table}'");
            Check("#14 stage_02 투입 10마리", s1.spawned == 10, $"리포트 투입 {s1.spawned}, 규칙 마릿수 {s1.rCount}");
            Check("#18 결과 표시 시 설정 패널 비활성", !_settings.PanelRoot.activeSelf && rootGo.activeSelf, $"SettingsPanel active={_settings.PanelRoot.activeSelf}, 결과 패널 active={rootGo.activeSelf}");
            Check("#2 결과 패널이 보임(켜짐·최상단 sibling)", rootGo.activeInHierarchy && rootGo.transform.GetSiblingIndex() == rootGo.transform.parent.childCount - 1,
                $"activeInHierarchy={rootGo.activeInHierarchy} sibling {rootGo.transform.GetSiblingIndex()}/{rootGo.transform.parent.childCount - 1}");

            // #12: 입력 주입 전 timeScale 은 이미 1 이어야 의미가 있다. Space·Enter 를 6틱 누른 뒤 상태 확인.
            float scaleBefore = Time.timeScale;
            var kb = Kb();
            bool pressedSeen = false;
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space, Key.Enter));
            for (int i = 0; i < 6; i++) { yield return null; if (kb.spaceKey.isPressed || kb.enterKey.isPressed) pressedSeen = true; }
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return WaitFrames(5);
            Check("#12 대기 중 Enter·Space 무시", Mathf.Abs(Time.timeScale - 1f) < 0.01f && _gpm.currentPhase == GamePhase.Verification && _phaseCtl.IsAwaitingResult && pressedSeen,
                $"주입 전 timeScale {scaleBefore:F2} → 후 {Time.timeScale:F2}, phase {_gpm.currentPhase}, 대기 {_phaseCtl.IsAwaitingResult}, 키 상태 반영 관찰 {pressedSeen}");

            // #2 결과 화면 레이아웃·픽셀
            yield return MeasureLayout($"{s1.tag}·결과");
            if (!_shotDone) { _shotDone = true; MeasurePixels("result"); }

            // #5·#6(상세)·#8 일관성
            yield return CheckReportTabs(s1, false);

            // #1: 종료(대기 진입) 2초 뒤에도 Verification·대기 유지 — 위 측정 시간과 별개로 최소 2초 보장
            float spent = Time.realtimeSinceStartup - tAw;
            if (spent < 2f) yield return WaitReal(2f - spent);
            Check("#1 검증 종료 2초 뒤 페이즈·대기", _gpm.currentPhase == GamePhase.Verification && _phaseCtl.IsAwaitingResult, $"종료 후 {Time.realtimeSinceStartup - tAw:F1}초: phase {_gpm.currentPhase}, IsAwaitingResult {_phaseCtl.IsAwaitingResult}");

            yield return ContinueFlow(s1);

            // ═══ S2: stage_04 + 전 슬롯 설치(연구 없음) → 함정 처치가 실제로 발생하는 판. #5 #6 #7 #8 ═══
            // (stage_01 함정 4개·라이프 1 은 첫 돌파로 바로 끝나 처치 0 이라 B 판정 대상이 못 된다)
            Res s2 = null;
            for (int i = 0; i < 2; i++)
            {
                var c = new Res { tag = $"S2 stage_04 전슬롯(seed {4001 + i})", map = "stage_04" };
                yield return RunScenario(c, Place.Greedy, 4001 + i);
                if (!c.ok) { Abort("S2 결과 대기 실패"); yield break; }
                s2 = c;
                if (c.deaths >= 1 || i == 1) break; // 마지막 판은 패널이 열린 채로 측정해야 하므로 계속하지 않는다
                yield return ContinueFlow(c);
            }
            Check("#18 [S2] 결과 표시 시 설정 패널 비활성", !_settings.PanelRoot.activeSelf && rootGo.activeSelf, $"SettingsPanel active={_settings.PanelRoot.activeSelf}, 결과 패널 active={rootGo.activeSelf}");
            yield return MeasureLayout($"{s2.tag}·결과");
            yield return CheckReportTabs(s2, true);
            yield return ContinueFlow(s2);
            Res base04 = s2;

            // ═══ S3: stage_03 → #14 ═══
            var s3 = new Res { tag = "S3 stage_03 무방비", map = "stage_03" };
            yield return RunScenario(s3, Place.None, 3001);
            if (!s3.ok) { Abort("S3 결과 대기 실패"); yield break; }
            Check("#14 stage_03 전용 스폰 테이블", s3.table.Contains("Stage03"), $"테이블 '{s3.table}'");
            Check("#14 stage_03 투입 10마리", s3.spawned == 10, $"리포트 투입 {s3.spawned}, 규칙 마릿수 {s3.rCount}");
            yield return ContinueFlow(s3);

            // ═══ S4: #13 강제 Build 전환 시 해제 ═══
            var s4 = new Res { tag = "S4 강제 전환", map = "stage_02" };
            yield return RunScenario(s4, Place.None, 2002);
            if (!s4.ok) { Abort("S4 결과 대기 실패"); yield break; }
            bool beforeOpen = rootGo.activeSelf && _phaseCtl.IsAwaitingResult;
            _gpm.SetPhase(GamePhase.Build, true);
            yield return null;
            bool panelOff = !rootGo.activeSelf, awaitOff = !_phaseCtl.IsAwaitingResult;
            yield return WaitReal(1.0f); // 잔여 코루틴이 뒤늦게 페이즈를 바꾸지 않는지
            Check("#13 강제 Build 전환 시 패널·대기 해제", beforeOpen && panelOff && awaitOff && _gpm.currentPhase == GamePhase.Build,
                $"전환 전 열림·대기={beforeOpen} → 다음 프레임 패널 꺼짐={panelOff}, 대기 해제={awaitOff}, 1초 뒤 phase {_gpm.currentPhase}");

            // ═══ S5: #11 마릿수 규칙의 실제 반영 (침입 차단 Lv3 → 10 − 3 = 7) ═══
            if (!SetResearch(nodeCount, Mathf.Min(3, nodeCount.MaxLevel))) { Abort("침입 차단 레벨 설정 실패"); yield break; }
            var s5 = new Res { tag = "S5 침입차단 Lv3", map = "stage_02" };
            yield return RunScenario(s5, Place.None, 2001);
            if (!s5.ok) { Abort("S5 결과 대기 실패"); yield break; }
            Check("#11 침입 차단 감소 3 → 투입 7마리", s5.spawned == 7 && s5.rCount == 7 && s5.spawned == s1.spawned - 3,
                $"리포트 투입 {s5.spawned}, 규칙 마릿수 {s5.rCount}, 연구 없을 때 {s1.spawned}");
            yield return ContinueFlow(s5);

            // ═══ S6: #16 방어선 보강 Lv1 — stage_04, 방어가 강한 배치(전 슬롯)에서 시작해 설치 수를 줄여 가며 ═══
            // "1 ≤ 돌파 < 라이프" 판을 찾는다(전 슬롯은 돌파 0 이라 성립하지 않을 수 있어 설치 수를 단계적으로 줄임).
            // 판정 기준(실패 아님 → 「계속」 후 Play)은 그대로다. 실행 시간 상한 안에서만 시도.
            if (!SetResearch(nodeLife, 1)) { Abort("방어선 보강 Lv1 설정 실패"); yield break; }
            var s6 = new List<Res>(); bool matched = false; Res firstMatch = null;
            var tries = new (int limit, int seed)[] { (int.MaxValue, 4001), (int.MaxValue, 4002), (8, 4001), (6, 4001), (4, 4001), (3, 4001), (2, 4001), (2, 4002), (1, 4001), (1, 4002) };
            for (int i = 0; i < tries.Length && !matched; i++)
            {
                if (EditorApplication.timeSinceStartup - _wallStart > 430) break;
                var c = new Res { tag = $"S6 방어선보강Lv1(stage_04 설치≤{(tries[i].limit == int.MaxValue ? "전체" : tries[i].limit.ToString())}, seed {tries[i].seed})", map = "stage_04" };
                yield return RunScenario(c, Place.Greedy, tries[i].seed, tries[i].limit);
                if (!c.ok) { Abort("S6 결과 대기 실패"); yield break; }
                s6.Add(c);
                if (c.reached >= 1 && c.reached < c.rLife) { matched = true; firstMatch = c; }
                if (!c.breached && !_shotDone2)
                {
                    _shotDone2 = true;
                    yield return MeasureLayout($"{c.tag}·결과(성공)");
                    yield return ClickButton("reportTabButton"); yield return MeasureLayout($"{c.tag}·리포트(성공)"); yield return ClickButton("resultTabButton");
                }
                yield return ContinueFlow(c);
            }
            string s6log = string.Join("; ", s6.Select(c => $"[{c.tag}] 돌파 {c.reached}/라이프 {c.rLife} → {(c.breached ? "실패" : "성공")}→{c.next}"));
            bool consistent = s6.All(c => c.breached == (c.reached >= c.rLife));
            Check("#16 Lv1 라이프 = 연구 없을 때 +1", s6.Count > 0 && s6.All(c => c.rLife == base04.rLife + 1), $"연구 없음(stage_04) 라이프 {base04.rLife}, Lv1 라이프 [{string.Join(",", s6.Select(c => c.rLife))}]");
            Check("#16 판정 = 돌파 수 ≥ 라이프 (전 판)", s6.Count > 0 && consistent, s6log);
            Check("#16 방어선 보강 Lv1, 1마리 돌파 시 실패 아님", matched && !firstMatch.breached && firstMatch.next == GamePhase.Play,
                matched ? $"{firstMatch.tag}: 돌파 {firstMatch.reached}/라이프 {firstMatch.rLife} → breached={firstMatch.breached}, 계속 후 {firstMatch.next}" : "'돌파 1 ≤ n < 라이프' 판이 나오지 않아 시나리오 미성립 — " + s6log);

            // ═══ 집계: #3 #4 ═══
            var breaches = _all.Where(a => a.ok && a.breached && a.continued).ToList();
            var successes = _all.Where(a => a.ok && !a.breached && a.continued).ToList();
            Check("#3 실패 → 「계속」 → Build (클릭 1초 내)", breaches.Count >= 1 && breaches.All(a => a.next == GamePhase.Build && a.nextAfter >= 0f && a.nextAfter <= 1f),
                breaches.Count == 0 ? "자연 실패 판이 나오지 않음" : $"{breaches.Count}판: " + string.Join(", ", breaches.Select(a => $"{a.tag}→{a.next}({a.nextAfter:F2}s)")));
            Check("#4 성공 → 「계속」 → Play + 플레이어 rb.simulated", successes.Count >= 1 && successes.All(a => a.next == GamePhase.Play && a.simulated),
                successes.Count == 0 ? "자연 성공 판이 나오지 않음(S2·S6 stage_04 모두 실패)" : $"{successes.Count}판: " + string.Join(", ", successes.Select(a => $"{a.tag}→{a.next} simulated={a.simulated}")));

            Check("[진단] 시나리오 요약", true, string.Join(" || ", _all.Select(a => $"{a.tag}: 돌파 {a.reached}/{a.rLife} 처치 {a.deaths} 저지 {a.stopped}/{a.spawned} 시간초과 {a.timedOut} 시간한도 {a.rTime:F0}s")));
        }
        static bool _shotDone2;
        static Action _restoreCanvas;

        // ═══════════════ 종료 ═══════════════
        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            try { if (_addedKeyboard && Keyboard.current != null) InputSystem.RemoveDevice(Keyboard.current); } catch { }
            RestoreResearch();
            Check("콘솔 오류 0개 (무해 목록 제외)", _errors.Count == 0, $"{_errors.Count}개");
            var sb = new StringBuilder();
            sb.AppendLine($"RESULT: {(_fail == 0 ? "PASS" : "FAIL")} (fail {_fail} / total {_results.Count})");
            foreach (var r in _results) sb.AppendLine(r);
            sb.AppendLine($"CONSOLE ERRORS ({_errors.Count}):");
            foreach (var e in _errors) sb.AppendLine("  " + e);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_out, sb.ToString(), new UTF8Encoding(false));
            EditorApplication.Exit(_fail == 0 ? 0 : 1);
        }
    }
}
