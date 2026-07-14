using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TagEventHook — 태그 수명주기 이벤트 훅 골격
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 태그 수명주기 이벤트 훅(스폰 시 코스트 차감, 사망 시 쉴드 부여/재화, 피격 시
    /// 백스텝 등). 구체 훅 클래스는 해당 기능 단계에서 추가된다 — 이 단계에서는
    /// AgentContext 가 디스패치할 수 있도록 시그니처만 정의한다.
    /// </summary>
    public abstract class TagEventHook : ScriptableObject
    {
        /// <summary>개체 스폰 직후 호출. 예: 가성비 타파 코스트 차감.</summary>
        public virtual void OnSpawn(AgentContext agent) { }

        /// <summary>개체 사망 시 호출. 예: 기사단 쉴드, 도둑 재화.</summary>
        public virtual void OnDeath(AgentContext agent) { }

        /// <summary>함정에 피격당했을 때 호출. 예: 백스텝.</summary>
        public virtual void OnTrapHit(AgentContext agent, TrapBase trap) { }
    }
}
