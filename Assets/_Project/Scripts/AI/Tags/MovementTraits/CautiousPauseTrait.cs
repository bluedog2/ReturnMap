using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CautiousPauseTrait — 신중함(M-03): 전방 2칸 내 함정 감지 시 1초 정지
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="TrapBase.ActiveTraps"/> 를 순회해 진행 방향 기준 1~2칸 앞, 같은 행(y ±1
    /// 허용)에 함정이 있으면 1초 정지한다. <see cref="TraitState.counter"/> 에 마지막으로
    /// 정지를 유발한 셀의 x 좌표를 저장해, 같은 자리에서 매 프레임 재정지하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/신중함 (Cautious Pause)", fileName = "Trait_CautiousPause")]
    public class CautiousPauseTrait : MovementTrait
    {
        [SerializeField, Min(0f)]
        [Tooltip("함정 감지 시 정지할 시간(초).")]
        private float pauseSeconds = 1f;

        public override TraitAction OnBeforeMove(ref TraitState s, in MoveQuery q)
        {
            // 이미 이 자리에서 정지 처리를 했으면 반복 정지하지 않는다.
            if (s.counter == q.From.x + 1) return TraitAction.None; // +1: 기본값(0)과 x=-1 구분

            var traps = TrapBase.ActiveTraps;
            for (int i = 0; i < traps.Count; i++)
            {
                TrapBase trap = traps[i];
                if (trap == null) continue;

                GridCoord cell = WorldToCell(trap.transform.position);
                if (Mathf.Abs(cell.y - q.From.y) > 1) continue;

                int forward = (cell.x - q.From.x) * q.DirX;
                if (forward < 1 || forward > 2) continue;

                s.counter = q.From.x + 1;
                return new TraitAction { pauseSeconds = pauseSeconds };
            }

            return TraitAction.None;
        }
    }
}
