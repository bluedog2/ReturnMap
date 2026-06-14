using UnityEngine;

namespace ReTrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  열거형 & 인터페이스
    // ═══════════════════════════════════════════════════════════════════════════

    public enum PlayerAnimState
    {
        Idle,       // 0
        Run,        // 1
        JumpStart,  // 2  — 상승 중 루프 (Char_Jump_Start)
        JumpLoop,   // 3  — 하강 중 루프 (Char_Fall_Loop)
        Land,       // 4
        Dash,       // 5
        Knockback,  // 6  — 피격 넉백 (Lord_Hit)
        Death       // 7  — 사망 (Death)
    }

    public interface IPlayerAnimState
    {
        void OnEnter();
        void OnUpdate();
        void OnExit();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FSM 컨트롤러
    //  - PlayerController 의 이벤트를 구독하여 상태 전이를 트리거합니다.
    //  - OnUpdate() 에서는 연속 조건(속도, 착지 여부)을 폴링합니다.
    // ═══════════════════════════════════════════════════════════════════════════

    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimationFSM : MonoBehaviour
    {
        // ── 애니메이션 클립 해시 ─────────────────────────────────────────────
        public static readonly int HASH_IDLE       = Animator.StringToHash("Idle");
        public static readonly int HASH_RUN        = Animator.StringToHash("Run");
        public static readonly int HASH_JUMP_START = Animator.StringToHash("Jump_Start");
        public static readonly int HASH_JUMP_LOOP  = Animator.StringToHash("Jump_Loop");
        public static readonly int HASH_LAND       = Animator.StringToHash("Land");
        public static readonly int HASH_DASH       = Animator.StringToHash("Dash");
        public static readonly int HASH_KNOCKBACK  = Animator.StringToHash("Lord_Hit");
        public static readonly int HASH_DEATH      = Animator.StringToHash("Death");

        // ── 내부 참조 ────────────────────────────────────────────────────────
        public PlayerController Controller { get; private set; }
        private Animator anim;

        // ── 상태 인스턴스 ─────────────────────────────────────────────────────
        private IPlayerAnimState[] states;
        private IPlayerAnimState   current;

        public PlayerAnimState CurrentState { get; private set; }

        /// <summary>사망 상태 진입 여부. true 이면 다른 모든 전이를 차단합니다.</summary>
        public bool IsDead { get; private set; }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            Controller = GetComponent<PlayerController>();
            anim       = GetComponentInChildren<Animator>();

            // 상태 인스턴스 생성 (enum 인덱스와 배열 순서를 맞춥니다)
            states = new IPlayerAnimState[]
            {
                new IdleState(this),      // 0 Idle
                new RunState(this),       // 1 Run
                new JumpStartState(this), // 2 JumpStart
                new JumpLoopState(this),  // 3 JumpLoop
                new LandState(this),      // 4 Land
                new DashState(this),      // 5 Dash
                new KnockbackState(this), // 6 Knockback
                new DeathState(this),     // 7 Death
            };
        }

        private void OnEnable()
        {
            Controller.OnJump      += HandleJump;
            Controller.OnLand      += HandleLand;
            Controller.OnDashStart += HandleDashStart;
            Controller.OnDashEnd   += HandleDashEnd;
            Controller.OnKnockback += HandleKnockback;
        }

        private void OnDisable()
        {
            Controller.OnJump      -= HandleJump;
            Controller.OnLand      -= HandleLand;
            Controller.OnDashStart -= HandleDashStart;
            Controller.OnDashEnd   -= HandleDashEnd;
            Controller.OnKnockback -= HandleKnockback;
        }

        private void Start() => TransitionTo(PlayerAnimState.Idle);

        private void Update() => current?.OnUpdate();

        // ── 이벤트 핸들러 (PlayerController → FSM) ────────────────────────────

        private void HandleJump()      => TransitionTo(PlayerAnimState.JumpStart);
        private void HandleDashStart() => TransitionTo(PlayerAnimState.Dash);
        private void HandleKnockback() => TransitionTo(PlayerAnimState.Knockback);

        private void HandleLand()
        {
            // 넉백 경직 중 착지는 Hit 모션을 유지 (넉백 종료 후 KnockbackState 가 복귀 처리)
            if (Controller.IsKnockedBack) return;
            TransitionTo(PlayerAnimState.Land);
        }
        private void HandleDashEnd()
        {
            if (Controller.IsGrounded)
                TransitionTo(Mathf.Abs(Controller.MoveInput.x) > 0.01f
                    ? PlayerAnimState.Run : PlayerAnimState.Idle);
            else
                TransitionTo(Controller.IsFalling
                    ? PlayerAnimState.JumpLoop : PlayerAnimState.JumpStart);
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        /// <summary>사망 모션을 재생합니다. 호출 후에는 다른 상태로 전이되지 않습니다.</summary>
        public void PlayDeath()
        {
            if (IsDead) return;
            TransitionTo(PlayerAnimState.Death);
            IsDead = true;
        }

        public void TransitionTo(PlayerAnimState next)
        {
            // 사망 후에는 어떤 상태로도 전이하지 않음
            if (IsDead) return;

            if (current == states[(int)next]) return;

            current?.OnExit();
            CurrentState = next;
            current      = states[(int)next];
            current.OnEnter();
        }

        /// <summary>Animator 에 클립을 즉시 재생합니다. 상태 클래스에서 호출.</summary>
        public void Play(int hash)
        {
            if (anim == null) return;

            // Animator 컨트롤러에 해당 State 가 없을 경우 안전하게 무시
            if (!anim.HasState(0, hash))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[PlayerAnimFSM] Animator 에 State 가 없습니다: {hash}");
#endif
                return;
            }

            anim.CrossFade(hash, 0f);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  상태 구현체
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Idle — 바닥에 정지.</summary>
    internal class IdleState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal IdleState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_IDLE);

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(c.Velocity.y > 0.1f ? PlayerAnimState.JumpStart : PlayerAnimState.JumpLoop);
                return;
            }
            if (Mathf.Abs(c.MoveInput.x) > 0.01f)
                fsm.TransitionTo(PlayerAnimState.Run);
        }

        public void OnExit() { }
    }

    /// <summary>Run — 바닥 이동 중.</summary>
    internal class RunState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal RunState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_RUN);

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(c.Velocity.y > 0.1f ? PlayerAnimState.JumpStart : PlayerAnimState.JumpLoop);
                return;
            }
            if (Mathf.Abs(c.MoveInput.x) < 0.01f && Mathf.Abs(c.Velocity.x) < 0.1f)
                fsm.TransitionTo(PlayerAnimState.Idle);
        }

        public void OnExit() { }
    }

    /// <summary>
    /// JumpStart — 상승 중 루프 (Char_Jump_Start 스프라이트).
    /// velocity.y 가 0 아래로 떨어지면 JumpLoop(하강) 으로 전이.
    /// </summary>
    internal class JumpStartState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal JumpStartState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_JUMP_START);

        public void OnUpdate()
        {
            // 정점 체공(hang time) 구간은 상승 모션 유지, 확실히 떨어질 때만 하강 루프로 전이
            if (fsm.Controller.IsFalling)
                fsm.TransitionTo(PlayerAnimState.JumpLoop);
        }

        public void OnExit() { }
    }

    /// <summary>JumpLoop — 하강 중 루프 (Char_Fall_Loop 스프라이트). Land 이벤트로 탈출.</summary>
    internal class JumpLoopState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal JumpLoopState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_JUMP_LOOP);

        public void OnUpdate() { } // OnLand 이벤트가 전이 처리

        public void OnExit() { }
    }

    /// <summary>Land — 착지 모션. 클립 완료 후 Idle / Run 으로 전이.</summary>
    internal class LandState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        private const float CLIP_DURATION = 0.33f;

        internal LandState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_LAND);
            timer = CLIP_DURATION;
        }

        public void OnUpdate()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
                fsm.TransitionTo(Mathf.Abs(fsm.Controller.MoveInput.x) > 0.01f
                    ? PlayerAnimState.Run : PlayerAnimState.Idle);
        }

        public void OnExit() { }
    }

    /// <summary>
    /// Dash — 무적 대시 중.
    /// Animator 에 "Dash" 클립이 있으면 재생, 없으면 현재 클립 유지.
    /// 대시 종료는 OnDashEnd 이벤트가 처리.
    /// </summary>
    internal class DashState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal DashState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()  => fsm.Play(PlayerAnimationFSM.HASH_DASH);
        public void OnUpdate() { }
        public void OnExit()   { }
    }

    /// <summary>
    /// Knockback — 피격 넉백 경직 (Lord_Hit).
    /// <see cref="PlayerController.IsKnockedBack"/> 가 false 가 되면 현재 상황에 맞는 상태로 복귀.
    /// </summary>
    internal class KnockbackState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal KnockbackState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_KNOCKBACK);

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (c.IsKnockedBack) return; // 넉백 경직 지속 중 — Hit 모션 유지

            // 넉백 종료 → 지면/공중 상황에 맞게 복귀 (HandleDashEnd 와 동일 규칙)
            if (c.IsGrounded)
                fsm.TransitionTo(Mathf.Abs(c.MoveInput.x) > 0.01f
                    ? PlayerAnimState.Run : PlayerAnimState.Idle);
            else
                fsm.TransitionTo(c.IsFalling
                    ? PlayerAnimState.JumpLoop : PlayerAnimState.JumpStart);
        }

        public void OnExit() { }
    }

    /// <summary>
    /// Death — 사망 모션 (Death). 한 번 재생 후 마지막 프레임에서 정지.
    /// <see cref="PlayerAnimationFSM.PlayDeath"/> 로 진입하며, 다른 상태로 자동 전이하지 않습니다.
    /// </summary>
    internal class DeathState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal DeathState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_DEATH);

        public void OnUpdate() { } // 사망 후에는 전이하지 않음

        public void OnExit() { }
    }
}
