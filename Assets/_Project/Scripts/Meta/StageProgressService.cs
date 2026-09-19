using System;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageProgressService — 스테이지 진행(클리어 기록) 영속 서비스
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 스테이지별 "최초 클리어 여부"와 진행 원천 데이터(최고 클리어 시간·최저 데스 수)를
    /// 영속 기록하는 정적 서비스. 클리어 보상(재화)이 <b>스테이지당 딱 한 번만</b> 지급되도록
    /// 게이트하는 데 사용합니다(<see cref="StageGoalTrigger"/> 참조). 재도전으로 같은 스테이지를
    /// 다시 깨도 재화가 재지급되지 않아 무한 파밍을 막습니다.
    ///
    /// <para><b>영속화</b>: mapId 별 <see cref="PlayerPrefs"/> 키에 직접 read/write 합니다.
    /// 정적 캐시를 두지 않으므로 "Fast Play(Reload Domain 끄기)" 반복 재생에서도
    /// 디스크 값과 어긋나지 않습니다(도메인 리로드 초기화 훅 불필요).</para>
    ///
    /// <para><b>범위</b>: <see cref="RecordClear"/> 는 최고 클리어 시간/최저 데스 수를 <b>원천
    /// 데이터로만</b> 저장합니다 — 별점/랭크 계산식·UI는 보상·평가 기획 확정 전이라 여기서
    /// 만들지 않습니다(기획 리뷰 §3-1 보상·성장 구조). 공식이 정해지면 이 데이터를 그대로
    /// 소비하면 됩니다.</para>
    /// </summary>
    public static class StageProgressService
    {
        private const string ClearedKeyPrefix    = "ReTrap.Progress.Cleared.";
        private const string LastPlayedMapKey    = "ReTrap.Progress.LastPlayedMapId";
        private const string BestTimeKeyPrefix   = "ReTrap.Progress.BestTime.";
        private const string BestDeathsKeyPrefix = "ReTrap.Progress.BestDeaths.";

        /// <summary>어떤 스테이지가 <b>처음</b> 클리어될 때 발행. 인자는 mapId.</summary>
        public static event Action<string> OnStageFirstCleared;

        /// <summary>해당 스테이지를 이미 (한 번이라도) 클리어한 적이 있는가.</summary>
        public static bool IsCleared(string mapId)
            => !string.IsNullOrEmpty(mapId)
               && PlayerPrefs.GetInt(ClearedKeyPrefix + mapId, 0) == 1;

        /// <summary>
        /// 스테이지 클리어를 기록합니다. <b>이번 호출로 처음 기록되면 true</b>, 이미
        /// 클리어 상태였으면 false 를 반환합니다 — 호출부는 이 반환값으로 "최초 클리어
        /// 보상을 지급할지"를 원자적으로 판정할 수 있습니다.
        /// </summary>
        public static bool MarkCleared(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return false;
            if (IsCleared(mapId))            return false;

            PlayerPrefs.SetInt(ClearedKeyPrefix + mapId, 1);
            PlayerPrefs.Save();

            OnStageFirstCleared?.Invoke(mapId);
            return true;
        }

        /// <summary>특정 스테이지의 클리어 기록을 지웁니다 (개발/디버그용).</summary>
        public static void ResetStage(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return;
            PlayerPrefs.DeleteKey(ClearedKeyPrefix + mapId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 마지막으로 플레이(전환)한 mapId. 기록만 하는 API — 부팅 시 "이어하기"로 연결하는 것은
        /// 기획 결정 대기 중이며, <see cref="MapLoader.Start"/> 의 <c>_autoLoadMapId</c> 자동 로드
        /// 경로와 이중 로드 충돌 위험이 있으므로 여기서 부트 동작을 바꾸지 않는다.
        /// </summary>
        public static string LastPlayedMapId
            => PlayerPrefs.GetString(LastPlayedMapKey, string.Empty);

        /// <summary>스테이지 전환 시 마지막 플레이 mapId 를 기록합니다 (기존 클리어 기록과 같은 키 규약).</summary>
        public static void SetLastPlayedMapId(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return;
            PlayerPrefs.SetString(LastPlayedMapKey, mapId);
            PlayerPrefs.Save();
        }

        // ── 스테이지별 진행 데이터 (원천 수집 — 평가 공식은 기획 미정) ──────────

        /// <summary>해당 스테이지의 최고(가장 짧은) 클리어 시간(초). 기록이 없으면 null.</summary>
        public static float? BestClearTime(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return null;
            string key = BestTimeKeyPrefix + mapId;
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : (float?)null;
        }

        /// <summary>해당 스테이지의 최저 사망 횟수. 기록이 없으면 null.</summary>
        public static int? BestDeaths(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return null;
            string key = BestDeathsKeyPrefix + mapId;
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
        }

        /// <summary>
        /// 스테이지 클리어 결과를 기록합니다. <b>별점/랭크 계산은 하지 않습니다</b> —
        /// 공식이 확정되면 바로 쓸 수 있도록 원천 데이터(최고 기록)만 모아둡니다.
        /// 클리어 시간과 데스 수는 서로 독립적으로 "더 좋을 때만" 갱신됩니다
        /// (예: 이번 판이 시간은 신기록이지만 데스는 더 많을 수 있음 — 각 지표를 각자의 최고치로 관리).
        /// 첫 기록이면 두 값 모두 무조건 저장합니다.
        /// </summary>
        public static void RecordClear(string mapId, float clearSeconds, int deaths)
        {
            if (string.IsNullOrEmpty(mapId)) return;

            string timeKey   = BestTimeKeyPrefix   + mapId;
            string deathsKey = BestDeathsKeyPrefix + mapId;

            bool timeIsRecord   = !PlayerPrefs.HasKey(timeKey)   || clearSeconds < PlayerPrefs.GetFloat(timeKey);
            bool deathsIsRecord = !PlayerPrefs.HasKey(deathsKey) || deaths       < PlayerPrefs.GetInt(deathsKey);

            if (timeIsRecord)   PlayerPrefs.SetFloat(timeKey, clearSeconds);
            if (deathsIsRecord) PlayerPrefs.SetInt(deathsKey, deaths);
            if (timeIsRecord || deathsIsRecord) PlayerPrefs.Save();

            // 지금은 별점/랭크 UI가 없으므로 확인 수단이 로그뿐 — 평가 공식 확정 전까지 유지.
            Debug.Log($"[StageProgressService] {mapId} 클리어 기록 — " +
                      $"시간 {clearSeconds:0.00}s(신기록:{timeIsRecord}), 데스 {deaths}(신기록:{deathsIsRecord})");
        }
    }
}
