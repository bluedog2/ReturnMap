using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════
    //  SpecProbe17 — SPEC-17 사운드 v1 배치모드 Play 검증 (AC1~AC12). 임시, 커밋하지 않음
    //  AC13(스모크)·AC14(수동)은 범위 밖. 판정은 SoundManager 관찰 프로퍼티(카운터 증가분)만 사용한다
    //  (배치 환경에 오디오 장치가 없을 수 있어 isPlaying 등 엔진 재생 상태는 쓰지 않는다).
    // ═══════════════════════════════════════════════════════════════
    public static class SpecProbe17
    {
        const string ScenePath = "Assets/_Project/ResourcceEX/Scenes/SampleScene.unity";
        const string ArrowPath = "Assets/_Project/ResourcceEX/Prefabs/Traps/ArrowShooter.prefab";
        const string MixerPath = "Assets/_Project/ResourcceEX/Audio/ReTrapMixer.mixer";

        static int _step = -1;
        static float _stepStartReal;
        static double _wallStart;
        static bool _init, _armed, _fb;
        static float _tSeen, _fbT;
        static int _base, _act0, _dmg0, _steal0, _k;
        static float _tLast;

        static AudioMixer _mixer;
        static AudioClip _sfxClip, _bgmBuild, _bgmVerif, _bgmPlay;
        static AudioConfig _cfgMain, _cfgNoVerif, _cfg8, _cfgNoMixer, _cfg12;
        static GameObject _smGo, _arrowPrefab;
        static ArrowShooter _near, _far;
        static int _nearAct, _farAct, _dudAct, _otherAct, _dmg;
        static PlayerHealth _ph;
        static bool _subscribed;

        static readonly List<string> _results = new List<string>();
        static readonly List<string> _errors = new List<string>();
        static int _fail;
        static string _out, _dir;

        public static void Run()
        {
            _out = Arg("-probeOutput") ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "probe17.txt");
            _dir = Path.GetDirectoryName(_out);
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
            if (t != LogType.Error && t != LogType.Exception) return;
            if (s != null && s.Contains("UnityEditor.Search.")) return; // SearchInit.IndexationOnStartup (무해)
            if (_errors.Count < 30) _errors.Add($"[{t}] {m.Split('\n')[0]}");
        }

        static string Arg(string n) { var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

        static void Check(string name, bool ok, string detail)
        {
            if (!ok) _fail++;
            _results.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
            Debug.Log($"[PROBE17] {(ok ? "PASS" : "FAIL")} {name} — {detail}");
        }

        static void Next() { _step++; _stepStartReal = Time.unscaledTime; _init = false; _armed = false; _fb = false; _k = 0; }

        static SoundManager S => SoundManager.Instance;

        static void OnAct(TrapBase t)
        {
            if (t == _near) _nearAct++;
            else if (t == _far) _farAct++;
            else _otherAct++;
        }
        static void OnDmg(PlayerHealth p, int a) { _dmg++; }

        // ── 구성 헬퍼 ──
        static AudioClip Silent(string n, float sec) => AudioClip.Create(n, Mathf.CeilToInt(44100 * sec), 1, 44100, false);

        static SfxEntry E(SfxId id, int prio, int maxInst, float minInt) => new SfxEntry
        {
            id = id, clips = new[] { _sfxClip }, volume = 1f, pitchMin = 1f, pitchMax = 1f,
            priority = prio, maxInstances = maxInst, minInterval = minInt
        };

        static AudioConfig MakeCfg(AudioMixer mixer, AudioClip verif, int maxVoices, SfxEntry[] entries)
        {
            var c = ScriptableObject.CreateInstance<AudioConfig>();
            c.Configure(mixer, 1.0f, 0.6f, 0.8f, _bgmBuild, verif, _bgmPlay, 1.0f, maxVoices, 15f, entries);
            return c;
        }

        static Vector3 ListenerPos()
        {
            var l = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
            if (l != null) return l.transform.position;
            return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }

        static void Place(ArrowShooter s, float dist)
        {
            if (s == null) return;
            var p = ListenerPos(); s.transform.position = new Vector3(p.x + dist, p.y, 0f);
        }

        static ArrowShooter Spawn(float dist)
        {
            var p = ListenerPos();
            var go = UnityEngine.Object.Instantiate(_arrowPrefab, new Vector3(p.x + dist, p.y, 0f), Quaternion.identity);
            return go.GetComponent<ArrowShooter>();
        }

        static void Arm(int actNow)
        {
            _armed = true; _base = S.SfxPlayCount; _act0 = actNow; _dmg0 = _dmg; _tSeen = -1f;
        }

        /// <summary>활성 관측 후 0.2초(실시간) 뒤 true. cap 초과 시에도 true(timedOut).</summary>
        static bool WaitAct(int actNow, float tr, float cap, out bool timedOut)
        {
            timedOut = false;
            if (actNow - _act0 >= 1)
            {
                if (_tSeen < 0f) _tSeen = tr;
                return tr - _tSeen >= 0.2f;
            }
            if (tr >= cap) { timedOut = true; return true; }
            return false;
        }

        static long Measure(SoundManager sm)
        {
            long a = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) sm.PlaySfx(SfxId.TrapActivate);
            long b = GC.GetAllocatedBytesForCurrentThread();
            return b - a;
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - _wallStart > 180) { Check("timeout", false, "180초 초과 (step " + _step + ")"); Finish(); return; }
            if (_step == 99) { if (!EditorApplication.isPlaying) Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            var gpm = GamePhaseManager.Instance;
            if (Mathf.Abs(Time.timeScale - 1f) > 0.01f) Time.timeScale = 1f; // VerificationSpeedController 가 배속을 걸어도 측정 오염 방지
            float tr = Time.unscaledTime - _stepStartReal;
            var sm = S;
            switch (_step)
            {
                case -1: if (gpm == null) return; _step = 0; _stepStartReal = Time.unscaledTime; return;

                case 0: // 준비: 믹서·클립·설정·SoundManager
                    if (tr < 1.5f) return;
                    gpm.SetPhase(GamePhase.Build, true);
                    _mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                    _arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath);
                    _sfxClip = Silent("p17_sfx", 1f);
                    _bgmBuild = Silent("p17_build", 3f); _bgmVerif = Silent("p17_verif", 3f); _bgmPlay = Silent("p17_play", 3f);
                    var entries = new[] { E(SfxId.TrapActivate, 100, 3, 0.06f), E(SfxId.PlayerHit, 200, 3, 0f) };
                    _cfgMain = MakeCfg(_mixer, _bgmVerif, 8, entries);
                    _cfgNoVerif = MakeCfg(_mixer, null, 8, entries);
                    _cfg8 = MakeCfg(_mixer, _bgmVerif, 8, new[] { E(SfxId.TrapActivate, 100, 99, 0f), E(SfxId.PlayerHit, 200, 3, 0f) });
                    _cfg12 = MakeCfg(_mixer, _bgmVerif, 8, new[] { E(SfxId.TrapActivate, 100, 99, 0f), E(SfxId.PlayerHit, 200, 3, 0f) });
                    _cfgNoMixer = MakeCfg(null, _bgmVerif, 8, entries);
                    if (SoundManager.Instance == null) _smGo = new GameObject("SpecProbe17_SoundManager");
                    if (_smGo != null) _smGo.AddComponent<SoundManager>();
                    if (!_subscribed) { TrapBase.OnTrapActivated += OnAct; PlayerHealth.OnAnyDamageTaken += OnDmg; _subscribed = true; }
                    Next(); return;

                case 1: // SoundManager Start 대기(수 프레임) 후 설정 적용 — Build 페이즈에서
                    if (sm == null || tr < 0.4f) return;
                    sm.ApplyConfig(_cfgMain);
                    Next(); return;

                case 2: // AC1 (a)
                    if (tr < 0.2f) return;
                    Check("AC1 시작(Build) BGM = buildBgm", sm.CurrentBgmClip == _bgmBuild, $"clip={(sm.CurrentBgmClip != null ? sm.CurrentBgmClip.name : "null")} phase={gpm.currentPhase}");
                    Next(); return;

                case 3: // AC1 (b) + AC10 + 이후 Play 전환
                    if (tr < 1.5f) return;
                    Check("AC1 1.5초 후 가중치 ≥ 0.99", sm.CurrentBgmWeight >= 0.99f, $"weight={sm.CurrentBgmWeight:F3}");
                    {
                        bool m1 = _mixer != null;
                        int gb = m1 ? _mixer.FindMatchingGroups("BGM").Length : -1, gs = m1 ? _mixer.FindMatchingGroups("SFX").Length : -1;
                        Check("AC10-M1 믹서 로드·그룹·바인딩", m1 && gb == 1 && gs == 1 && sm.MixerBound, $"mixer={(m1 ? "ok" : "null")} BGM그룹={gb} SFX그룹={gs} MixerBound={sm.MixerBound}");
                        float vm = 0, vb = 0, vs = 0; bool got = m1 && _mixer.GetFloat("MasterVolume", out vm) & _mixer.GetFloat("BgmVolume", out vb) & _mixer.GetFloat("SfxVolume", out vs);
                        string note = "";
                        if (!got) { vm = sm.GetAppliedDb(SoundBus.Master); vb = sm.GetAppliedDb(SoundBus.Bgm); vs = sm.GetAppliedDb(SoundBus.Sfx); note = " (오디오 비활성: GetFloat 실패 → GetAppliedDb 대체)"; }
                        Check("AC10-M2 dB 0 / -4.44 / -1.94 (±0.1)", Mathf.Abs(vm - 0f) <= 0.1f && Mathf.Abs(vb + 4.44f) <= 0.1f && Mathf.Abs(vs + 1.94f) <= 0.1f, $"master={vm:F2} bgm={vb:F2} sfx={vs:F2}{note}");
                        sm.SetBusVolume(SoundBus.Sfx, 0f);
                        float d0 = sm.GetAppliedDb(SoundBus.Sfx);
                        Check("AC10-M3 SetBusVolume(Sfx,0) → -80dB", Mathf.Abs(d0 + 80f) <= 0.1f, $"sfx={d0:F2}");
                        sm.SetBusVolume(SoundBus.Sfx, 0.8f);
                    }
                    gpm.SetPhase(GamePhase.Play, true);
                    Next(); return;

                case 4: // AC2 (a): 전환 직후(다음 틱) 크로스페이드 중
                    Check("AC2 Build→Play 직후 IsCrossfading", sm.IsCrossfading, $"crossfading={sm.IsCrossfading} phase={gpm.currentPhase} (경과 {tr:F2}s)");
                    Next(); return;

                case 5: // AC2 (b)
                    if (tr < 1.5f) return;
                    Check("AC2 1.5초 후 크로스페이드 종료·playBgm", !sm.IsCrossfading && sm.CurrentBgmClip == _bgmPlay, $"crossfading={sm.IsCrossfading} clip={(sm.CurrentBgmClip != null ? sm.CurrentBgmClip.name : "null")} phase={gpm.currentPhase}");
                    gpm.SetPhase(GamePhase.Verification, true);
                    Next(); return;

                case 6: // AC3 (a)
                    if (tr < 0.2f) return;
                    Check("AC3 Verification → verificationBgm", sm.CurrentBgmClip == _bgmVerif, $"clip={(sm.CurrentBgmClip != null ? sm.CurrentBgmClip.name : "null")} phase={gpm.currentPhase}");
                    gpm.SetPhase(GamePhase.Play, true);
                    Next(); return;

                case 7:
                    if (tr < 1.5f) return;
                    sm.ApplyConfig(_cfgNoVerif);
                    gpm.SetPhase(GamePhase.Verification, true);
                    Next(); return;

                case 8: // AC3 (b)
                    if (tr < 0.2f) return;
                    Check("AC3 verificationBgm=null → playBgm", sm.CurrentBgmClip == _bgmPlay, $"clip={(sm.CurrentBgmClip != null ? sm.CurrentBgmClip.name : "null")} phase={gpm.currentPhase}");
                    Next(); return;

                case 9: // AC4 준비: 크로스페이드 없는 상태에서 같은 클립 페이즈 재발행
                    if (tr < 1.5f) return;
                    gpm.SetPhase(GamePhase.Play, true);
                    Next(); return;

                case 10:
                    _dmg0 = sm.IsCrossfading ? 1 : 0; // 임시: 첫 번째 관측 저장
                    gpm.SetPhase(GamePhase.Play, true); // 같은 페이즈 한 번 더 발행
                    Next(); return;

                case 11: // AC4
                    Check("AC4 같은 클립 페이즈 재발행 → IsCrossfading false", _dmg0 == 0 && !sm.IsCrossfading && sm.CurrentBgmClip == _bgmPlay, $"1차 crossfading={_dmg0 == 1} 2차 crossfading={sm.IsCrossfading} clip={(sm.CurrentBgmClip != null ? sm.CurrentBgmClip.name : "null")} phase={gpm.currentPhase}");
                    Next(); return;

                case 12: // AC5 준비 (Build) — 기존 함정 비활성화로 측정 오염 방지
                    if (!_init)
                    {
                        _init = true;
                        gpm.SetPhase(GamePhase.Build, true);
                        var pre = new List<TrapBase>(TrapBase.ActiveTraps);
                        foreach (var t in pre) if (t != null) t.gameObject.SetActive(false);
                        sm.ApplyConfig(_cfgMain);
                    }
                    if (tr < 0.8f) return;
                    _near = Spawn(3f);
                    Next(); return;

                case 13: // AC5 Build 중 함정 작동 → 0
                    Place(_near, 3f);
                    if (!_armed) { if (tr < 0.3f) return; Arm(_nearAct); }
                    if (tr < 3.0f) return;
                    if (_nearAct == _act0 && !_fb)
                    {
                        // 자연 발동 관측 불가(Build 에서는 함정이 발사하지 않을 수 있음) → 보강: 보호 메서드 RaiseActivated 를 리플렉션으로 호출
                        typeof(TrapBase).GetMethod("RaiseActivated", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_near, null);
                        _fb = true; _fbT = tr; return;
                    }
                    if (_fb && tr < _fbT + 0.3f) return;
                    {
                        int delta = sm.SfxPlayCount - _base, dmgD = _dmg - _dmg0;
                        Check("AC5 Build 중 함정 작동 → SfxPlayCount +0", delta - dmgD == 0 && gpm.currentPhase == GamePhase.Build,
                            $"delta={delta} 발동={_nearAct - _act0}회{(_fb ? "(자연 발동 없음→리플렉션 보강 1회)" : "(자연)")} 피격={dmgD} phase={gpm.currentPhase}");
                    }
                    gpm.SetPhase(GamePhase.Play, true); _near.Mutate(TrapState.Normal);
                    Next(); return;

                case 14: // AC5 Play 거리 3 → +1
                    Place(_near, 3f);
                    if (!_armed) { if (tr < 0.5f) return; Arm(_nearAct); }
                    {
                        bool to;
                        if (!WaitAct(_nearAct, tr, 5.5f, out to)) return;
                        int delta = sm.SfxPlayCount - _base, dmgD = _dmg - _dmg0, act = _nearAct - _act0;
                        Check("AC5 Play·거리3 함정 작동 → +1", !to && act == 1 && delta - dmgD == 1,
                            to ? "측정 불가: 5초 내 함정 자연 발동 미관측" : $"delta={delta} 발동={act} 피격={dmgD} phase={gpm.currentPhase}");
                    }
                    UnityEngine.Object.Destroy(_near.gameObject); _near = null;
                    Next(); return;

                case 15: // AC5 Play 거리 30 → 0
                    if (!_init) { _init = true; _far = Spawn(30f); }
                    Place(_far, 30f);
                    if (!_armed) { if (tr < 0.5f) return; Arm(_farAct); }
                    {
                        bool to;
                        if (!WaitAct(_farAct, tr, 5.5f, out to)) return;
                        int delta = sm.SfxPlayCount - _base, dmgD = _dmg - _dmg0, act = _farAct - _act0;
                        Check("AC5 Play·거리30 함정 작동 → +0", !to && act >= 1 && delta - dmgD == 0,
                            to ? "측정 불가: 5초 내 함정 자연 발동 미관측(음성 판정 무의미)" : $"delta={delta} 발동={act} 피격={dmgD} phase={gpm.currentPhase}");
                    }
                    UnityEngine.Object.Destroy(_far.gameObject); _far = null;
                    Next(); return;

                case 16: // AC5 Dud → 0 (거리 3)
                    if (!_init) { _init = true; _near = Spawn(3f); _near.Mutate(TrapState.Dud); }
                    Place(_near, 3f);
                    if (!_armed) { if (tr < 0.5f) return; Arm(_nearAct); }
                    if (tr < 3.0f) return;
                    {
                        int delta = sm.SfxPlayCount - _base, dmgD = _dmg - _dmg0, act = _nearAct - _act0;
                        Check("AC5 Dud 함정 → +0 (발행 없음)", act == 0 && delta - dmgD == 0, $"delta={delta} 발동={act} 피격={dmgD} phase={gpm.currentPhase}");
                    }
                    _near.Mutate(TrapState.Normal);
                    Next(); return;

                case 17: // 대조: Dud 해제 후 정상 작동 시 +1 (프로브 감도 확인)
                    Place(_near, 3f);
                    if (!_armed) Arm(_nearAct);
                    {
                        bool to;
                        if (!WaitAct(_nearAct, tr, 5.5f, out to)) return;
                        int delta = sm.SfxPlayCount - _base, dmgD = _dmg - _dmg0, act = _nearAct - _act0;
                        Check("AC5 (대조) Dud 해제 후 작동 → +1", !to && act == 1 && delta - dmgD == 1, to ? "측정 불가: 자연 발동 미관측" : $"delta={delta} 발동={act} 피격={dmgD}");
                    }
                    UnityEngine.Object.Destroy(_near.gameObject); _near = null;
                    Next(); return;

                case 18: // AC6 — 같은 프레임 10회
                    if (!_init) { _init = true; sm.ApplyConfig(_cfgMain); }
                    if (tr < 1.5f) return;
                    {
                        int p0 = sm.SfxPlayCount, r0 = sm.SfxRejectedCount;
                        for (int i = 0; i < 10; i++) sm.PlaySfx(SfxId.TrapActivate);
                        int dp = sm.SfxPlayCount - p0, dr = sm.SfxRejectedCount - r0;
                        Check("AC6 같은 프레임 10회 → Play +1 / Rejected +9", dp == 1 && dr == 9, $"play +{dp} rejected +{dr}");
                    }
                    Next(); return;

                case 19: // AC7 — 0.1초 간격 5회
                    if (tr < 1.5f) return;
                    if (_k == 0) { _steal0 = sm.SfxStealCount; _tLast = Time.unscaledTime - 1f; }
                    if (_k < 5)
                    {
                        if (Time.unscaledTime - _tLast < 0.1f) return;
                        sm.PlaySfx(SfxId.TrapActivate); _tLast = Time.unscaledTime; _k++;
                        if (_k < 5) return;
                    }
                    {
                        int ac = sm.GetActiveCount(SfxId.TrapActivate), ds = sm.SfxStealCount - _steal0;
                        Check("AC7 0.1초 간격 5회 → Active 3 / Steal +2", ac == 3 && ds == 2, $"active={ac} steal +{ds} (5회 소요 {Time.unscaledTime - _stepStartReal - 1.5f:F2}s)");
                    }
                    Next(); return;

                case 20: // AC8 준비
                    if (tr < 1.5f) return;
                    sm.ApplyConfig(_cfg8);
                    Next(); return;

                case 21: // AC8
                    if (tr < 0.1f) return;
                    {
                        int s0 = sm.SfxStealCount, d0 = sm.SfxDroppedCount;
                        for (int i = 0; i < 12; i++) sm.PlaySfx(SfxId.TrapActivate);
                        int act = sm.ActiveSfxVoices, ds = sm.SfxStealCount - s0, dd = sm.SfxDroppedCount - d0;
                        Check("AC8 12회 → Active ≤ 8 / Steal +4 / Dropped +0", act <= 8 && ds == 4 && dd == 0, $"active={act} steal +{ds} dropped +{dd}");
                        int s1 = sm.SfxStealCount;
                        sm.PlaySfx(SfxId.PlayerHit);
                        int ds2 = sm.SfxStealCount - s1;
                        Check("AC8 PlayerHit(priority 200) → LastSfxId=PlayerHit / Steal +1", sm.LastSfxId == SfxId.PlayerHit && ds2 == 1, $"last={sm.LastSfxId} steal +{ds2} active={sm.ActiveSfxVoices}");
                    }
                    Next(); return;

                case 22: // AC9 준비: 플레이어 확보
                    if (!_init) { _init = true; sm.ApplyConfig(_cfgMain); }
                    if (tr < 1.5f) return;
                    if (_ph == null) _ph = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
                    if (_ph == null)
                    {
                        if (tr < 8f) return;
                        Check("AC9 PlayerHealth 확보", false, "측정 불가: Play 8초 내 PlayerHealth 없음 (phase=" + gpm.currentPhase + ")");
                        Next(); _step = 24; return;
                    }
                    if (_ph.IsInvincible && tr < 8f) return;
                    _ph.RestoreFullHp();
                    _base = sm.SfxPlayCount;
                    _ph.TakeDamage(1);
                    Next(); return;

                case 23: // AC9 판정 (다음 틱)
                    Check("AC9 TakeDamage(1) → LastSfxId=PlayerHit / +1", sm.LastSfxId == SfxId.PlayerHit && sm.SfxPlayCount - _base == 1, $"last={sm.LastSfxId} delta={sm.SfxPlayCount - _base} hp={_ph.CurrentHp}");
                    Next(); return;

                case 24: // AC11 준비
                    if (!_init) { _init = true; sm.ApplyConfig(_cfgNoMixer); }
                    if (tr < 1.5f) return;
                    {
                        float vol = -1f; string cn = sm.CurrentBgmClip != null ? sm.CurrentBgmClip.name : "null";
                        foreach (var src in sm.GetComponentsInChildren<AudioSource>(true))
                            if (src.clip != null && src.clip == sm.CurrentBgmClip) vol = Mathf.Max(vol, src.volume);
                        int p0 = sm.SfxPlayCount;
                        bool acc = sm.PlaySfx(SfxId.TrapActivate);
                        int dp = sm.SfxPlayCount - p0;
                        Check("AC11 mixer=null → MixerBound false / BGM volume 0.6±0.01 / SFX +1",
                            !sm.MixerBound && Mathf.Abs(vol - 0.6f) <= 0.01f && acc && dp == 1,
                            $"MixerBound={sm.MixerBound} bgmVol={vol:F3} (clip={cn}, crossfading={sm.IsCrossfading}, phase={gpm.currentPhase}) accepted={acc} sfx +{dp}");
                    }
                    Next(); return;

                case 25: // AC12 준비 (수락/탈취 경로)
                    if (!_init) { _init = true; sm.ApplyConfig(_cfg12); }
                    if (tr < 1.5f) return;
                    {
                        Measure(sm); // 워밍업
                        long b = Measure(sm);
                        Check("AC12 PlaySfx 100회 GC 0B (수락·탈취 경로)", b == 0, $"{b} B");
                    }
                    Next(); return;

                case 26: // AC12 (minInterval 거부 경로)
                    if (!_init) { _init = true; sm.ApplyConfig(_cfgMain); }
                    if (tr < 1.5f) return;
                    {
                        Measure(sm); // 워밍업
                        long b = Measure(sm);
                        Check("AC12 PlaySfx 100회 GC 0B (minInterval 거부 경로)", b == 0, $"{b} B");
                    }
                    _step = 99; EditorApplication.ExitPlaymode(); return;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            if (_subscribed) { TrapBase.OnTrapActivated -= OnAct; PlayerHealth.OnAnyDamageTaken -= OnDmg; _subscribed = false; }
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
