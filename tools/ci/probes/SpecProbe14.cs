using System;
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
    //  SpecProbe14 — SPEC-14 스테이지별 함정 허용 목록 배치모드 Play 검증. 임시, 커밋하지 않음
    // ═══════════════════════════════════════════════════════════════
    public static class SpecProbe14
    {
        const string ScenePath   = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const string Spike       = "TrapDefinition_Spike";
        const string Arrow       = "TrapDefinition_ArrowShooter";
        const string Hammer      = "TrapDefinition_DropHammer";
        const string Stage1      = "stage_01";
        const string Stage2      = "stage_02";
        const string Stage3      = "stage_03";

        static int _step = -1;
        static int _stepFrame;
        static float _stepStartReal;
        static double _wallStart;

        static BuildPhaseController _build;
        static BuildHudController _hud;
        static MapLoader _loader;
        static List<Button> _buttons;
        static List<Image> _frames;
        static int _iSpike = -1, _iArrow = -1, _iHammer = -1;
        static int _prevSelected;
        static readonly List<string> _buildLogs = new List<string>();
        static bool _directStart;
        static string _origAuto;

        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors = new List<string>();
        static int _fail;
        static string _out, _dir;

        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "probe14.txt");
            _dir = Path.GetDirectoryName(_out);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 진짜 '직접 시작': 씬(메모리상, 저장 안 함)의 MapLoader._autoLoadMapId 를 stage_01 로 바꾼 뒤 Play 진입.
            // 씬 리로드를 끄므로 편집 중 상태가 그대로 Play 에 쓰인다. 배치모드 프로세스라 종료 시 원복 불필요(저장 안 함).
            var ml = UnityEngine.Object.FindFirstObjectByType<MapLoader>();
            if (ml != null)
            {
                var so = new SerializedObject(ml);
                var p = so.FindProperty("_autoLoadMapId");
                if (p != null)
                {
                    _origAuto = p.stringValue; p.stringValue = Stage1;
                    so.ApplyModifiedPropertiesWithoutUndo(); _directStart = true;
                }
            }
            Application.logMessageReceived += OnLog;
            _wallStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// 콘솔 Error·Exception 수집(무해 목록 제외) + 설치 거절 사유 로그("[Build] 설치 불가")는 별도 수집.
        /// </summary>
        static void OnLog(string m, string s, LogType t)
        {
            if (t == LogType.Log && m != null && m.StartsWith("[Build] 설치 불가")) { _buildLogs.Add(m); return; }
            if (t != LogType.Error && t != LogType.Exception) return;
            if (s != null && s.Contains("UnityEditor.Search.")) return; // SearchInit.IndexationOnStartup (무해)
            if (_errors.Count < 30) _errors.Add($"[{t}] {m.Split('\n')[0]}");
        }

        static string Arg(string n) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

        static void Check(string name, bool ok, string detail)
        {
            if (!ok) _fail++;
            _results.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
            Debug.Log($"[PROBE14] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static void Next() { _step++; _stepFrame = Time.frameCount; _stepStartReal = Time.unscaledTime; }

        static T Field<T>(object o, string name) where T : class
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f?.GetValue(o) as T;
        }

        static string Counts()
        {
            int on = _buttons.Count(b => b != null && b.interactable);
            int off = _buttons.Count - on;
            return $"활성 {on} · 비활성 {off}";
        }

        static bool HammerOnlyOff()
        {
            if (_buttons.Count == 0 || _iHammer < 0) return false;
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i].interactable == (i == _iHammer)) return false;
            return _buttons.Count(b => b.interactable) == 2 && _buttons.Count(b => !b.interactable) == 1;
        }

        /// <summary>HUD 선택 프레임(금색 테두리)이 켜진 인덱스 목록.</summary>
        static List<int> SelectedFrames()
        {
            var r = new List<int>();
            for (int i = 0; i < _frames.Count; i++)
            {
                var c = _frames[i].color;
                if (c.r > 0.9f && c.g > 0.75f && c.b < 0.35f) r.Add(i);
            }
            return r;
        }

        static bool MapReady(string id) =>
            _loader.IsLoaded && !_loader.IsLoading && _loader.CurrentMap != null && _loader.CurrentMap.mapId == id;

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > 150) { Check("timeout", false, "150초 초과"); Finish(); return; }
            if (_step == 99) { if (!EditorApplication.isPlaying) Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            var gpm = GamePhaseManager.Instance;
            int fr = Time.frameCount - _stepFrame;
            float tr = Time.unscaledTime - _stepStartReal;
            switch (_step)
            {
                case -1:
                    if (gpm == null) return;
                    _step = 0; _stepFrame = Time.frameCount; _stepStartReal = Time.unscaledTime; return;

                case 0: // 씬 자동 로드(직접 시작 경로) 완료 + HUD 버튼 생성 대기
                {
                    _build = UnityEngine.Object.FindFirstObjectByType<BuildPhaseController>();
                    _hud = UnityEngine.Object.FindFirstObjectByType<BuildHudController>();
                    _loader = MapLoader.Instance;
                    bool ready = _build != null && _hud != null && _loader != null && _build.TrapsReady && _loader.IsLoaded && !_loader.IsLoading && _loader.CurrentMap != null;
                    if (ready)
                    {
                        _buttons = Field<List<Button>>(_hud, "_trapButtons");
                        _frames = Field<List<Image>>(_hud, "_buttonFrames");
                        ready = _buttons != null && _frames != null && _buttons.Count == _build.TrapDefinitions.Count && _buttons.Count > 0;
                    }
                    if (!ready)
                    {
                        if (tr > 40f) { Check("초기 준비", false, $"build={_build != null} hud={_hud != null} loader={_loader != null}"); Finish(); }
                        return;
                    }
                    for (int i = 0; i < _build.TrapDefinitions.Count; i++)
                    {
                        string n = _build.TrapDefinitions[i].name;
                        if (n == Spike) _iSpike = i; else if (n == Arrow) _iArrow = i; else if (n == Hammer) _iHammer = i;
                    }
                    Check("초기 준비 (함정 인덱스 이름 조회)", _iSpike >= 0 && _iArrow >= 0 && _iHammer >= 0,
                        $"Spike={_iSpike} Arrow={_iArrow} Hammer={_iHammer} 정의수={_build.TrapDefinitions.Count} 버튼수={_buttons.Count} 초기맵={_loader.CurrentMap.mapId}");
                    if (_iSpike < 0 || _iArrow < 0 || _iHammer < 0) { Finish(); return; }
                    gpm.SetPhase(GamePhase.Build, true);
                    Next(); return;
                }

                case 1: // A9: 직접 시작 경로 — 맵 전환 없이 초기 자동 로드 상태 그대로 관찰
                    if (fr < 5) return;
                    if (!_directStart)
                        _results.Add("SKIP | A9 stage_01 직접 시작 | 자동화 불가: MapLoader._autoLoadMapId 필드를 찾지 못함");
                    else
                        Check("A9 stage_01 직접 시작(_autoLoadMapId=stage_01 로 Play 진입) → 해머만 비활성",
                            _loader.CurrentMap.mapId == Stage1 && HammerOnlyOff(),
                            $"맵={_loader.CurrentMap.mapId} (원래 씬 값 {_origAuto}) {Counts()} 해머 버튼 interactable={_buttons[_iHammer].interactable}");
                    Next(); return;

                case 2: // 다른 맵으로 전환 후 stage_01 로 되돌아와 A3·A5·A6 측정 (초기 맵 비의존)
                    _loader.LoadMap(Stage2); _step = 19; Next(); return;

                case 20:
                    if (!MapReady(Stage2))
                    {
                        if (tr > 40f) { Check("stage_02 로드", false, $"현재맵 {_loader.CurrentMap?.mapId}"); Finish(); }
                        return;
                    }
                    _loader.LoadMap(Stage1); Next(); return;

                case 21:
                    if (!MapReady(Stage1))
                    {
                        if (tr > 40f) { Check("stage_01 로드", false, $"현재맵 {_loader.CurrentMap?.mapId}"); Finish(); }
                        return;
                    }
                    TrapMutationManager.Instance.RollNewSeed();
                    gpm.SetPhase(GamePhase.Build, true);
                    _step = 22; _stepFrame = Time.frameCount; _stepStartReal = Time.unscaledTime; return;

                case 22: // A3, A5, A6 (stage_01)
                {
                    if (fr < 10) return;
                    Check("A3 stage_01 Build HUD → 해머만 비활성", HammerOnlyOff(),
                        $"{Counts()} (해머 인덱스 {_iHammer} interactable={_buttons[_iHammer].interactable})");

                    // A5: 허용 함정(Arrow)으로 선택 후 해머 선택 시도 → 불변
                    _build.SelectTrap(_iArrow);
                    int before = _build.SelectedIndex;
                    _build.SelectTrap(_iHammer); // 숫자키/클릭과 같은 공개 경로
                    Check("A5 stage_01 해머 SelectTrap → 선택 불변",
                        before == _iArrow && _build.SelectedIndex == before,
                        $"이전 {before} → 이후 {_build.SelectedIndex} (해머 인덱스 {_iHammer}, IsTrapAllowed={_build.IsTrapAllowed(_iHammer)})");

                    // A6: 선택 인덱스를 리플렉션(_selected)으로 해머로 강제 → 실제 TryPlace 호출
                    var selField = typeof(BuildPhaseController).GetField("_selected", BindingFlags.Instance | BindingFlags.NonPublic);
                    var tryPlace = typeof(BuildPhaseController).GetMethod("TryPlace", BindingFlags.Instance | BindingFlags.NonPublic);
                    var slot = TrapSlotRegistry.GetEmpty().FirstOrDefault(s => s.Anchor == TrapAnchor.Ceiling) ?? TrapSlotRegistry.GetEmpty().FirstOrDefault();
                    if (selField == null || tryPlace == null || slot == null)
                    {
                        Check("A6 stage_01 해머 설치 시도 → 거절", false, $"_selected={(selField != null)} TryPlace={(tryPlace != null)} 빈슬롯={(slot != null)}");
                    }
                    else
                    {
                        selField.SetValue(_build, _iHammer);
                        _buildLogs.Clear();
                        tryPlace.Invoke(_build, new object[] { slot.GridX, slot.GridY });
                        bool logged = _buildLogs.Any(l => l.Contains("잠긴 함정"));
                        Check("A6 stage_01 해머 설치 시도 → 거절",
                            slot.IsEmpty && logged,
                            $"강제 방식: 리플렉션으로 BuildPhaseController._selected={_iHammer}(해머) 설정 후 TryPlace({slot.GridX},{slot.GridY}, anchor={slot.Anchor}) 호출 / 슬롯 IsEmpty={slot.IsEmpty} / 로그: {(_buildLogs.Count > 0 ? string.Join(" | ", _buildLogs) : "(없음)")}");
                        _build.SelectTrap(_iSpike); // 복구
                    }
                    _step = 2; Next(); return; // → case 3
                }

                case 3:
                    _loader.LoadMap(Stage3); Next(); return;

                case 4: // stage_03 로드 완료
                    if (!MapReady(Stage3))
                    {
                        if (tr > 40f) { Check("stage_03 로드", false, $"현재맵 {_loader.CurrentMap?.mapId} loaded={_loader.IsLoaded} loading={_loader.IsLoading}"); Finish(); }
                        return;
                    }
                    TrapMutationManager.Instance.RollNewSeed();
                    gpm.SetPhase(GamePhase.Build, true);
                    Next(); return;

                case 5: // A7
                {
                    if (fr < 10) return;
                    string cnt = Counts();
                    bool allOn = _buttons.Count(b => b.interactable) == _buttons.Count;
                    _build.SelectTrap(_iHammer);
                    bool selected = _build.SelectedIndex == _iHammer;
                    var tryPlace = typeof(BuildPhaseController).GetMethod("TryPlace", BindingFlags.Instance | BindingFlags.NonPublic);
                    var slot = TrapSlotRegistry.GetEmpty().FirstOrDefault(s => s.Anchor == TrapAnchor.Ceiling);
                    int placed = 0; string trapName = "-";
                    if (tryPlace != null && slot != null)
                    {
                        _buildLogs.Clear();
                        tryPlace.Invoke(_build, new object[] { slot.GridX, slot.GridY });
                        if (!slot.IsEmpty) { placed = 1; trapName = slot.OccupiedTrap != null ? slot.OccupiedTrap.name : "?"; }
                    }
                    Check("A7 stage_03 해머 활성 + Ceiling 설치 성공",
                        allOn && selected && placed == 1,
                        $"{cnt} / 해머 선택={selected} / Ceiling 빈슬롯={(slot != null)} 설치 {placed} ({trapName}) / 거절 로그: {(_buildLogs.Count > 0 ? string.Join(" | ", _buildLogs) : "(없음)")}");
                    Next(); return;
                }

                case 6: // A8 준비: 해머가 선택된 상태에서 stage_01 로드
                    _prevSelected = _build.SelectedIndex;
                    _loader.LoadMap(Stage1); Next(); return;

                case 7:
                    if (!MapReady(Stage1))
                    {
                        if (tr > 40f) { Check("stage_01 재로드", false, $"현재맵 {_loader.CurrentMap?.mapId} loaded={_loader.IsLoaded} loading={_loader.IsLoading}"); Finish(); }
                        return;
                    }
                    TrapMutationManager.Instance.RollNewSeed();
                    gpm.SetPhase(GamePhase.Build, true);
                    Next(); return;

                case 8: // A8
                {
                    if (fr < 10) return;
                    var sel = SelectedFrames();
                    Check("A8 해머 선택 상태에서 stage_01 로드 → 선택이 Spike 로 이동",
                        _prevSelected == _iHammer && _build.SelectedIndex == _iSpike && sel.Count == 1 && sel[0] == _iSpike,
                        $"전환 전 선택 {_prevSelected}(해머={_iHammer}) / 전환 후 SelectedIndex={_build.SelectedIndex}(Spike={_iSpike}) / HUD 선택 프레임 인덱스 [{string.Join(",", sel)}] / {Counts()}");
                    _step = 99; EditorApplication.ExitPlaymode(); return;
                }
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
            Check("콘솔 오류 0개 (무해 목록 제외)", _errors.Count == 0, $"{_errors.Count}개");
            int total = _results.Count(r => !r.StartsWith("SKIP")), pass = total - _fail; // SKIP 은 집계 제외
            var sb = new StringBuilder();
            sb.AppendLine($"RESULT: {(_fail == 0 ? "PASS" : "FAIL")} (fail {_fail} / total {total})");
            sb.AppendLine($"PASS {pass}/{total}");
            foreach (var r in _results) sb.AppendLine(r);
            sb.AppendLine($"CONSOLE ERRORS ({_errors.Count}):");
            foreach (var e in _errors) sb.AppendLine("  " + e);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_out, sb.ToString(), new UTF8Encoding(false));
            EditorApplication.Exit(_fail == 0 ? 0 : 1);
        }
    }
}
