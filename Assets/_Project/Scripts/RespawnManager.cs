using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  RespawnManager — 사망 처리 및 리스폰 매니저
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 플레이어 사망을 감지해 리스폰 시퀀스를 실행하는 싱글턴 매니저.
    ///
    /// <para><b>리스폰 흐름</b></para>
    /// <code>
    /// PlayerHealth.OnDeath
    ///   → 입력 비활성화
    ///   → deathDelay 대기 (사망 연출)
    ///   → 리스폰 지점 이동 + 물리 초기화
    ///   → HP 전체 회복 + 리스폰 무적 부여
    ///   → 페이즈 처리: 빌드 복귀 설정이면 SetPhase(Build),
    ///     Play 유지 + 현재 Play 페이즈일 때만 TrapMutationManager.OnPlayerRespawn() (시드/변이)
    ///   → 입력 재활성화
    ///   → OnRespawn 이벤트 발행
    /// </code>
    ///
    /// <para><b>씬 구성</b></para>
    /// 씬에 하나의 RespawnManager 게임오브젝트를 두면 됩니다.
    /// PlayerController 를 Inspector 에서 할당하거나, 없으면 씬에서 자동 탐색합니다.
    ///
    /// <para><b>체크포인트 연동</b></para>
    /// 체크포인트 오브젝트에서 <see cref="SetRespawnPoint"/> 를 호출해 리스폰 위치를 갱신합니다.
    /// </summary>
    public class RespawnManager : MonoBehaviour
    {
        // ── 싱글턴 ───────────────────────────────────────────────────────────

        public static RespawnManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("참조")]
        [SerializeField]
        [Tooltip("PlayerController 가 붙은 오브젝트. 비워두면 씬에서 자동 탐색.")]
        private PlayerController playerController;

        [Header("리스폰 설정")]
        [SerializeField]
        [Tooltip("스테이지 최초 시작 리스폰 지점. 체크포인트로 덮어쓸 수 있음.")]
        private Vector2 defaultRespawnPoint;

        [SerializeField, Min(0f)]
        [Tooltip("사망 연출 대기 시간(초). 사망 애니메이션 길이에 맞게 조정.")]
        private float deathDelay = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("리스폰 후 무적 지속 시간(초). 부활 직후 즉사 방지용.")]
        private float respawnInvincibleDuration = 1.5f;

        [Header("페이즈")]
        [SerializeField]
        [Tooltip("true: 사망 후 빌드 페이즈로 복귀 (함정 재배치 가능)\n" +
                 "false: 플레이 페이즈 유지 + 함정 재변이만 수행")]
        private bool respawnGoesToBuild = false;

        // ── 공개 상태 ─────────────────────────────────────────────────────────

        /// <summary>총 사망 횟수.</summary>
        public int     DeathCount    { get; private set; }

        /// <summary>리스폰 시퀀스 진행 중 여부.</summary>
        public bool    IsRespawning  { get; private set; }

        /// <summary>현재 유효한 리스폰 지점.</summary>
        public Vector2 RespawnPoint  { get; private set; }

        // ── 이벤트 ────────────────────────────────────────────────────────────

        /// <summary>사망 직후(딜레이 전) 발행. 사망 연출 UI 등에 활용.</summary>
        public static event Action OnDeath;

        /// <summary>리스폰 완료(입력 재활성화 직후) 발행. 화면 복귀 연출 등에 활용.</summary>
        public static event Action OnRespawn;

        // ── Private ───────────────────────────────────────────────────────────

        private PlayerHealth playerHealth;
        private PlayerInput  playerInput;

        /// <summary>OnDeath 구독 여부(중복 구독 방지 플래그).</summary>
        private bool isDeathSubscribed;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            RespawnPoint = defaultRespawnPoint;
        }

        private void OnEnable()
        {
            EnsurePlayerRefs();

            MapLoader.OnMapLoaded += HandleMapLoaded;
        }

        private void OnDisable()
        {
            if (isDeathSubscribed && playerHealth != null)
            {
                playerHealth.OnDeath -= HandleDeath;
                isDeathSubscribed = false;
            }

            MapLoader.OnMapLoaded -= HandleMapLoaded;
        }

        // ── 참조 확보 ─────────────────────────────────────────────────────────

        /// <summary>
        /// PlayerController/PlayerHealth/PlayerInput 참조를 확보하고
        /// PlayerHealth.OnDeath 구독을 보장합니다.
        /// 플레이어가 아직 씬에 없으면(늦은 스폰 등) 조용히 실패하고,
        /// 이후 재호출 시점(OnEnable, 맵 로드 완료, 공개 API 사용 직전)에 재시도합니다.
        /// 매 프레임 폴링은 하지 않습니다.
        /// </summary>
        private void EnsurePlayerRefs()
        {
            // PlayerController 자동 탐색 (Inspector 미할당 시)
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            if (playerController != null)
            {
                if (playerHealth == null)
                    playerHealth = playerController.GetComponent<PlayerHealth>();
                if (playerInput == null)
                    playerInput = playerController.GetComponent<PlayerInput>();
            }

            // OnDeath 구독 보장 (중복 구독 방지)
            if (!isDeathSubscribed && playerHealth != null)
            {
                playerHealth.OnDeath += HandleDeath;
                isDeathSubscribed = true;
            }

            if (playerController == null)
            {
                Debug.LogWarning("[RespawnManager] PlayerController 를 찾지 못했습니다. " +
                                 "Inspector 에서 직접 할당하거나, 플레이어 스폰 이후 재시도됩니다.");
            }
        }

        // ── 맵 연동 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 맵 로드 완료 시 리스폰 지점을 맵의 spawnPoint 로 갱신하고
        /// 플레이어를 그 위치로 즉시 배치합니다.
        /// </summary>
        private void HandleMapLoaded(MapData map)
        {
            // 맵 로드 시점에 플레이어가 새로 스폰됐을 수 있으므로 참조 재확보 시도
            EnsurePlayerRefs();

            Vector2 origin = MapLoader.Instance != null ? MapLoader.Instance.MapOrigin : Vector2.zero;
            Vector2 spawn  = map.CellToWorld(map.spawnPoint.x, map.spawnPoint.y, origin);

            SetRespawnPoint(spawn);

            // 시작 위치 배치 (리스폰과 동일한 텔레포트 + 상태 초기화)
            if (playerController != null)
            {
                playerController.transform.position = new Vector3(
                    spawn.x, spawn.y, playerController.transform.position.z);
                playerController.ResetState();
            }
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 체크포인트 도달 시 리스폰 지점을 갱신합니다.
        /// </summary>
        public void SetRespawnPoint(Vector2 point)
        {
            EnsurePlayerRefs();

            RespawnPoint = point;
#if UNITY_EDITOR
            Debug.Log($"[RespawnManager] 리스폰 지점 갱신 → {point}");
#endif
        }

        // ── 내부 — 리스폰 시퀀스 ─────────────────────────────────────────────

        private void HandleDeath()
        {
            if (IsRespawning) return;
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            IsRespawning = true;
            DeathCount++;

            // ── 1. 사망 이벤트 (사망 UI·사운드 트리거용) ──────────────────────
            OnDeath?.Invoke();

            // ── 2. 입력 비활성화 ───────────────────────────────────────────────
            playerInput?.DeactivateInput();

            // ── 3. 사망 연출 대기 ──────────────────────────────────────────────
            yield return new WaitForSeconds(deathDelay);

            // ── 4. 위치 이동 + 물리/상태 완전 초기화 ──────────────────────────
            if (playerController != null)
            {
                playerController.transform.position = new Vector3(
                    RespawnPoint.x,
                    RespawnPoint.y,
                    playerController.transform.position.z);

                playerController.ResetState();
            }

            // ── 5. HP 전체 회복 + 리스폰 무적 ────────────────────────────────
            if (playerHealth != null)
            {
                playerHealth.RestoreFullHp();
                if (respawnInvincibleDuration > 0f)
                    playerHealth.SetInvincible(respawnInvincibleDuration);
            }

            // ── 6. 페이즈 처리 + 함정 재변이 ──────────────────────────────────
            // 재변이는 "Play 페이즈를 유지한 채 재도전"할 때만 수행한다.
            // - 빌드 복귀 시: SetPhase(Build) 가 ResetAll 로 초기화하므로 변이 호출은 낭비
            // - Play 외 페이즈에서의 사망(빌드/검증 중 함정 접촉 등): 변이를 실행하면
            //   "검증은 원본(Normal) 검증" 불변식이 깨진다 — 위치/HP 복구만 하고 변이는 금지
            var phaseManager = GamePhaseManager.Instance;

            if (respawnGoesToBuild)
                phaseManager?.SetPhase(GamePhase.Build);
            else if (phaseManager == null || phaseManager.currentPhase == GamePhase.Play)
                TrapMutationManager.Instance?.OnPlayerRespawn();
            // else: Build/Verification 중 사망 — 페이즈 변이 정책은 ApplyPhase 가 관장

            // ── 8. 입력 재활성화 ───────────────────────────────────────────────
            playerInput?.ActivateInput();

            IsRespawning = false;

            // ── 9. 리스폰 완료 이벤트 ─────────────────────────────────────────
            OnRespawn?.Invoke();
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            // 리스폰 지점 표시
            Vector2 p = Application.isPlaying ? RespawnPoint : defaultRespawnPoint;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(new Vector3(p.x, p.y, 0f), 0.3f);
            Gizmos.DrawLine(new Vector3(p.x - 0.3f, p.y + 0.5f, 0f),
                            new Vector3(p.x,         p.y,        0f));
            Gizmos.DrawLine(new Vector3(p.x + 0.3f, p.y + 0.5f, 0f),
                            new Vector3(p.x,         p.y,        0f));
        }

#if UNITY_EDITOR
        [ContextMenu("테스트: 리스폰 강제 실행")]
        private void EditorTestRespawn()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[RespawnManager] 플레이 모드에서만 테스트 가능합니다.");
                return;
            }
            StartCoroutine(RespawnRoutine());
        }
#endif
    }
}
