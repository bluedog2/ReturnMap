namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StatModifier — 스펙 태그가 개체 기본 스탯에 가하는 증감 규칙
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>개체 스탯 중 태그로 증감 가능한 항목.</summary>
    public enum AgentStatType
    {
        /// <summary>이동속도 배율 기준치.</summary>
        MoveSpeed,

        /// <summary>최대 체력.</summary>
        MaxHP,

        /// <summary>피격 넉백 배율.</summary>
        KnockbackMultiplier,

        /// <summary>크기 배율.</summary>
        Scale,
    }

    /// <summary>스탯 증감 연산 종류.</summary>
    public enum StatModifierOp
    {
        /// <summary>덧셈.</summary>
        Add,

        /// <summary>곱셈.</summary>
        Multiply,
    }

    /// <summary>
    /// 태그 1개가 특정 스탯에 가하는 증감 규칙 1건.
    /// <para>
    /// 적용 순서 규약: 같은 <see cref="AgentStatType"/> 에 대해 <see cref="StatModifierOp.Add"/>
    /// 를 모두 합산한 뒤, 그 결과에 <see cref="StatModifierOp.Multiply"/> 를 모두 곱한다.
    /// (기본값 + Add 합) × (Multiply 곱) 순서 — 곱셈 태그끼리 서로 곱하지 않고 각각 결과에
    /// 곱해지는 표준 "가산 후 승산" 규칙.
    /// </para>
    /// <para>인스펙터 배열 요소로 직렬화되므로 public 필드를 유지한다.</para>
    /// </summary>
    [System.Serializable]
    public struct StatModifier
    {
        public AgentStatType stat;
        public StatModifierOp op;
        public float value;
    }
}
