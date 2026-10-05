using System;
using UnityEngine;
using UnityEngine.Audio;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AudioConfig — 사운드 설정 (ScriptableObject)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 한 SFX 의 등록 정보. <see cref="AudioConfig.SfxEntries"/> 에 나열한다.
    /// </summary>
    [Serializable]
    public class SfxEntry
    {
        [Tooltip("이 엔트리가 담당하는 SFX 종류.")]
        public SfxId id;

        [Tooltip("재생할 클립 후보. 재생할 때마다 무작위로 하나를 고른다. 비어 있으면 재생하지 않는다.")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        [Tooltip("엔트리 기본 볼륨(선형 0~1).")]
        public float volume = 1f;

        [Tooltip("피치 하한. 재생마다 [pitchMin, pitchMax] 에서 무작위.")]
        public float pitchMin = 1f;

        [Tooltip("피치 상한.")]
        public float pitchMax = 1f;

        [Tooltip("우선순위. 보이스가 가득 찼을 때 값이 큰 소리가 작은 소리를 밀어낸다.")]
        public int priority = 100;

        [Min(1)]
        [Tooltip("같은 SFX 동시 재생 상한. 초과하면 가장 오래된 보이스를 끊고 재생한다.")]
        public int maxInstances = 3;

        [Min(0f)]
        [Tooltip("같은 SFX 최소 재생 간격(초). 이 안에 다시 요청되면 무시한다.")]
        public float minInterval = 0.05f;
    }

    /// <summary>
    /// 사운드 설정 단일 소스: 믹서 · 버스 볼륨 · 페이즈별 BGM · SFX 등록 표.
    /// <see cref="SoundManager"/> 가 참조하며, 없으면 SoundManager 가 클립 없이 무음으로 동작한다.
    /// 에셋 경로: <c>Assets/_Project/Settings/AudioConfig.asset</c> (ReTrap → Setup 메뉴가 생성).
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/Audio Config", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [Header("믹서")]
        [SerializeField]
        [Tooltip("AudioMixer(Master → BGM·SFX 그룹, 노출 파라미터 MasterVolume/BgmVolume/SfxVolume). null 이면 AudioSource 볼륨으로 대신하는 폴백 모드.")]
        private AudioMixer mixer;

        [Header("볼륨 (선형 0~1)")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("마스터 볼륨.")]
        private float masterVolume = 1.0f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("BGM 볼륨.")]
        private float bgmVolume = 0.6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("SFX 볼륨.")]
        private float sfxVolume = 0.8f;

        [Header("BGM (페이즈별 별도 트랙)")]
        [SerializeField]
        [Tooltip("빌드 페이즈 BGM.")]
        private AudioClip buildBgm;

        [SerializeField]
        [Tooltip("검증 페이즈 BGM. 비어 있으면 플레이 BGM 을 쓴다.")]
        private AudioClip verificationBgm;

        [SerializeField]
        [Tooltip("플레이 페이즈 BGM.")]
        private AudioClip playBgm;

        [SerializeField, Min(0f)]
        [Tooltip("BGM 크로스페이드 시간(초, 실시간).")]
        private float crossfadeSeconds = 1.0f;

        [Header("SFX")]
        [SerializeField, Min(1)]
        [Tooltip("SFX AudioSource 풀 크기(전체 동시 재생 상한).")]
        private int maxSfxVoices = 8;

        [SerializeField, Min(0f)]
        [Tooltip("함정 SFX 를 재생하는 리스너와의 최대 거리. 이보다 멀면 무시.")]
        private float trapSfxMaxDistance = 14f;

        [SerializeField]
        [Tooltip("SFX 등록 표. id 중복 시 앞의 것만 사용.")]
        private SfxEntry[] sfxEntries = DefaultEntries();

        public AudioMixer Mixer             => mixer;
        public float MasterVolume           => masterVolume;
        public float BgmVolume              => bgmVolume;
        public float SfxVolume              => sfxVolume;
        public AudioClip BuildBgm           => buildBgm;
        public AudioClip VerificationBgm    => verificationBgm;
        public AudioClip PlayBgm            => playBgm;
        public float CrossfadeSeconds       => crossfadeSeconds;
        public int MaxSfxVoices             => maxSfxVoices;
        public float TrapSfxMaxDistance     => trapSfxMaxDistance;
        public SfxEntry[] SfxEntries        => sfxEntries;

        /// <summary>기본 엔트리 2개(클립 빈칸). 에디터 메뉴가 에셋 생성 시 호출한다.</summary>
        public static SfxEntry[] DefaultEntries()
        {
            return new[]
            {
                new SfxEntry
                {
                    id = SfxId.TrapActivate, volume = 1f, pitchMin = 0.95f, pitchMax = 1.05f,
                    priority = 100, maxInstances = 3, minInterval = 0.06f,
                },
                new SfxEntry
                {
                    id = SfxId.PlayerHit, volume = 1f, pitchMin = 1f, pitchMax = 1f,
                    priority = 200, maxInstances = 1, minInterval = 0.10f,
                },
            };
        }

        /// <summary>
        /// 런타임/프로브용 구성 주입. 에셋 변경 없이 인스턴스(CreateInstance)에 값을 채울 때 사용.
        /// </summary>
        public void Configure(AudioMixer mixer, float master, float bgm, float sfx,
                              AudioClip build, AudioClip verification, AudioClip play,
                              float crossfade, int maxVoices, float trapMaxDistance,
                              SfxEntry[] entries)
        {
            this.mixer = mixer;
            masterVolume = master; bgmVolume = bgm; sfxVolume = sfx;
            buildBgm = build; verificationBgm = verification; playBgm = play;
            crossfadeSeconds = crossfade;
            maxSfxVoices = maxVoices;
            trapSfxMaxDistance = trapMaxDistance;
            sfxEntries = entries;
        }
    }
}
