using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CliffReverseTrait — 안전제일(M-04): 낭떠러지 감지 시 반전
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 2칸 이상 하강하는 이동을 감지하면 이동을 거부(veto)하고 2칸 후퇴한다. 되돌아간
    /// 뒤에도 경로는 동일하므로 다시 낭떠러지에 도달해 재차 반전 — 자연스러운 왕복이
    /// 발생하며, 최종적으로 검증 시간 초과로 방어 성공 처리되는 것이 의도된 결과다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/안전제일 (Cliff Reverse)", fileName = "Trait_CliffReverse")]
    public class CliffReverseTrait : MovementTrait
    {
        public override TraitAction OnBeforeMove(ref TraitState s, in MoveQuery q)
        {
            if (q.IsDescending && q.DropHeight >= 2)
                return new TraitAction { vetoMove = true, stepBackCells = 2 };

            return TraitAction.None;
        }
    }
}
