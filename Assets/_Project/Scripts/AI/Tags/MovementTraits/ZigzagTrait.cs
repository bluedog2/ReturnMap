using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ZigzagTrait — 갈지자(M-06): 2칸 전진마다 뒤로 1칸 무빙
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="TraitState.counter"/> 로 셀 도착 횟수를 세다가 2칸마다 1칸 후퇴를
    /// 요청한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/갈지자 (Zigzag)", fileName = "Trait_Zigzag")]
    public class ZigzagTrait : MovementTrait
    {
        public override TraitAction OnCellAdvanced(ref TraitState s)
        {
            s.counter++;
            if (s.counter % 2 != 0) return TraitAction.None;

            return new TraitAction { stepBackCells = 1 };
        }
    }
}
