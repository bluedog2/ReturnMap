using System.Collections.Generic;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  WeightedTagSelector — 가중치 추첨으로 랜덤 태그 슬롯을 채우는 순수 함수
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 확정 태그 + 가중치 추첨으로 뽑은 랜덤 태그를 합쳐 개체 1명의 태그 목록을
    /// 구성하는 정적 순수 클래스. 재현성을 위해 <see cref="System.Random"/> 을
    /// 호출부에서 주입받는다 (UnityEngine.Random 사용 금지).
    /// </summary>
    public static class WeightedTagSelector
    {
        /// <summary>
        /// 확정 태그 + 랜덤 슬롯 수만큼 가중치 추첨으로 태그를 뽑아 목록으로 반환한다.
        /// <para>규칙: 결과 크기는 <see cref="TagSet.MaxTagsPerAgent"/> 를 초과하지 않는다
        /// (확정 태그가 먼저 자리를 차지). 중복 태그 금지. exclusionGroup 이 0 이 아니고
        /// 이미 뽑힌 태그와 그룹이 같은 후보는 제외. 가중치 ≤ 0 후보는 제외. 유효 후보가
        /// 바닥나면 남은 슬롯은 비운 채 종료한다 (에러 아님).</para>
        /// </summary>
        public static List<AITagDefinition> Roll(
            IReadOnlyList<StageSpawnTable.TagWeightEntry> weights,
            IReadOnlyList<AITagDefinition> fixedTags,
            int randomSlots,
            System.Random rng)
        {
            var result = new List<AITagDefinition>(TagSet.MaxTagsPerAgent);

            // ── 확정 태그 먼저 채움 (상한 초과 방지) ─────────────────────────
            if (fixedTags != null)
            {
                for (int i = 0; i < fixedTags.Count && result.Count < TagSet.MaxTagsPerAgent; i++)
                {
                    if (fixedTags[i] != null)
                        result.Add(fixedTags[i]);
                }
            }

            // ── 랜덤 슬롯을 가중치 추첨으로 채움 ─────────────────────────────
            int slotsToFill = randomSlots;
            if (result.Count + slotsToFill > TagSet.MaxTagsPerAgent)
                slotsToFill = TagSet.MaxTagsPerAgent - result.Count;

            if (weights == null || slotsToFill <= 0)
                return result;

            var candidates = new List<StageSpawnTable.TagWeightEntry>(weights.Count);

            for (int slot = 0; slot < slotsToFill; slot++)
            {
                candidates.Clear();
                float totalWeight = 0f;

                for (int i = 0; i < weights.Count; i++)
                {
                    StageSpawnTable.TagWeightEntry entry = weights[i];
                    AITagDefinition tag = entry.tag;

                    if (tag == null) continue;
                    if (entry.baseWeight <= 0f) continue;
                    if (IsAlreadyChosen(result, tag)) continue;
                    if (tag.ExclusionGroup != 0 && HasExclusionConflict(result, tag.ExclusionGroup)) continue;

                    candidates.Add(entry);
                    totalWeight += entry.baseWeight;
                }

                if (candidates.Count == 0 || totalWeight <= 0f)
                    break; // 유효 후보 바닥 — 남은 슬롯은 비운 채 종료

                double roll = rng.NextDouble() * totalWeight;
                AITagDefinition chosen = null;
                double cumulative = 0.0;

                for (int i = 0; i < candidates.Count; i++)
                {
                    cumulative += candidates[i].baseWeight;
                    if (roll < cumulative)
                    {
                        chosen = candidates[i].tag;
                        break;
                    }
                }

                if (chosen == null)
                    chosen = candidates[candidates.Count - 1].tag; // 부동소수 오차 보정 폴백

                result.Add(chosen);
            }

            return result;
        }

        private static bool IsAlreadyChosen(List<AITagDefinition> chosen, AITagDefinition tag)
        {
            for (int i = 0; i < chosen.Count; i++)
                if (chosen[i] == tag) return true;
            return false;
        }

        private static bool HasExclusionConflict(List<AITagDefinition> chosen, int exclusionGroup)
        {
            for (int i = 0; i < chosen.Count; i++)
            {
                AITagDefinition tag = chosen[i];
                if (tag != null && tag.ExclusionGroup != 0 && tag.ExclusionGroup == exclusionGroup)
                    return true;
            }
            return false;
        }
    }
}
