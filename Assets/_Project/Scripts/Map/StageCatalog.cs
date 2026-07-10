using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageCatalog — 스테이지 순서 단일 소스 (ScriptableObject)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 게임에 등장하는 스테이지(mapId) 목록과 순서를 담는 단일 소스입니다.
    ///
    /// <para>맵 하나하나는 <c>StreamingAssets/Maps/{mapId}.json</c> 에 있고,
    /// 이 카탈로그는 "어떤 순서로 이어지는가" 만 관리합니다. 스테이지 클리어 후
    /// 다음 스테이지를 찾거나(<see cref="GetNextMapId"/>), 스테이지 선택 UI에서
    /// 목록을 순회할 때(<see cref="GetByIndex"/>) 사용합니다.</para>
    ///
    /// <para>에디터에서 <b>ReTrap → Setup → 맵 시스템 세팅</b> 실행 시
    /// StreamingAssets/Maps 를 스캔해 자동으로 시드됩니다(멱등 — 이미 있으면 덮어쓰지 않음).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/Stage Catalog", fileName = "StageCatalog")]
    public class StageCatalog : ScriptableObject
    {
        /// <summary>카탈로그 엔트리 1개 — 스테이지 mapId 와 표시용 이름.</summary>
        [Serializable]
        public class StageEntry
        {
            [Tooltip("StreamingAssets/Maps/{mapId}.json 의 mapId — 로드 키.")]
            public string mapId;

            [Tooltip("스테이지 선택 UI 등에 표시할 이름. 비워두면 mapId 로 대체.")]
            public string displayName;
        }

        [Header("스테이지 순서")]
        [Tooltip("게임에 등장하는 순서대로 나열된 스테이지 목록.")]
        [SerializeField] private List<StageEntry> _stages = new List<StageEntry>();

        /// <summary>등록된 스테이지 수.</summary>
        public int Count => _stages.Count;

        /// <summary>인덱스로 엔트리 조회. 범위 밖이면 null.</summary>
        public StageEntry GetByIndex(int index)
            => (index >= 0 && index < _stages.Count) ? _stages[index] : null;

        /// <summary>mapId 로 인덱스 조회. 없으면 -1.</summary>
        public int IndexOf(string mapId)
        {
            for (int i = 0; i < _stages.Count; i++)
                if (_stages[i].mapId == mapId)
                    return i;
            return -1;
        }

        /// <summary>
        /// 현재 mapId 다음 스테이지의 mapId 를 반환합니다.
        /// 현재 스테이지가 마지막이거나 카탈로그에 없으면 null.
        /// </summary>
        public string GetNextMapId(string currentMapId)
        {
            int idx = IndexOf(currentMapId);
            if (idx < 0 || idx + 1 >= _stages.Count) return null;
            return _stages[idx + 1].mapId;
        }
    }
}
