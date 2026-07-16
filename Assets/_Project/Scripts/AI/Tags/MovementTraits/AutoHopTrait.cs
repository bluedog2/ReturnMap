using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AutoHopTrait — 천진난만(M-01): 착지 즉시 쿨타임 없이 계속 호핑
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 모든 이동을 가로 2칸 규모의 호핑처럼 보이는 아크로 강제한다. 이동 방향/고저에
    /// 관계없이 항상 <see cref="ArcSpec.useArc"/> 를 켠다 — "착지하자마자 다시 뛴다"는
    /// 태그 설명(끊임없는 호핑)을 반영.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/천진난만 (Auto Hop)", fileName = "Trait_AutoHop")]
    public class AutoHopTrait : MovementTrait
    {
        public override void ModifyArc(ref ArcSpec arc)
        {
            arc.useArc          = true;
            arc.horizontalCells = 2;
            arc.height          = 0.6f;
        }
    }
}
