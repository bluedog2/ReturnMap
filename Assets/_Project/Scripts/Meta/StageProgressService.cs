using System;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageProgressService — 스테이지 진행(클리어 기록) 영속 서비스
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 스테이지별 "최초 클리어 여부"를 영속 기록하는 정적 서비스. 클리어 보상(재화)이
    /// <b>스테이지당 딱 한 번만</b> 지급되도록 게이트하는 데 사용합니다
    /// (<see cref="StageGoalTrigger"/> 참조). 재도전으로 같은 스테이지를 다시 깨도
    /// 재화가 재지급되지 않아 무한 파밍을 막습니다.
    ///
    /// <para><b>영속화</b>: mapId 별 <see cref="PlayerPrefs"/> 키에 직접 read/write 합니다.
    /// 정적 캐시를 두지 않으므로 "Fast Play(Reload Domain 끄기)" 반복 재생에서도
    /// 디스크 값과 어긋나지 않습니다(도메인 리로드 초기화 훅 불필요).</para>
    ///
    /// <para><b>범위</b>: 지금은 "클리어했는가"의 불리언만 기록합니다. 별점/랭크·클리어
    /// 시간·데스 수 같은 상세 진행 데이터는 보상·평가 기획 확정 시 이 서비스를 확장하거나
    /// 별도 세이브 계층으로 분리할 것 (기획 리뷰 §3-1 보상·성장 구조).</para>
    /// </summary>
    public static class StageProgressService
    {
        private const string ClearedKeyPrefix = "ReTrap.Progress.Cleared.";

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
    }
}
