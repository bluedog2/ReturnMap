using System;
using UnityEngine;
using UnityEngine.Audio;

namespace ReTrap
{
    /// <summary>믹서/볼륨 버스 종류.</summary>
    public enum SoundBus { Master, Bgm, Sfx }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SoundManager — BGM 크로스페이드 + SFX 보이스 풀 관리 싱글톤
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <para><b>역할</b></para>
    /// <list type="bullet">
    ///   <item>페이즈별 BGM 을 A/B 두 AudioSource 로 크로스페이드(실시간, unscaled)</item>
    ///   <item>Awake 에 선생성한 AudioSource 풀로 SFX 재생 — 최소 간격·동시 재생 상한·보이스 상한·우선순위 스틸링</item>
    ///   <item>AudioMixer(노출 파라미터 MasterVolume/BgmVolume/SfxVolume) 로 볼륨 관리.
    ///         믹서가 없거나 그룹을 못 찾으면 AudioSource 볼륨으로 대신하는 폴백 모드</item>
    ///   <item>함정 발동(<see cref="TrapBase.OnTrapActivated"/>)·플레이어 피격(<see cref="PlayerHealth.OnAnyDamageTaken"/>) 이벤트 구독</item>
    /// </list>
    /// <para>시간 장부는 <see cref="Time.unscaledTime"/> 하나로 통일한다(검증 배속·배치모드 오디오 장치 없음에도 안전).
    /// <c>AudioSource.isPlaying</c> 은 신뢰하지 않고 내부 장부(_voiceEnd)로 활성 여부를 판정한다.</para>
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        // ── 인스펙터 ──────────────────────────────────────────────────────────

        [Header("설정")]
        [SerializeField]
        [Tooltip("사운드 설정 에셋. 비어 있으면 클립 없이 무음으로 동작한다(예외 없음).")]
        private AudioConfig config;

        // ── 상수 ──────────────────────────────────────────────────────────────

        private const string MasterParam = "MasterVolume";
        private const string BgmParam    = "BgmVolume";
        private const string SfxParam    = "SfxVolume";
        private const int    DefaultVoices = 8;
        private const float  DefaultTrapMaxDistance = 14f;
        private const float  ListenerRetryInterval = 1f;

        // ── 상태 ──────────────────────────────────────────────────────────────

        // 볼륨(선형). 설정이 없을 때의 기본값은 AudioConfig 기본값과 같다.
        private float _master = 1f, _bgm = 0.6f, _sfx = 0.8f;
        private bool  _started;
        private bool  _paramWarned;

        // BGM
        private AudioSource _bgmA, _bgmB;
        private AudioSource _bgmActive;          // 현재(들어오는) 트랙 소스
        private AudioSource _bgmOutgoing;        // 나가는 트랙 소스
        private AudioClip   _bgmTarget;
        private float       _bgmWeight = 1f;     // 들어오는 트랙 가중치 0~1
        private float       _outStartLevel;      // 전환 시작 시 나가는 트랙 레벨
        private bool        _bgmInitialized;

        // SFX 풀 (Awake/ApplyConfig 에서만 할당)
        private AudioSource[] _voices;
        private float[]       _voiceStart;
        private float[]       _voiceEnd;
        private int[]         _voiceEntry;
        private int[]         _voicePriority;
        private int           _voiceCount;

        private SfxEntry[] _entries;     // (int)SfxId 인덱스
        private float[]    _lastPlay;    // 엔트리별 마지막 수락 시각

        private GamePhase _phase = GamePhase.Build;

        private AudioListener _listener;
        private float         _nextListenerSearch;

        // ── 관찰 프로퍼티 ─────────────────────────────────────────────────────

        public bool      MixerBound         { get; private set; }
        public AudioClip CurrentBgmClip     => _bgmTarget;
        public bool      IsCrossfading      => _bgmWeight < 1f;
        public float     CurrentBgmWeight   => _bgmWeight;
        public int       SfxPlayCount       { get; private set; }
        public int       SfxRejectedCount   { get; private set; }
        public int       SfxStealCount      { get; private set; }
        public int       SfxDroppedCount    { get; private set; }
        public SfxId?    LastSfxId          { get; private set; }

