using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  SpecProbe16 — SPEC-16 메타 시스템(설정 패널) 배치모드 Play 검증 (AC1~AC10, AC15). 임시, 커밋하지 않음
    //  AC11·AC12 는 EditMode, AC13 수동, AC14 는 스모크+SpecProbe17 — 범위 밖.
    //  씬 파일에는 설정 패널이 없으므로 Play 진입 전에 MetaUiSetup.BuildSettingsPanel 로 메모리상에만 만든다(씬 저장 금지).
    // ═══════════════════════════════════════════════════════════════
    public static class SpecProbe16
    {
        const string ScenePath = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const string MixerPath = "Assets/_Project/ResourcceEX/Audio/ReTrapMixer.mixer";

        // 설계 확정 기준값
        const float PanelW = 560f, PanelH = 380f;     // 기준 해상도(1920x1080) 단위
        const float To720 = 720f / 1080f;             // 1080 기준 → 720p 환산 ×0.667
        const float MinClick720 = 16f, MinText720 = 12f;

        static readonly string[] FloatKeys = { "ReTrap.Settings.MasterVolume", "ReTrap.Settings.BgmVolume", "ReTrap.Settings.SfxVolume" };
        static readonly string[] IntKeys = { "ReTrap.Settings.ResWidth", "ReTrap.Settings.ResHeight", "ReTrap.Settings.Fullscreen" };
        static readonly Dictionary<string, float> _bakF = new Dictionary<string, float>();
        static readonly Dictionary<string, int> _bakI = new Dictionary<string, int>();

        static int _step = -1, _stepFrame;
        static float _stepStartReal;
        static double _wallStart;
        static bool _init;

        static SettingsPanel _panel;
        static Canvas _canvas;
        static AudioMixer _mixer;
        static AudioConfig _cfg;
        static AudioClip _clip;
        static GameObject _smGo;
        static bool _openedForAc3;

        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors = new List<string>();
        static int _fail;
        static string _out, _dir;

        // ── 진입 ──
        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "probe16.txt");
            _dir = Path.GetDirectoryName(_out);
            Application.logMessageReceived += OnLog;
            _wallStart = EditorApplication.timeSinceStartup;
            try
            {
                BackupAndClearPrefs();
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var canvasGo = GameObject.Find("UICanvas");
                if (canvasGo == null) { Check("[준비] UICanvas 발견", false, "씬에 UICanvas 없음"); Finish(); return; }
                MetaUiSetup.BuildSettingsPanel(canvasGo.transform); // 메모리상 씬에만 반영, 저장하지 않음
                EditorApplication.update += Tick;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Check("[준비] Run 예외", false, e.GetType().Name + ": " + e.Message);
                Finish();
            }
        }

        static void BackupAndClearPrefs()
        {
            _bakF.Clear(); _bakI.Clear();
            foreach (var k in FloatKeys) { if (PlayerPrefs.HasKey(k)) _bakF[k] = PlayerPrefs.GetFloat(k); PlayerPrefs.DeleteKey(k); }
            foreach (var k in IntKeys) { if (PlayerPrefs.HasKey(k)) _bakI[k] = PlayerPrefs.GetInt(k); PlayerPrefs.DeleteKey(k); }
            PlayerPrefs.Save();
        }

        static void RestorePrefs()
        {
            foreach (var k in FloatKeys) PlayerPrefs.DeleteKey(k);
            foreach (var k in IntKeys) PlayerPrefs.DeleteKey(k);
            foreach (var kv in _bakF) PlayerPrefs.SetFloat(kv.Key, kv.Value);
            foreach (var kv in _bakI) PlayerPrefs.SetInt(kv.Key, kv.Value);
            PlayerPrefs.Save();
        }

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
            Debug.Log($"[PROBE16] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static void Next() { _step++; _stepStartReal = Time.unscaledTime; _stepFrame = Time.frameCount; _init = false; }

        static SoundManager S => SoundManager.Instance;
        static bool Near(float a, float b, float tol) => Mathf.Abs(a - b) <= tol;

        // ── 믹서 실측(오디오 장치가 없어 GetFloat 이 실패하면 GetAppliedDb 로 대체하고 표시) ──
        static float MixerDb(string param, SoundBus bus, out bool measured)
        {
            float v = 0f;
            measured = _mixer != null && _mixer.GetFloat(param, out v);
            return measured ? v : S.GetAppliedDb(bus);
        }

        // ── 좌표 헬퍼: RectTransform → 화면 px Rect ──
        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static bool Overlap(Rect a, Rect b) => a.xMin < b.xMax - 0.5f && b.xMin < a.xMax - 0.5f && a.yMin < b.yMax - 0.5f && b.yMin < a.yMax - 0.5f;

        static float EffectiveFont(Text t)
        {
            float size = t.resizeTextForBestFit ? t.cachedTextGenerator.fontSizeUsedForBestFit : t.fontSize;
            float k = _canvas.transform.lossyScale.x > 0f ? t.transform.lossyScale.x / _canvas.transform.lossyScale.x : 1f;
            return size * k; // 기준(1080) 단위
        }

        static bool IsOpen => _panel != null && _panel.PanelRoot != null && _panel.PanelRoot.activeSelf;

        // ── AC1 / AC2 측정 ──
        static void MeasureAc1Ac2(GamePhaseManager gpm)
        {
            var root = _panel.PanelRoot;
            float sf = _canvas.scaleFactor;
            Canvas.ForceUpdateCanvases();
            var rt = (RectTransform)root.transform;
            Rect r = ScreenRect(rt);
            bool last = root.transform.parent != null && root.transform.GetSiblingIndex() == root.transform.parent.childCount - 1;
            bool sizeOk = Near(r.width, PanelW * sf, 2f) && Near(r.height, PanelH * sf, 2f);
            Check("AC1 열림·activeInHierarchy·최상단 sibling·실측 크기",
                root.activeInHierarchy && last && sizeOk && gpm.currentPhase == GamePhase.Build,
                $"active={root.activeInHierarchy} 최상단={last} phase={gpm.currentPhase} 실측={r.width:F1}x{r.height:F1}px 기대={PanelW * sf:F1}x{PanelH * sf:F1}px(scale {sf:F3}, Screen {Screen.width}x{Screen.height}, 모드 {_canvas.renderMode})");

            // ── AC2 ──
            var items = new List<KeyValuePair<string, RectTransform>>();
            var clicks = new List<KeyValuePair<string, RectTransform>>();
            string[] bn = { "Master", "Bgm", "Sfx" };
            for (int i = 0; i < 3; i++)
            {
                var sl = _panel.GetVolumeSlider((SoundBus)i); var tx = _panel.GetVolumeText((SoundBus)i);
                if (sl != null) { clicks.Add(new KeyValuePair<string, RectTransform>("슬라이더" + bn[i], (RectTransform)sl.transform)); }
                if (tx != null) items.Add(new KeyValuePair<string, RectTransform>("퍼센트" + bn[i], (RectTransform)tx.transform));
            }
            if (_panel.ResolutionDropdown != null) clicks.Add(new KeyValuePair<string, RectTransform>("드롭다운", (RectTransform)_panel.ResolutionDropdown.transform));
            if (_panel.FullscreenToggle != null) clicks.Add(new KeyValuePair<string, RectTransform>("토글", (RectTransform)_panel.FullscreenToggle.transform));
            if (_panel.CloseButton != null) clicks.Add(new KeyValuePair<string, RectTransform>("닫기", (RectTransform)_panel.CloseButton.transform));

            var small = new List<string>(); var sizes = new StringBuilder();
            foreach (var kv in clicks)
            {
                Rect s = ScreenRect(kv.Value);
                float min720 = Mathf.Min(s.width, s.height) / sf * To720;
                sizes.Append($"{kv.Key}={s.width:F0}x{s.height:F0}px({min720:F1}@720p) ");
                if (min720 < MinClick720) small.Add(kv.Key);
            }
            Check("AC2 클릭 대상 6종 존재 + 720p 환산 ≥16px", clicks.Count == 6 && small.Count == 0,
                $"대상 {clicks.Count}/6, 미달 [{string.Join(",", small)}] {sizes}");

            // 글자: 패널 안 활성 Text 전부
            var smallTxt = new List<string>(); int nTxt = 0; float minTxt = float.MaxValue;
            foreach (var t in root.GetComponentsInChildren<Text>(false))
            {
                if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                nTxt++;
                float f720 = EffectiveFont(t) * To720; minTxt = Mathf.Min(minTxt, f720);
                if (f720 < MinText720) smallTxt.Add($"'{t.text}'={f720:F1}");
            }
            var dd = _panel.ResolutionDropdown;
            if (dd != null && dd.itemText != null)
            {
                float f720 = dd.itemText.fontSize * To720; // 템플릿(비활성)이라 위 루프에서 빠지는 드롭다운 목록 글자
                minTxt = Mathf.Min(minTxt, f720);
                if (f720 < MinText720) smallTxt.Add($"드롭다운항목={f720:F1}");
            }
            Check("AC2 글자 720p 환산 ≥12px", nTxt > 0 && smallTxt.Count == 0, $"Text {nTxt}개, 최소 {(nTxt > 0 ? minTxt : 0):F1}px@720p, 미달 [{string.Join(", ", smallTxt)}]");

            // 겹침: 클릭 대상 + 퍼센트 텍스트 + 상호작용 요소 밖 라벨 Text
            var all = new List<KeyValuePair<string, RectTransform>>(clicks); all.AddRange(items);
            foreach (var t in root.GetComponentsInChildren<Text>(false))
            {
                if (!t.gameObject.activeInHierarchy) continue;
                if (t.GetComponentInParent<Selectable>() != null) continue;
                bool dup = false; foreach (var kv in items) if (kv.Value == t.transform) dup = true;
                if (!dup) all.Add(new KeyValuePair<string, RectTransform>("라벨'" + t.text + "'", (RectTransform)t.transform));
            }
            var overlaps = new List<string>();
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    if (Overlap(ScreenRect(all[i].Value), ScreenRect(all[j].Value))) overlaps.Add(all[i].Key + "×" + all[j].Key);
            Check("AC2 요소 사각형 겹침 0", overlaps.Count == 0, $"검사 {all.Count}개, 겹침 {overlaps.Count} [{string.Join(", ", overlaps)}]");
            var outside = new List<string>();
            foreach (var kv in all) { var s = ScreenRect(kv.Value); if (s.xMin < r.xMin - 1f || s.xMax > r.xMax + 1f || s.yMin < r.yMin - 1f || s.yMax > r.yMax + 1f) outside.Add(kv.Key); }
            Check("AC2 요소가 패널 안에 위치", outside.Count == 0, $"밖으로 나간 요소 [{string.Join(", ", outside)}]");
        }

        // ── 픽셀 가시성: Game 뷰 실화면(오버레이 UI 포함)을 프레임 끝에 패널 on/off 각각 캡처해 차이 영역을 비교 ──
        // (cam.Render 는 URP 2D 에서 오버레이 UI 를 못 찍어 차이 0 이 나왔다 → ScreenCapture 로 교체)
        public class ProbeHost : MonoBehaviour { }

        static int _pixelState; // 0 미시작 · 1 진행 중 · 2 완료
        static ProbeHost _host;

        static void StartPixel()
        {
            _pixelState = 1;
            if (_host == null) { var go = new GameObject("SpecProbe16_Host"); _host = go.AddComponent<ProbeHost>(); }
            _host.StartCoroutine(PixelRoutine());
        }

        static System.Collections.IEnumerator PixelRoutine()
        {
            var root = _panel.PanelRoot;
            Texture2D on = null, off = null; string err = null;
            yield return new WaitForEndOfFrame();
            try { on = ScreenCapture.CaptureScreenshotAsTexture(); } catch (Exception e) { err = e.Message; }
            root.SetActive(false);
            yield return null;
            yield return new WaitForEndOfFrame();
            try { off = ScreenCapture.CaptureScreenshotAsTexture(); } catch (Exception e) { err = e.Message; }
            root.SetActive(true);
            try
            {
                if (on == null || off == null || on.width != off.width || on.height != off.height) { Check("AC1 픽셀: 패널 on/off 차이 영역", false, "캡처 실패: " + err); }
                else
                {
                    int W = on.width, H = on.height; float sf = _canvas.scaleFactor;
                    File.WriteAllBytes(Path.Combine(_dir, "probe16_panel.png"), on.EncodeToPNG());
                    Rect er = ScreenRect((RectTransform)root.transform); // 실측 위치 주변만 비교(다른 게임 움직임 배제)
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(er.xMin) - 30), x1 = Mathf.Min(W, Mathf.CeilToInt(er.xMax) + 30);
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(er.yMin) - 30), y1 = Mathf.Min(H, Mathf.CeilToInt(er.yMax) + 30);
                    var pa = on.GetPixels(); var pb = off.GetPixels();
                    int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue, n = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            Color A = pa[y * W + x], B = pb[y * W + x];
                            if (Mathf.Abs(A.r - B.r) + Mathf.Abs(A.g - B.g) + Mathf.Abs(A.b - B.b) < 0.08f) continue;
                            n++; minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                        }
                    int w = n > 0 ? maxX - minX + 1 : 0, h = n > 0 ? maxY - minY + 1 : 0;
                    float ew = PanelW * sf, eh = PanelH * sf;
                    Check("AC1 픽셀: 패널 on/off 차이 영역 ≈ 560x380(×스케일)", n > 0 && Near(w, ew, 4f) && Near(h, eh, 4f),
                        $"차이 영역 {w}x{h}px (차이 픽셀 {n}) 기대 {ew:F0}x{eh:F0} ±4 (캡처 {W}x{H}, scale {sf:F3}, ScreenCapture)");
                }
            }
            catch (Exception e) { Check("AC1 픽셀: 패널 on/off 차이 영역", false, "예외: " + e.Message); }
            finally
            {
                if (on != null) UnityEngine.Object.Destroy(on);
                if (off != null) UnityEngine.Object.Destroy(off);
                _pixelState = 2;
            }
        }

        static AudioClip Silent(string n, float sec) => AudioClip.Create(n, Mathf.CeilToInt(44100 * sec), 1, 44100, false);

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > 150) { Check("timeout", false, "150초 초과 (step " + _step + ")"); Finish(); return; }
            if (_step == 99) { if (!EditorApplication.isPlaying) Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            var gpm = GamePhaseManager.Instance;
            if (Mathf.Abs(Time.timeScale - 1f) > 0.01f) Time.timeScale = 1f; // VerificationSpeedController 배속 오염 방지(타이밍 판정이 없어 측정은 그대로 유효)
            float tr = Time.unscaledTime - _stepStartReal;
            int frames = Time.frameCount - _stepFrame;
            var sm = S;
            switch (_step)
            {
                case -1: if (gpm == null) return; _step = 0; _stepStartReal = Time.unscaledTime; _stepFrame = Time.frameCount; return;

                case 0: // 준비: Build 페이즈, SoundManager(없으면 생성), 기본값 AudioConfig
                    if (tr < 1.5f) return;
                    gpm.SetPhase(GamePhase.Build, true);
                    _mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                    _clip = Silent("p16_bgm", 3f);
                    _cfg = ScriptableObject.CreateInstance<AudioConfig>();
                    _cfg.Configure(_mixer, 1.0f, 0.6f, 0.8f, _clip, _clip, _clip, 1.0f, 8, 15f, AudioConfig.DefaultEntries());
                    if (SoundManager.Instance == null) { _smGo = new GameObject("SpecProbe16_SoundManager"); _smGo.AddComponent<SoundManager>(); }
                    Next(); return;

                case 1: // SoundManager Start 이후(믹서 SetFloat 유효) 기본 설정 적용, 패널 확보
                    if (sm == null || tr < 0.5f) return;
                    sm.ApplyConfig(_cfg);
                    _panel = UnityEngine.Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
                    bool ok = _panel != null && _panel.PanelRoot != null;
                    if (ok) _canvas = _panel.PanelRoot.GetComponentInParent<Canvas>().rootCanvas;
                    Check("[준비] 설정 패널 구성·초기 닫힘", ok && _canvas != null && !_panel.PanelRoot.activeSelf && _panel.PanelRoot.name == "SettingsPanel"
                        && _panel.GetVolumeSlider(SoundBus.Master) != null && _panel.GetVolumeSlider(SoundBus.Bgm) != null && _panel.GetVolumeSlider(SoundBus.Sfx) != null
                        && _panel.ResolutionDropdown != null && _panel.FullscreenToggle != null && _panel.CloseButton != null,
                        ok ? $"name={_panel.PanelRoot.name} activeSelf={_panel.PanelRoot.activeSelf}" : "SettingsPanel/PanelRoot 없음");
                    if (!ok || _canvas == null) { _step = 99; EditorApplication.ExitPlaymode(); return; }
                    Next(); return;

                case 2: // AC1: Build 에서 TogglePanel
                    if (tr < 0.3f) return;
                    gpm.SetPhase(GamePhase.Build, true);
                    _panel.TogglePanel();
                    Next(); return;

                case 3: // 레이아웃 반영 후 AC1·AC2·AC5·픽셀
                    if (_pixelState == 1) return;
                    if (_pixelState == 2) { Next(); return; }
                    if (frames < 3 || tr < 0.3f) return;
                    MeasureAc1Ac2(gpm);
                    {
                        // AC5: 저장값 없음 → 기본값
                        int vm = Mathf.RoundToInt(_panel.GetVolumeSlider(SoundBus.Master).value), vb = Mathf.RoundToInt(_panel.GetVolumeSlider(SoundBus.Bgm).value), vs = Mathf.RoundToInt(_panel.GetVolumeSlider(SoundBus.Sfx).value);
                        float dm = MixerDb("MasterVolume", SoundBus.Master, out bool m1), db = MixerDb("BgmVolume", SoundBus.Bgm, out bool m2), ds = MixerDb("SfxVolume", SoundBus.Sfx, out bool m3);
                        string note = (m1 && m2 && m3) ? "" : " (오디오 비활성: mixer.GetFloat 실패 → GetAppliedDb 대체)";
                        Check("AC5 저장값 없음 → 슬라이더 20/12/16", vm == 20 && vb == 12 && vs == 16, $"slider={vm}/{vb}/{vs}");
                        Check("AC5 저장값 없음 → dB 0/-4.44/-1.94 (±0.1)", Near(dm, 0f, 0.1f) && Near(db, -4.44f, 0.1f) && Near(ds, -1.94f, 0.1f), $"master={dm:F2} bgm={db:F2} sfx={ds:F2}{note}");
                    }
                    StartPixel(); return;

                case 4: // AC6: BGM 슬라이더 10 (UI 이벤트 경로)
                    if (!_init) { _init = true; _panel.GetVolumeSlider(SoundBus.Bgm).value = 10f; return; }
                    if (frames < 2) return;
                    {
                        float db = MixerDb("BgmVolume", SoundBus.Bgm, out bool m);
                        float pref = PlayerPrefs.GetFloat("ReTrap.Settings.BgmVolume", -1f);
                        string txt = _panel.GetVolumeText(SoundBus.Bgm).text.Trim();
                        Check("AC6 BGM 슬라이더 10 → 믹서 -6.02 dB", Near(db, -6.02f, 0.1f), $"bgm={db:F2}dB{(m ? "" : " (GetAppliedDb 대체)")}");
                        Check("AC6 BGM 슬라이더 10 → PlayerPrefs 0.5", PlayerPrefs.HasKey("ReTrap.Settings.BgmVolume") && Near(pref, 0.5f, 0.001f), $"pref={pref:F4}");
                        Check("AC6 BGM 슬라이더 10 → 퍼센트 텍스트 \"50%\"", txt == "50%", $"text='{txt}'");
                    }
                    Next(); return;

                case 5: // AC7: 효과음 0
                    if (!_init) { _init = true; _panel.GetVolumeSlider(SoundBus.Sfx).value = 0f; return; }
                    if (frames < 2) return;
                    {
                        float d = sm.GetAppliedDb(SoundBus.Sfx);
                        Check("AC7 효과음 슬라이더 0 → -80 dB", Near(d, -80f, 0.1f), $"sfx={d:F2}dB");
                    }
                    Next(); return;

                case 6: // AC8: Bgm 0.5 저장 상태에서 ApplyConfig 재호출
                    if (!_init)
                    {
                        _init = true;
                        bool saved = Near(PlayerPrefs.GetFloat("ReTrap.Settings.BgmVolume", -1f), 0.5f, 0.001f);
                        if (!saved) PlayerPrefs.SetFloat("ReTrap.Settings.BgmVolume", 0.5f); // AC6 이 실패해도 AC8 은 독립 판정
                        sm.ApplyConfig(_cfg);
                        return;
                    }
                    if (frames < 2) return;
                    {
                        float d = sm.GetAppliedDb(SoundBus.Bgm);
                        Check("AC8 Bgm 0.5 저장 후 ApplyConfig 재호출 → 유지 -6.02 dB", Near(d, -6.02f, 0.1f), $"bgm={d:F2}dB (-4.44 이면 저장값을 덮어쓴 것)");
                    }
                    Next(); return;

                case 7: // AC10: 1280x720 + 창모드
                    if (!_init)
                    {
                        _init = true;
                        var dd = _panel.ResolutionDropdown; var tg = _panel.FullscreenToggle;
                        int idx = -1; var opts = new List<string>();
                        for (int i = 0; i < dd.options.Count; i++)
                        {
                            opts.Add(dd.options[i].text);
                            if (idx < 0 && Regex.IsMatch(dd.options[i].text, @"1280\s*[xX×]\s*720")) idx = i;
                        }
                        if (idx < 0)
                        {
                            Check("AC10 1280x720 + 창모드 → ResWidth/ResHeight/Fullscreen = 1280/720/0", false, "해상도 드롭다운에 1280x720 항목 없음(batchmode 모니터 크기 의존 가능). 옵션: " + string.Join(" | ", opts));
                            Next(); _step = 8; return;
                        }
                        if (dd.value == idx && dd.options.Count > 1) dd.value = (idx + 1) % dd.options.Count; // 값 변경 이벤트가 확실히 발생하도록
                        tg.isOn = false;
                        dd.value = idx;
                        return;
                    }
                    if (frames < 2) return;
                    {
                        bool has = PlayerPrefs.HasKey("ReTrap.Settings.ResWidth") && PlayerPrefs.HasKey("ReTrap.Settings.ResHeight") && PlayerPrefs.HasKey("ReTrap.Settings.Fullscreen");
                        int w = PlayerPrefs.GetInt("ReTrap.Settings.ResWidth", -1), h = PlayerPrefs.GetInt("ReTrap.Settings.ResHeight", -1), f = PlayerPrefs.GetInt("ReTrap.Settings.Fullscreen", -1);
                        Check("AC10 1280x720 + 창모드 → ResWidth/ResHeight/Fullscreen = 1280/720/0", has && w == 1280 && h == 720 && f == 0, $"{w}x{h} fullscreen={f} (키 존재 {has})");
                    }
                    Next(); return;

                case 8: // AC9: 닫기 버튼 → 저장
                    if (!_init) { _init = true; _panel.CloseButton.onClick.Invoke(); return; }
                    if (frames < 2) return;
                    {
                        bool a = PlayerPrefs.HasKey("ReTrap.Settings.MasterVolume"), b = PlayerPrefs.HasKey("ReTrap.Settings.BgmVolume"), c = PlayerPrefs.HasKey("ReTrap.Settings.SfxVolume");
                        Check("AC9 닫으면 저장 — 볼륨 3키 HasKey", a && b && c && !IsOpen, $"Master={a} Bgm={b} Sfx={c} 닫힘={!IsOpen} (디스크 기록 자체는 PlayerPrefs.Save 호출 여부를 관찰할 수 없어 키 존재로 판정)");
                    }
                    gpm.SetPhase(GamePhase.Build, true);
                    Next(); return;

                case 9: // AC3 준비: Build 에서 열기
                    if (tr < 0.2f) return;
                    _panel.TogglePanel();
                    Next(); return;

                case 10: // 열림 확인 → Verification 전환
                    if (frames < 2) return;
                    _openedForAc3 = IsOpen;
                    gpm.SetPhase(GamePhase.Verification, true);
                    Next(); return;

                case 11: // AC3 (Verification)
                    if (frames < 2 || tr < 0.1f) return;
                    Check("AC3 열린 채 Verification 전환 → 자동 닫힘", _openedForAc3 && !IsOpen, $"전환 전 열림={_openedForAc3} 전환 후 activeSelf={IsOpen} phase={gpm.currentPhase}");
                    gpm.SetPhase(GamePhase.Build, true);
                    Next(); return;

                case 12:
                    if (tr < 0.2f) return;
                    _panel.TogglePanel();
                    Next(); return;

                case 13:
                    if (frames < 2) return;
                    _openedForAc3 = IsOpen;
                    gpm.SetPhase(GamePhase.Play, true);
                    Next(); return;

                case 14: // AC3 (Play)
                    if (frames < 2 || tr < 0.1f) return;
                    Check("AC3 열린 채 Play 전환 → 자동 닫힘", _openedForAc3 && !IsOpen, $"전환 전 열림={_openedForAc3} 전환 후 activeSelf={IsOpen} phase={gpm.currentPhase}");
                    if (gpm.currentPhase != GamePhase.Play) gpm.SetPhase(GamePhase.Play, true);
                    _panel.TogglePanel(); // AC4
                    Next(); return;

                case 15: // AC4
                    if (frames < 2) return;
                    Check("AC4 Play 에서 TogglePanel → 열리지 않음", !IsOpen && gpm.currentPhase == GamePhase.Play, $"activeSelf={IsOpen} phase={gpm.currentPhase}");
                    _step = 99; EditorApplication.ExitPlaymode(); return;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
            RestorePrefs();
            // AC15: 복원 결과가 백업과 일치
            var diffs = new List<string>();
            foreach (var k in FloatKeys)
            {
                bool had = _bakF.TryGetValue(k, out float bv), has = PlayerPrefs.HasKey(k);
                if (had != has || (had && !Mathf.Approximately(bv, PlayerPrefs.GetFloat(k)))) diffs.Add(k);
            }
            foreach (var k in IntKeys)
            {
                bool had = _bakI.TryGetValue(k, out int bv), has = PlayerPrefs.HasKey(k);
                if (had != has || (had && bv != PlayerPrefs.GetInt(k))) diffs.Add(k);
            }
            Check("AC15 종료 후 PlayerPrefs 원복", diffs.Count == 0, $"백업 {_bakF.Count + _bakI.Count}키, 불일치 [{string.Join(",", diffs)}]");
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
