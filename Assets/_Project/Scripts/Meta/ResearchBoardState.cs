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
