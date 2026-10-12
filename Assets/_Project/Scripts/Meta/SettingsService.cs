using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SettingsService — 사용자 설정(음량 3버스 + 화면) 영속화 정적 서비스
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 음량(마스터·BGM·효과음)과 화면(해상도·전체화면) 설정을 <see cref="PlayerPrefs"/>(<c>ReTrap.Settings.*</c>)에
    /// 저장/조회한다. 다른 메타 서비스와 달리 <b>static 필드·이벤트를 두지 않고</b> 매번 PlayerPrefs 를 직접
    /// 읽고 쓴다(Fast Play 도메인 리로드 OFF 에서도 상태 오염 없음).
    /// <para>음량은 선형 0~1, UI 는 21단계(5%) 슬라이더. 화면 적용은 에디터·배치모드에서는 값만 저장한다.</para>
    /// </summary>
    public static class SettingsService
    {
        private const string KeyPrefix     = "ReTrap.Settings.";
        private const string MasterKey     = KeyPrefix + "MasterVolume";
        private const string BgmKey        = KeyPrefix + "BgmVolume";
        private const string SfxKey        = KeyPrefix + "SfxVolume";
        private const string ResWidthKey   = KeyPrefix + "ResWidth";
        private const string ResHeightKey  = KeyPrefix + "ResHeight";
        private const string FullscreenKey = KeyPrefix + "Fullscreen";

        /// <summary>볼륨 슬라이더 최대 단계(0~20, 5% 간격).</summary>
        public const int VolumeSteps = 20;

        /// <summary>해상도 고정 목록(16:9, 오름차순).</summary>
        private static readonly Vector2Int[] ResolutionTable =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
        };

        private static readonly Vector2Int FallbackResolution = new Vector2Int(1280, 720);

        // ── 음량 ──────────────────────────────────────────────────────────────

        /// <summary>저장된 버스 볼륨(선형 0~1). 저장값이 없으면 false.</summary>
        public static bool TryGetVolume(SoundBus bus, out float linear)
        {
            string key = VolumeKey(bus);
            if (PlayerPrefs.HasKey(key))
            {
                linear = Mathf.Clamp01(PlayerPrefs.GetFloat(key));
                return true;
            }
            linear = 0f;
            return false;
        }

        /// <summary>버스 볼륨 저장(0~1 클램프). Save 는 하지 않는다 — <see cref="Flush"/> 로 일괄 기록.</summary>
        public static void SetVolume(SoundBus bus, float linear)
        {
            PlayerPrefs.SetFloat(VolumeKey(bus), Mathf.Clamp01(linear));
        }

        /// <summary>선형 볼륨 → 슬라이더 단계(0~20, 반올림).</summary>
        internal static int StepFromLinear(float linear)
        {
            return Mathf.Clamp(Mathf.RoundToInt(linear * VolumeSteps), 0, VolumeSteps);
        }

        /// <summary>슬라이더 단계(0~20) → 선형 볼륨.</summary>
        internal static float LinearFromStep(int step)
        {
            return Mathf.Clamp(step, 0, VolumeSteps) / (float)VolumeSteps;
        }

        private static string VolumeKey(SoundBus bus)
        {
            switch (bus)
            {
                case SoundBus.Master: return MasterKey;
                case SoundBus.Bgm:    return BgmKey;
                default:              return SfxKey;
            }
        }

        // ── 화면 ──────────────────────────────────────────────────────────────

        /// <summary>선택 가능한 해상도 목록(고정 목록 중 현재 모니터 이하). 비면 1280x720 하나.</summary>
        public static Vector2Int[] GetResolutionOptions()
        {
            Resolution cur = Screen.currentResolution;
            return FilterResolutions(cur.width, cur.height);
        }

        /// <summary>고정 목록에서 (maxW, maxH) 이하인 항목만 오름차순으로. 결과가 비면 1280x720 하나.</summary>
        internal static Vector2Int[] FilterResolutions(int maxW, int maxH)
        {
            var list = new List<Vector2Int>(ResolutionTable.Length);
            for (int i = 0; i < ResolutionTable.Length; i++)
            {
                if (ResolutionTable[i].x <= maxW && ResolutionTable[i].y <= maxH)
                    list.Add(ResolutionTable[i]);
            }
            if (list.Count == 0) list.Add(FallbackResolution);
            return list.ToArray();
        }

        /// <summary>저장된 해상도. 저장값이 없으면 false.</summary>
        public static bool TryGetResolution(out Vector2Int resolution)
        {
            if (PlayerPrefs.HasKey(ResWidthKey) && PlayerPrefs.HasKey(ResHeightKey))
            {
                resolution = new Vector2Int(PlayerPrefs.GetInt(ResWidthKey), PlayerPrefs.GetInt(ResHeightKey));
                return true;
            }
            resolution = default;
            return false;
        }

        /// <summary>저장된 전체화면 여부. 저장값이 없으면 false.</summary>
        public static bool TryGetFullscreen(out bool fullscreen)
        {
            if (PlayerPrefs.HasKey(FullscreenKey))
            {
                fullscreen = PlayerPrefs.GetInt(FullscreenKey) != 0;
                return true;
            }
            fullscreen = false;
            return false;
        }

        /// <summary>해상도·전체화면을 저장하고 즉시 적용한다(에디터·배치모드는 저장만).</summary>
        public static void SetDisplay(int width, int height, bool fullscreen)
        {
            PlayerPrefs.SetInt(ResWidthKey, width);
            PlayerPrefs.SetInt(ResHeightKey, height);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            ApplyDisplay(width, height, fullscreen);
        }

        /// <summary>
        /// 부팅 시 저장된 화면 설정을 적용한다. 키가 하나도 없거나 에디터·배치모드면 no-op.
        /// 저장 해상도가 모니터보다 크면 목록의 최댓값으로 적용한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplySavedDisplay()
        {
            if (Application.isEditor || Application.isBatchMode) return;

            bool hasRes = TryGetResolution(out Vector2Int res);
            bool hasFs  = TryGetFullscreen(out bool fs);
            if (!hasRes && !hasFs) return;

            if (!hasRes) res = new Vector2Int(Screen.width, Screen.height);
            if (!hasFs)  fs  = Screen.fullScreenMode != FullScreenMode.Windowed;

            Vector2Int[] options = GetResolutionOptions();
            Vector2Int max = options[options.Length - 1];
            if (res.x > max.x || res.y > max.y) res = max;

            ApplyDisplay(res.x, res.y, fs);
        }

        /// <summary>대기 중인 PlayerPrefs 변경을 디스크에 기록한다(설정 패널 닫을 때 1회).</summary>
        public static void Flush()
        {
            PlayerPrefs.Save();
        }

        private static void ApplyDisplay(int width, int height, bool fullscreen)
        {
            if (Application.isEditor || Application.isBatchMode) return;
            Screen.SetResolution(width, height,
                fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