        /// <summary>현재 재생 중(장부상)인 SFX 보이스 수.</summary>
        public int ActiveSfxVoices
        {
            get
            {
                float now = Time.unscaledTime;
                int n = 0;
                for (int i = 0; i < _voiceCount; i++)
                    if (_voiceEnd[i] > now) n++;
                return n;
            }
        }

        /// <summary>해당 SFX 의 현재 활성 보이스 수.</summary>
        public int GetActiveCount(SfxId id)
        {
            float now = Time.unscaledTime;
            int idx = (int)id, n = 0;
            for (int i = 0; i < _voiceCount; i++)
                if (_voiceEnd[i] > now && _voiceEntry[i] == idx) n++;
            return n;
        }

        /// <summary>마지막으로 적용한 버스 볼륨의 dB 값(믹서 유무와 무관, -80 이 하한).</summary>
        public float GetAppliedDb(SoundBus bus) => LinearToDb(GetBusVolume(bus));

        // ── 수명 ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _bgmA = CreateSource("BGM_A", true);
            _bgmB = CreateSource("BGM_B", true);
            _bgmActive   = _bgmA;
            _bgmOutgoing = _bgmB;

            if (config == null)
                Debug.LogWarning("[SoundManager] AudioConfig 가 지정되지 않아 무음으로 동작합니다.");

