using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ResearchBoardState — 연구소 성장 보드 투자 상태 (저장/로드)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 연구 노드별 투자 레벨을 보관하고 JSON 파일로 영속화하는 정적 클래스.
    /// <see cref="Dictionary{TKey,TValue}"/> 는 JsonUtility 로 직접 직렬화할 수 없어
    /// [Serializable] 페어 리스트로 감싼 래퍼를 통해 저장/로드한다.
    /// </summary>
    public static class ResearchBoardState
    {
        [Serializable]
        private struct NodeLevelPair
        {
            public string nodeId;
            public int    level;
        }

        [Serializable]
        private class SaveData
        {
            public List<NodeLevelPair> entries = new List<NodeLevelPair>();
        }

        /// <summary>노드가 다음 레벨로 업그레이드될 때 발행 (노드, 업그레이드 후 레벨).</summary>
        public static event Action<ResearchNodeDefinition, int> OnUpgraded;

        // nodeId → 투자 레벨. 지연 초기화 (최초 접근 시 파일에서 로드).
        private static Dictionary<string, int> _levels;

        /// <summary>
        /// 플레이 진입마다(도메인 리로드 여부 무관) 캐시를 무효화한다. "Fast Play(Reload Domain
        /// 끄기)" 반복 재생 시 이전 세션의 _levels 잔상이 남아 외부 파일 수정(또는 ResetAll)이
        /// 반영되지 않는 문제를 막는다 — EnsureLoaded 지연 로드 구조 덕분에 null 로만 만들면
        /// 다음 접근 시 자동으로 디스크에서 재로드된다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadOnPlayEnter()
        {
            _levels = null;
        }

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "research_board.json");

        /// <summary>이 노드의 현재 투자 레벨 (미투자 = 0).</summary>
        public static int GetLevel(ResearchNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.NodeId)) return 0;

            EnsureLoaded();
            return _levels.TryGetValue(node.NodeId, out int level) ? level : 0;
        }

        /// <summary>
        /// 다음 레벨로 업그레이드를 시도한다. 이미 최대 레벨이거나 재화가 부족하면
        /// 아무 변화 없이 false 를 반환한다. 성공 시 즉시 저장하고 <see cref="OnUpgraded"/> 를 발행한다.
        /// </summary>
        public static bool TryUpgrade(ResearchNodeDefinition node)
        {
            if (node == null) return false;
            EnsureLoaded();

            int current = GetLevel(node);
            int next    = current + 1;
            if (next > node.MaxLevel) return false;

            int cost = node.GetCost(next);
            // 방어: costPerLevel 이 weightDeltaPerLevel 과 길이가 안 맞아 GetCost 가 범위 밖으로
            // 0 을 반환하면 TrySpend(0) 이 항상 성공해 "무료 업그레이드" 버그가 생긴다 — 비용이
            // 0 이하면 데이터 설정 오류로 간주하고 업그레이드 자체를 거부한다.
            if (cost <= 0)
            {
                Debug.LogWarning($"[ResearchBoardState] '{node.NodeId}' 레벨 {next} 비용이 " +
                                  $"{cost} 입니다(costPerLevel 배열 길이 확인 필요) — 업그레이드를 거부합니다.");
                return false;
            }
            if (!CurrencyService.TrySpend(cost)) return false;

            _levels[node.NodeId] = next;
            Save();
            OnUpgraded?.Invoke(node, next);
            return true;
        }

        /// <summary>개발용 — 모든 투자 상태를 초기화하고 저장한다.</summary>
        public static void ResetAll()
        {
            EnsureLoaded();
            _levels.Clear();
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_levels != null) return;

            _levels = new Dictionary<string, int>();

            try
            {
                string path = SavePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (data?.entries != null)
                    {
                        foreach (var pair in data.entries)
                        {
                            if (!string.IsNullOrEmpty(pair.nodeId))
                                _levels[pair.nodeId] = pair.level;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResearchBoardState] 로드 실패: {ex.Message}");
            }
        }

        private static void Save()
        {
            var data = new SaveData();
            foreach (var kv in _levels)
                data.entries.Add(new NodeLevelPair { nodeId = kv.Key, level = kv.Value });

            try
            {
                string json = JsonUtility.ToJson(data);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResearchBoardState] 저장 실패: {ex.Message}");
            }
        }
    }
}
