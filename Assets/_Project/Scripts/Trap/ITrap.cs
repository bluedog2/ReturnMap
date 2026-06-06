namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapState — 카르마 변이 4단계
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 플레이 페이즈 진입 시 TrapMutationManager 가 각 함정에 부여하는 변이 상태.
    /// </summary>
    public enum TrapState
    {
        /// <summary>기획 스펙대로 정상 작동.</summary>
        Normal,

        /// <summary>고장 — 작동 완전 정지. 안전하게 통과 가능.</summary>
        Dud,

        /// <summary>악화 — 속도·범위 증가, 독 추가 등 치명적으로 변모.</summary>
        Critical,

        /// <summary>축복 — 발판으로 굳거나 이동 속도를 둔화시켜 오히려 플레이어에게 유리.</summary>
        Beneficial,
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ITrap
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 모든 함정 컴포넌트가 구현해야 하는 계약.
    /// <para>
    /// ┌ 빌드 페이즈 ──────────────────────────────────┐
    /// │  BaseCost / DangerLevel 로 배치 비용 계산.     │
    /// │  GetNodeCostWeight() 로 AI 길찾기 가중치 제공. │
    /// └───────────────────────────────────────────────┘
    /// ┌ 플레이 페이즈 ─────────────────────────────────┐
    /// │  Mutate(TrapState) 로 변이 적용.               │
    /// │  ResetToNormal() 로 빌드 페이즈 복귀 초기화.   │
    /// └───────────────────────────────────────────────┘
    /// </para>
    /// </summary>
    public interface ITrap
    {
        // ── 상태 ────────────────────────────────────────────────────────────

        /// <summary>현재 변이 상태 (Normal 이 기본값).</summary>
        TrapState CurrentState { get; }

        // ── 코스트 & 위험도 ──────────────────────────────────────────────────

        /// <summary>
        /// 위험도 0(안전) ~ 10(치명).
        /// 빌드 페이즈 배치 비용과 AI NodeCost 계산의 원천 값.
        /// </summary>
        int DangerLevel { get; }

        /// <summary>
        /// 역코스트 — DangerLevel 이 높을수록 값이 낮아 빌드 예산 소비가 적다.
        /// <code>BaseCost = (10 - DangerLevel) * 10</code>
        /// 예) DangerLevel=10 → BaseCost=0 / DangerLevel=0 → BaseCost=100
        /// </summary>
        int BaseCost { get; }

        // ── 변이 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 플레이 페이즈 진입 시 <c>TrapMutationManager</c> 가 호출.
        /// 시각 힌트·물리·동작 모두 변경됨.
        /// </summary>
        void Mutate(TrapState newState);

        /// <summary>빌드 페이즈로 복귀 시 Normal 로 리셋.</summary>
        void ResetToNormal();

        // ── AI 길찾기 ────────────────────────────────────────────────────────

        /// <summary>
        /// A* 알고리즘의 NodeCost 가중치.
        /// 기본값은 DangerLevel 그대로 사용하며, 함정마다 오버라이드 가능.
        /// </summary>
        float GetNodeCostWeight();
    }
}
