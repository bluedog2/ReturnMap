using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CowardTrait — 겁쟁이(M-07): 수평 라인 화살 슈터 감지 시 0.5초 엎드림
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="TrapBase.ActiveTraps"/> 중 <see cref="ArrowShooter"/> 만 골라 같은 행
    /// (y ±1 허용)에 있으면 0.5초 정지한다. <see cref="TraitState.phase"/> 에 마지막으로
    /// 반응한 셀의 x 좌표를 저장해 슈터별로 반복 정지하지 않는다(단순화: 마지막 감지
    /// 셀 기준 1회).
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/겁쟁이 (Coward)", fileName = "Trait_Coward")]
    public class CowardTrait : MovementTrait
    {
        [SerializeField, Min(0f)]
        [Tooltip("슈터 감지 시 엎드릴 시간(초).")]
        private float pauseSeconds = 0.5f;

        public override TraitAction OnBeforeMove(ref TraitState s, in MoveQuery q)
        {
            if (s.phase == q.From.x + 1) return TraitAction.None; // +1: 기본값(0)과 x=-1 구분

            var traps = TrapBase.ActiveTraps;
            for (int i = 0; i < traps.Count; i++)
            {
                if (!(traps[i] is ArrowShooter shooter) || shooter == null) continue;

                GridCoord cell = WorldToCell(shooter.transform.position);
                if (Mathf.Abs(cell.y - q.From.y) > 1) continue;

                s.phase = q.From.x + 1;
                return new TraitAction { pauseSeconds = pauseSeconds };
            }

            return TraitAction.None;
        }
    }
}
