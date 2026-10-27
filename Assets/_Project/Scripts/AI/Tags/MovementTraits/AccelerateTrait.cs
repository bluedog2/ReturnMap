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
    /// <para>배율 상한은 인스펙터(<c>maxMultiplier</c>)로 조정한다 — 터널링(지형/함정 통과)
    /// 위험을 막는 안전핀이며 기본값은 ×4.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/가속 (Accelerate)", fileName = "Trait_Accelerate")]
    public class AccelerateTrait : MovementTrait
    {
        private const float RatePerSecond = 0.1f;

        [SerializeField, Min(1)]
        [Tooltip("가속 배율 상한. 1 이면 가속하지 않는다.")]
        private float maxMultiplier = 4f;

        public override float GetSpeedMultiplier(ref TraitState s, in MoveQuery q)
        {
            s.timer += Time.deltaTime;
            return ComputeMultiplier(s.timer, maxMultiplier);
        }

        /// <summary>이동 누적 시간과 상한으로 가속 배율을 계산한다. 순수 함수 (결과 ≤ 상한, 상한이 1 미만이면 1).</summary>
        internal static float ComputeMultiplier(float movingSeconds, float maxMultiplier)
        {
            float cap = Mathf.Max(1f, maxMultiplier);
            return Mathf.Min(1f + RatePerSecond * movingSeconds, cap);
        }
    }
}
