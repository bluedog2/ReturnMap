using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SleepwalkerTrait — 잠만보(M-10): 3칸 전진마다 1초 수면 정지
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary><see cref="TraitState.counter"/> 로 셀 도착 횟수를 세어 3칸마다 1초 정지한다.</summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/잠만보 (Sleepwalker)", fileName = "Trait_Sleepwalker")]
    public class SleepwalkerTrait : MovementTrait
    {
        public override TraitAction OnCellAdvanced(ref TraitState s)
        {
            s.counter++;
            if (s.counter % 3 != 0) return TraitAction.None;

            return new TraitAction { pauseSeconds = 1f };
        }
    }
}
