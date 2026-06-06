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
        [SerializeField] private float moveSpeed       = 8f;
        [SerializeField] private float acceleration    = 50f;
        [SerializeField] private float deceleration    = 50f;

        [Header("Jump")]
        [SerializeField] private float jumpForce            = 12f;
        [SerializeField] private float gravityScale         = 3f;
        [SerializeField] private float fallGravityMultiplier = 1.5f;

        [Header("Variable Jump — Hold to go higher")]
        [Tooltip("버튼을 누르고 있는 동안 적용할 중력 배율 (1보다 작을수록 높이 올라감)")]
        [SerializeField] private float jumpHoldGravityMult  = 0.35f;
        [Tooltip("중력 감소가 유지되는 최대 시간 (초). 이 시간 이상 누르면 최대 높이)")]
        [SerializeField] private float maxJumpHoldTime      = 0.25f;
        [Tooltip("버튼을 놨을 때 속도를 줄이는 비율. 1=절단 없음, 0.5=절반")]
        [SerializeField] private float jumpCutMultiplier    = 0.75f;

        [Header("Advanced Jump")]
        [SerializeField] private float coyoteTime         = 0.15f;
        [SerializeField] private float jumpBufferTime     = 0.15f;
        [SerializeField] private float hangTimeThreshold  = 1f;
        [SerializeField] private float hangTimeGravityMult = 0.5f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed    = 18f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 0.1f;
        [SerializeField] private int   maxDashCount = 3;

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float     groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundLayer;

        [Header("References")]
        [SerializeField] private Transform visualContainer;

        // ── 외부 공개 상태 (FSM 이 읽음) ──────────────────────────────────────

        public Vector2 MoveInput       { get; private set; }
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

        // ── Private ───────────────────────────────────────────────────────────

        private Rigidbody2D    rb;
        private PlayerInput    playerInput;
        private SpriteRenderer visualSR;   // Visual 자식의 SpriteRenderer (flipX 용)

        private float coyoteTimeCounter;
        private float jumpBufferCounter;
        private float dashCooldownCounter;
        private float dashTimer;
        private bool  wasGroundedLastFrame;

        // Variable jump
        private bool  isHoldingJump;
        private float jumpHoldTimer;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            rb          = GetComponent<Rigidbody2D>();
            playerInput = GetComponent<PlayerInput>();

            rb.gravityScale = gravityScale;
            RemainingDashes = maxDashCount;

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
            UpdateFacingDirection(); // IsFacingRight 방향만 갱신
        }

        // Animator 갱신(Update → Animator → LateUpdate) 순서이므로
        // LateUpdate 에서 Scale 을 덮어써야 Animator WriteDefaults 의 영향을 받지 않습니다.
        private void LateUpdate()
        {
            ApplyFlip();
        }

        private void FixedUpdate()
        {
            if (!IsDashing)
            {
                ApplyMovement();
                ApplyGravityScale();
            }
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
                // 홀드 중단 → 중력 감소 종료
                isHoldingJump = false;

                // 아직 상승 중이면 속도를 살짝 줄여 빠른 응답감 제공
                // (jumpCutMultiplier=1 로 설정하면 완전히 부드러운 물리만 사용)
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
                coyoteTimeCounter = coyoteTime;
                RemainingDashes   = maxDashCount; // 착지 시 대시 충전

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
            if (!IsGrounded)        coyoteTimeCounter  -= Time.deltaTime;
            jumpBufferCounter  -= Time.deltaTime;
            dashCooldownCounter -= Time.deltaTime;
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
            jumpBufferCounter  = 0f;
            coyoteTimeCounter  = 0f;
            IsJumping          = true;
            isHoldingJump      = true;   // 홀드 중력 감소 시작
            jumpHoldTimer      = 0f;
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

        /// <summary>오브젝트 획득 등으로 외부에서 대시 충전.</summary>
        public void AddDashCharge(int count = 1)
            => RemainingDashes = Mathf.Min(RemainingDashes + count, maxDashCount);

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
            if (rb.linearVelocity.y < 0f)
            {
                // 하강 중 → 중력 강화 + 홀드 종료
                isHoldingJump   = false;
                rb.gravityScale = gravityScale * fallGravityMultiplier;
            }
            else if (isHoldingJump && jumpHoldTimer < maxJumpHoldTime && IsJumping)
            {
                // 버튼 홀드 중 + 상승 중 + 홀드 시간 이내 → 중력 감소 (더 높이)
                jumpHoldTimer  += Time.fixedDeltaTime;
                rb.gravityScale = gravityScale * jumpHoldGravityMult;
            }
            else if (IsJumping && Mathf.Abs(rb.linearVelocity.y) < hangTimeThreshold)
            {
                // 정점 부근 → 중력 감소 (체공감)
                rb.gravityScale = gravityScale * hangTimeGravityMult;
            }
            else
            {
                rb.gravityScale = gravityScale;
            }
        }

        // ── Sprite Flip ───────────────────────────────────────────────────────

        /// <summary>입력에 따라 IsFacingRight 플래그만 갱신. Update 에서 호출.</summary>
        private void UpdateFacingDirection()
        {
            if      (MoveInput.x > 0.01f)  IsFacingRight = true;
            else if (MoveInput.x < -0.01f) IsFacingRight = false;
        }

        /// <summary>
        /// IsFacingRight 에 따라 SpriteRenderer.flipX 를 설정합니다.
        /// localScale 대신 flipX 를 사용하므로 Animator 의 Scale 바인딩 영향을 받지 않습니다.
        /// LateUpdate 에서 호출 → Animator 갱신(Update~LateUpdate 사이) 이후 실행 보장.
        /// </summary>
        private void ApplyFlip()
        {
            if (visualSR == null) return;
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
