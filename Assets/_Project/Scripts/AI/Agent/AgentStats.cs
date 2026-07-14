using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentStats — 태그가 접힌 최종 스탯 (스폰 시 1회 계산 후 캐시)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="AgentArchetype"/> 기본치에 <see cref="TagSet.ApplyStat"/> 을 적용해
    /// 접어(fold) 낸 개체 1명의 최종 스탯.
    /// <para>
    /// <b>스폰 시 1회 계산 후 캐시</b> — <see cref="AgentContext.Initialize"/> 에서만
    /// 생성되며, 프레임 중 재계산하지 않는다 (매 프레임 ApplyStat 재호출 금지).
    /// </para>
    /// </summary>
    public readonly struct AgentStats
    {
        /// <summary>이동속도 (칸/초).</summary>
        public readonly float MoveSpeed;

        /// <summary>최대 체력.</summary>
        public readonly int MaxHP;

        /// <summary>피격 넉백 배율.</summary>
        public readonly float KnockbackMultiplier;

        /// <summary>크기 배율 (거대화 2배 등).</summary>
        public readonly float Scale;

        private AgentStats(float moveSpeed, int maxHp, float knockbackMultiplier, float scale)
        {
            MoveSpeed           = moveSpeed;
            MaxHP               = maxHp;
            KnockbackMultiplier = knockbackMultiplier;
            Scale               = scale;
        }

        /// <summary>
        /// archetype 기본치에 tags 를 적용해 최종 스탯을 계산합니다.
        /// archetype 이 null 이면 안전 기본값(이동속도 4, HP 1, 넉백 1배, 크기 1배)을 사용합니다.
        /// </summary>
        public static AgentStats From(AgentArchetype archetype, TagSet tags)
        {
            float baseMoveSpeed  = archetype != null ? archetype.BaseMoveSpeed           : 4f;
            int   baseMaxHP      = archetype != null ? archetype.BaseMaxHP               : 1;
            float baseKnockback  = archetype != null ? archetype.BaseKnockbackMultiplier : 1f;
            const float baseScale = 1f; // AgentArchetype 에 기본 크기 필드가 없음 — 태그가 없으면 항상 1배

            TagSet t = tags ?? TagSet.Empty;

            float moveSpeed = t.ApplyStat(AgentStatType.MoveSpeed, baseMoveSpeed);
            int   maxHp     = Mathf.Max(1, Mathf.RoundToInt(t.ApplyStat(AgentStatType.MaxHP, baseMaxHP)));
            float knockback = t.ApplyStat(AgentStatType.KnockbackMultiplier, baseKnockback);
            float scale     = t.ApplyStat(AgentStatType.Scale, baseScale);

            return new AgentStats(moveSpeed, maxHp, knockback, scale);
        }
    }
}
