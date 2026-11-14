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
        JumpStart,  // 2  — 점프 시작 (Hero_JumpStart)
        Rise,       // 3  — 상승 루프 (Hero_Rise, 클립 Jump_Loop)
        Apex,       // 4  — 정점 체공 (Hero_Apex)
        Fall,       // 5  — 하강 루프 (Hero_Fall, 클립 Fall_Loop)
        Land,       // 6
        Dash,       // 7
        Knockback,  // 8  — 피격 넉백 (Lord_Hit)
        Death,      // 9  — 사망 (Death)
        BagOut,     // 10 — 긴 대기: 가방 꺼내기
        LongIdle,   // 11 — 긴 대기 루프
        BagIn,      // 12 — 긴 대기: 가방 넣기
        RunStart,   // 13 — 달리기 출발
        RunStop,    // 14 — 달리기 멈춤
        Turn        // 15 — 달리기 중 방향 전환
    }

    public interface IPlayerAnimState
    {
        void OnEnter();
        void OnUpdate();
        void OnExit();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  주인공 애니메이션 타이밍 (초)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 상태별 클립 길이(프레임 수 / fps). Px32Rules.CharacterSpec 의 주인공 규격과 일치해야 한다
    /// (Px32Rules 쪽 주석도 이 클래스와 일치를 요구).
    /// </summary>
    internal static class HeroAnimTiming
    {
        internal const float JumpStart    = 4f  / 20f;
        internal const float Apex         = 3f  / 12f;
        internal const float Land         = 4f  / 20f;
        internal const float Hit          = 4f  / 16f;
        internal const float BagOut       = 6f  / 12f;
        internal const float LongIdleLoop = 12f / 10f;
        internal const float BagIn        = 4f  / 12f;
        internal const float RunStart     = 3f  / 20f;
        internal const float RunStop      = 4f  / 20f;
        internal const float Turn         = 3f  / 20f;
        /// <summary>Turn 클립에서 그림 좌우가 새 방향으로 뒤집히는 시점 (3프레임 중 3번째 프레임 시작).</summary>
        internal const float TurnFlipAt   = 2f  / 20f;
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
        public static readonly int HASH_RISE       = Animator.StringToHash("Jump_Loop");  // 상승 루프 (Hero_Rise)
        public static readonly int HASH_APEX       = Animator.StringToHash("Apex");
        public static readonly int HASH_FALL       = Animator.StringToHash("Fall_Loop");
        public static readonly int HASH_LAND       = Animator.StringToHash("Land");
        public static readonly int HASH_DASH       = Animator.StringToHash("Dash");
        public static readonly int HASH_KNOCKBACK  = Animator.StringToHash("Lord_Hit");
        public static readonly int HASH_DEATH      = Animator.StringToHash("Death");
        public static readonly int HASH_BAG_OUT    = Animator.StringToHash("Bag_Out");
        public static readonly int HASH_LONG_IDLE  = Animator.StringToHash("Long_Idle");
        public static readonly int HASH_BAG_IN     = Animator.StringToHash("Bag_In");
        public static readonly int HASH_RUN_START  = Animator.StringToHash("Run_Start");
        public static readonly int HASH_RUN_STOP   = Animator.StringToHash("Run_Stop");
        public static readonly int HASH_TURN       = Animator.StringToHash("Turn");

        // ── 인스펙터 ─────────────────────────────────────────────────────────
        [Header("긴 대기")]
        [Tooltip("Idle 상태로 이 시간(초) 동안 입력이 없으면 가방을 꺼내는 긴 대기(BagOut)로 들어갑니다.")]
        [SerializeField] private float idleToLongIdleDelay = 6f;
        [Tooltip("긴 대기 루프를 이 횟수만큼 재생한 뒤 가방을 넣고(BagIn) Idle 로 돌아갑니다.")]
        [SerializeField] private int longIdleLoops = 3;

        internal float IdleToLongIdleDelay => idleToLongIdleDelay;
        internal int   LongIdleLoops       => longIdleLoops;

        // ── 내부 참조 ────────────────────────────────────────────────────────
        public PlayerController Controller { get; private set; }
        private Animator     anim;
        private PlayerHealth _health;

        // ── 상태 인스턴스 ─────────────────────────────────────────────────────
        private IPlayerAnimState[] states;
        private IPlayerAnimState   current;

        public PlayerAnimState CurrentState { get; private set; }

        /// <summary>사망 상태 진입 여부. true 이면 다른 모든 전이를 차단합니다.</summary>
        public bool IsDead { get; private set; }

        /// <summary>Turn 진입 시 이전 시선 방향 (true=오른쪽). <see cref="BeginTurn"/> 이 설정.</summary>
        internal bool TurnOldFacingRight { get; private set; }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            Controller = GetComponent<PlayerController>();
            anim       = GetComponentInChildren<Animator>();
            _health    = GetComponent<PlayerHealth>();

            // 상태 인스턴스 생성 (enum 인덱스와 배열 순서를 맞춥니다)
            states = new IPlayerAnimState[]
            {
                new IdleState(this),      // 0 Idle
                new RunState(this),       // 1 Run
                new JumpStartState(this), // 2 JumpStart
                new RiseState(this),      // 3 Rise
                new ApexState(this),      // 4 Apex
                new FallState(this),      // 5 Fall
                new LandState(this),      // 6 Land
                new DashState(this),      // 7 Dash
                new KnockbackState(this), // 8 Knockback
                new DeathState(this),     // 9 Death
                new BagOutState(this),    // 10 BagOut
                new LongIdleState(this),  // 11 LongIdle
                new BagInState(this),     // 12 BagIn
                new RunStartState(this),  // 13 RunStart
                new RunStopState(this),   // 14 RunStop
                new TurnState(this),      // 15 Turn
            };
        }

        private void OnEnable()
        {
            Controller.OnJump      += HandleJump;
            Controller.OnLand      += HandleLand;
            Controller.OnDashStart += HandleDashStart;
            Controller.OnDashEnd   += HandleDashEnd;
            Controller.OnKnockback += HandleKnockback;

            if (_health != null) _health.OnDeath += PlayDeath;
            RespawnManager.OnRespawn += HandleRespawn;
        }

        private void OnDisable()
        {
            Controller.OnJump      -= HandleJump;
            Controller.OnLand      -= HandleLand;
            Controller.OnDashStart -= HandleDashStart;
            Controller.OnDashEnd   -= HandleDashEnd;
            Controller.OnKnockback -= HandleKnockback;

            if (_health != null) _health.OnDeath -= PlayDeath;
            RespawnManager.OnRespawn -= HandleRespawn;

            Controller.VisualFacingOverride = 0;
        }

        /// <summary>리스폰 완료 시 사망 상태를 풀고 Idle 로 복귀.</summary>
        private void HandleRespawn()
        {
            IsDead = false;
            TransitionTo(PlayerAnimState.Idle);
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

            // 이동 입력이 있으면 착지 모션을 건너뛰고 바로 Run (착지 후 미끄러지는 느낌 방지)
            if (Mathf.Abs(Controller.MoveInput.x) > 0.01f)
            {
                TransitionTo(PlayerAnimState.Run);
                return;
            }
            TransitionTo(PlayerAnimState.Land);
        }
        private void HandleDashEnd()
        {
            if (Controller.IsGrounded)
                TransitionTo(Mathf.Abs(Controller.MoveInput.x) > 0.01f
                    ? PlayerAnimState.Run : PlayerAnimState.Idle);
            else
                TransitionTo(AirStateFor(Controller.Velocity.y, Controller.HangTimeThreshold));
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────────

        /// <summary>수직 속도로 공중 상태 결정: vy ≥ hang → Rise, vy ≤ -hang → Fall, 그 사이 → Apex.</summary>
        internal static PlayerAnimState AirStateFor(float vy, float hang)
        {
            if (vy >= hang)  return PlayerAnimState.Rise;
            if (vy <= -hang) return PlayerAnimState.Fall;
            return PlayerAnimState.Apex;
        }

        /// <summary>지면 상태에서 발이 떨어졌을 때의 공중 상태 (위로 움직이면 Rise, 아니면 Fall).</summary>
        internal PlayerAnimState GroundLeaveState()
            => Controller.Velocity.y > 0.1f ? PlayerAnimState.Rise : PlayerAnimState.Fall;

        /// <summary>가로 입력 부호: 오른쪽 +1, 왼쪽 -1, 없음 0.</summary>
        internal int InputSign
        {
            get
            {
                float x = Controller.MoveInput.x;
                return x > 0.01f ? 1 : x < -0.01f ? -1 : 0;
            }
        }

        /// <summary>전환 그림용 시선 덮어쓰기 (0=해제). 조작·대시 방향은 바뀌지 않는다.</summary>
        internal void SetVisualFacing(int dir) => Controller.VisualFacingOverride = dir;

        /// <summary>이전 시선 방향을 기록하고 Turn 상태로 전환한다.</summary>
        internal void BeginTurn(bool oldFacingRight)
        {
            TurnOldFacingRight = oldFacingRight;
            TransitionTo(PlayerAnimState.Turn);
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

    /// <summary>
    /// Idle — 바닥에 정지. 가로 입력이 있으면 RunStart,
    /// 입력 없이 <c>idleToLongIdleDelay</c> 초가 지나면 BagOut(긴 대기). 위·아래 입력은 대기 시간을 초기화한다.
    /// </summary>
    internal class IdleState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float idleTimer;
        internal IdleState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_IDLE);
            idleTimer = 0f;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }
            if (Mathf.Abs(c.MoveInput.x) > 0.01f)
            {
                fsm.TransitionTo(PlayerAnimState.RunStart);
                return;
            }
            if (Mathf.Abs(c.MoveInput.y) > 0.5f)
            {
                idleTimer = 0f; // 위·아래 입력은 "아직 조작 중"으로 본다
                return;
            }

            idleTimer += Time.deltaTime;
            if (idleTimer >= fsm.IdleToLongIdleDelay)
                fsm.TransitionTo(PlayerAnimState.BagOut);
        }

        public void OnExit() { }
    }

    /// <summary>
    /// Run — 바닥 이동 중. 반대 방향 입력이면 Turn, 입력이 없으면 RunStop.
    /// 진입 시 입력 부호(없으면 IsFacingRight)를 기록해 두고 그 값과 비교한다
    /// (PlayerController 와의 실행 순서에 의존하지 않기 위해 IsFacingRight 를 비교에 쓰지 않음).
    /// </summary>
    internal class RunState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private bool runFacingRight;
        internal RunState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_RUN);
            int sign = fsm.InputSign;
            runFacingRight = sign != 0 ? sign > 0 : fsm.Controller.IsFacingRight;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }

            int sign = fsm.InputSign;
            if (sign == 0)
            {
                fsm.TransitionTo(PlayerAnimState.RunStop);
                return;
            }
            if ((sign > 0) != runFacingRight)
            {
                fsm.BeginTurn(runFacingRight);
                return;
            }
            runFacingRight = sign > 0; // 같은 방향 — 갱신
        }

        public void OnExit() { }
    }

    /// <summary>JumpStart — 점프 시작 모션. 확실히 떨어지면 Fall, 클립이 끝나면 속도에 맞는 공중 상태로.</summary>
    internal class JumpStartState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        internal JumpStartState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_JUMP_START);
            timer = HeroAnimTiming.JumpStart;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            // 정점 체공(hang time) 구간은 Fall 로 보내지 않고, 확실히 떨어질 때만 Fall
            if (c.IsFalling)
            {
                fsm.TransitionTo(PlayerAnimState.Fall);
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
                fsm.TransitionTo(PlayerAnimationFSM.AirStateFor(c.Velocity.y, c.HangTimeThreshold));
        }

        public void OnExit() { }
    }

    /// <summary>Rise — 상승 루프 (Hero_Rise). 속도가 체공 문턱 아래로 내려가면 Apex, 확실히 떨어지면 Fall.</summary>
    internal class RiseState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal RiseState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_RISE);

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (c.IsFalling)
            {
                fsm.TransitionTo(PlayerAnimState.Fall);
                return;
            }
            if (c.Velocity.y < c.HangTimeThreshold)
                fsm.TransitionTo(PlayerAnimState.Apex);
        }

        public void OnExit() { }
    }

    /// <summary>Apex — 정점 체공 모션. 다시 빨리 올라가면 Rise, 클립이 끝난 뒤 확실히 떨어지면 Fall.</summary>
    internal class ApexState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        internal ApexState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_APEX);
            timer = HeroAnimTiming.Apex;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (c.Velocity.y >= c.HangTimeThreshold)
            {
                fsm.TransitionTo(PlayerAnimState.Rise);
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f && c.IsFalling)
                fsm.TransitionTo(PlayerAnimState.Fall);
        }

        public void OnExit() { }
    }

    /// <summary>Fall — 하강 루프 (Hero_Fall). Land 이벤트로 탈출.</summary>
    internal class FallState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        internal FallState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter() => fsm.Play(PlayerAnimationFSM.HASH_FALL);

        public void OnUpdate() { } // OnLand 이벤트가 전이 처리

        public void OnExit() { }
    }

    /// <summary>Land — 착지 모션. 클립 완료 후 Idle / Run 으로 전이.</summary>
    internal class LandState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;

        internal LandState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_LAND);
            timer = HeroAnimTiming.Land;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;

            // 착지 모션 중 다시 공중에 뜨면(점프·낙하) 해당 상태로 즉시 전환
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }
            // 이동 입력이 시작되면 모션 완료를 기다리지 않고 즉시 Run
            if (Mathf.Abs(c.MoveInput.x) > 0.01f)
            {
                fsm.TransitionTo(PlayerAnimState.Run);
                return;
            }

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
    /// 경직(화살 0.15s)이 클립(0.25s)보다 짧아도 제자리에 서 있으면 복귀 프레임까지 재생한다
    /// (이동·공중이면 Land 처럼 바로 전환).
    /// </summary>
    internal class KnockbackState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        internal KnockbackState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_KNOCKBACK);
            timer = HeroAnimTiming.Hit;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            timer -= Time.deltaTime;
            if (c.IsKnockedBack) return; // 넉백 경직 지속 중 — Hit 모션 유지
            if (timer > 0f && c.IsGrounded && Mathf.Abs(c.MoveInput.x) <= 0.01f) return; // 서 있으면 복귀 프레임까지

            // 넉백 종료 → 지면/공중 상황에 맞게 복귀 (HandleDashEnd 와 동일 규칙)
            if (c.IsGrounded)
                fsm.TransitionTo(Mathf.Abs(c.MoveInput.x) > 0.01f
                    ? PlayerAnimState.Run : PlayerAnimState.Idle);
            else
                fsm.TransitionTo(PlayerAnimationFSM.AirStateFor(c.Velocity.y, c.HangTimeThreshold));
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

    /// <summary>
    /// BagOut — 긴 대기 진입: 가방 꺼내기. 가로 입력이면 RunStart, 위·아래 입력만이면 Idle,
    /// 클립이 끝나면 LongIdle.
    /// </summary>
    internal class BagOutState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        internal BagOutState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_BAG_OUT);
            timer = HeroAnimTiming.BagOut;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }
            if (Mathf.Abs(c.MoveInput.x) > 0.01f)
            {
                fsm.TransitionTo(PlayerAnimState.RunStart);
                return;
            }
            if (Mathf.Abs(c.MoveInput.y) > 0.5f)
            {
                fsm.TransitionTo(PlayerAnimState.Idle);
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
                fsm.TransitionTo(PlayerAnimState.LongIdle);
        }

        public void OnExit() { }
    }

    /// <summary>
    /// LongIdle — 긴 대기 루프. 가로 입력이면 BagIn 을 생략하고 RunStart, 위·아래 입력만이면 BagIn,
    /// <c>longIdleLoops</c> 번 반복이 끝나면 BagIn.
    /// </summary>
    internal class LongIdleState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float elapsed;
        internal LongIdleState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_LONG_IDLE);
            elapsed = 0f;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }
            if (Mathf.Abs(c.MoveInput.x) > 0.01f)
            {
                fsm.TransitionTo(PlayerAnimState.RunStart);
                return;
            }
            if (Mathf.Abs(c.MoveInput.y) > 0.5f)
            {
                fsm.TransitionTo(PlayerAnimState.BagIn);
                return;
            }
            elapsed += Time.deltaTime;
            if (elapsed >= fsm.LongIdleLoops * HeroAnimTiming.LongIdleLoop)
                fsm.TransitionTo(PlayerAnimState.BagIn);
        }

        public void OnExit() { }
    }

    /// <summary>BagIn — 긴 대기 종료: 가방 넣기. 가로 입력이면 RunStart, 클립이 끝나면 Idle.</summary>
    internal class BagInState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        internal BagInState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_BAG_IN);
            timer = HeroAnimTiming.BagIn;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }
            if (Mathf.Abs(c.MoveInput.x) > 0.01f)
            {
                fsm.TransitionTo(PlayerAnimState.RunStart);
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
                fsm.TransitionTo(PlayerAnimState.Idle);
        }

        public void OnExit() { }
    }

    /// <summary>RunStart — 달리기 출발 모션. 입력이 사라지면 Idle, 클립이 끝나면 Run.</summary>
    internal class RunStartState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        internal RunStartState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_RUN_START);
            timer = HeroAnimTiming.RunStart;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }
            if (fsm.InputSign == 0)
            {
                fsm.TransitionTo(PlayerAnimState.Idle);
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
                fsm.TransitionTo(PlayerAnimState.Run);
        }

        public void OnExit() { }
    }

    /// <summary>
    /// RunStop — 달리기 멈춤 모션. 같은 방향 입력이면 Run, 반대 방향이면 Turn, 클립이 끝나면 Idle.
    /// 진입 시 IsFacingRight 를 기록해 이후 비교에 쓴다.
    /// </summary>
    internal class RunStopState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float timer;
        private bool stopFacingRight;
        internal RunStopState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_RUN_STOP);
            timer = HeroAnimTiming.RunStop;
            stopFacingRight = fsm.Controller.IsFacingRight;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }

            int sign = fsm.InputSign;
            if (sign != 0)
            {
                if ((sign > 0) == stopFacingRight) fsm.TransitionTo(PlayerAnimState.Run);
                else                               fsm.BeginTurn(stopFacingRight);
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
                fsm.TransitionTo(PlayerAnimState.Idle);
        }

        public void OnExit() { }
    }

    /// <summary>
    /// Turn — 방향 전환 모션. 진입 시 그림 시선을 이전 방향으로 붙잡아 두었다가(VisualFacingOverride)
    /// <see cref="HeroAnimTiming.TurnFlipAt"/> 에 새 방향으로 넘긴다. 조작 방향(IsFacingRight)은 즉시 바뀌어 있다.
    /// 이전 방향으로 입력이 되돌아오면 Run, 클립이 끝나면 입력 유무에 따라 Run / RunStop.
    /// </summary>
    internal class TurnState : IPlayerAnimState
    {
        private readonly PlayerAnimationFSM fsm;
        private float elapsed;
        private bool oldFacingRight;
        internal TurnState(PlayerAnimationFSM fsm) => this.fsm = fsm;

        public void OnEnter()
        {
            fsm.Play(PlayerAnimationFSM.HASH_TURN);
            oldFacingRight = fsm.TurnOldFacingRight;
            fsm.SetVisualFacing(oldFacingRight ? 1 : -1);
            elapsed = 0f;
        }

        public void OnUpdate()
        {
            var c = fsm.Controller;
            if (!c.IsGrounded)
            {
                fsm.TransitionTo(fsm.GroundLeaveState());
                return;
            }

            int sign = fsm.InputSign;
            if (sign != 0 && (sign > 0) == oldFacingRight)
            {
                fsm.TransitionTo(PlayerAnimState.Run); // 이전 방향으로 되돌아옴
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= HeroAnimTiming.TurnFlipAt)
                fsm.SetVisualFacing(0);
            if (elapsed >= HeroAnimTiming.Turn)
                fsm.TransitionTo(sign != 0 ? PlayerAnimState.Run : PlayerAnimState.RunStop);
        }

        public void OnExit() => fsm.SetVisualFacing(0);
    }
}
