using UnityEngine;
using UnityEngine.UI;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PhaseControlsGuide — 화면 하단 페이즈별 조작 안내 + Build "시작" 버튼
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 화면 하단에 현재 <see cref="GamePhase"/> 에 맞는 조작법을 안내하고,
    /// Build 페이즈에서만 검증/플레이로 넘어가는 "시작" 버튼(Enter 키와 동일 기능)을 노출합니다.
    ///
    /// <para><b>씬 배선</b>: <c>ReTrap.EditorTools.MetaUiSetup</c> 이 UICanvas 아래에
    /// 하단 바 UI를 생성하고 <see cref="guideText"/>/<see cref="startButton"/>/
    /// <see cref="startButtonRoot"/> 를 SerializedObject 로 배선합니다. 수동 배선도 가능.</para>
    ///
    /// <para><b>초기 동기화 규약</b>: 다른 페이즈 구독자(VerificationSpeedController 등)와
    /// 동일하게, OnEnable 에서 이벤트만 구독하고 <see cref="Start"/> 에서
    /// <see cref="GamePhaseManager.currentPhase"/> 를 직접 읽어 1회 동기화합니다
    /// (재활성화/씬 시작 시점에 이미 다른 페이즈가 진행 중이어도 안내가 먹통이 되지 않도록).</para>
    /// </summary>
    public class PhaseControlsGuide : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("UI")]
        [Tooltip("화면 하단 조작 안내 텍스트. 페이즈 전환 시에만 갱신됩니다(프레임 폴링 금지).")]
        [SerializeField] private Text guideText;

        [Tooltip("▶ 검증 시작 버튼. 클릭 시 GamePhaseManager.AdvanceFromBuild() 호출 (Enter 와 동일).")]
        [SerializeField] private Button startButton;

        [Tooltip("startButton 을 감싸는 표시/숨김 대상 오브젝트. Build 페이즈에서만 활성화됩니다.")]
        [SerializeField] private GameObject startButtonRoot;

        // ── 페이즈별 안내 문구 (실제 키 바인딩 반영, 확인된 것만 구체 키로 표기) ──

        // Build: BuildPhaseController 조작(1~9 선택/X 철거/좌클릭 설치/우클릭 철거/Enter 시작)
        private const string BuildGuideText =
            "1~3 함정 선택 · X 철거 모드 · 좌클릭 설치 · 우클릭 철거 · [시작] 또는 Enter";

        // Verification: VerificationSpeedController 조작(Space 배속 순환/Enter 스킵)
        private const string VerificationGuideText =
            "관전 중 · Space 배속(1→2→4x) · Enter 스킵";

        // Play: InputSystem_Actions.inputactions 실제 바인딩 — Move(WASD/방향키), Jump(Space), Dash(Z)
        // + BuildPhaseController.Update 의 B 키(빌드 복귀)
        private const string PlayGuideText =
            "WASD/방향키 이동 · Space 점프 · Z 대시 · B 빌드 복귀";

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // 중복 리스너 방지를 위해 Awake 에서 1회만 연결
            if (startButton != null)
                startButton.onClick.AddListener(HandleStartClick);
        }

        private void OnEnable() => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;

        private void Start()
        {
            // 초기 동기화 규약: 구독자는 초기 상태를 이벤트가 아니라 currentPhase 직접 읽기로
            // 동기화한다 (VerificationSpeedController/StagePreviewPanel 과 동일 패턴).
            if (GamePhaseManager.Instance != null)
                HandlePhaseChanged(GamePhaseManager.Instance.currentPhase);
        }

        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (guideText != null)
            {
                guideText.text = phase switch
                {
                    GamePhase.Build        => BuildGuideText,
                    GamePhase.Verification => VerificationGuideText,
                    GamePhase.Play         => PlayGuideText,
                    _                       => string.Empty,
                };
            }

            if (startButtonRoot != null)
                startButtonRoot.SetActive(phase == GamePhase.Build);
        }

        // ── 버튼 ──────────────────────────────────────────────────────────────

        /// <summary>Enter 키와 동일하게 빌드 완료 → 검증/플레이 전환.</summary>
        private void HandleStartClick() => GamePhaseManager.Instance?.AdvanceFromBuild();
    }
}
