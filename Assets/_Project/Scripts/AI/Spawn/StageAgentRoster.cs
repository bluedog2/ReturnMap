using System.Collections.Generic;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  StageAgentRoster — 스테이지 진입 시 확정되는 전체 웨이브 구성 (런타임)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 스테이지 진입 시 시드 고정으로 전체 웨이브 구성을 1회 확정하는 로스터.
    /// 진입 전 UI·검증 페이즈·방어/플레이 페이즈가 같은 로스터를 읽어
    /// "UI에서 본 구성 = 실제 스폰" 을 보증한다.
    /// </summary>
    public sealed class StageAgentRoster
    {
        /// <summary>개체 1명의 확정된 스폰 계획 (아키타입 + 부여된 태그).</summary>
        public readonly struct AgentSpawnPlan
        {
            public readonly AgentArchetype Archetype;
            public readonly TagSet Tags;

            public AgentSpawnPlan(AgentArchetype archetype, TagSet tags)
            {
                Archetype = archetype;
                Tags      = tags;
            }
        }

        private readonly List<AgentSpawnPlan> _plans;

        /// <summary>확정된 전체 스폰 계획 목록 (읽기 전용).</summary>
        public IReadOnlyList<AgentSpawnPlan> Plans => _plans;

        /// <summary>이 로스터를 확정할 때 사용한 시드.</summary>
        public int Seed { get; }

        /// <summary>이 로스터의 원본 스폰 테이블.</summary>
        public StageSpawnTable Table { get; }

        private StageAgentRoster(StageSpawnTable table, int seed, List<AgentSpawnPlan> plans)
        {
            Table  = table;
            Seed   = seed;
            _plans = plans;
        }

        /// <summary>
        /// 스폰 테이블과 시드로 전체 웨이브 구성을 1회 확정한다.
        /// <paramref name="weightOverrides"/> 가 null 이면 <paramref name="table"/> 의
        /// BaseWeights 를 사용하고, 아니면 그것을 사용한다 (아웃게임 보정이 적용된 최종
        /// 가중치를 6단계 TagWeightService 가 넘겨줄 자리).
        /// </summary>
        public static StageAgentRoster Build(
            StageSpawnTable table,
            int seed,
            IReadOnlyList<StageSpawnTable.TagWeightEntry> weightOverrides = null)
        {
            var rng     = new System.Random(seed);
            var weights = weightOverrides ?? table.BaseWeights;
            var plans   = new List<AgentSpawnPlan>();

            StageSpawnTable.AgentSpawnEntry[] entries = table.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    StageSpawnTable.AgentSpawnEntry entry = entries[i];
                    if (entry.archetype == null) continue;

                    for (int n = 0; n < entry.count; n++)
                    {
                        List<AITagDefinition> tags = WeightedTagSelector.Roll(
                            weights, entry.fixedTags, entry.randomTagSlots, rng);

                        plans.Add(new AgentSpawnPlan(entry.archetype, new TagSet(tags)));
                    }
                }
            }

            return new StageAgentRoster(table, seed, plans);
        }
    }
}
