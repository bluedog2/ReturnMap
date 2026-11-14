using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  HeroAnimProbe — 주인공 애니메이션 FSM Play 수용 기준 배치모드 검증. 임시, 커밋하지 않음
    //
    //  입력 방식: Unity Input System 가상 Keyboard/Mouse 디바이스 + QueueStateEvent
    //  (Move=D/A, Jump=Space, Dash=Z — InputSystem_Actions 의 Keyboard&Mouse 바인딩).
    //  가상 키보드로 Move 가 안 들어오면 리플렉션(MoveInput 백킹필드·jumpBufferCounter·TryDash)으로 자동 대체하고
    //  결과 파일 헤더(# 입력 방식)에 어느 쪽을 썼는지 적는다.
    //  프레임별 샘플은 결과 txt 옆 *_frames.csv 로 저장.
    // ═══════════════════════════════════════════════════════════════
    public static class HeroAnimProbe
    {
        const string ScenePath = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const float OverallTimeout = 180f;

        // 관찰 대상 state 이름 → 시트 스프라이트 접두(구분을 위해 '_' 포함)
        static readonly string[] StateNames =
        {
            "Idle", "Run", "Jump_Start", "Jump_Loop", "Apex", "Fall_Loop", "Land", "Dash", "Lord_Hit", "Death",
            "Bag_Out", "Long_Idle", "Bag_In", "Run_Start", "Run_Stop", "Turn"
        };
        static readonly string[] SpritePrefix =
        {
            "Hero_Idle_", "Hero_Run_", "Hero_JumpStart_", "Hero_Rise_", "Hero_Apex_", "Hero_Fall_", "Hero_Land_", "Hero_Dash_", "Hero_Hit_", "Hero_Death_",
            "Hero_BagOut_", "Hero_LongIdle_", "Hero_BagIn_", "Hero_RunStart_", "Hero_RunStop_", "Hero_Turn_"
        };
        static readonly Dictionary<int, string> HashToName = new Dictionary<int, string>();

        class Smp
        {
            public int f; public float t, dt; public string scn, st, nx, sp;
            public bool tr, flip, face, gr; public float vx, vy, x, y;
        }
        class Seg { public string st; public float t0, t1; public int i0, i1; }

        static readonly List<Smp> _s = new List<Smp>();
        static Smp _cur;
        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors = new List<string>();
        static readonly List<string> _animWarns = new List<string>();
        static int _fail;
        static string _out, _dir;
        static double _wallStart;

        // 시나리오 상태머신
        static int _scn = -1, _sub;
        static float _sStart;
        static string _scnName = "setup";
        static int _lastFrame = -1;

        // 참조
        static PlayerController _pc; static PlayerHealth _ph; static Animator _anim; static SpriteRenderer _vsr; static Rigidbody2D _rb;
        static Vector3 _spawn;
        static Vector3 _turnPos;
        static BuildPhaseController _build;

        // 입력
        static Keyboard _kb; static Mouse _mouse;
        static bool _useRefl; static string _inputNote = "미결정";
        static bool kD, kA, kSp, kZ, pD, pA, pSp, pZ;
        static float _inT; static int _inFrame, _i1;
        static int _zOffFrame;

        // 보조 상태
        static float _idleSince = -1f, _holdT = -1f; static string _holdKey;
        static float _t0Local; static int _i0;
        static float _vx0;
        static float _respT = -1f;
        static bool _seenFlag;

        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "heroanim.txt");
            _dir = Path.GetDirectoryName(_out);
            for (int i = 0; i < StateNames.Length; i++) HashToName[Animator.StringToHash(StateNames[i])] = StateNames[i];
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Application.logMessageReceived += OnLog;
            _wallStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string m, string s, LogType t)
        {
            if (t == LogType.Warning)
            {
                if (m != null && (m.Contains("[PlayerAnimFSM]") || m.Contains("State 가 없습니다")) && _animWarns.Count < 30) _animWarns.Add(m.Split('\n')[0]);
                return;
            }
            if (t != LogType.Error && t != LogType.Exception) return;
            if (s != null && s.Contains("UnityEditor.Search.")) return; // SearchInit.IndexationOnStartup (무해)
            if (_errors.Count < 30) _errors.Add($"[{t}] {m.Split('\n')[0]}");
        }

        static string Arg(string n) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

        static void Check(string name, bool ok, string detail)
        {
            if (!ok) _fail++;
            _results.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
            Debug.Log($"[HEROANIM] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        // ── 입력 주입 ───────────────────────────────────────────────────────
        static void Keys(bool d, bool a, bool sp, bool z)
        {
            kD = d; kA = a; kSp = sp; kZ = z;
            Push();
        }

        static void Push()
        {
            if (!_useRefl)
            {
                var keys = new List<Key>();
                if (kD) keys.Add(Key.D);
                if (kA) keys.Add(Key.A);
                if (kSp) keys.Add(Key.Space);
                if (kZ) keys.Add(Key.Z);
                InputSystem.QueueStateEvent(_kb, new KeyboardState(keys.ToArray()));
            }
            else
            {
                if (kD != pD || kA != pA)
                {
                    float x = (kD ? 1f : 0f) - (kA ? 1f : 0f);
                    var f = typeof(PlayerController).GetField("<MoveInput>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    f?.SetValue(_pc, new Vector2(x, 0f));
                }
                if (kSp && !pSp)
                {
                    var f = typeof(PlayerController).GetField("jumpBufferCounter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    f?.SetValue(_pc, 0.15f);
                }
                if (kZ && !pZ)
                    typeof(PlayerController).GetMethod("TryDash", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(_pc, null);
            }
            pD = kD; pA = kA; pSp = kSp; pZ = kZ;
        }

        static void MarkInput() { _inT = Time.time; _inFrame = Time.frameCount; _i1 = _s.Count; }

        static void PulseDash() { Keys(kD, kA, kSp, true); _zOffFrame = Time.frameCount + 3; MarkInput(); }

        // ── 샘플링 ──────────────────────────────────────────────────────────
        static void SampleFrame()
        {
            if (_pc == null || _anim == null) return;
            var si = _anim.GetCurrentAnimatorStateInfo(0);
            var ni = _anim.GetNextAnimatorStateInfo(0);
            bool tr = _anim.IsInTransition(0);
            string st = HashToName.TryGetValue(si.shortNameHash, out var n) ? n : "?" + si.shortNameHash;
            string nx = tr ? (HashToName.TryGetValue(ni.shortNameHash, out var n2) ? n2 : "?" + ni.shortNameHash) : "";
            var v = _pc.Velocity;
            var sm = new Smp
            {
                f = Time.frameCount, t = Time.time, dt = Time.deltaTime, scn = _scnName, st = st, nx = nx, tr = tr,
                flip = _vsr != null && _vsr.flipX, face = _pc.IsFacingRight, gr = _pc.IsGrounded,
                vx = v.x, vy = v.y, x = _pc.transform.position.x, y = _pc.transform.position.y,
                sp = _vsr != null && _vsr.sprite != null ? _vsr.sprite.name : "(null)"
            };
            _s.Add(sm); _cur = sm;
        }

        static float AvgDt(int from, int to)
        {
            float sum = 0; int c = 0;
            for (int i = Mathf.Max(0, from); i < to && i < _s.Count; i++) { sum += _s[i].dt; c++; }
            return c > 0 ? sum / c : 0.02f;
        }

        static List<Seg> Runs(int from, int to)
        {
            var r = new List<Seg>();
            to = Mathf.Min(to, _s.Count);
            for (int i = Mathf.Max(0, from); i < to; i++)
            {
                if (r.Count == 0 || r[r.Count - 1].st != _s[i].st)
                {
                    if (r.Count > 0) { r[r.Count - 1].t1 = _s[i].t; r[r.Count - 1].i1 = i; }
                    r.Add(new Seg { st = _s[i].st, t0 = _s[i].t, t1 = _s[i].t, i0 = i, i1 = i + 1 });
                }
                else { r[r.Count - 1].t1 = _s[i].t; r[r.Count - 1].i1 = i + 1; }
            }
            return r;
        }

        static string Seq(List<Seg> r, float baseT)
        {
            var sb = new StringBuilder();
            foreach (var x in r)
            {
                if (sb.Length > 0) sb.Append(" > ");
                sb.Append($"{x.st}[{x.t0 - baseT:F2}+{x.t1 - x.t0:F2}s/{x.i1 - x.i0}f]");
                if (sb.Length > 700) { sb.Append(" ..."); break; }
            }
            return sb.ToString();
        }

        static string F(float v) => v.ToString("F3", CultureInfo.InvariantCulture);

        // ── 보조 판정 ───────────────────────────────────────────────────────
        static bool IdleOk(float minHold)
        {
            bool ok = _cur != null && _cur.st == "Idle" && _cur.gr && Mathf.Abs(_cur.vx) < 0.05f;
            if (!ok) { _idleSince = -1f; return false; }
            if (_idleSince < 0f) _idleSince = Time.time;
            return Time.time - _idleSince >= minHold;
        }

        static bool Hold(string key, bool cond, float min)
        {
            if (!cond) { if (_holdKey == key) _holdT = -1f; return false; }
            if (_holdKey != key || _holdT < 0f) { _holdKey = key; _holdT = Time.time; }
            return Time.time - _holdT >= min;
        }

        /// <summary>시나리오 제한 시간 초과면 FAIL 을 남기고 true(시나리오 종료).</summary>
        static bool Guard(float maxSec)
        {
            if (Time.time - _sStart <= maxSec) return false;
            Check($"[{_scnName}] 시나리오 시간 초과", false, $"sub={_sub}, state={_cur?.st}, vx={(_cur != null ? F(_cur.vx) : "-")}, grounded={_cur?.gr}");
            return true;
        }

        static void ResetBody()
        {
            Keys(false, false, false, false);
            Vector3 p = new Vector3(_spawn.x, _spawn.y, _pc.transform.position.z);
            _pc.transform.position = p;
            _rb.position = new Vector2(p.x, p.y);
            _pc.ResetState();
            _rb.linearVelocity = Vector2.zero;
            _ph.RestoreFullHp();
            _ph.SetInvincible(100000f);
            _idleSince = -1f; _holdT = -1f; _holdKey = null;
        }

        static void StartScn(string name) { _scnName = name; _sub = 0; _sStart = Time.time; }

        // ── Tick ────────────────────────────────────────────────────────────
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > OverallTimeout) { Check("timeout", false, $"{OverallTimeout:F0}초 초과 (시나리오 {_scn} '{_scnName}' sub={_sub})"); Finish(); return; }
            if (_scn == 99) { if (!EditorApplication.isPlaying) Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;

            SampleFrame();
            if (_zOffFrame > 0 && Time.frameCount >= _zOffFrame) { _zOffFrame = 0; Keys(kD, kA, kSp, false); }

            bool done;
            switch (_scn)
            {
                case -1: done = Setup(); break;
                case 0: done = S1(); break;
                case 1: done = S2(); break;
                case 2: done = S3(); break;
                case 3: done = S4(); break;
                case 4: done = S5(); break;
                case 5: done = S6(); break;
                case 6: done = S7(); break;
                case 7: done = S8(); break;
                case 8: done = S9(); break;
                default: done = true; break;
            }
            if (done)
            {
                if (_scn >= 8) { Keys(false, false, false, false); _scn = 99; EditorApplication.ExitPlaymode(); return; }
                _scn++;
                StartScn($"s{_scn + 1}");
                if (_scn >= 0 && _pc != null) Keys(false, false, false, false);
            }
        }

        // ── 준비: 맵 로드·페이즈·입력 경로 ──────────────────────────────────
        static bool Setup()
        {
            switch (_sub)
            {
                case 0:
                    {
                        if (_build == null) _build = UnityEngine.Object.FindFirstObjectByType<BuildPhaseController>();
                        bool ready = GamePhaseManager.Instance != null && MapLoader.Instance != null && MapLoader.Instance.IsLoaded
                                     && _build != null && _build.TrapsReady && RespawnManager.Instance != null;
                        if (!ready) { if (Time.time - _sStart > 40f) { Check("초기 준비", false, "맵·매니저 로드 40초 초과"); _scn = 98; Finish(); } return false; }
                        _pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                        if (_pc == null) return false;
                        _ph = _pc.GetComponent<PlayerHealth>(); _rb = _pc.GetComponent<Rigidbody2D>();
                        _anim = _pc.GetComponentInChildren<Animator>(true);
                        var vis = _pc.transform.Find("Visual"); _vsr = vis != null ? vis.GetComponent<SpriteRenderer>() : null;
                        bool ok = _ph != null && _rb != null && _anim != null && _vsr != null;
                        Check("준비: 플레이어 구성", ok, $"health={_ph != null} rb={_rb != null} animator={_anim != null} visualSR={_vsr != null}");
                        if (!ok) { Finish(); return false; }
                        var rp = RespawnManager.Instance.RespawnPoint;
                        _spawn = new Vector3(rp.x, rp.y, _pc.transform.position.z);
                        RespawnManager.OnRespawn += OnRespawnEv;

                        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
                        try
                        {
                            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        }
                        catch (Exception e) { Debug.Log("[HEROANIM] InputSettings 설정 실패(무시): " + e.Message); }
                        try
                        {
                            _kb = InputSystem.AddDevice<Keyboard>("HeroProbeKeyboard");
                            _mouse = InputSystem.AddDevice<Mouse>("HeroProbeMouse");
                            var pi = _pc.GetComponent<PlayerInput>();
                            pi.SwitchCurrentControlScheme("Keyboard&Mouse", _kb, _mouse);
                        }
                        catch (Exception e) { _inputNote = "가상 디바이스 설정 예외: " + e.Message; }

                        GamePhaseManager.Instance.SetPhase(GamePhase.Play, true);
                        ResetBody();
                        _sStart = Time.time; _sub = 1; return false;
                    }
                case 1:
                    if (BuildCameraController.IsCinematicPlaying) { if (Time.time - _sStart > 12f) { _sub = 2; } return false; }
                    if (Time.time - _sStart < 1.0f) return false;
                    Keys(true, false, false, false); MarkInput(); _sub = 3; return false;
                case 2: // 연출이 안 끝나도 진행(조작 잠금으로 입력 경로 판정이 불가할 수 있음)
                    Keys(true, false, false, false); MarkInput(); _sub = 3; return false;
                case 3:
                    if (_pc.MoveInput.x > 0.5f)
                    {
                        _inputNote = "Input System 가상 Keyboard/Mouse + QueueStateEvent (Move=D/A, Jump=Space, Dash=Z)";
                        Keys(false, false, false, false); _t0Local = Time.time; _sub = 4; return false;
                    }
                    if (Time.time - _inT > 0.7f)
                    {
                        Keys(false, false, false, false);
                        _useRefl = true;
                        _inputNote = "리플렉션 대체 (가상 키보드로 MoveInput 이 들어오지 않음 — MoveInput 백킹필드 / jumpBufferCounter / TryDash). " + _inputNote;
                        Keys(true, false, false, false);
                        bool ok = _pc.MoveInput.x > 0.5f;
                        Keys(false, false, false, false);
                        Check("준비: 입력 주입 경로", ok, _inputNote);
                        _t0Local = Time.time; _sub = 4;
                    }
                    return false;
                case 4:
                    if (Time.time - _t0Local < 0.8f) return false;
                    if (!_useRefl) Check("준비: 입력 주입 경로", true, _inputNote);
                    return true;
            }
            return false;
        }

        static void OnRespawnEv() { _respT = Time.time; }

        // ── S1: 장시간 대기 (기준 2) ────────────────────────────────────────
        static bool S1()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0f)) return Guard(10f);
                    _t0Local = Time.time; _i0 = _s.Count - 1; _seenFlag = false; _sub = 2; return false;
                case 2:
                    {
                        if (_cur.st == "Bag_In") _seenFlag = true;
                        float e = Time.time - _t0Local;
                        bool back = _seenFlag && _cur.st == "Idle";
                        if (!back && e < 20f) return false;
                        float adt = AvgDt(_i0, _s.Count);
                        var runs = Runs(_i0, _s.Count);
                        string seq = Seq(runs, _t0Local);
                        Seg bo = runs.Find(x => x.st == "Bag_Out"), li = runs.Find(x => x.st == "Long_Idle");
                        Check("[2a] 무입력 약 6초 후 Bag_Out 시작", bo != null && Mathf.Abs(bo.t0 - _t0Local - 6f) <= 0.7f + adt,
                              bo != null ? $"Bag_Out 시작 {bo.t0 - _t0Local:F2}s (기준 6±0.7+프레임)" : "Bag_Out 미관측 / " + seq);
                        var names = new List<string>(); foreach (var r in runs) names.Add(r.st);
                        bool order = names.Count >= 5 && names[0] == "Idle" && names[1] == "Bag_Out" && names[2] == "Long_Idle" && names[3] == "Bag_In" && names[4] == "Idle";
                        Check("[2b] Idle > Bag_Out > Long_Idle > Bag_In > Idle 순서", order, seq);
                        float ld = li != null ? li.t1 - li.t0 : -1f;
                        Check("[2c] Long_Idle 약 3.6초", li != null && Mathf.Abs(ld - 3.6f) <= 0.4f + adt, $"Long_Idle {ld:F2}s (기준 3.6±0.4+프레임), 평균 dt {adt:F3}");
                        return true;
                    }
            }
            return false;
        }

        // ── S2: Long_Idle 중 이동 입력 (기준 3) ─────────────────────────────
        static bool S2()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0f)) return Guard(10f);
                    _sub = 2; return false;
                case 2:
                    if (!Hold("li", _cur.st == "Long_Idle", 0.5f)) return Guard(16f);
                    _i0 = _s.Count; Keys(true, false, false, false); MarkInput(); _sub = 3; return false;
                case 3:
                    if (Time.time - _inT < 0.6f) return false;
                    {
                        Keys(false, false, false, false);
                        int n = _s.Count; float adt = AvgDt(_i1, n);
                        float tol = 0.05f + 3f * adt;
                        int iRun = -1, iVx = -1; bool bagIn = false;
                        for (int i = _i1; i < n; i++)
                        {
                            if (iRun < 0 && (_s[i].st == "Run_Start" || _s[i].st == "Run")) iRun = i;
                            if (iVx < 0 && _s[i].vx > 0.1f) iVx = i;
                            if (_s[i].st == "Bag_In") bagIn = true;
                        }
                        string seq = Seq(Runs(_i1 - 1, n), _inT);
                        Check("[3a] Long_Idle 중 → 입력 시 Run_Start/Run 으로 즉시 전환", iRun >= 0 && _s[iRun].t - _inT <= tol,
                              iRun >= 0 ? $"입력 후 {_s[iRun].t - _inT:F3}s({_s[iRun].f - _inFrame}프레임) {_s[iRun].st} (허용 {tol:F3}s) / {seq}" : "Run_Start/Run 미관측 / " + seq);
                        float tolV = 0.05f + 2f * adt + Time.fixedDeltaTime * 2f;
                        Check("[3b] 다음 FixedUpdate 안에 수평 속도 발생", iVx >= 0 && _s[iVx].t - _inT <= tolV,
                              iVx >= 0 ? $"입력 후 {_s[iVx].t - _inT:F3}s({_s[iVx].f - _inFrame}프레임) vx={F(_s[iVx].vx)} (허용 {tolV:F3}s)" : "vx>0.1 미관측");
                        Check("[3c] Bag_In 을 거치지 않음", !bagIn, bagIn ? "Bag_In 관측 / " + seq : "Bag_In 없음");
                        return true;
                    }
            }
            return false;
        }

        // ── S3: Idle → 이동 (기준 4) ────────────────────────────────────────
        static bool S3()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0.3f)) return Guard(10f);
                    Keys(true, false, false, false); MarkInput(); _sub = 2; return false;
                case 2:
                    if (Time.time - _inT < 1.0f) return false;
                    {
                        Keys(false, false, false, false);
                        int n = _s.Count; float adt = AvgDt(_i1, n);
                        var runs = Runs(_i1, n);
                        string seq = Seq(runs, _inT);
                        Seg rs = runs.Find(x => x.st == "Run_Start");
                        int idx = rs != null ? runs.IndexOf(rs) : -1;
                        bool toRun = idx >= 0 && idx + 1 < runs.Count && runs[idx + 1].st == "Run";
                        float d = rs != null ? rs.t1 - rs.t0 : -1f;
                        Check("[4a] Idle 에서 → 입력: Run_Start 후 Run", toRun, seq);
                        Check("[4b] Run_Start 약 0.15초", rs != null && Mathf.Abs(d - 0.15f) <= 0.05f + 2f * adt, $"Run_Start {d:F3}s (기준 0.15±0.05+2프레임), 평균 dt {adt:F3}");
                        return true;
                    }
            }
            return false;
        }

        // 달리는 중 상태 확보 보조: 방향키 유지 후 Run 이 0.25초 안정되면 true
        static bool RunStable(bool right) => Hold("run" + right, _cur.st == "Run" && (right ? _cur.vx > 6f : _cur.vx < -6f), 0.25f);

        // ── S4: 방향 전환 Turn (기준 5) ─────────────────────────────────────
        static bool S4()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0.3f)) return Guard(10f);
                    Keys(true, false, false, false); _sub = 2; return false;
                case 2:
                    if (!RunStable(true)) return Guard(12f);
                    _vx0 = _cur.vx; _turnPos = _pc.transform.position; Keys(false, true, false, false); MarkInput(); _sub = 3; return false;
                case 3:
                    if (Time.time - _inT < 1.0f) return false;
                    AnalyzeTurn("a", "우→좌", _i1, false, _vx0);
                    // 거울 측정은 왼쪽 벽에 막히지 않게 오른쪽으로 달려 온 지점(지면)으로 옮긴 뒤 왼쪽 달리기부터 시작
                    ResetBody();
                    _pc.transform.position = _turnPos; _rb.position = new Vector2(_turnPos.x, _turnPos.y);
                    Keys(false, true, false, false);
                    _sub = 4; return false;
                case 4:
                    if (!RunStable(false)) return Guard(12f);
                    _vx0 = _cur.vx; Keys(true, false, false, false); MarkInput(); _sub = 5; return false;
                case 5:
                    if (Time.time - _inT < 1.0f) return false;
                    AnalyzeTurn("b", "좌→우(거울)", _i1, true, _vx0);
                    Keys(false, false, false, false);
                    return true;
            }
            return false;
        }

        static void AnalyzeTurn(string tag, string label, int iIn, bool toRight, float vx0)
        {
            int n = _s.Count; float adt = AvgDt(iIn, n);
            int iT = -1;
            for (int i = iIn; i < n; i++) if (_s[i].st == "Turn") { iT = i; break; }
            string seq = Seq(Runs(iIn - 1, n), _inT);
            if (iT < 0)
            {
                Check($"[5{tag}] {label}: Turn 재생 후 Run", false, "Turn 미관측 / " + seq);
                Check($"[5{tag}] {label}: Turn 초반 옛 방향 flipX", false, "Turn 미관측");
                Check($"[5{tag}] {label}: flipX 전환 시점·유지", false, "Turn 미관측");
                Check($"[5{tag}] {label}: 조작 즉시(속도 방향 전환 시작)", false, "Turn 미관측");
                return;
            }
            int iEnd = iT; while (iEnd < n && _s[iEnd].st == "Turn") iEnd++;
            float t0 = _s[iT].t;
            bool nextRun = iEnd < n && _s[iEnd].st == "Run";
            Check($"[5{tag}] {label}: Turn 재생 후 Run", nextRun, $"Turn {(iEnd > iT ? _s[iEnd - 1].t - t0 : 0f):F3}s / {seq}");

            // 초반 ≈0.1초: IsFacingRight 는 이미 바뀌었는데 flipX 는 옛 방향 그림
            int earlyN = 0, earlyBad = 0; string bad = "";
            for (int i = iT; i < iEnd; i++)
            {
                float e = _s[i].t - t0; if (e > 0.08f) break;
                earlyN++;
                if (_s[i].face != toRight || _s[i].flip != toRight) { earlyBad++; if (bad.Length < 160) bad += $"[{e:F3}s face={_s[i].face} flip={_s[i].flip}]"; }
            }
            Check($"[5{tag}] {label}: Turn 초반 ~0.1초 IsFacingRight={toRight} 인데 flipX={toRight}(옛 방향 그림)", earlyN >= 1 && earlyBad == 0, $"초반 샘플 {earlyN}개 중 불일치 {earlyBad}개 {bad}");

            // flipX 전환 시점 ≈0.1초 이후, 이후 유지
            int iFlip = -1;
            for (int i = iT; i < n; i++) if (_s[i].flip == !toRight) { iFlip = i; break; }
            int lateBad = 0;
            if (iFlip >= 0) for (int i = iFlip; i < n; i++) if (_s[i].flip != !toRight) lateBad++;
            float fd = iFlip >= 0 ? _s[iFlip].t - t0 : -1f;
            Check($"[5{tag}] {label}: flipX 전환 시점(~0.1초)·이후 유지", iFlip >= 0 && Mathf.Abs(fd - 0.1f) <= 0.05f + 2f * adt && lateBad == 0,
                  iFlip >= 0 ? $"Turn 진입 후 {fd:F3}s 에 flipX={!toRight}, 이후 되돌아간 샘플 {lateBad}개 (기준 0.1±0.05+2프레임)" : "flipX 전환 미관측");

            // 조작 즉시: 입력 직후 IsFacingRight·수평속도 방향
            float tol = 0.05f + 3f * adt;
            int iFace = -1, iV = -1;
            for (int i = iIn; i < n; i++)
            {
                if (iFace < 0 && _s[i].face == toRight) iFace = i;
                if (iV < 0 && (toRight ? _s[i].vx > vx0 + 0.3f : _s[i].vx < vx0 - 0.3f)) iV = i;
            }
            bool ok = iFace >= 0 && iV >= 0 && _s[iFace].t - _inT <= tol && _s[iV].t - _inT <= tol;
            Check($"[5{tag}] {label}: 조작 즉시(IsFacingRight·속도 방향 전환 시작)", ok,
                  $"vx0={F(vx0)} / IsFacingRight 변경 {(iFace >= 0 ? (_s[iFace].t - _inT).ToString("F3") : "미관측")}s, 속도 감소·증가 시작 {(iV >= 0 ? (_s[iV].t - _inT).ToString("F3") : "미관측")}s (허용 {tol:F3}s)");
        }

        // ── S5: Turn 중 대시 (기준 6) ───────────────────────────────────────
        static bool S5()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0.3f)) return Guard(10f);
                    Keys(true, false, false, false); _sub = 2; return false;
                case 2:
                    if (!RunStable(true)) return Guard(12f);
                    Keys(false, true, false, false); _t0Local = Time.time; _sub = 3; return false;
                case 3:
                    if (_cur.st == "Turn") { PulseDash(); _sub = 4; return false; }
                    if (Time.time - _t0Local > 0.6f)
                    {
                        Check("[6] Turn 중 대시 → 즉시 Dash", false, "Turn 상태를 관측하지 못해 측정 불가 / " + Seq(Runs(_s.Count - 40, _s.Count), _t0Local));
                        Keys(false, false, false, false); return true;
                    }
                    return false;
                case 4:
                    if (Time.time - _inT < 0.5f) return false;
                    {
                        Keys(false, false, false, false);
                        int n = _s.Count; float adt = AvgDt(_i1, n); float tol = 0.05f + 3f * adt;
                        int iD = -1, bad = 0, dn = 0;
                        for (int i = _i1; i < n; i++)
                            if (_s[i].st == "Dash") { if (iD < 0) iD = i; dn++; if (_s[i].flip == _s[i].face) bad++; }
                        string seq = Seq(Runs(_i1 - 1, n), _inT);
                        Check("[6a] Turn 중 대시 입력 → 즉시 Dash", iD >= 0 && _s[iD].t - _inT <= tol,
                              iD >= 0 ? $"입력 후 {_s[iD].t - _inT:F3}s({_s[iD].f - _inFrame}프레임) (허용 {tol:F3}s) / {seq}" : "Dash 미관측 / " + seq);
                        Check("[6b] Dash 중 flipX == !IsFacingRight (덮어쓰기 해제)", dn > 0 && bad == 0, $"Dash 샘플 {dn}개 중 불일치 {bad}개 (face={(iD >= 0 ? _s[iD].face.ToString() : "-")} flip={(iD >= 0 ? _s[iD].flip.ToString() : "-")})");
                        return true;
                    }
            }
            return false;
        }

        // ── S6: 입력 해제 → Run_Stop (기준 7) ───────────────────────────────
        static bool S6()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0.3f)) return Guard(10f);
                    Keys(true, false, false, false); _sub = 2; return false;
                case 2:
                    if (!RunStable(true)) return Guard(12f);
                    Keys(false, false, false, false); MarkInput(); _sub = 3; return false;
                case 3:
                    if (Time.time - _inT < 0.9f) return false;
                    {
                        int n = _s.Count; float adt = AvgDt(_i1, n);
                        var runs = Runs(_i1, n); string seq = Seq(runs, _inT);
                        Seg rs = runs.Find(x => x.st == "Run_Stop");
                        int idx = rs != null ? runs.IndexOf(rs) : -1;
                        bool toIdle = idx >= 0 && idx + 1 < runs.Count && runs[idx + 1].st == "Idle";
                        float d = rs != null ? rs.t1 - rs.t0 : -1f;
                        Check("[7a] 입력 해제 → Run_Stop → Idle", toIdle, seq);
                        Check("[7b] Run_Stop 약 0.2초", rs != null && Mathf.Abs(d - 0.2f) <= 0.05f + 2f * adt, $"Run_Stop {d:F3}s (기준 0.2±0.05+2프레임)");
                        _sub = 4; return false;
                    }
                case 4:
                    ResetBody(); _sub = 5; return false;
                case 5:
                    if (!IdleOk(0.3f)) return Guard(14f);
                    Keys(true, false, false, false); _sub = 6; return false;
                case 6:
                    if (!RunStable(true)) return Guard(18f);
                    Keys(false, false, false, false); _sub = 7; return false;
                case 7:
                    if (_cur.st == "Run_Stop") { _t0Local = _cur.t; _sub = 8; return false; }
                    if (Time.time - _sStart > 20f) { Check("[7c] Run_Stop 중 같은 방향 입력 → 즉시 Run", false, "Run_Stop 미관측"); return true; }
                    return false;
                case 8:
                    if (_cur.st == "Run_Stop" && _cur.t - _t0Local >= 0.05f) { Keys(true, false, false, false); MarkInput(); _sub = 9; }
                    else if (_cur.st != "Run_Stop") { Check("[7c] Run_Stop 중 같은 방향 입력 → 즉시 Run", false, "입력 전에 Run_Stop 종료(" + _cur.st + ")"); return true; }
                    return false;
                case 9:
                    if (Time.time - _inT < 0.5f) return false;
                    {
                        Keys(false, false, false, false);
                        int n = _s.Count; float adt = AvgDt(_i1, n); float tol = 0.05f + 3f * adt;
                        var runs = Runs(_i1 - 1, n); string seq = Seq(runs, _inT);
                        int iR = -1; bool idleOrStart = false;
                        for (int i = _i1; i < n; i++)
                        {
                            if (_s[i].st == "Run") { iR = i; break; }
                            if (_s[i].st == "Idle" || _s[i].st == "Run_Start") idleOrStart = true;
                        }
                        Check("[7c] Run_Stop 중 같은 방향 입력 → 즉시 Run", iR >= 0 && _s[iR].t - _inT <= tol && !idleOrStart,
                              iR >= 0 ? $"입력 후 {_s[iR].t - _inT:F3}s Run (허용 {tol:F3}s), 중간 Idle/Run_Start {idleOrStart} / {seq}" : "Run 미관측 / " + seq);
                        return true;
                    }
            }
            return false;
        }

        // ── S7: 점프 순서 (기준 8a) ─────────────────────────────────────────
        static bool S7()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0.3f)) return Guard(10f);
                    Keys(false, false, true, false); MarkInput(); _t0Local = Time.time; _sub = 2; return false;
                case 2:
                    if (Time.time - _t0Local >= 0.25f) { Keys(false, false, false, false); _sub = 3; }
                    return false;
                case 3:
                    {
                        bool landed = false; for (int i = _i1; i < _s.Count; i++) if (_s[i].st == "Land") landed = true;
                        if (!(landed && _cur.st != "Land") && Time.time - _inT < 4.5f) return false;
                        int n = _s.Count; var runs = Runs(_i1 - 1, n); string seq = Seq(runs, _inT);
                        string[] order = { "Jump_Start", "Jump_Loop", "Apex", "Fall_Loop", "Land" };
                        int prev = -1; bool ok = true; var miss = new StringBuilder();
                        foreach (var nm in order)
                        {
                            int fi = -1; for (int i = _i1; i < n; i++) if (_s[i].st == nm) { fi = i; break; }
                            if (fi < 0) { ok = false; miss.Append(nm + " 미관측 "); continue; }
                            if (fi < prev) { ok = false; miss.Append(nm + " 순서 역전 "); }
                            prev = Mathf.Max(prev, fi);
                        }
                        Check("[8a] 점프: Jump_Start > Jump_Loop > Apex > Fall_Loop > Land 순서(각 ≥1프레임)", ok, (miss.Length > 0 ? miss + "/ " : "") + seq);
                        return true;
                    }
            }
            return false;
        }

        // ── S8: 점프 없이 낙하 → Fall_Loop, Apex 없음 (기준 8b, 공중 순간이동 대체) ──
        static bool S8()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0.3f)) return Guard(10f);
                    {
                        Vector3 p = _pc.transform.position + new Vector3(0f, 2.5f, 0f);
                        _pc.transform.position = p; _rb.position = new Vector2(p.x, p.y); _rb.linearVelocity = Vector2.zero;
                        MarkInput(); _t0Local = Time.time; _sub = 2;
                    }
                    return false;
                case 2:
                    {
                        bool landed = false; for (int i = _i1; i < _s.Count; i++) if (_s[i].st == "Land") landed = true;
                        if (!(landed && _cur.st != "Land") && Time.time - _t0Local < 4f) return false;
                        int n = _s.Count; var runs = Runs(_i1 - 1, n); string seq = Seq(runs, _t0Local);
                        bool fall = false, apex = false, jump = false;
                        for (int i = _i1; i < n; i++)
                        {
                            if (_s[i].st == "Fall_Loop") fall = true;
                            if (_s[i].st == "Apex") apex = true;
                            if (_s[i].st == "Jump_Start" || _s[i].st == "Jump_Loop") jump = true;
                        }
                        const string note = "(대체: 낭떠러지 대신 공중 +2.5 순간이동, vy=0 으로 낙하) ";
                        Check("[8b] 낙하(점프 없음) → Fall_Loop 관측 " + note, fall, seq);
                        Check("[8c] 낙하(점프 없음) 중 Apex 가 나오지 않음 " + note, !apex, (apex ? "Apex 관측 / " : "Apex 없음") + (jump ? " (Jump_* 도 관측) / " : " / ") + seq);
                        return true;
                    }
            }
            return false;
        }

        // ── S9: 피격 → 사망 → 리스폰 (기준 9) ───────────────────────────────
        static bool S9()
        {
            switch (_sub)
            {
                case 0: ResetBody(); _sub = 1; return false;
                case 1:
                    if (!IdleOk(0.3f)) return Guard(10f);
                    Keys(false, true, false, false); _t0Local = Time.time; _sub = 2; return false; // 왼쪽을 보게 한 뒤(리스폰 flipX 검증력 확보)
                case 2:
                    if (Time.time - _t0Local < 0.25f) return false;
                    Keys(false, false, false, false); _sub = 3; return false;
                case 3:
                    if (!IdleOk(0.4f)) return Guard(10f);
                    _ph.SetInvincible(0.001f); _t0Local = Time.time; _sub = 4; return false;
                case 4:
                    if (Time.time - _t0Local < 0.3f) return false;
                    _i0 = _s.Count; MarkInput();
                    if (_ph.MaxHp > 1) _ph.TakeDamage(1);
                    _pc.ApplyKnockback(new Vector2(4f, 0f), 0.15f);
                    _sub = 5; return false;
                case 5:
                    if (Time.time - _inT < 0.9f) return false;
                    {
                        int n = _s.Count; float adt = AvgDt(_i0, n);
                        var runs = Runs(_i0, n); string seq = Seq(runs, _inT);
                        Seg hit = runs.Find(x => x.st == "Lord_Hit");
                        float d = hit != null ? hit.t1 - hit.t0 : -1f;
                        Check("[9a] 피격(넉백) → Lord_Hit 약 0.25초", hit != null && Mathf.Abs(d - 0.25f) <= 0.05f + 2f * adt,
                              hit != null ? $"Lord_Hit {d:F3}s (기준 0.25±0.05+2프레임), maxHp={_ph.MaxHp} hp={_ph.CurrentHp} / {seq}" : $"Lord_Hit 미관측 (maxHp={_ph.MaxHp} hp={_ph.CurrentHp}) / {seq}");
                        _ph.SetInvincible(0.001f); _sub = 6; return false;
                    }
                case 6:
                    if (!IdleOk(0.3f)) return Guard(14f);
                    _sub = 7; return false;
                case 7:
                    if (_ph.IsInvincible) return Guard(16f);
                    _i0 = _s.Count; _respT = -1f; _t0Local = Time.time;
                    _ph.TakeDamage(_ph.MaxHp); _sub = 8; return false;
                case 8:
                    if (_respT < 0f) { if (Time.time - _t0Local > 6f) { Check("[9b] 사망 → 리스폰", false, "OnRespawn 미발생 6초"); return true; } return false; }
                    if (Time.time < _respT + 0.6f) return false;
                    {
                        int n = _s.Count; var runs = Runs(_i0, n); string seq = Seq(runs, _t0Local);
                        int iD = -1; for (int i = _i0; i < n; i++) if (_s[i].st == "Death") { iD = i; break; }
                        int nonDeath = 0, cnt = 0;
                        if (iD >= 0) for (int i = iD; i < n && _s[i].t < _respT; i++) { cnt++; if (_s[i].st != "Death") nonDeath++; }
                        Check("[9b] 사망 → Death 재생 후 리스폰 전까지 상태 고정", iD >= 0 && nonDeath == 0 && cnt > 0, iD >= 0 ? $"Death 진입 {_s[iD].t - _t0Local:F3}s, 리스폰 {_respT - _t0Local:F3}s, 그 사이 {cnt}프레임 중 Death 아님 {nonDeath} / {seq}" : "Death 미관측 / " + seq);
                        Check("[9c] 리스폰 → Idle, flipX == !IsFacingRight", _cur.st == "Idle" && _cur.flip == !_cur.face,
                              $"리스폰+{Time.time - _respT:F2}s: state={_cur.st} flipX={_cur.flip} IsFacingRight={_cur.face}{(_cur.face ? " (오른쪽 — 검증력 약함)" : "")}");
                        return true;
                    }
            }
            return false;
        }

        // ── 종료 ────────────────────────────────────────────────────────────
        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            RespawnManager.OnRespawn -= OnRespawnEv;
            Time.timeScale = 1f;
            try { Keys(false, false, false, false); } catch { }

            // 기준 10: state ↔ 스프라이트 시트
            var prefix = new Dictionary<string, string>();
            for (int i = 0; i < StateNames.Length; i++) prefix[StateNames[i]] = SpritePrefix[i];
            var total = new Dictionary<string, int>(); var bad = new Dictionary<string, int>(); var ex = new Dictionary<string, string>();
            var unknown = new HashSet<string>();
            for (int i = 0; i < _s.Count; i++)
            {
                var m = _s[i];
                if (!prefix.ContainsKey(m.st)) { unknown.Add(m.st); continue; }
                if (m.tr) continue;                                 // 전환 블렌딩 중 제외
                if (i > 0 && _s[i - 1].st != m.st) continue;        // state 진입 첫 프레임 제외
                total.TryGetValue(m.st, out int tc); total[m.st] = tc + 1;
                if (!m.sp.StartsWith(prefix[m.st], StringComparison.Ordinal))
                {
                    bad.TryGetValue(m.st, out int bc); bad[m.st] = bc + 1;
                    if (!ex.ContainsKey(m.st)) ex[m.st] = m.sp;
                }
            }
            var seen = new List<string>(); var unseen = new List<string>(); var badList = new List<string>();
            foreach (var nm in StateNames) { if (total.ContainsKey(nm)) seen.Add($"{nm}({total[nm]})"); else unseen.Add(nm); }
            foreach (var kv in bad) badList.Add($"{kv.Key}: {kv.Value}/{total[kv.Key]} 불일치 예 '{ex[kv.Key]}'");
            if (_s.Count > 0)
                Check("[10] state 별 Visual 스프라이트가 해당 시트에서 옴", bad.Count == 0 && seen.Count > 0,
                      $"관측 state {string.Join(",", seen)} / 미관측 {(unseen.Count == 0 ? "없음" : string.Join(",", unseen))} / 불일치 {(badList.Count == 0 ? "없음" : string.Join("; ", badList))} / 알 수 없는 state {(unknown.Count == 0 ? "없음" : string.Join(",", unknown))} (진입 첫 프레임·전환 중 제외)");
            else Check("[10] state 별 Visual 스프라이트가 해당 시트에서 옴", false, "샘플 없음");

            // 기준 1: 경고·오류
            bool noWarn = true; foreach (var w in _animWarns) if (w.Contains("State 가 없습니다")) noWarn = false;
            Check("[1a] \"[PlayerAnimFSM] Animator 에 State 가 없습니다\" 경고 0건", noWarn, _animWarns.Count == 0 ? "0건" : string.Join(" | ", _animWarns));
            Check("[1b] 콘솔 오류 0개 (무해 목록 제외)", _errors.Count == 0, $"{_errors.Count}개");

            var sb = new StringBuilder();
            sb.AppendLine($"RESULT: {(_fail == 0 ? "PASS" : "FAIL")} (fail {_fail} / total {_results.Count})");
            sb.AppendLine($"# 입력 방식: {_inputNote}");
            sb.AppendLine($"# 샘플 {_s.Count}프레임, 평균 dt {AvgDt(0, _s.Count):F4}s, fixedDeltaTime {Time.fixedDeltaTime:F4}s, 프레임별 CSV: {Path.GetFileName(CsvPath())}");
            foreach (var r in _results) sb.AppendLine(r);
            sb.AppendLine($"CONSOLE ERRORS ({_errors.Count}):");
            foreach (var e in _errors) sb.AppendLine("  " + e);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_out, sb.ToString(), new UTF8Encoding(false));
            WriteCsv();
            EditorApplication.Exit(_fail == 0 ? 0 : 1);
        }

        static string CsvPath() => Path.Combine(_dir, Path.GetFileNameWithoutExtension(_out) + "_frames.csv");

        static void WriteCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("scenario,frame,time,dt,state,nextState,inTransition,flipX,isFacingRight,velX,velY,grounded,posX,posY,sprite");
            foreach (var m in _s)
                sb.AppendLine(string.Join(",", m.scn, m.f, F(m.t), F(m.dt), m.st, m.nx, m.tr ? 1 : 0, m.flip ? 1 : 0, m.face ? 1 : 0, F(m.vx), F(m.vy), m.gr ? 1 : 0, F(m.x), F(m.y), m.sp));
            File.WriteAllText(CsvPath(), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
