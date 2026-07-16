using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AccelerateTrait — 가속(S-05): 이동 중 시간이 지날수록 가속
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="TraitState.timer"/> 에 <b>실제 이동 중</b> 경과한 시간을 누적해
    /// (1 + 0.1×초) 배로 가속한다. 정지/대기 구간에서는 GetSpeedMultiplier 자체가
    /// 호출되지 않으므로 순수 정지 시간은 누적되지 않는다.
    /// <para>⚠️ 상한(×4)은 기획 미확정 수치 — "이동속도 무한 증가"로만 적혀 있어 터널링
    /// (지형/함정 통과) 위험을 막기 위한 임시 안전핀이다. 기획 확정 시 조정할 것.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/가속 (Accelerate)", fileName = "Trait_Accelerate")]
    public class AccelerateTrait : MovementTrait
    {
        private const float RatePerSecond = 0.1f;
        private const float MaxMultiplier = 4f; // ⚠️ 임시 안전핀 — 기획 확정 시 조정

        public override float GetSpeedMultiplier(ref TraitState s, in MoveQuery q)
        {
            s.timer += Time.deltaTime;
            return Mathf.Min(1f + RatePerSecond * s.timer, MaxMultiplier);
        }
    }
}
