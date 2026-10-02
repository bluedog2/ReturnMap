using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  SpecProbe9b — SPEC-9 rev 2 배치모드 Play 검증 (로직 + 픽셀 가시성). 임시, 커밋하지 않음
    // ═══════════════════════════════════════════════════════════════
    public static class SpecProbe9b
    {
        const string ScenePath  = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const string ArrowPath  = "Assets/_Project/ResourcceEX/Prefabs/Traps/ArrowShooter.prefab";
        const string HammerPath = "Assets/_Project/ResourcceEX/Prefabs/Traps/DropHammer.prefab";
        const string SpikePath  = "Assets/_Project/ResourcceEX/Prefabs/Traps/SpikeTrap.prefab";
        static readonly Vector3 VisualPos = new Vector3(16.5f, 9.5f, 0f);

        static int _step = -1;
        static float _stepStart, _stepStartReal;
        static double _wallStart;
        static ArrowShooter _a, _v;
        static GameObject _hammer, _spike;
        static Canvas _canvas, _vCanvas;
        static Image _fill;
        static float _prev;
        static readonly List<float> _resets = new List<float>();
        static bool _anyVis, _first; static float _firstP; static bool _firstVis;
        static int _verifFrames, _verifVisFrames, _restarts;
        static float _wantScale = 1f;

        /// <summary>
        /// 배속 측정 중 timeScale 유지. VerificationSpeedController 가 페이즈 이벤트마다 timeScale 을
        /// 기본값으로 되돌리므로, 덮어써졌으면 다시 걸고 그 구간 측정은 버린다(오염 방지).
        /// </summary>
        static void HoldScale()
        {
            if (Mathf.Abs(Time.timeScale - _wantScale) < 0.01f) return;
            Time.timeScale = _wantScale; _resets.Clear(); _restarts++;
        }
        static bool _shot;
        static Color _lastColor;
        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors = new List<string>();
        static int _fail;
        static string _out, _dir;

        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "probe9b.txt");
            _dir = Path.GetDirectoryName(_out);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Application.logMessageReceived += OnLog;
            _wallStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// 콘솔 Error·Exception 수집. 알려진 무해 오류(에디터 검색 인덱서의 배치모드 예외)만 제외하고,
        /// 나머지는 하나라도 있으면 Finish 에서 FAIL 처리한다 — 게임 코드 예외를 놓치지 않기 위해.
        /// </summary>
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
            Debug.Log($"[PROBE9B] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static void Next() { _step++; _stepStart = Time.time; _stepStartReal = Time.unscaledTime; _resets.Clear(); _anyVis = false; _first = false; _shot = false; _verifFrames = _verifVisFrames = 0; _restarts = 0; }

        static void Sample(bool unscaled = false)
        {
            float p = _a.CycleProgress01;
            bool vis = _canvas.enabled;
            if (!_first) { _first = true; _firstP = p; _firstVis = vis; }
            if (p < _prev - 0.5f)
            {
                // 자연 발사(가득 참→0)만 주기로 센다. 주기 중간에서 0 으로 떨어지면 페이즈 전환 등으로
                // 발사 루틴이 재시작된 것 → 그 이전 측정은 버리고 다시 센다 (프로브 측정 오염 방지)
                if (_prev >= 0.85f) _resets.Add(unscaled ? Time.unscaledTime : Time.time);
                else { _resets.Clear(); _restarts++; }
            }
            _prev = p;
            if (vis) _anyVis = true;
            if (GamePhaseManager.Instance.currentPhase == GamePhase.Verification) { _verifFrames++; if (vis) _verifVisFrames++; }
            _lastColor = _fill.color;
        }

        static float Period() => _resets.Count < 2 ? -1f : (_resets[_resets.Count - 1] - _resets[0]) / (_resets.Count - 1);

        // ── 픽셀 가시성: 게이지 on/off 두 번 렌더해 차이로 게이지 영역을 찾는다 ──
        static void Shot(string tag)
        {
            var cam = Camera.main;
            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24);
            var prevTarget = cam.targetTexture;
            Texture2D Render()
            {
                Canvas.ForceUpdateCanvases();
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(W, H, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply();
                RenderTexture.active = null; return t;
            }
            var on = Render();
            _vCanvas.enabled = false; bool aWas = _canvas.enabled; _canvas.enabled = false;
            var off = Render();
            _vCanvas.enabled = true; _canvas.enabled = aWas;
            cam.targetTexture = prevTarget;
            File.WriteAllBytes(Path.Combine(_dir, $"probe9b_{tag}.png"), on.EncodeToPNG());

            // 기대 중심 (RT 좌표)
            Vector3 c = cam.WorldToViewportPoint(_vCanvas.transform.position);
            int cx = Mathf.RoundToInt(c.x * W), cy = Mathf.RoundToInt(c.y * H);
            Vector3 cellTop = cam.WorldToViewportPoint(_v.transform.position + new Vector3(0, 0.5f, 0));
            int cellTopY = Mathf.RoundToInt(cellTop.y * H);
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue, n = 0, green = 0, dark = 0;
            float lumDiffSum = 0;
            var pa = on.GetPixels(); var pb = off.GetPixels();
            for (int y = Mathf.Max(0, cy - 200); y < Mathf.Min(H, cy + 200); y++)
            for (int x = Mathf.Max(0, cx - 200); x < Mathf.Min(W, cx + 200); x++)
            {
                Color A = pa[y * W + x], B = pb[y * W + x];
                if (Mathf.Abs(A.r - B.r) + Mathf.Abs(A.g - B.g) + Mathf.Abs(A.b - B.b) < 0.08f) continue;
                n++; minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                if (A.g > 0.6f && A.g > A.r + 0.1f && A.g > A.b + 0.3f) green++;
                else if (Mathf.Abs(A.r - B.r) + Mathf.Abs(A.g - B.g) + Mathf.Abs(A.b - B.b) > 0.45f) { dark++; lumDiffSum += Lum(B) - Lum(A); } // 안티앨리어싱 가장자리 제외
            }
            int w = n > 0 ? maxX - minX + 1 : 0, h = n > 0 ? maxY - minY + 1 : 0;
            float darkDiff = dark > 0 ? lumDiffSum / dark : 0;
            Check($"[{tag}] 화면상 지름 ≥40px", w >= 40 && h >= 40, $"{w}×{h}px (cam ortho {cam.orthographicSize:F2}, 진행도 {_v.CycleProgress01:F2})");
            Check($"[{tag}] 채움 연두색", green >= 30, $"연두 픽셀 {green}개");
            Check($"[{tag}] 배경 링이 타일보다 어두움", darkDiff >= 0.3f, $"밝기 차 평균 {darkDiff:F2} (픽셀 {dark})");
            Check($"[{tag}] 링이 함정 칸 위", n > 0 && minY > cellTopY, $"링 하단 y={minY}, 칸 상단 y={cellTopY}");
            UnityEngine.Object.Destroy(on); UnityEngine.Object.Destroy(off); rt.Release();
        }

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > 150) { Check("timeout", false, "150초 초과"); Finish(); return; }
            if (_step == 99) { if (!EditorApplication.isPlaying) Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            var gpm = GamePhaseManager.Instance;
            float t = Time.time - _stepStart, tr = Time.unscaledTime - _stepStartReal;
            switch (_step)
            {
                case -1: if (gpm == null) return; _step = 0; _stepStart = Time.time; return;
                case 0:
                    if (t < 1.5f) return;
                    gpm.SetPhase(GamePhase.Build, true);
                    var arrow = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath);
                    _a = UnityEngine.Object.Instantiate(arrow, new Vector3(-80, -80, 0), Quaternion.identity).GetComponent<ArrowShooter>();
                    _v = UnityEngine.Object.Instantiate(arrow, VisualPos, Quaternion.identity).GetComponent<ArrowShooter>();
                    _hammer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HammerPath), new Vector3(-90, -80, 0), Quaternion.identity);
                    _spike = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SpikePath), new Vector3(-100, -80, 0), Quaternion.identity);
                    Next(); return;
                case 1:
                    if (_canvas == null)
                    {
                        _canvas = _a.GetComponentInChildren<Canvas>(true); _vCanvas = _v.GetComponentInChildren<Canvas>(true);
                        foreach (var img in _a.GetComponentsInChildren<Image>(true)) if (img.type == Image.Type.Filled) _fill = img;
                        Check("게이지 구성", _canvas != null && _fill != null && _vCanvas != null, $"layer={_canvas?.sortingLayerName} order={_canvas?.sortingOrder}");
                    }
                    Sample();
                    if (t < 1.5f) return;
                    Check("Build 에서 숨김", !_anyVis && _a.IsCycleActive, $"visible={_anyVis} cycleActive={_a.IsCycleActive}");
                    gpm.SetPhase(GamePhase.Play, true); _a.Mutate(TrapState.Normal); _v.Mutate(TrapState.Normal);
                    Next(); return;
                case 2: // Play Normal + 스크린샷
                    Sample();
                    if (t < 0.05f) { var cp = Camera.main.transform.position; _v.transform.position = new Vector3(Mathf.Floor(cp.x) + 2.5f, Mathf.Floor(cp.y) + 0.5f, 0f); } // Play 카메라(플레이어 추적) 화면 안으로
                    if (!_shot && _v.CycleProgress01 > 0.55f && t > 1.2f) { _shot = true; Shot("play"); }
                    if (t < 3.3f) return;
                    Check("Play 첫 대기부터 표시(QA-8)", _firstVis && _firstP < 0.15f, $"visible={_firstVis} progress={_firstP:F2}");
                    Check("Play·Normal 주기 1초", Mathf.Abs(Period() - 1f) < 0.12f, $"리셋 {_resets.Count}회 평균 {Period():F3}s");
                    Check("Play·Normal 연두", _lastColor.g > 0.9f && _lastColor.r < 0.7f && _lastColor.b < 0.4f, $"color={_lastColor}");
                    _a.Mutate(TrapState.Critical); Next(); return;
                case 3:
                    Sample(); if (t < 2.3f) return;
                    Check("Critical 0.5초·빨강", Mathf.Abs(Period() - 0.5f) < 0.08f && _lastColor.r > 0.8f && _lastColor.g < 0.5f, $"주기 {Period():F3}s color={_lastColor}");
                    _a.Mutate(TrapState.Beneficial); Next(); return;
                case 4:
                    Sample(); if (t < 2.3f) return;
                    Check("Beneficial 1초·금색", _anyVis && Mathf.Abs(Period() - 1f) < 0.12f && _lastColor.b < 0.5f && _lastColor.g > 0.5f && _lastColor.r > 0.8f, $"주기 {Period():F3}s color={_lastColor}");
                    _a.Mutate(TrapState.Dud); Next(); return;
                case 5:
                    Sample(); if (t < 1.2f) return;
                    Check("Play·Dud 숨김", !_anyVis && !_a.IsCycleActive, $"visible={_anyVis}");
                    _a.Mutate(TrapState.Normal); Next(); return;
                case 6:
                    Sample(); if (t < 0.6f) return;
                    Check("재시작 시 0부터", _firstVis && _firstP < 0.15f, $"progress={_firstP:F2}");
                    _v.transform.position = VisualPos; gpm.SetPhase(GamePhase.Verification, true); Next(); return;
                case 7: // Verification 표시 + 스크린샷 + 1x 주기
                    Sample();
                    if (!_shot && _v.CycleProgress01 > 0.55f && t > 1.2f && gpm.currentPhase == GamePhase.Verification) { _shot = true; Shot("defense"); }
                    if (t < 3.3f) return;
                    Check("디펜스(Verification) 에서 표시", _verifFrames > 0 && _verifVisFrames >= _verifFrames - 2, $"Verification 프레임 {_verifFrames} 중 표시 {_verifVisFrames} (현재 페이즈 {gpm.currentPhase})");
                    Check("디펜스 연두(전부 Normal)", _lastColor.g > 0.9f && _lastColor.r < 0.7f, $"color={_lastColor}");
                    Check("디펜스 1x 주기 1초", Mathf.Abs(Period() - 1f) < 0.12f, $"평균 {Period():F3}s");
                    _wantScale = 2f; Time.timeScale = 2f; Next(); return;
                case 8:
                    HoldScale(); Sample(true); if (tr < 2.2f || (_resets.Count < 3 && tr < 6f)) return; // 버린 만큼 더 잰다
                    Check("배속 2x → 실시간 주기 0.5초", Mathf.Abs(Period() - 0.5f) < 0.07f, $"리셋 {_resets.Count}회 평균 {Period():F3}s(실시간), 재시작·배속 덮어쓰기 {_restarts}회 제외");
                    _wantScale = 4f; Time.timeScale = 4f; Next(); return;
                case 9:
                    HoldScale(); Sample(true); if (tr < 1.6f || (_resets.Count < 4 && tr < 6f)) return;
                    Check("배속 4x → 실시간 주기 0.25초", Mathf.Abs(Period() - 0.25f) < 0.05f, $"리셋 {_resets.Count}회 평균 {Period():F3}s(실시간), 재시작·배속 덮어쓰기 {_restarts}회 제외");
                    _wantScale = 1f; Time.timeScale = 1f;
                    if (gpm.currentPhase != GamePhase.Verification) gpm.SetPhase(GamePhase.Verification, true);
                    _a.Mutate(TrapState.Dud); Next(); return;
                case 10:
                    Sample(); if (t < 0.8f) return;
                    Check("디펜스 중 Dud → 숨김", !_anyVis, $"visible={_anyVis}");
                    _a.Mutate(TrapState.Normal); gpm.SetPhase(GamePhase.Build, true);
                    Check("디펜스→Build 즉시 숨김", !_canvas.enabled, $"enabled={_canvas.enabled}");
                    Check("DropHammer·SpikeTrap 게이지 없음", _hammer.GetComponentInChildren<TrapCooldownGauge>(true) == null && _spike.GetComponentInChildren<TrapCooldownGauge>(true) == null, "범위 밖");
                    Check("IsVisiblePhase 순수 함수", !TrapCooldownGauge.IsVisiblePhase(GamePhase.Build) && TrapCooldownGauge.IsVisiblePhase(GamePhase.Verification) && TrapCooldownGauge.IsVisiblePhase(GamePhase.Play), "Build=F Verif=T Play=T");
                    _step = 99; EditorApplication.ExitPlaymode(); return;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
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
