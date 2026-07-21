using System.Collections.Generic;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TagWeightService — 최종 태그 가중치 공식의 유일한 진실 소스 (정적 순수)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 기획 공식 "최종 등장 확률(%) = (태그 기본 가중치 + 아웃게임 보정 가중치) /
    /// 해당 카테고리 전체 가중치 총합 × 100" 을 계산하는 정적 순수 클래스.
    /// <para>
    /// <see cref="ComputeFinalWeights"/> 로 만든 최종 가중치를 <see cref="StagePreviewPanel"/>
    /// (표기)과 <see cref="StageAgentRoster.Build"/> (실제 추첨)가 동일하게 사용해야
    /// "표기와 실제 굴림이 같은 데이터" 를 보증한다 — 이것이 이 서비스가 존재하는 이유다.
    /// </para>
    /// <para><b>후속 작업</b>: <see cref="VerificationDirector"/> 의 EnsureRoster 에서
    /// 이 서비스의 최종 가중치를 <see cref="StageAgentRoster.Build"/> 의 weightOverrides 로
    /// 주입하는 통합은 아직 되어 있지 않다 (Director 는 6단계에서 건드리지 않음 — 후속 작업).</para>
    /// </summary>
    public static class TagWeightService
    {
        /// <summary>
        /// <paramref name="table"/> 의 기본 가중치에 <paramref name="nodes"/> 의 투자 레벨에 따른
        /// 보정치를 합산해 <paramref name="results"/> 에 채운다 (0 미만은 0으로 클램프).
        /// <paramref name="results"/> 는 호출자가 재사용하는 리스트로, 이 메서드는 항상
        /// 먼저 Clear 후 채운다 (프레임 할당 최소화). <paramref name="nodes"/> 가 null 이면
        /// 보정 없이 기본 가중치만 사용한다. 런타임에 에셋을 스캔하는 오버로드는 제공하지
        /// 않는다 — 호출자가 노드 목록을 인스펙터 등으로 보유해야 한다.
        /// </summary>
        public static void ComputeFinalWeights(
            StageSpawnTable table,
            IReadOnlyList<ResearchNodeDefinition> nodes,
            List<StageSpawnTable.TagWeightEntry> results)
        {
            results.Clear();
            if (table == null) return;

            StageSpawnTable.TagWeightEntry[] baseWeights = table.BaseWeights;
            if (baseWeights == null) return;

            for (int i = 0; i < baseWeights.Length; i++)
            {
                StageSpawnTable.TagWeightEntry entry = baseWeights[i];

                float final = entry.baseWeight + SumNodeDelta(entry.tag, nodes);
                if (final < 0f) final = 0f;

                entry.baseWeight = final;
                results.Add(entry);
            }
        }

        /// <summary>대상 태그에 걸린 모든 노드의 (투자 레벨 기준) 보정치 합.</summary>
        private static float SumNodeDelta(AITagDefinition tag, IReadOnlyList<ResearchNodeDefinition> nodes)
        {
            if (tag == null || nodes == null) return 0f;

            float sum = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResearchNodeDefinition node = nodes[i];
                if (node == null) continue;

                int level = ResearchBoardState.GetLevel(node);
                if (level <= 0) continue;

                AITagDefinition[] targets = node.TargetTags;
                if (targets == null) continue;

                for (int t = 0; t < targets.Length; t++)
                {
                    if (targets[t] == tag)
                    {
                        sum += node.GetDelta(level);
                        break;
                    }
                }
            }

            return sum;
        }

        /// <summary>
        /// 기획 공식대로 <paramref name="tag"/> 와 같은 카테고리의 가중치 총합 대비
        /// <paramref name="tag"/> 가중치의 비율(%)을 반환한다. 진입 전 UI 표기용.
        /// 카테고리 총합이 0 이하면 0을 반환한다.
        /// </summary>
        public static float GetDisplayProbability(
            IReadOnlyList<StageSpawnTable.TagWeightEntry> finalWeights, AITagDefinition tag)
        {
            if (finalWeights == null || tag == null) return 0f;

            float categorySum = 0f;
            float tagWeight   = 0f;

            for (int i = 0; i < finalWeights.Count; i++)
            {
                StageSpawnTable.TagWeightEntry entry = finalWeights[i];
                if (entry.tag == null || entry.baseWeight <= 0f) continue;
                if (entry.tag.Category != tag.Category) continue;

                categorySum += entry.baseWeight;
                if (entry.tag == tag) tagWeight = entry.baseWeight;
            }

            if (categorySum <= 0f) return 0f;
            return tagWeight / categorySum * 100f;
        }
    }
}