            ConfigureFromAsset();
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
            TrapBase.OnTrapActivated        += HandleTrapActivated;
            PlayerHealth.OnAnyDamageTaken   += HandleDamageTaken;
            if (_started) ReadCurrentPhase();
        }

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
            TrapBase.OnTrapActivated        -= HandleTrapActivated;
            PlayerHealth.OnAnyDamageTaken   -= HandleDamageTaken;
        }

        private void Start()
        {
            if (Instance != this) return;
            _started = true;
            ApplyVolumes();      // Awake 에서는 믹서 SetFloat 가 무시되므로 Start 이후 적용
            ReadCurrentPhase();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            UpdateBgm(Time.unscaledDeltaTime);
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>설정을 교체하고 풀·믹서·볼륨·BGM 을 다시 맞춘다.</summary>
        public void ApplyConfig(AudioConfig newConfig)
        {
            config = newConfig;
            ConfigureFromAsset();
            if (_started)
            {
                ApplyVolumes();
                RefreshBgmTarget();
            }
        }

        /// <summary>버스 볼륨(선형 0~1) 설정. 설정 UI 진입점.</summary>
        public void SetBusVolume(SoundBus bus, float linear)
        {
            linear = Mathf.Clamp01(linear);
            switch (bus)
            {
                case SoundBus.Master: _master = linear; break;
                case SoundBus.Bgm:    _bgm    = linear; break;
                case SoundBus.Sfx:    _sfx    = linear; break;
            }
            if (_started) ApplyVolumes();
        }

        /// <summary>
        /// SFX 재생. 수락되어 재생을 시작하면 true.
        /// 규칙: ① 엔트리·클립 없음 거부 ② minInterval 거부 ③ 같은 SFX 상한 초과 시 가장 오래된 것 스틸
        /// ④ 빈 보이스 사용 ⑤ 풀 가득이면 최저 우선순위(동률 시 가장 오래된) 스틸 또는 버림 ⑥ 무작위 클립·피치 재생.
        /// </summary>
        public bool PlaySfx(SfxId id, Vector3? pos = null)
        {
            if (_entries == null) return false;   // 중복 인스턴스 등 미초기화

            int idx = (int)id;
            SfxEntry e = (idx >= 0 && idx < _entries.Length) ? _entries[idx] : null;
            if (e == null || e.clips == null || e.clips.Length == 0)
            {
                SfxRejectedCount++;
                return false;
            }

            float now = Time.unscaledTime;
            if (now - _lastPlay[idx] < e.minInterval)
            {
                SfxRejectedCount++;
                return false;
            }

            AudioClip clip = e.clips[e.clips.Length == 1 ? 0 : UnityEngine.Random.Range(0, e.clips.Length)];
            if (clip == null)
            {
                SfxRejectedCount++;
                return false;
            }

            int v = ChooseVoice(_voiceStart, _voiceEnd, _voiceEntry, _voicePriority, _voiceCount,
                                now, idx, e.maxInstances, e.priority, out bool stolen);
            if (v < 0)
            {
                SfxDroppedCount++;
                return false;
            }
            if (stolen) SfxStealCount++;

            float pitch = Mathf.Approximately(e.pitchMin, e.pitchMax)
                ? e.pitchMin
                : UnityEngine.Random.Range(e.pitchMin, e.pitchMax);
            if (pitch < 0.01f) pitch = 0.01f;

            AudioSource src = _voices[v];
            src.Stop();
            if (pos.HasValue) src.transform.position = pos.Value;
            src.clip   = clip;
            src.volume = e.volume * (MixerBound ? 1f : _master * _sfx);
            src.pitch  = pitch;
            src.Play();

            _voiceStart[v]    = now;
            _voiceEnd[v]      = now + clip.length / pitch;
            _voiceEntry[v]    = idx;
            _voicePriority[v] = e.priority;
            _lastPlay[idx]    = now;

            SfxPlayCount++;
            LastSfxId = id;
            return true;
        }

        // ── 순수 로직 (테스트 대상) ───────────────────────────────────────────

        /// <summary>선형 볼륨(0~1) → dB. 0.0001 이하는 -80.</summary>
        internal static float LinearToDb(float v)
        {
            return v <= 0.0001f ? -80f : 20f * Mathf.Log10(v);
        }

        /// <summary>
        /// 보이스 선택. 반환값 = 사용할 보이스 인덱스, 버림이면 -1. <paramref name="stolen"/> = 활성 보이스를 끊었는가.
        /// 활성 판정: voiceEnd &gt; now. 같은 엔트리 활성 수 ≥ maxInstances 면 그중 가장 오래된 것,
        /// 아니면 빈 보이스, 없으면 최저 우선순위(동률이면 가장 오래된)가 newPriority 이하일 때 그것.
        /// 할당 없음.
        /// </summary>
        internal static int ChooseVoice(float[] voiceStart, float[] voiceEnd, int[] voiceEntry, int[] voicePriority,
                                        int count, float now, int entryIdx, int maxInstances, int newPriority,
                                        out bool stolen)
        {
            stolen = false;
            int sameCount = 0, oldestSame = -1, free = -1, lowest = -1;
            for (int i = 0; i < count; i++)
            {
                if (voiceEnd[i] <= now)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (voiceEntry[i] == entryIdx)
                {
                    sameCount++;
                    if (oldestSame < 0 || voiceStart[i] < voiceStart[oldestSame]) oldestSame = i;
                }
                if (lowest < 0 || voicePriority[i] < voicePriority[lowest] ||
                    (voicePriority[i] == voicePriority[lowest] && voiceStart[i] < voiceStart[lowest]))
                    lowest = i;
            }

            if (sameCount >= maxInstances && oldestSame >= 0) { stolen = true; return oldestSame; }
            if (free >= 0) return free;
            if (lowest >= 0 && voicePriority[lowest] <= newPriority) { stolen = true; return lowest; }
            return -1;
        }

        /// <summary>페이즈에 대응하는 BGM 클립. Verification 은 없으면 Play 로 폴백.</summary>
        internal static AudioClip SelectBgm(GamePhase phase, AudioClip build, AudioClip verification, AudioClip play)
        {
            switch (phase)
            {
                case GamePhase.Build:        return build;
                case GamePhase.Verification: return verification != null ? verification : play;
                default:                     return play;
            }
        }

        // ── 구성 ──────────────────────────────────────────────────────────────

        private AudioSource CreateSource(string objName, bool loop)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake  = false;
            s.spatialBlend = 0f;
            s.loop         = loop;
            return s;
        }

        /// <summary>config(없을 수 있음)로부터 볼륨·풀·엔트리·믹서 바인딩을 구성한다. 할당은 여기서만.</summary>
        private void ConfigureFromAsset()
        {
            if (config != null)
            {
                _master = config.MasterVolume;
                _bgm    = config.BgmVolume;
                _sfx    = config.SfxVolume;
            }

            EnsureVoicePool(Mathf.Max(1, config != null ? config.MaxSfxVoices : DefaultVoices));

            // 엔트리 표 — (int)SfxId 인덱스로 펼침
            int idCount = Enum.GetValues(typeof(SfxId)).Length;
            _entries  = new SfxEntry[idCount];
            _lastPlay = new float[idCount];
            for (int i = 0; i < idCount; i++) _lastPlay[i] = -9999f;
            var list = config != null ? config.SfxEntries : null;
            if (list != null)
            {
                for (int i = 0; i < list.Length; i++)
                {
                    var e = list[i];
                    if (e == null) continue;
                    int idx = (int)e.id;
                    if (idx < 0 || idx >= idCount) continue;
                    if (_entries[idx] != null)
                    {
                        Debug.LogWarning($"[SoundManager] SFX id 중복: {e.id} — 앞의 엔트리만 사용합니다.");
                        continue;
                    }
                    _entries[idx] = e;
                }
            }

            BindMixer();
        }

        private void EnsureVoicePool(int want)
        {
            int have = _voices != null ? _voices.Length : 0;
            if (have < want)
            {
                var voices  = new AudioSource[want];
                var starts  = new float[want];
                var ends    = new float[want];
                var entries = new int[want];
                var prios   = new int[want];
                for (int i = 0; i < want; i++)
                {
                    if (i < have)
                    {
                        voices[i]  = _voices[i];
                        starts[i]  = _voiceStart[i];
                        ends[i]    = _voiceEnd[i];
                        entries[i] = _voiceEntry[i];
                        prios[i]   = _voicePriority[i];
                    }
                    else
                    {
                        voices[i]  = CreateSource("SFX_" + i, false);
                        entries[i] = -1;
                    }
                }
                _voices = voices; _voiceStart = starts; _voiceEnd = ends;
                _voiceEntry = entries; _voicePriority = prios;
            }
            // 줄어든 경우: 초과 보이스는 끊고 장부를 비운다
            for (int i = want; i < have; i++)
            {
                _voices[i].Stop();
                _voiceEnd[i] = 0f;
            }
            _voiceCount = want;
        }

        private void BindMixer()
        {
            MixerBound = false;
            AudioMixer mixer = config != null ? config.Mixer : null;
            AudioMixerGroup bgmGroup = null, sfxGroup = null;
            if (mixer != null)
            {
                var bg = mixer.FindMatchingGroups("BGM");
                var sg = mixer.FindMatchingGroups("SFX");
                if (bg != null && bg.Length > 0) bgmGroup = bg[0];
                if (sg != null && sg.Length > 0) sfxGroup = sg[0];
                if (bgmGroup == null || sfxGroup == null)
                {
                    Debug.LogWarning("[SoundManager] 믹서에서 BGM/SFX 그룹을 찾지 못해 폴백 모드로 동작합니다.");
                    bgmGroup = null;
                    sfxGroup = null;
                }
                else MixerBound = true;
            }

            _bgmA.outputAudioMixerGroup = bgmGroup;
            _bgmB.outputAudioMixerGroup = bgmGroup;
            for (int i = 0; i < _voices.Length; i++)
                _voices[i].outputAudioMixerGroup = sfxGroup;
        }

        private float GetBusVolume(SoundBus bus)
        {
            switch (bus)
            {
                case SoundBus.Master: return _master;
                case SoundBus.Bgm:    return _bgm;
                default:              return _sfx;
            }
        }

        /// <summary>믹서 모드: SetFloat(dB). 폴백 모드: 소스 볼륨에 곱한다(이중 적용 방지).</summary>
        private void ApplyVolumes()
        {
            if (MixerBound)
            {
                var mixer = config.Mixer;
                bool ok = mixer.SetFloat(MasterParam, LinearToDb(_master));
                ok &= mixer.SetFloat(BgmParam, LinearToDb(_bgm));
                ok &= mixer.SetFloat(SfxParam, LinearToDb(_sfx));
                if (!ok && !_paramWarned)
                {
                    _paramWarned = true;
                    Debug.LogWarning("[SoundManager] 믹서 노출 파라미터 설정에 실패했습니다(MasterVolume/BgmVolume/SfxVolume).");
                }
            }
            UpdateBgmVolumes();
        }

        // ── BGM ───────────────────────────────────────────────────────────────

        private void ReadCurrentPhase()
        {
            var gpm = GamePhaseManager.Instance;
            if (gpm != null) _phase = gpm.currentPhase;
            RefreshBgmTarget();
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            _phase = phase;
            RefreshBgmTarget();
        }

        /// <summary>현재 페이즈의 BGM 으로 전환. 대상 클립이 같으면 무시(재시작 없음).</summary>
        private void RefreshBgmTarget()
        {
            if (_bgmA == null) return;
            AudioClip next = config != null
                ? SelectBgm(_phase, config.BuildBgm, config.VerificationBgm, config.PlayBgm)
                : null;
            if (_bgmInitialized && next == _bgmTarget) return;
            StartCrossfade(next);
        }

        private void StartCrossfade(AudioClip next)
        {
            _bgmInitialized = true;
            float level = _bgmWeight;                  // 지금 들리는 트랙의 레벨
            // 이전 페이드아웃 소스는 정리하고, 현재 트랙이 나가는 트랙이 된다.
            _bgmOutgoing.Stop();
            _bgmOutgoing.clip = null;
            AudioSource incoming = _bgmOutgoing;
            _bgmOutgoing   = _bgmActive;
            _bgmActive     = incoming;
            _outStartLevel = level;

            _bgmTarget      = next;
            _bgmActive.clip = next;
            if (next != null) _bgmActive.Play();

            float fade = config != null ? config.CrossfadeSeconds : 0f;
            _bgmWeight = fade <= 0f ? 1f : 0f;
            UpdateBgmVolumes();
            if (_bgmWeight >= 1f) FinishCrossfade();
        }

        private void UpdateBgm(float dt)
        {
            if (_bgmWeight >= 1f) return;
            float fade = config != null ? Mathf.Max(0.0001f, config.CrossfadeSeconds) : 0.0001f;
            _bgmWeight = Mathf.Min(1f, _bgmWeight + dt / fade);
            UpdateBgmVolumes();
            if (_bgmWeight >= 1f) FinishCrossfade();
        }

        private void FinishCrossfade()
        {
            _bgmOutgoing.Stop();
            _bgmOutgoing.clip = null;
        }

        private void UpdateBgmVolumes()
        {
            if (_bgmA == null) return;
            float factor = MixerBound ? 1f : _master * _bgm;
            _bgmActive.volume   = _bgmWeight * factor;
            _bgmOutgoing.volume = _outStartLevel * (1f - _bgmWeight) * factor;
        }

        // ── 이벤트 ────────────────────────────────────────────────────────────

        private void HandleTrapActivated(TrapBase trap)
        {
            if (trap == null) return;
            if (_phase == GamePhase.Build) return;

            float maxDist = config != null ? config.TrapSfxMaxDistance : DefaultTrapMaxDistance;
            if (TryGetListenerPosition(out Vector3 lp))
            {
                Vector3 tp = trap.transform.position;
                float dx = tp.x - lp.x, dy = tp.y - lp.y;
                if (dx * dx + dy * dy > maxDist * maxDist) return;
            }
            PlaySfx(SfxId.TrapActivate, trap.transform.position);
        }

        private void HandleDamageTaken(PlayerHealth health, int amount)
        {
            PlaySfx(SfxId.PlayerHit);
        }

        /// <summary>AudioListener(없으면 Camera.main) 위치. 둘 다 없으면 false(거리 필터 통과).</summary>
        private bool TryGetListenerPosition(out Vector3 pos)
        {
            if (_listener == null && Time.unscaledTime >= _nextListenerSearch)
            {
                _nextListenerSearch = Time.unscaledTime + ListenerRetryInterval;
                _listener = FindFirstObjectByType<AudioListener>();
            }
            if (_listener != null && _listener.isActiveAndEnabled)
            {
                pos = _listener.transform.position;
                return true;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                pos = cam.transform.position;
                return true;
            }
            pos = default;
            return false;
        }
    }
}
