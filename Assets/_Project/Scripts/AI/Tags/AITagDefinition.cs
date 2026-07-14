using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AITagDefinition — AI 태그 1종의 데이터 에셋
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AI 태그 1종(35종 중 하나)의 스펙을 담는 ScriptableObject. 태그 1종 = 에셋 1개.
    /// <para>
    /// 대부분의 태그는 데이터만으로 완성된다: 스펙 태그는 <see cref="statModifiers"/> 만,
    /// 면역 태그는 <see cref="immunities"/>/<see cref="flags"/> 만, 이동 태그는
    /// <see cref="movementTrait"/> 만 채우는 식이다.
    /// </para>
    /// <para>위치 권장: Assets/_Project/Settings/AI/Tags/Tag_Xxx.asset</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI 태그(Tag Definition)", fileName = "Tag_")]
    public class AITagDefinition : ScriptableObject
    {
        [Header("정체성")]
        [SerializeField]
        [Tooltip("저장/디버그 키 (예: \"M-01\").")]
        private string tagId;

        [SerializeField]
        [Tooltip("소속 카테고리 (이동 / 함정 방어·면역 / 스펙).")]
        private AITagCategory category;

        [SerializeField]
        [Tooltip("표시 이름 (예: \"천진난만\").")]
        private string displayName;

        [SerializeField]
        [Tooltip("진입 전 UI·머리 위 아이콘.")]
        private Sprite icon;

        [SerializeField, TextArea]
        [Tooltip("0.1초 안에 파악할 수 있는 한 줄 설명.")]
        private string shortDescription;

        [Header("조합 규칙")]
        [SerializeField]
        [Tooltip("0 = 제한 없음. 같은 그룹 번호끼리 동시 부여 금지 " +
                 "(예: 속도군=과속/태평함/스피드스타, 점프군=높이뛰기/멀리뛰기).")]
        private int exclusionGroup;

        [Header("효과 — 스탯")]
        [SerializeField]
        [Tooltip("이 태그가 개체 기본 스탯에 가하는 증감 규칙 목록.")]
        private StatModifier[] statModifiers;

        [Header("효과 — 면역/특성")]
        [SerializeField]
        [Tooltip("면역인 피해 타입 마스크.")]
        private DamageType immunities;

        [SerializeField]
        [Tooltip("특수 거동 플래그.")]
        private SpecialFlag flags;

        [Header("효과 — 이동 로직")]
        [SerializeField]
        [Tooltip("이동 태그의 런타임 로직 SO. 이동 태그가 아니면 비워둔다(null 허용).")]
        private MovementTrait movementTrait;

        [Header("효과 — 이벤트 훅")]
        [SerializeField]
        [Tooltip("스폰/사망/피격 등 수명주기 이벤트에 반응하는 훅 목록.")]
        private TagEventHook[] eventHooks;

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────────

        /// <summary>저장/디버그 키.</summary>
        public string TagId => tagId;

        /// <summary>소속 카테고리.</summary>
        public AITagCategory Category => category;

        /// <summary>표시 이름.</summary>
        public string DisplayName => displayName;

        /// <summary>진입 전 UI·머리 위 아이콘.</summary>
        public Sprite Icon => icon;

        /// <summary>한 줄 설명.</summary>
        public string ShortDescription => shortDescription;

        /// <summary>배타 그룹 번호. 0 = 제한 없음.</summary>
        public int ExclusionGroup => exclusionGroup;

        /// <summary>스탯 증감 규칙 목록.</summary>
        public StatModifier[] StatModifiers => statModifiers;

        /// <summary>면역인 피해 타입 마스크.</summary>
        public DamageType Immunities => immunities;

        /// <summary>특수 거동 플래그.</summary>
        public SpecialFlag Flags => flags;

        /// <summary>이동 태그의 런타임 로직 SO (null 허용).</summary>
        public MovementTrait MovementTrait => movementTrait;

        /// <summary>수명주기 이벤트 훅 목록.</summary>
        public TagEventHook[] EventHooks => eventHooks;

#if UNITY_EDITOR
        /// <summary>에셋 생성기 전용 — 런타임 호출 금지.</summary>
        internal void EditorInit(string tagId, AITagCategory category, string displayName,
            string shortDescription, int exclusionGroup, StatModifier[] statModifiers,
            DamageType immunities, SpecialFlag flags)
        {
            this.tagId            = tagId;
            this.category         = category;
            this.displayName      = displayName;
            this.shortDescription = shortDescription;
            this.exclusionGroup   = exclusionGroup;
            this.statModifiers    = statModifiers;
            this.immunities       = immunities;
            this.flags            = flags;
        }
#endif
    }
}
