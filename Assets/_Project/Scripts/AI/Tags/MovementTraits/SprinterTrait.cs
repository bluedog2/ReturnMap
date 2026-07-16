using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SprinterTrait — 스프린터(S-11): 스폰 후 5칸 +100% 질주, 이후 -50%
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="TraitState.counter"/> 를 누적 전진 칸수로 사용한다. 5칸까지는(0~4번째
    /// 전진 구간) 이동속도 ×2, 그 이후는 ×0.5 로 급감한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/스프린터 (Sprinter)", fileName = "Trait_Sprinter")]
    public class SprinterTrait : MovementTrait
    {
        [SerializeField, Min(1)]
        [Tooltip("전력 질주가 유지되는 전진 칸수.")]
        private int sprintCells = 5;

        public override TraitAction OnCellAdvanced(ref TraitState s)
        {
            s.counter++;
            return TraitAction.None;
        }

        public override float GetSpeedMultiplier(ref TraitState s, in MoveQuery q)
            => s.counter < sprintCells ? 2f : 0.5f;
    }
}
