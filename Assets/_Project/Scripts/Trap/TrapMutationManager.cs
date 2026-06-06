using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  TrapMutationManager — 카르마 변이 시스템
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 플레이 페이즈 진입 시 씬의 모든 함정에 변이를 적용하는 싱글턴 매니저.
    ///
    /// <para><b>확률 테이블</b></para>
    /// <list type="table">
    ///   <item><term>Normal</term>    <description>50 %</description></item>
    ///   <item><term>Dud</term>       <description>20 %</description></item>
    ///   <item><term>Critical</term>  <description>20 %</description></item>
    ///   <item><term>Beneficial</term><description>10 %</description></item>
    /// </list>
    ///
    /// <para><b>시드 정책</b></para>
    /// <list type="bullet">
    ///   <item>초보 모드: 스테이지 시작 시 고정된 시드 유지 → 사망해도 함정 상태 동일 (암기 가능)</item>
    ///   <item>하드 모드: 사망할 때마다 새 시드로 리롤 → 매 시도마다 다른 배치</item>
    /// </list>
    /// </summary>
    public class TrapMutationManager : MonoBehaviour
    {
        // ── 싱글턴 ───────────────────────────────────────────────────────────

        public static TrapMutationManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("변이 확률 (합계가 1.0 이 되도록 설정)")]
        [SerializeField, Range(0f, 1f)] private float normalChance     = 0.50f;
        [SerializeField, Range(0f, 1f)] private float dudChance        = 0.20f;
        [SerializeField, Range(0f, 1f)] private float criticalChance   = 0.20f;
        // beneficialChance = 1 - (normal + dud + critical) 으로 자동 계산

        [Header("난이도")]
        [SerializeField]
        [Tooltip("true = 하드 모드: 사망마다 시드 리롤. false = 초보 모드: 시드 고정.")]
        private bool hardMode = false;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private int            currentSeed;
        private System.Random  rng;
        private bool           hasMutated; // Play 중 중복 호출 방지

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            RollNewSeed();
        }

        private void OnEnable()
        {
            GamePhaseManager.OnPhaseChanged += HandlePhaseChange;
        }

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChange;
        }

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        private void HandlePhaseChange(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Play:
                    // Play 진입 시 변이 적용 (중복 실행 방지)
                    if (!hasMutated)
                    {
                        MutateAll();
                        hasMutated = true;
                    }
                    break;

                case GamePhase.Build:
                    // 빌드로 돌아오면 모든 함정 리셋
                    ResetAll();
                    hasMutated = false;
                    break;
            }
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>씬의 모든 TrapBase 에 변이 적용. 같은 시드 = 같은 결과.</summary>
        public void MutateAll()
        {
            // 매번 같은 시드로 RNG 재초기화 → 함정 순서가 같으면 결과도 동일
            rng = new System.Random(currentSeed);

            var traps = FindObjectsByType<TrapBase>(FindObjectsSortMode.InstanceID);
            foreach (var trap in traps)
                trap.Mutate(RollState());

#if UNITY_EDITOR
            Debug.Log($"[MutationManager] MutateAll — Seed:{currentSeed}  Traps:{traps.Length}");
#endif
        }

        /// <summary>모든 함정을 Normal 로 초기화.</summary>
        public void ResetAll()
        {
            var traps = FindObjectsByType<TrapBase>(FindObjectsSortMode.None);
            foreach (var trap in traps)
                trap.ResetToNormal();
        }

        /// <summary>
        /// 새 시드 생성. 하드 모드에서 사망 시 RespawnManager 가 호출.
        /// </summary>
        public void RollNewSeed()
        {
            currentSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            rng         = new System.Random(currentSeed);
        }

        /// <summary>
        /// 사망 후 리스폰 처리. RespawnManager 에서 호출.
        /// <list type="bullet">
        ///   <item>하드 모드: 시드 리롤 후 즉시 재변이</item>
        ///   <item>초보 모드: 기존 시드로 재변이 (결과 동일)</item>
        /// </list>
        /// </summary>
        public void OnPlayerRespawn()
        {
            if (hardMode) RollNewSeed();
            hasMutated = false;
            MutateAll();
            hasMutated = true;
        }

        // ── 내부 — RNG ────────────────────────────────────────────────────────

        private TrapState RollState()
        {
            float r = (float)rng.NextDouble();

            if (r < normalChance)
                return TrapState.Normal;

            r -= normalChance;
            if (r < dudChance)
                return TrapState.Dud;

            r -= dudChance;
            if (r < criticalChance)
                return TrapState.Critical;

            return TrapState.Beneficial;
        }

#if UNITY_EDITOR
        [ContextMenu("테스트: MutateAll")]
        private void EditorMutateAll() => MutateAll();

        [ContextMenu("테스트: ResetAll")]
        private void EditorResetAll() => ResetAll();

        [ContextMenu("테스트: 새 시드로 리롤")]
        private void EditorRollAndMutate() { RollNewSeed(); MutateAll(); }
#endif
    }
}
