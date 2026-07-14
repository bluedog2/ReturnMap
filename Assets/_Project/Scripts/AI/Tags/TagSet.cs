using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TagSet — 개체 1명에 부여된 태그 묶음 (런타임, 스폰 시 1회 접기)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 에이전트 1개체에 부여된 <see cref="AITagDefinition"/> 묶음과, 스폰 시 1회 접어
    /// (fold) 캐시해 둔 면역/플래그 결과를 담는 런타임 클래스 (ScriptableObject 아님).
    /// </summary>
    public sealed class TagSet
    {
        /// <summary>개체당 부여 가능한 태그 상한.</summary>
        public const int MaxTagsPerAgent = 3;

        private readonly List<AITagDefinition> _tags;

        /// <summary>부여된 태그 목록 (읽기 전용).</summary>
        public IReadOnlyList<AITagDefinition> Tags => _tags;

        /// <summary>전 태그의 면역 마스크를 OR 로 접은 결과.</summary>
        public DamageType ImmunityMask { get; }

        /// <summary>전 태그의 특수 플래그를 OR 로 접은 결과.</summary>
        public SpecialFlag Flags { get; }

        /// <summary>태그 0개인 빈 세트.</summary>
        public static readonly TagSet Empty = new TagSet(null);

        public TagSet(IReadOnlyList<AITagDefinition> tags)
        {
            _tags = new List<AITagDefinition>(MaxTagsPerAgent);

            if (tags != null)
            {
                int count = tags.Count;
                if (count > MaxTagsPerAgent)
                {
                    Debug.LogError($"TagSet: 태그 개수({count})가 상한({MaxTagsPerAgent})을 " +
                                    "초과해 앞 3개만 사용합니다.");
                    count = MaxTagsPerAgent;
                }

                for (int i = 0; i < count; i++)
                    _tags.Add(tags[i]);
            }

            DamageType immunityMask = DamageType.None;
            SpecialFlag flags       = SpecialFlag.None;

            for (int i = 0; i < _tags.Count; i++)
            {
                AITagDefinition tag = _tags[i];
                if (tag == null) continue;

                immunityMask |= tag.Immunities;
                flags        |= tag.Flags;
            }

            ImmunityMask = immunityMask;
            Flags        = flags;
        }

        /// <summary>주어진 피해 타입에 면역인지 (비트 AND 판정).</summary>
        public bool IsImmuneTo(DamageType type)
        {
            return (ImmunityMask & type) != DamageType.None;
        }

        /// <summary>주어진 특수 플래그를 보유했는지 (enum.HasFlag 박싱 회피, 직접 & 연산).</summary>
        public bool HasFlag(SpecialFlag flag)
        {
            return (Flags & flag) == flag;
        }

        /// <summary>
        /// 전 태그의 statModifiers 를 Add 전부 합산 → Multiply 전부 곱산 순서로 적용해
        /// 반환한다. 스폰 시 1회만 호출되는 용도 (매 프레임 호출 금지).
        /// </summary>
        public float ApplyStat(AgentStatType stat, float baseValue)
        {
            float sum  = 0f;
            float mult = 1f;

            for (int i = 0; i < _tags.Count; i++)
            {
                AITagDefinition tag = _tags[i];
                if (tag == null || tag.StatModifiers == null) continue;

                StatModifier[] modifiers = tag.StatModifiers;
                for (int j = 0; j < modifiers.Length; j++)
                {
                    StatModifier mod = modifiers[j];
                    if (mod.stat != stat) continue;

                    if (mod.op == StatModifierOp.Add)
                        sum += mod.value;
                    else
                        mult *= mod.value;
                }
            }

            return (baseValue + sum) * mult;
        }
    }
}
