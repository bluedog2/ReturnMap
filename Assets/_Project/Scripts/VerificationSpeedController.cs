using UnityEngine;
using UnityEngine.InputSystem;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  VerificationSpeedController — 검증 페이즈 관전 배속/스킵
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 검증 페이즈(관전 전용)의 지루함을 줄이는 배속(2x/4x)·스킵 컨트롤
    /// (기획 리뷰 7월 §3-5: "관전만 하는 페이즈라 지루해질 위험 — 배속·스킵 버튼").
    ///
    /// <para><b>동작</b>: 검증 페이즈 동안만 <see cref="Time.timeScale"/> 을 조절하고,
    /// 페이즈를 벗어나거나 이 컴포넌트가 비활성화되면 <b>반드시 1로 복원</b>합니다
    /// (timeScale 오염은 빌드/플레이 페이즈 전체를 깨뜨리므로 복원이 최우선 불변식).</para>
    ///
    /// <para><b>조작</b>: Space = 배속 순환(1→2→4→1), Enter = 스킵(고속 소진).
    /// UI 버튼 배선용 공개 메서드(<see cref="CycleSpeed"/>/<see cref="Skip"/>)도 제공 —
    /// HUD 버튼이 생기면 그쪽에서 호출하면 됩니다.</para>
    ///
    /// <para><b>씬 배선</b>: Managers 오브젝트에 추가만 하면 동작합니다.</para>
    /// </summary>
    public class VerificationSpeedController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("배속")]
        [SerializeField]
        [Tooltip("Space 키(또는 UI 버튼)로 순환할 배속 단계. 기획: 1x → 2x → 4x.")]
        private float[] speedSteps = { 1f, 2f, 4f };

        [SerializeField, Min(1f)]
        [Tooltip("스킵(Enter) 시 적용할 고속 배속. 시뮬레이션을 실제로 고속 소진시켜 " +
                 "판정 결과는 정상 배속과 동일하게 유지된다 (결과 조작 없음).")]
        private float skipSpeed = 10f;

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private int  _stepIndex;
        private bool _active;

        /// <summary>현재 배속 (HUD 표기용).</summary>
        public float CurrentSpeed => _active ? Time.timeScale : 1f;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable() => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;

        private void OnDisable()
        {
            GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;
            RestoreSpeed(); // 검증 중 비활성화돼도 timeScale 오염 방지
        }

        private void Update()
        {
            if (!_active) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) CycleSpeed();
            if (keyboard.enterKey.wasPressedThisFrame) Skip();
        }

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            _active = phase == GamePhase.Verification;

            if (_active)
            {
                // 검증 진입마다 1x 로 리셋 — 직전 판의 배속이 이어지지 않게
                _stepIndex     = 0;
                Time.timeScale = speedSteps.Length > 0 ? speedSteps[0] : 1f;
            }
            else
            {
                RestoreSpeed();
            }
        }

        // ── 공개 API (키 입력·UI 버튼 공용) ──────────────────────────────────

        /// <summary>배속 단계 순환 (1→2→4→1). 검증 페이즈가 아니면 무시.</summary>
        public void CycleSpeed()
        {
            if (!_active || speedSteps.Length == 0) return;
            _stepIndex     = (_stepIndex + 1) % speedSteps.Length;
            Time.timeScale = speedSteps[_stepIndex];
        }

        /// <summary>스킵 — 시뮬레이션 고속 소진. 검증 페이즈가 아니면 무시.</summary>
        public void Skip()
        {
            if (!_active) return;
            Time.timeScale = skipSpeed;
        }

        // ── 내부 ──────────────────────────────────────────────────────────────

        private void RestoreSpeed() => Time.timeScale = 1f;
    }
}
