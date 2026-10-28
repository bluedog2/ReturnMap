using System.Collections.Generic;
using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationRuleSet — 검증 1회에 적용되는 확정 규칙 값
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>제한 시간·투입 마릿수·라이프(허용 돌파 수)로 확정된 검증 규칙.</summary>
    public readonly struct VerificationRuleSet
    {
        /// <summary>제한 시간(초).</summary>
        public readonly float TimeLimit;

        /// <summary>실제 투입 마릿수.</summary>
        public readonly int AgentCount;

        /// <summary>허용 돌파 수. 도달 수가 이 값 이상이면 방어 실패.</summary>
        public readonly int Life;

        public VerificationRuleSet(float timeLimit, int agentCount, int life)
        {
            TimeLimit  = timeLimit;
            AgentCount = agentCount;
            Life       = life;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationRules — 검증 페이즈 규칙 계산 (정적 순수)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 스폰 테이블의 범위(시간·마릿수·breachThreshold)와 연구 보정을 합쳐 검증 규칙을 확정하는
    /// 정적 순수 클래스. <see cref="Resolve"/> 는 정적 상태를 읽지 않아 EditMode 에서 그대로
    /// 검증할 수 있고, 연구 레벨 조회가 필요한 <see cref="SumEffect"/> 만
    /// <see cref="ResearchBoardState"/> 를 사용한다.
    /// </summary>
    public static class VerificationRules
    {
        /// <summary>
        /// 규칙을 확정한다.
        /// <para>마릿수 = clamp(총원 − 감소, min(하한, 총원), min(상한, 총원)),
        /// 시간 = clamp(기본 − 단축, 하한, 상한), 라이프 = clamp(breachThreshold + 보너스, 1, 마릿수).</para>
        /// <para><paramref name="table"/> 이 null 이면(폴백 로스터) 마릿수 = 총원, 시간 = 기본 시간,
        /// 라이프 = clamp(1 + 보너스, 1, 마릿수) 로 연구 단축·감소를 적용하지 않는다.</para>
        /// </summary>
        public static VerificationRuleSet Resolve(
            StageSpawnTable table, int rosterTotal,
            float baseTime, int lifeBonus, float timeCut, int countCut)
        {
            int total = Mathf.Max(0, rosterTotal);

            if (table == null)
            {
                int fallbackLife = Mathf.Clamp(1 + Mathf.Max(0, lifeBonus), 1, Mathf.Max(1, total));
                return new VerificationRuleSet(baseTime, total, fallbackLife);
            }

            int countMin = Mathf.Min(table.AgentCountMin, total);
            int countMax = Mathf.Max(countMin, Mathf.Min(table.AgentCountMax, total));
            int count    = Mathf.Clamp(total - Mathf.Max(0, countCut), countMin, countMax);

            float timeMin = table.TimeLimitMin;
            float timeMax = Mathf.Max(timeMin, table.TimeLimitMax);
            float time    = Mathf.Clamp(baseTime - Mathf.Max(0f, timeCut), timeMin, timeMax);

            int life = Mathf.Clamp(table.BreachThreshold + Mathf.Max(0, lifeBonus), 1, Mathf.Max(1, count));

            return new VerificationRuleSet(time, count, life);
        }

        /// <summary>
        /// <paramref name="effect"/> 종류 노드들의 (투자 레벨 기준) 보정 수치 합.
        /// 수치는 노드의 <c>weightDeltaPerLevel</c> 레벨 값을 그대로 쓴다.
        /// </summary>
        public static float SumEffect(IReadOnlyList<ResearchNodeDefinition> nodes, ResearchEffect effect)
        {
            if (nodes == null) return 0f;

            float sum = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResearchNodeDefinition node = nodes[i];
                if (node == null || node.Effect != effect) continue;

                int level = ResearchBoardState.GetLevel(node);
                if (level <= 0) continue;

                sum += node.GetDelta(level);
            }
            return sum;
        }
    }
}
