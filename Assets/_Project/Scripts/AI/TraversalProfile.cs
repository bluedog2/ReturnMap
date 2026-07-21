namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TraversalProfile — 검증 AI 이동 능력 프로파일 (중력 인지 경로계획 입력)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 태그/아키타입에서 파생된 개체 1명의 <b>플랫포머 이동 능력치</b>.
    /// <see cref="NavGrid"/> 빌드와 <see cref="AStarPathPlanner"/> 의 링크 확장이
    /// 이 값을 소비해 "걸을 수 있나 / 뛸 수 있나 / 낙하를 감수하나"를 판단합니다.
    /// <para>
    /// 태그 SO(<see cref="AITagDefinition"/>)를 직접 참조하지 않고 <see cref="MovementTrait"/>
    /// 의 <see cref="MovementTrait.ModifyTraversal"/> 질의로 파생시켜 순환 의존을 피합니다.
    /// </para>
    /// </summary>
    public readonly struct TraversalProfile
    {
        /// <summary>점프로 오를 수 있는 최대 칸수 (기본 1).</summary>
        public readonly int MaxJumpHeight;

        /// <summary>점프로 건널 수 있는 최대 가로 칸수 (기본 2).</summary>
        public readonly int MaxJumpDistance;

        /// <summary>자진 낙하를 감수하는 최대 낙차 (기본 4, 지름길 중독이면 int.MaxValue).</summary>
        public readonly int MaxFallHeight;

        /// <summary>면역 함정의 위험 비용을 0으로 만드는 피해 타입 마스크.</summary>
        public readonly DamageType Immunities;

        /// <summary>지름길 중독(D-11) — 낙하 비용 페널티가 없다.</summary>
        public readonly bool RecklessDrop;

        public TraversalProfile(int maxJumpHeight, int maxJumpDistance, int maxFallHeight,
                                DamageType immunities, bool recklessDrop)
        {
            MaxJumpHeight   = maxJumpHeight;
            MaxJumpDistance = maxJumpDistance;
            MaxFallHeight   = maxFallHeight;
            Immunities      = immunities;
            RecklessDrop    = recklessDrop;
        }

        /// <summary>태그가 하나도 없는 기본 개체의 프로파일 (1/2/4/면역없음/지름길중독아님).</summary>
        public static TraversalProfile Default { get; } =
            new TraversalProfile(1, 2, 4, DamageType.None, false);

        /// <summary>
        /// 개체의 <see cref="TagSet"/> 에서 이동 능력 프로파일을 파생시킵니다. 로컬 초기값은
        /// <see cref="Default"/> 에서 시작해(단일 소스 — 하드코딩 중복 방지) 각 태그의
        /// <see cref="MovementTrait"/> 가 있으면 <see cref="MovementTrait.ModifyTraversal"/> 로
        /// 점프/낙하 스펙을 질의합니다(점프계는 여러 개면 각 Trait 이 Mathf.Max 로 상향,
        /// 낙하 허용치는 Mathf.Min 으로 하향). 면역은 <see cref="TagSet"/> 의 접힌 결과를
        /// 그대로 사용합니다.
        /// <para><b>낙하 허용치 우선순위</b>: 지름길 중독(RecklessDrop)이 기본값을
        /// int.MaxValue 로 올려놔도, 안전제일(<see cref="CliffReverseTrait"/>)의
        /// ModifyTraversal 은 Mathf.Min 으로 하향 클램프하므로 순서와 무관하게 항상 더
        /// 작은 값(=안전제일)이 이긴다 — 두 태그가 동시 부여돼도 "낙사 메타 차단"이 우선.</para>
        /// </summary>
        public static TraversalProfile From(TagSet tags)
        {
            int  maxJumpHeight   = Default.MaxJumpHeight;
            int  maxJumpDistance = Default.MaxJumpDistance;
            bool recklessDrop    = tags != null && tags.HasFlag(SpecialFlag.RecklessDrop);
            int  maxFallHeight   = recklessDrop ? int.MaxValue : Default.MaxFallHeight;

            if (tags != null)
            {
                var list = tags.Tags;
                for (int i = 0; i < list.Count; i++)
                {
                    MovementTrait trait = list[i] != null ? list[i].MovementTrait : null;
                    trait?.ModifyTraversal(ref maxJumpHeight, ref maxJumpDistance, ref maxFallHeight);
                }
            }

            DamageType immunities = tags != null ? tags.ImmunityMask : DamageType.None;

            return new TraversalProfile(maxJumpHeight, maxJumpDistance, maxFallHeight, immunities, recklessDrop);
        }

        /// <summary>프로파일 동일성 비교용 해시 (그리드 캐시 키로 쓸 것을 염두에 둔 값).</summary>
        public int CacheKey
        {
            get
            {
                int hash = 17;
                hash = hash * 31 + MaxJumpHeight;
                hash = hash * 31 + MaxJumpDistance;
                hash = hash * 31 + (MaxFallHeight == int.MaxValue ? -1 : MaxFallHeight);
                hash = hash * 31 + (int)Immunities;
                hash = hash * 31 + (RecklessDrop ? 1 : 0);
                return hash;
            }
        }
    }
}
