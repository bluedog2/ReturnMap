using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentArchetype — 태그가 얹히기 전의 기본 개체 스펙 (ScriptableObject)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 태그가 얹히기 전의 기본 개체(적) 스펙을 담는 에셋. 최종 스탯 = 이 기본치에
    /// <see cref="TagSet.ApplyStat"/> 을 적용한 값(2단계 AgentContext 에서 합성).
    /// <para>위치 권장: Assets/_Project/Settings/AI/Archetypes/Archetype_Xxx.asset</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI 아키타입(Agent Archetype)", fileName = "Archetype_")]
    public class AgentArchetype : ScriptableObject
    {
        [Header("정체성")]
        [SerializeField]
        [Tooltip("표시 이름 (예: \"고블린 척후병\").")]
        private string displayName;

        [Header("프리팹")]
        [SerializeField]
        [Tooltip("스폰할 VerificationAgent 프리팹.")]
        private VerificationAgent agentPrefab;

        [Header("기본 스탯 (태그 적용 전)")]
        [SerializeField]
        [Tooltip("기준 이동속도 (칸/초).")]
        private float baseMoveSpeed = 4f;

        [SerializeField]
        [Tooltip("최대 체력. 태그(뚱뚱이 +1, 거대화 3 등)가 여기에 얹힌다. " +
                 "함정 1대에 즉사하지 않고 데미지를 받으며 전진하는 그림을 위해 기본 3.")]
        private int baseMaxHP = 3;

        [SerializeField]
        [Tooltip("피격 넉백 배율.")]
        private float baseKnockbackMultiplier = 1f;

        [Header("성향")]
        [SerializeField]
        [Tooltip("개체별 성향 (null 허용). 경로 계획 파라미터·개체별 AIMemory 학습에 사용됨.")]
        private AIPersonality personality;

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────────

        /// <summary>표시 이름.</summary>
        public string DisplayName => displayName;

        /// <summary>스폰할 VerificationAgent 프리팹.</summary>
        public VerificationAgent AgentPrefab => agentPrefab;

        /// <summary>기준 이동속도 (칸/초).</summary>
        public float BaseMoveSpeed => baseMoveSpeed;

        /// <summary>최대 체력.</summary>
        public int BaseMaxHP => baseMaxHP;

        /// <summary>피격 넉백 배율.</summary>
        public float BaseKnockbackMultiplier => baseKnockbackMultiplier;

        /// <summary>개체별 성향 (null 허용).</summary>
        public AIPersonality Personality => personality;

#if UNITY_EDITOR
        /// <summary>에셋 생성기 전용 — 런타임 호출 금지.</summary>
        internal void EditorInit(string displayName, VerificationAgent agentPrefab,
            float baseMoveSpeed, int baseMaxHP, float baseKnockbackMultiplier,
            AIPersonality personality)
        {
            this.displayName             = displayName;
            this.agentPrefab             = agentPrefab;
            this.baseMoveSpeed           = baseMoveSpeed;
            this.baseMaxHP               = baseMaxHP;
            this.baseKnockbackMultiplier = baseKnockbackMultiplier;
            this.personality             = personality;
        }
#endif
    }
}
