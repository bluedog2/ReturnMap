using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BalanceConfig — 밸런스 수치 단일 소스 (ScriptableObject)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 씬 컴포넌트에 흩어져 있던 밸런스 수치(변이 확률·검증 제한 시간·클리어 보상)를 모은 에셋.
    /// TrapMutationManager / VerificationDirector / MapLoader 가 참조하며,
    /// 미지정 시 각 컴포넌트의 기존 필드(폴백)를 사용합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReTrap/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("변이 확률")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normal 확률")]
        private float normalChance = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Dud(불발) 확률")]
        private float dudChance = 0.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Critical 확률. Beneficial 확률은 1 - (세 확률의 합) 으로 자동 계산")]
        private float criticalChance = 0.2f;

        [Header("검증")]
        [SerializeField, Min(1f)]
        [Tooltip("검증 페이즈 제한 시간(초). 이 시간 안에 못 뚫으면 방어 성공")]
        private float verificationTimeLimit = 60f;

        [Header("보상")]
        [SerializeField, Min(0)]
        [Tooltip("스테이지 클리어 시 지급할 박살 난 지구본 수")]
        private int stageClearReward = 10;

        public float NormalChance          => normalChance;
        public float DudChance             => dudChance;
        public float CriticalChance        => criticalChance;
        /// <summary>1 - (Normal + Dud + Critical), 음수면 0.</summary>
        public float BeneficialChance      => Mathf.Max(0f, 1f - (normalChance + dudChance + criticalChance));
        public float VerificationTimeLimit => verificationTimeLimit;
        public int   StageClearReward      => stageClearReward;

        private void OnValidate()
        {
            float sum = normalChance + dudChance + criticalChance;
            if (sum > 1f + 1e-4f)
                Debug.LogWarning($"[BalanceConfig] {name}: 변이 확률 합({sum:F2})이 1을 초과합니다 — Beneficial 은 0 으로 처리됩니다.", this);
        }
    }
}
