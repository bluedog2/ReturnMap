using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ResearchSchool — 연구소 3개 학파
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// "지구 평평 협회 연구소" 성장 보드의 3개 학파. 기획 「6. AI」 5장.
    /// </summary>
    public enum ResearchSchool
    {
        /// <summary>기하학 파괴.</summary>
        GeometryBreak,

        /// <summary>대지 진리.</summary>
        EarthTruth,

        /// <summary>자본주의 역공학.</summary>
        CapitalReverse,
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ResearchNodeDefinition — 연구 노드 1개의 데이터 에셋
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 연구소 성장 보드의 노드 1개(레벨별 재화 비용 + 태그 가중치 보정)를 담는
    /// ScriptableObject. 노드 1개 = 에셋 1개.
    /// <para>위치 권장: Assets/_Project/Settings/Meta/Research/Research_Xxx.asset</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/연구 노드(Research Node)", fileName = "Research_")]
    public class ResearchNodeDefinition : ScriptableObject
    {
        [Header("정체성")]
        [SerializeField]
        [Tooltip("저장 키 (예: \"Research_낙하법칙의진실\").")]
        private string nodeId;

        [SerializeField]
        [Tooltip("소속 학파.")]
        private ResearchSchool school;

        [SerializeField]
        [Tooltip("표시 이름.")]
        private string displayName;

        [SerializeField, TextArea]
        [Tooltip("연구 노드 설명.")]
        private string description;

        [Header("대상 태그")]
        [SerializeField]
        [Tooltip("이 노드가 가중치를 보정하는 태그 목록.")]
        private AITagDefinition[] targetTags;

        [Header("레벨별 보정")]
        [SerializeField]
        [Tooltip("레벨별 가중치 보정치. 인덱스 0 = 레벨 1 값. 누적이 아니라 " +
                 "그 레벨에서의 총 보정치 자체다 (기획 표기 +15/+30/+50 = 레벨 1/2/3 각각의 최종 보정치). " +
                 "음수 허용 (감소 노드).")]
        private float[] weightDeltaPerLevel;

        [SerializeField]
        [Tooltip("레벨별 재화(박살 난 지구본) 비용. weightDeltaPerLevel 과 배열 길이가 같아야 한다.")]
        private int[] costPerLevel;

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────────

        /// <summary>저장 키.</summary>
        public string NodeId => nodeId;

        /// <summary>소속 학파.</summary>
        public ResearchSchool School => school;

        /// <summary>표시 이름.</summary>
        public string DisplayName => displayName;

        /// <summary>연구 노드 설명.</summary>
        public string Description => description;

        /// <summary>이 노드가 가중치를 보정하는 태그 목록.</summary>
        public AITagDefinition[] TargetTags => targetTags;

        /// <summary>레벨별 가중치 보정치 (읽기 전용 원본 — <see cref="GetDelta"/> 사용 권장).</summary>
        public float[] WeightDeltaPerLevel => weightDeltaPerLevel;

        /// <summary>레벨별 재화 비용.</summary>
        public int[] CostPerLevel => costPerLevel;

        /// <summary>최대 레벨 (= weightDeltaPerLevel 배열 길이).</summary>
        public int MaxLevel => weightDeltaPerLevel?.Length ?? 0;

        /// <summary>
        /// 해당 레벨에서 적용되는 가중치 보정치. 0 = 미투자(항상 0f), 1..MaxLevel 은
        /// weightDeltaPerLevel[level-1] 값을 그대로 반환한다 (레벨 간 누적 합산이 아님).
        /// </summary>
        public float GetDelta(int level)
        {
            if (level <= 0 || weightDeltaPerLevel == null) return 0f;
            if (level > weightDeltaPerLevel.Length) level = weightDeltaPerLevel.Length;
            return weightDeltaPerLevel[level - 1];
        }

        /// <summary>해당 레벨에 도달하기 위한 비용. 범위 밖이면 0.</summary>
        public int GetCost(int level)
        {
            if (level <= 0 || costPerLevel == null) return 0;
            if (level > costPerLevel.Length) return 0;
            return costPerLevel[level - 1];
        }

        private void OnValidate()
        {
            if (weightDeltaPerLevel != null && costPerLevel != null &&
                weightDeltaPerLevel.Length != costPerLevel.Length)
            {
                Debug.LogWarning($"[ResearchNodeDefinition] '{name}' — weightDeltaPerLevel 길이" +
                                  $"({weightDeltaPerLevel.Length})와 costPerLevel 길이({costPerLevel.Length})가 다릅니다.");
            }
        }

#if UNITY_EDITOR
        /// <summary>에셋 생성기 전용 — 런타임 호출 금지.</summary>
        internal void EditorInit(string nodeId, ResearchSchool school, string displayName,
            string description, AITagDefinition[] targetTags,
            float[] weightDeltaPerLevel, int[] costPerLevel)
        {
            this.nodeId              = nodeId;
            this.school              = school;
            this.displayName         = displayName;
            this.description         = description;
            this.targetTags          = targetTags;
            this.weightDeltaPerLevel = weightDeltaPerLevel;
            this.costPerLevel        = costPerLevel;
        }
#endif
    }
}
