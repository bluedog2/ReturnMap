using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageSpawnTable — 스테이지 1개의 스폰 구성 (ScriptableObject)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 스테이지 1개에서 스폰할 개체 구성(아키타입·확정 태그·랜덤 태그 슬롯)과
    /// 태그 기본 가중치, 스폰 간격, 방어 실패(뚫림) 조건을 담는 에셋.
    /// <para>위치 권장: Assets/_Project/Settings/AI/SpawnTables/SpawnTable_Stage01.asset</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/스테이지 스폰 테이블(Stage Spawn Table)", fileName = "SpawnTable_Stage")]
    public class StageSpawnTable : ScriptableObject
    {
        /// <summary>
        /// 아키타입 1종에 대한 스폰 묶음. 기획 예시 "10마리 중 5마리는 [맷집왕] 확정,
        /// 5마리는 랜덤" 을 표현한다.
        /// <para>제약: fixedTags.Length + randomTagSlots ≤ TagSet.MaxTagsPerAgent 여야 한다.</para>
        /// </summary>
        [System.Serializable]
        public struct AgentSpawnEntry
        {
            [Tooltip("스폰할 아키타입.")]
            public AgentArchetype archetype;

            [Tooltip("스폰 개체 수.")]
            public int count;

            [Tooltip("확정 부여 태그 목록.")]
            public AITagDefinition[] fixedTags;

            [Tooltip("가중치 추첨으로 채울 랜덤 태그 슬롯 수. " +
                      "fixedTags.Length + randomTagSlots ≤ TagSet.MaxTagsPerAgent 여야 한다.")]
            public int randomTagSlots;
        }

        /// <summary>스테이지 기본 태그 가중치 테이블의 항목 1건.</summary>
        [System.Serializable]
        public struct TagWeightEntry
        {
            [Tooltip("가중치 대상 태그.")]
            public AITagDefinition tag;

            [Tooltip("기본 가중치. 0 이하면 추첨에서 제외된다.")]
            public float baseWeight;
        }

        [Header("스폰 구성")]
        [SerializeField]
        [Tooltip("아키타입별 스폰 묶음 목록.")]
        private AgentSpawnEntry[] entries;

        [Header("태그 가중치")]
        [SerializeField]
        [Tooltip("스테이지 기본 태그 가중치 테이블.")]
        private TagWeightEntry[] baseWeights;

        [Header("스폰 타이밍")]
        [SerializeField]
        [Tooltip("개체 간 스폰 간격 (초).")]
        private float spawnInterval = 1.5f;

        [Header("방어 실패 조건")]
        [SerializeField, Min(1)]
        [Tooltip("이 수 이상의 적이 골에 도달하면 방어 실패(뚫림). 기획 변경 가능성이 있어 데이터로 둠.")]
        private int breachThreshold = 1;

        [Header("검증 규칙 범위")]
        [SerializeField, Min(0f)]
        [Tooltip("검증 제한 시간 하한(초). 연구 단축 후에도 이 값 아래로 내려가지 않는다.")]
        private float timeLimitMin = 40f;

        [SerializeField, Min(0f)]
        [Tooltip("검증 제한 시간 상한(초).")]
        private float timeLimitMax = 60f;

        [SerializeField, Min(0)]
        [Tooltip("검증 투입 마릿수 하한. 실제 로스터 총원을 넘지 않는다.")]
        private int agentCountMin = 7;

        [SerializeField, Min(0)]
        [Tooltip("검증 투입 마릿수 상한. 실제 로스터 총원을 넘지 않는다.")]
        private int agentCountMax = 10;

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────────

        /// <summary>아키타입별 스폰 묶음 목록.</summary>
        public AgentSpawnEntry[] Entries => entries;

        /// <summary>스테이지 기본 태그 가중치 테이블.</summary>
        public TagWeightEntry[] BaseWeights => baseWeights;

        /// <summary>개체 간 스폰 간격 (초).</summary>
        public float SpawnInterval => spawnInterval;

        /// <summary>방어 실패(뚫림) 기준 골 도달 적 수.</summary>
        public int BreachThreshold => breachThreshold;

        /// <summary>검증 제한 시간 하한(초).</summary>
        public float TimeLimitMin => timeLimitMin;

        /// <summary>검증 제한 시간 상한(초).</summary>
        public float TimeLimitMax => timeLimitMax;

        /// <summary>검증 투입 마릿수 하한.</summary>
        public int AgentCountMin => agentCountMin;

        /// <summary>검증 투입 마릿수 상한.</summary>
        public int AgentCountMax => agentCountMax;
    }
}
