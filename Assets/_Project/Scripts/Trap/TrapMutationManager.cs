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

        private int  currentSeed;
        private bool hasMutated; // Play 중 중복 호출 방지

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            RollNewSeed();
        }

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        /// <summary>
        /// GamePhaseManager.SetPhase 가 이벤트 발행 이전에 <b>직접 호출</b>하는 진입점.
        /// 변이 적용은 이후 로직(AI 검증 등)이 최신 상태를 참조해야 하므로
        /// 이벤트 구독이 아니라 순서가 보장되는 직접 호출로 처리합니다.
        /// </summary>
        public void ApplyPhase(GamePhase phase)
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

                case GamePhase.Verification:
                    // 검증 페이즈: AI 는 원본(Normal) 배치를 검증한다는 설계 결정 — 변이 미적용
                    ResetAll();
                    hasMutated = false;
                    break;
            }
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 씬의 모든 TrapBase 에 변이 적용. 같은 시드 = 같은 결과.
        /// <para>변이는 함정의 <b>그리드 좌표 해시</b>로 독립 결정되므로
        /// 함정을 추가/제거해도 다른 함정의 변이는 변하지 않습니다
        /// (Build Phase 에서 배치를 바꿔도 비기너 모드 약속 유지).</para>
        /// </summary>
        public void MutateAll()
        {
            // TrapBase 정적 레지스트리 순회 — 씬 전체 스캔(FindObjectsByType) 대체.
            // 라이브 목록이라 순회 중 제거에 안전하도록 역순으로 돈다.
            var traps = TrapBase.ActiveTraps;
            for (int i = traps.Count - 1; i >= 0; i--)
                traps[i].Mutate(RollStateFor(traps[i]));

#if UNITY_EDITOR
            Debug.Log($"[MutationManager] MutateAll — Seed:{currentSeed}  Traps:{traps.Count}");
#endif
        }

        /// <summary>모든 함정을 Normal 로 초기화.</summary>
        public void ResetAll()
        {
            var traps = TrapBase.ActiveTraps;
            for (int i = traps.Count - 1; i >= 0; i--)
                traps[i].ResetToNormal();
        }

        /// <summary>
        /// 새 시드 생성. 하드 모드에서 사망 시 RespawnManager 가 호출.
        /// </summary>
        public void RollNewSeed()
        {
            currentSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
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

        /// <summary>
        /// 함정 1개의 변이를 <c>hash(시드, 그리드 좌표)</c> 로 독립 결정합니다.
        /// <list type="bullet">
        ///   <item>같은 시드 + 같은 칸 = 항상 같은 변이 (순서·개수 무관)</item>
        ///   <item>수동 배치 함정도 월드 좌표 → 그리드 칸 변환으로 동일하게 동작</item>
        /// </list>
        /// </summary>
        private TrapState RollStateFor(TrapBase trap)
        {
            // 맵 원점 기준 그리드 칸 좌표
            Vector2 origin = MapLoader.Instance != null ? MapLoader.Instance.MapOrigin : Vector2.zero;
            Vector2 local  = (Vector2)trap.transform.position - origin;
            int gx = Mathf.FloorToInt(local.x);
            int gy = Mathf.FloorToInt(local.y);

            // 시드 × 좌표 해시 (소수 곱 XOR — 인접 칸 상관성 제거)
            int hash = unchecked(currentSeed
                                 ^ (gx * 73856093)
                                 ^ (gy * 19349663));

            var localRng = new System.Random(hash);
            float r = (float)localRng.NextDouble();

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
