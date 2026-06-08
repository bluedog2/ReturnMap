using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace ReTrap
{
    /// <summary>
    /// 플레이어 물리 / 입력 전담 컨트롤러.
    /// 애니메이션은 PlayerAnimationFSM 이 담당하며, 이 클래스는 이벤트와 상태 프로퍼티만 노출합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Movement")]
        [SerializeField] private float moveSpeed    = 8f;
        [SerializeField] private float acceleration = 50f;
        [SerializeField] private float deceleration = 50f;

        [Header("Jump")]
        [SerializeField] private float jumpForce             = 12f;
        [SerializeField] private float gravityScale          = 3f;
        [SerializeField] private float fallGravityMultiplier = 1.5f;

        [Header("Variable Jump")]
        [Tooltip("버튼을 누르는 동안 적용할 중력 배율 (낮을수록 더 높이 점프)")]
        [SerializeField] private float jumpHoldGravityMult = 0.35f;
        [Tooltip("중력 감소가 유지되는 최대 시간 (초)")]
        [SerializeField] private float maxJumpHoldTime     = 0.25f;
        [Tooltip("버튼을 뗄 때 상승 속도를 줄이는 비율 (1=없음, 0=즉시 정지)")]
        [SerializeField] private float jumpCutMultiplier   = 0.75f;

        [Header("Advanced Jump")]
        [SerializeField] private float coyoteTime          = 0.15f;
        [SerializeField] private float jumpBufferTime      = 0.15f;
        [SerializeField] private float hangTimeThreshold   = 1f;
        [SerializeField] private float hangTimeGravityMult = 0.5f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed        = 18f;
        [SerializeField] private float dashDuration     = 0.2f;
        [SerializeField] private float dashCooldown     = 0.1f;   // 연속 대시 사이 최소 간격
        [SerializeField] private float dashRechargeTime = 1f;     // 전량 소진 후 전량 충전까지 대기 시간
        [SerializeField] private int   maxDashCount     = 3;

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float     groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundLayer;

        [Header("References")]
        [SerializeField] private Transform visualContainer;

        // ── 외부 공개 상태 (FSM 이 읽음) ──────────────────────────────────────

        public Vector2 MoveInput       { get; private set; }

        /// <summary>
        /// 현재 Rigidbody2D 선형 속도. FixedUpdate 와 같은 물리 컨텍스트에서 읽는 것을 권장합니다.
        /// </summary>
        public Vector2 Velocity        => rb.linearVelocity;

        public bool    IsGrounded      { get; private set; }
        public bool    IsJumping       { get; private set; }
        public bool    IsDashing       { get; private set; }
        public bool    IsFacingRight   { get; private set; } = true;
        public int     RemainingDashes { get; private set; }

        // ── 이벤트 (FSM 이 구독) ──────────────────────────────────────────────

        public event Action OnJump;
        public event Action OnLand;
        public event Action OnDashStart;
        public event Action OnDashEnd;

        // ── Private — 컴포넌트 참조 ──────────────────────────────────────────

        private Rigidbody2D    rb;
        private PlayerInput    playerInput;
        private SpriteRenderer visualSR;

        // ── Private — 상태 변수 ───────────────────────────────────────────────

        private float coyoteTimeCounter;
        private float jumpBufferCounter;
        private float dashCooldownCounter;
        private float dashRechargeCounter;
        private float dashTimer;
        private bool  wasGroundedLastFrame;
        private float knockbackTimer;

        private bool  isHoldingJump;
        private float jumpHoldTimer;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            rb          = GetComponent<Rigidbody2D>();
            playerInput = GetComponent<PlayerInput>();

            rb.gravityScale = gravityScale;
            RemainingDashes = maxDashCount;

            // visualContainer 는 Inspector 미설정 시 자동으로 "Visual" 자식을 탐색
            if (visualContainer == null)
                visualContainer = transform.Find("Visual");

            if (visualContainer != null)
                visualSR = visualContainer.GetComponent<SpriteRenderer>();
        }

        private void OnEnable()  => playerInput.onActionTriggered += HandleAction;
        private void OnDisable() => playerInput.onActionTriggered -= HandleAction;

        private void Update()
        {
            CheckGround();
            TickTimers();
            HandleJump();
            TickDash();
            UpdateFacingDirection();
        }

        // Animator 갱신(Update → Animator → LateUpdate) 순서이므로
        // LateUpdate 에서 flipX 를 덮어써야 Animator WriteDefaults 의 영향을 받지 않습니다.
        private void LateUpdate() => ApplyFlip();

        private void FixedUpdate()
        {
            if (IsDashing) return;

            // 넉백 중에는 수평 이동 연산을 건너뜀 — 감속 Force 가 즉시 넉백을 상쇄하는 것을 방지
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                ApplyGravityScale();
                return;
            }

            ApplyMovement();
            ApplyGravityScale();
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private void HandleAction(InputAction.CallbackContext ctx)
        {
            switch (ctx.action.name)
            {
                case "Move":
                    if (ctx.performed || ctx.canceled)
                        MoveInput = ctx.ReadValue<Vector2>();
                    break;

                case "Jump":
                    HandleJumpInput(ctx);
                    break;

                case "Dash":
                    if (ctx.started) TryDash();
                    break;
            }
        }

        private void HandleJumpInput(InputAction.CallbackContext ctx)
        {
            if (ctx.started)
                jumpBufferCounter = jumpBufferTime;

            if (ctx.canceled)
            {
                isHoldingJump = false;

                // 아직 상승 중이면 속도를 줄여 빠른 점프 컷 적용
                if (rb.linearVelocity.y > 0f)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                                                    rb.linearVelocity.y * jumpCutMultiplier);
                    coyoteTimeCounter = 0f;
                }
            }
        }

        // ── Ground ────────────────────────────────────────────────────────────

        private void CheckGround()
        {
            wasGroundedLastFrame = IsGrounded;
            IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            if (IsGrounded)
            {
                // 지면에 있는 동안 코요테 타임을 지속 충전 (절벽에서 뛰어내리는 순간부터 카운트다운)
                coyoteTimeCounter = coyoteTime;

                // 착지 순간에만 처리 (매 프레임 실행 방지)
                if (!wasGroundedLastFrame && rb.linearVelocity.y <= 0.1f)
                {
                    IsJumping = false;
                    OnLand?.Invoke();
                }
            }
        }

        // ── Timers ────────────────────────────────────────────────────────────

        private void TickTimers()
        {
            if (!IsGrounded)
                coyoteTimeCounter -= Time.deltaTime;

            jumpBufferCounter   = Mathf.Max(0f, jumpBufferCounter   - Time.deltaTime);
            dashCooldownCounter = Mathf.Max(0f, dashCooldownCounter - Time.deltaTime);

            // 대시 전량 소진 후 쿨타임 카운트다운 → 0 되면 전량 충전
            if (dashRechargeCounter > 0f)
            {
                dashRechargeCounter -= Time.deltaTime;
                if (dashRechargeCounter <= 0f)
                {
                    dashRechargeCounter = 0f;
                    RemainingDashes     = maxDashCount;
                }
            }
        }

        // ── Jump ──────────────────────────────────────────────────────────────

        private void HandleJump()
        {
            if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && !IsJumping && !IsDashing)
                PerformJump();
        }

        private void PerformJump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            IsJumping         = true;
            isHoldingJump     = true;
            jumpHoldTimer     = 0f;
            OnJump?.Invoke();
        }

        // ── Dash ──────────────────────────────────────────────────────────────

        private void TryDash()
        {
            if (RemainingDashes <= 0 || dashCooldownCounter > 0f || IsDashing) return;

            RemainingDashes--;
            dashCooldownCounter = dashCooldown;
            dashTimer           = dashDuration;
            IsDashing           = true;

            // 전량 소진 시 재충전 타이머 시작
            if (RemainingDashes <= 0)
                dashRechargeCounter = dashRechargeTime;

            float dir = Mathf.Abs(MoveInput.x) > 0.01f
                ? Mathf.Sign(MoveInput.x)
                : (IsFacingRight ? 1f : -1f);

            rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);
            rb.gravityScale   = 0f;

            OnDashStart?.Invoke();
        }

        private void TickDash()
        {
            if (!IsDashing) return;

            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                IsDashing       = false;
                rb.gravityScale = gravityScale;
                OnDashEnd?.Invoke();
            }
        }

        /// <summary>아이템 획득 등 외부 이벤트로 대시 횟수를 추가합니다.</summary>
        public void AddDashCharge(int count = 1)
            => RemainingDashes = Mathf.Min(RemainingDashes + count, maxDashCount);

        /// <summary>
        /// 리스폰 시 런타임 상태를 완전히 초기화합니다. RespawnManager 에서 호출.
        /// 대시·점프·넉백 상태와 물리 속도를 모두 리셋합니다.
        /// </summary>
        public void ResetState()
        {
            // 대시 초기화
            IsDashing           = false;
            dashTimer           = 0f;
            dashCooldownCounter = 0f;
            dashRechargeCounter = 0f;
            RemainingDashes     = maxDashCount;

            // 점프 초기화
            IsJumping         = false;
            isHoldingJump     = false;
            jumpHoldTimer     = 0f;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;

            // 넉백 초기화
            knockbackTimer = 0f;

            // 물리 초기화
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale   = gravityScale;
        }

        /// <summary>
        /// 함정 등 외부에서 순간 속도를 덮어씌워 넉백 처리.
        /// <paramref name="duration"/> 동안 플레이어 입력 이동 연산을 억제해
        /// 감속 Force 가 즉시 넉백을 상쇄하는 현상을 방지합니다.
        /// </summary>
        public void ApplyKnockback(Vector2 velocity, float duration = 0.15f)
        {
            if (IsDashing) return; // 대시 무적 중 넉백 면역
            rb.linearVelocity = velocity;
            knockbackTimer    = duration;
        }

        // ── Movement / Gravity ────────────────────────────────────────────────

        private void ApplyMovement()
        {
            float targetSpeed = MoveInput.x * moveSpeed;
            float speedDiff   = targetSpeed - rb.linearVelocity.x;
            float accelRate   = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
            rb.AddForce(speedDiff * accelRate * Vector2.right, ForceMode2D.Force);
        }

        private void ApplyGravityScale()
        {
            float vy = rb.linearVelocity.y;

            if (vy < 0f)
            {
                // 하강 중: 중력 강화 + 홀드 종료
                isHoldingJump   = false;
                rb.gravityScale = gravityScale * fallGravityMultiplier;
            }
            else if (isHoldingJump && jumpHoldTimer < maxJumpHoldTime && IsJumping)
            {
                // 버튼 홀드 중 + 상승 + 홀드 시간 이내: 중력 감소 (더 높이 점프)
                jumpHoldTimer  += Time.fixedDeltaTime;
                rb.gravityScale = gravityScale * jumpHoldGravityMult;
            }
            else if (IsJumping && Mathf.Abs(vy) < hangTimeThreshold)
            {
                // 정점 부근: 체공감을 위한 중력 감소
                rb.gravityScale = gravityScale * hangTimeGravityMult;
            }
            else
            {
                rb.gravityScale = gravityScale;
            }
        }

        // ── Sprite Flip ───────────────────────────────────────────────────────

        private void UpdateFacingDirection()
        {
            if      (MoveInput.x > 0.01f)  IsFacingRight = true;
            else if (MoveInput.x < -0.01f) IsFacingRight = false;
        }

        private void ApplyFlip()
        {
            if (visualSR != null)
                visualSR.flipX = !IsFacingRight;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
