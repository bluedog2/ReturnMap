using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ZigzagTrait — 갈지자(M-06): 2칸 전진마다 뒤로 1칸 무빙
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="TraitState.counter"/> 에 <see cref="MoveQuery.EffectiveCells"/>(실효
    /// 칸수, 인덱스 수가 아님 — 점프/낙하 링크는 한 세그먼트가 여러 칸일 수 있다)를
    /// 누적하다가 2칸에 도달하면 1칸 후퇴를 요청한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/갈지자 (Zigzag)", fileName = "Trait_Zigzag")]
    public class ZigzagTrait : MovementTrait
    {
        public override TraitAction OnCellAdvanced(ref TraitState s, in MoveQuery q)
        {
            s.counter += q.EffectiveCells;
            if (s.counter < 2) return TraitAction.None;

            s.counter -= 2;
            return new TraitAction { stepBackCells = 1 };
        }
    }
}
