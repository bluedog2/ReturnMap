using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AgentDamageSystem — 함정 → 검증 AI 피해의 유일한 진입점
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>피해 판정 결과.</summary>
    public enum DamageResult
    {
        /// <summary>면역 — 피해 없음, 부수효과 없음.</summary>
        Immune,

        /// <summary>흡수 — 피해 없음, 투사체 등은 소멸 처리(호출측 책임).</summary>
        Absorbed,

        /// <summary>피해 적용, 생존.</summary>
        Damaged,

        /// <summary>피해 적용, 사망.</summary>
        Killed,
    }

    /// <summary>
    /// 함정이 검증 AI 개체에게 피해를 주는 <b>유일한</b> 진입점. 면역/특수 플래그 판정,
    /// 실제 HP 차감, 피격 훅 디스패치를 이 한 곳에서 순서대로 처리합니다.
    /// <para>
    /// 새 함정/투사체가 AI 를 공격할 때는 <see cref="VerificationAgent.Kill"/> 을 직접
    /// 호출하지 말고 반드시 이 게이트웨이를 거쳐야 태그 시스템(면역·플래그)이 동작합니다.
    /// </para>
    /// </summary>
    public static class AgentDamageSystem
    {
        /// <summary>
        /// 함정→에이전트 피해의 유일한 진입점.
        /// </summary>
        /// <param name="agent">피격 대상의 AgentContext. 호출측이 미리 GetComponent 로 조회한다.</param>
        /// <param name="type">피해 타입 (면역 마스크 판정 기준).</param>
        /// <param name="sourcePos">피해 발생 위치 (철벽 방패 전/후방 판정용).</param>
        /// <param name="amount">피해량.</param>
        /// <param name="sourceTrap">피해를 가한 함정 (함정 혐오/저주부르미 처리용, null 허용).</param>
        public static DamageResult TryDamage(
            AgentContext agent, DamageType type, Vector2 sourcePos,
            int amount = 1, TrapBase sourceTrap = null)
        {
            // 1) 하위 호환 폴백 — AgentContext/AgentHealth 가 없는(미배선) 개체는 기존처럼 즉사.
            //    agent 자체가 null 인 호출은 발생하지 않도록 호출측(TrapBase 등)이 사전에
            //    분기해 VerificationAgent.Kill() 을 직접 호출해야 한다(몸체 참조가 없어 여기서는
            //    즉사시킬 대상을 찾을 수 없다). 여기서는 agent 는 있으나 AgentHealth 만 없는
            //    케이스를 실질적으로 방어한다.
            if (agent == null)
                return DamageResult.Killed;

            AgentHealth health = agent.Health;
            if (health == null)
            {
                agent.GetComponent<VerificationAgent>()?.Kill();
                return DamageResult.Killed;
            }

            // 2) 무적(i-frame) — 직전 피격의 무적 시간 중이면 피해·훅·부수효과 전부 없음.
            //    가시 진동 사이클마다 트리거가 재발동하는 연타사를 막는다.
            if (health.IsInvulnerable)
                return DamageResult.Immune;

            // 3) 공중부양(Hover) — 가시 센서가 미작동하므로 가시 피해는 아예 무시
            if (type == DamageType.Spike && agent.HasFlag(SpecialFlag.Hover))
                return DamageResult.Immune;

            // 4) 면역 마스크 — 태그가 선언한 면역 피해 타입
            if (agent.IsImmuneTo(type))
                return DamageResult.Immune;

            // 5) 철벽 방패(FrontShieldOnly) — 화살이 전방(바라보는 방향)에서 오면 면역,
            //    후방에서 오면 그대로 통과시켜 피해를 받는다.
            if (type == DamageType.Arrow && agent.HasFlag(SpecialFlag.FrontShieldOnly))
            {
                float dx      = sourcePos.x - agent.transform.position.x;
                int   hitSign = dx >= 0f ? 1 : -1;
                if (hitSign == agent.FacingSign)
                    return DamageResult.Immune;
            }

            // 6) 스펀지 몸(AbsorbProjectile) — 화살을 데미지 없이 흡수(소멸). 호출측이
            //    투사체 소멸 처리를 담당 — 뒤따르는 동료에게 갈 화살을 대신 지워주는 탱커.
            if (type == DamageType.Arrow && agent.HasFlag(SpecialFlag.AbsorbProjectile))
                return DamageResult.Absorbed;

            // 7) 피해 적용
            bool killed = health.TakeDamage(amount);

            // 8) 피격 훅 + 특수 플래그 후처리 — 여기 도달했다는 것 자체가 실제 피해가
            //    들어갔다는 뜻(면역/흡수는 이미 위에서 반환됨)이므로 조건 없이 실행한다.
            agent.DispatchTrapHitHooks(sourceTrap);

            if (agent.HasFlag(SpecialFlag.DestroyTrapOnHit) && sourceTrap != null)
            {
                // 함정 혐오 — 피격 시 해당 함정 영구 파괴.
                // ⚠️ Destroy 금지: "슬롯=지형" 불변식(설치된 함정 칸 = 밟는 지형) 때문에
                // 오브젝트는 유지하고 Dud 로 기능만 영구 정지시킨다.
                // TODO: 파괴된 것처럼 보이는 연출(스프라이트 교체 등)은 이후 단계에서 추가.
                sourceTrap.Mutate(TrapState.Dud);
            }

            // TODO: 저주부르미(CurseTrapOnHit) — 피격 시 해당 함정 저주. 효과 기획 미정.

            // TODO: 넉백 — Stats.KnockbackMultiplier 를 이용한 실제 변위 적용은
            // 3단계 로코모션 FSM 에서 처리한다. 여기서는 배율 계산만 노출.

            return killed ? DamageResult.Killed : DamageResult.Damaged;
        }
    }
}
