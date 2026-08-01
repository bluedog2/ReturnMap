using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ReTrap.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ResearchAssetGenerator — 연구소 노드 6종 에셋 자동 생성
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 기획 확정표(노션 「6. AI」 5장, 학파 3개 × 노드 2종 = 6종)를 코드에 내장해
    /// <see cref="ResearchNodeDefinition"/> 에셋을 일괄 생성/갱신합니다.
    /// <see cref="AITagAssetGenerator"/> 와 동일한 멱등 생성 패턴 — 이미 있는 에셋은
    /// 로드하여 EditorInit 으로 값을 덮어쓰고, 없으면 새로 만듭니다.
    /// <para>대상 태그는 <see cref="AITagDefinition.TagId"/> 로 매칭합니다
    /// (프로젝트에 <c>ReTrap/Setup/1. AI 태그 에셋 생성 (35종)</c> 이 먼저 실행되어 있어야 함).</para>
    /// </summary>
    public static class ResearchAssetGenerator
    {
        private const string RESEARCH_DIR = "Assets/_Project/Settings/Meta/Research";

        // 기획 미정 — 전 노드 공통 placeholder. TODO: 기획 확정되면 노드별로 분리.
        private static readonly int[] DefaultCostPerLevel = { 50, 100, 200 };

        /// <summary>연구 노드 1종의 테이블 행.</summary>
        private struct ResearchRow
        {
            public string          nodeId;
            public ResearchSchool  school;
            public string          displayName;
            public string          description;
            public string[]        targetTagIds;
            public float[]         weightDeltaPerLevel;

            public ResearchRow(string nodeId, ResearchSchool school, string displayName,
                                string description, string[] targetTagIds, float[] weightDeltaPerLevel)
            {
                this.nodeId              = nodeId;
                this.school              = school;
                this.displayName          = displayName;
                this.description          = description;
                this.targetTagIds         = targetTagIds;
                this.weightDeltaPerLevel  = weightDeltaPerLevel;
            }
        }

        // ── 노드 6종 테이블 (기획 확정값, costPerLevel 은 미정 placeholder) ──────
        private static readonly ResearchRow[] ResearchTable =
        {
            // ── 기하학 파괴 ───────────────────────────────────────────────────
            new ResearchRow("Research_낙하법칙의진실", ResearchSchool.GeometryBreak,
                "낙하 법칙의 진실",
                "과속/직진, 스프린터 계열 태그의 등장 가중치를 늘린다.",
                new[] { "M-02", "S-11" },
                new float[] { 15f, 30f, 50f }),

            new ResearchRow("Research_수면유도제", ResearchSchool.GeometryBreak,
                "수면 유도제",
                "잠만보, 신중함 계열 태그의 등장 가중치를 늘린다.",
                new[] { "M-10", "M-03" },
                new float[] { 20f, 40f, 60f }),

            // ── 대지 진리 ─────────────────────────────────────────────────────
            new ResearchRow("Research_각질제거", ResearchSchool.EarthTruth,
                "각질 제거",
                "둥글둥글, 공중부양 계열 태그의 등장 가중치를 줄인다 (감소 노드).",
                new[] { "D-01", "D-04" },
                new float[] { -20f, -40f, -60f }),

            new ResearchRow("Research_바람앞의깃털", ResearchSchool.EarthTruth,
                "바람 앞의 깃털",
                "경량화, 유리몸 계열 태그의 등장 가중치를 늘린다.",
                new[] { "S-02", "S-03" },
                new float[] { 15f, 30f, 50f }),

            // ── 자본주의 역공학 ───────────────────────────────────────────────
            // 기획 원문 '황금 납치범'은 태그 리스트(35종)에 없어 S-04 도둑으로 매핑한다.
            new ResearchRow("Research_길드창고털기", ResearchSchool.CapitalReverse,
                "길드 창고 털기",
                "도둑 태그의 등장 가중치를 늘린다. " +
                "(기획 원문 '황금 납치범'은 태그 리스트에 없어 S-04 도둑으로 매핑)",
                new[] { "S-04" },
                new float[] { 10f, 20f, 35f }),

            new ResearchRow("Research_헬멧압수수색", ResearchSchool.CapitalReverse,
                "헬멧 압수수색",
                "뚝배기 보호 태그의 등장 가중치를 줄인다 (감소 노드).",
                new[] { "D-03" },
                new float[] { -15f, -30f, -50f }),
        };

        // ── 메뉴 ──────────────────────────────────────────────────────────────

        [MenuItem("ReTrap/Setup/2. 연구소 노드 에셋 생성 (6종)", false, 2)]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/_Project", "Settings");
            EnsureFolder("Assets/_Project/Settings", "Meta");
            EnsureFolder("Assets/_Project/Settings/Meta", "Research");

            Dictionary<string, AITagDefinition> tagsById = LoadAllTagsById();

            int created = 0, updated = 0;
            foreach (ResearchRow row in ResearchTable)
            {
                string assetPath = $"{RESEARCH_DIR}/{row.nodeId}.asset";

                var node   = AssetDatabase.LoadAssetAtPath<ResearchNodeDefinition>(assetPath);
                bool isNew = node == null;
                if (isNew)
                    node = ScriptableObject.CreateInstance<ResearchNodeDefinition>();

                AITagDefinition[] targets = ResolveTags(row.targetTagIds, tagsById, row.nodeId);

                node.EditorInit(row.nodeId, row.school, row.displayName, row.description,
                                 targets, row.weightDeltaPerLevel, DefaultCostPerLevel);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(node, assetPath);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(node);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ResearchAssetGenerator] 완료 — 생성 {created}개, 갱신 {updated}개 " +
                      "(costPerLevel 은 기획 미정 placeholder 50/100/200 — TODO 확정 필요)");
        }

        /// <summary>프로젝트 내 모든 AITagDefinition 에셋을 tagId 로 색인한다.</summary>
        private static Dictionary<string, AITagDefinition> LoadAllTagsById()
        {
            var map = new Dictionary<string, AITagDefinition>();

            string[] guids = AssetDatabase.FindAssets("t:AITagDefinition");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tag = AssetDatabase.LoadAssetAtPath<AITagDefinition>(path);
                if (tag == null || string.IsNullOrEmpty(tag.TagId)) continue;

                map[tag.TagId] = tag;
            }

            return map;
        }

        private static AITagDefinition[] ResolveTags(
            string[] tagIds, Dictionary<string, AITagDefinition> tagsById, string nodeId)
        {
            if (tagIds == null || tagIds.Length == 0) return System.Array.Empty<AITagDefinition>();

            var result = new AITagDefinition[tagIds.Length];
            for (int i = 0; i < tagIds.Length; i++)
            {
                if (!tagsById.TryGetValue(tagIds[i], out AITagDefinition tag))
                {
                    Debug.LogWarning($"[ResearchAssetGenerator] {nodeId}: 태그 '{tagIds[i]}' 를 찾지 못했습니다. " +
                                      "'ReTrap/Setup/1. AI 태그 에셋 생성 (35종)' 을 먼저 실행했는지 확인하세요.");
                }
                result[i] = tag; // 못 찾으면 null — 이후 EditorInit 값 그대로 저장 (재실행 시 갱신됨)
            }

            return result;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}

// 강제 리임포트 트리거 (내용 해시 변경용 — 무해)
