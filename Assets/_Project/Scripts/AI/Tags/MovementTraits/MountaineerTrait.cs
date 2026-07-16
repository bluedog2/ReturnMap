using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MountaineerTrait — 등산가(D-10): 오르막 +100%, 평지 -30%
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>상승 이동은 ×2, 평지(상승도 하강도 아님)는 ×0.7, 하강은 배율 없음(×1).</summary>
    [CreateAssetMenu(menuName = "ReTrap/AI Trait/등산가 (Mountaineer)", fileName = "Trait_Mountaineer")]
    public class MountaineerTrait : MovementTrait
    {
        public override float GetSpeedMultiplier(ref TraitState s, in MoveQuery q)
        {
            if (q.IsAscending)  return 2f;
            if (!q.IsDescending) return 0.7f; // 평지
            return 1f;                        // 하강
        }
    }
}
