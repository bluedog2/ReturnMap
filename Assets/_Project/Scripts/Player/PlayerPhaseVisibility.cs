using UnityEngine;
using UnityEngine.InputSystem;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PlayerPhaseVisibility — 검증 페이즈 동안 플레이어를 완전히 숨김
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Player</b> 오브젝트에 부착합니다.
    ///
    /// <para><b>기획 근거</b>: 검증 페이즈는 "캐릭터 없이 카메라로만 관전"하는
    /// AI 관망 뷰(기획서 「3. 카메라」 2.2)다. 플레이어 캐릭터가 검증 중에도 씬에
    /// 남아 있으면 함정에 걸리거나 AI 동선의 시야를 방해하므로, 이 컴포넌트가
    /// 검증 페이즈 진입 시 플레이어를 완전히 비활성 상태로 만들고
    /// 검증을 벗어나면(Build/Play) 다시 복원한다.</para>
    ///
    /// <para><b>왜 SetActive(false) 를 쓰지 않는가</b>: 오브젝트 자체를 끄면
    /// 이 컴포넌트도 함께 비활성화되어 <see cref="GamePhaseManager.OnPhaseChanged"/>
    /// 를 더 이상 받을 수 없다 — 즉 스스로를 복원할 방법이 사라진다.
    /// 대신 렌더링/충돌/물리/입력 컴포넌트를 개별적으로 끈다.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerPhaseVisibility : MonoBehaviour
    {
        // ── Private — 컴포넌트 참조 (Awake 1회 캐시) ─────────────────────────

        private PlayerController _playerController;
        private Rigidbody2D      _rb;
        private PlayerInput      _playerInput;
        private SpriteRenderer[] _spriteRenderers;
        private Collider2D[]     _colliders;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _rb               = GetComponent<Rigidbody2D>();
            _playerInput      = GetComponent<PlayerInput>();
            _spriteRenderers  = GetComponentsInChildren<SpriteRenderer>(true);
            _colliders        = GetComponentsInChildren<Collider2D>(true);
        }

        private void OnEnable()  => GamePhaseManager.OnPhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GamePhaseManager.OnPhaseChanged -= HandlePhaseChanged;

        private void Start()
        {
            // 씬 시작 시점에 이미 검증 페이즈일 수 있으므로(BuildCameraController.Start 와 동일 패턴)
            // 현재 페이즈로 1회 동기화한다.
            if (GamePhaseManager.Instance != null)
                HandlePhaseChanged(GamePhaseManager.Instance.currentPhase);
        }

        // ── 페이즈 연동 ───────────────────────────────────────────────────────

        private void HandlePhaseChanged(GamePhase phase)
        {
            SetPlayerVisible(phase != GamePhase.Verification);
        }

        // ── 내부 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 플레이어의 렌더링/충돌/물리/입력을 일괄 토글합니다.
        /// 숨김 직전 상태를 개별 복원하는 게 아니라, 보일 때는 항상 전부 켜는 것이 정상 상태다.
        /// </summary>
        private void SetPlayerVisible(bool visible)
        {
            foreach (var sr in _spriteRenderers)
                sr.enabled = visible;

            foreach (var col in _colliders)
                col.enabled = visible; // 검증 중 함정에 걸리지 않도록

            if (_rb != null)
                _rb.simulated = visible; // 숨긴 채 중력으로 떨어지지 않도록

            if (visible)
            {
                // 리스폰 시퀀스가 사망 연출 중 입력을 꺼둔 상태일 수 있다.
                // 여기서 강제로 켜면 RespawnManager 의 입력 재활성화 시점과 어긋나므로,
                // 리스폰 진행 중에는 입력 복원을 건너뛰고 리스폰 시퀀스 자체가 켜도록 맡긴다.
                bool respawning = RespawnManager.Instance != null && RespawnManager.Instance.IsRespawning;
                if (_playerInput != null && !respawning)
                    _playerInput.ActivateInput();
            }
            else
            {
                _playerInput?.DeactivateInput();
            }

            if (_playerController != null)
                _playerController.enabled = visible;
        }
    }
}
