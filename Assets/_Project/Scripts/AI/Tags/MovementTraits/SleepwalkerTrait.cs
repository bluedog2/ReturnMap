using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SleepwalkerTrait — 잠만보(M-10): 3칸 전진마다 1초 수면 정지
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="TraitState.counter"/> 에 <see cref="MoveQuery.EffectiveCells"/>(실효
    /// 칸수 — 예: 4칸짜리 점프 링크는 '4칸'으로 센다)를 누적해 3칸에 도달할 때마다
    /// 1초 정지한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/잠만보 (Sleepwalker)", fileName = "Trait_Sleepwalker")]
    public class SleepwalkerTrait : MovementTrait
    {
        public override TraitAction OnCellAdvanced(ref TraitState s, in MoveQuery q)
        {
            s.counter += q.EffectiveCells;
            if (s.counter < 3) return TraitAction.None;

            s.counter -= 3;
            return new TraitAction { pauseSeconds = 1f };
        }
    }
}
