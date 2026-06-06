using UnityEngine;
using UnityEngine.InputSystem;

namespace ReTrap
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 8f;
        public float acceleration = 50f;
        public float deceleration = 50f;

        [Header("Jump Settings")]
        public float jumpForce = 12f;
        public float jumpCutMultiplier = 0.5f;
        public float gravityScale = 3f;
        public float fallGravityMultiplier = 1.5f;

        [Header("Advanced Jump Settings")]
        public float coyoteTime = 0.15f;
        public float jumpBufferTime = 0.15f;
        public float hangTimeThreshold = 1f;
        public float hangTimeGravityMult = 0.5f;

        [Header("Ground Detection")]
        public Transform groundCheck;
        public float groundCheckRadius = 0.2f;
        public LayerMask groundLayer;

        [Header("References")]
        public Transform visualContainer;

        private Rigidbody2D rb;
        private Vector2 moveInput;
        private bool isGrounded;
        private float coyoteTimeCounter;
        private float jumpBufferCounter;
        private bool isJumping;

        private PlayerInput playerInput;
        private Animator animator;

        private int currentAnimationHash;
        private float groundExitTimer;
        private const float GROUND_BUFFER = 0.1f;
        private bool isFacingRight = true;

        // Animation States
        private static readonly int PLAYER_IDLE = Animator.StringToHash("Idle");
        private static readonly int PLAYER_RUN = Animator.StringToHash("Run");
        private static readonly int PLAYER_JUMP_START = Animator.StringToHash("Jump_Start");
        private static readonly int PLAYER_JUMP_LOOP = Animator.StringToHash("Jump_Loop");
        private static readonly int PLAYER_FALL_LOOP = Animator.StringToHash("Fall_Loop");
        private static readonly int PLAYER_LAND = Animator.StringToHash("Land");

        private float jumpStartAnimTimer;
        private float landAnimTimer;
        private bool wasGroundedLastFrame;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;
            playerInput = GetComponent<PlayerInput>();
            animator = GetComponent<Animator>();
            
            if (visualContainer == null)
            {
                visualContainer = transform.Find("Visual");
            }
        }

        private void OnEnable()
        {
            if (playerInput != null)
            {
                playerInput.onActionTriggered += HandleAction;
            }
        }

        private void OnDisable()
        {
            if (playerInput != null)
            {
                playerInput.onActionTriggered -= HandleAction;
            }
        }

        private void HandleAction(InputAction.CallbackContext context)
        {
            if (context.performed || context.started || context.canceled)
            {
                string actionName = context.action.name;

                if (actionName == "Move")
                {
                    moveInput = context.ReadValue<Vector2>();
                }
                else if (actionName == "Jump")
                {
                    PerformJumpAction(context);
                }
            }
        }

        private void Update()
        {
            CheckGround();
            HandleTimers();
            HandleJump();
            UpdateAnimationState();
            FlipSprite();
        }

        private void UpdateAnimationState()
        {
            if (animator == null) return;

            // 1. Landing state priority
            if (landAnimTimer > 0)
            {
                landAnimTimer -= Time.deltaTime;
                ChangeAnimationState(PLAYER_LAND);
                return;
            }

            // 2. Jump Start (Takeoff) animation
            if (jumpStartAnimTimer > 0)
            {
                jumpStartAnimTimer -= Time.deltaTime;
                ChangeAnimationState(PLAYER_JUMP_START);
                
                // Transition to air loop if starting to fall
                if (rb.linearVelocity.y < -1.0f) jumpStartAnimTimer = 0;
                else return;
            }

            // 3. Air states
            if (!isGrounded)
            {
                if (rb.linearVelocity.y > 0.1f)
                    ChangeAnimationState(PLAYER_JUMP_LOOP);
                else
                    ChangeAnimationState(PLAYER_FALL_LOOP);
            }
            // 4. Ground states
            else
            {
                if (Mathf.Abs(moveInput.x) > 0.01f || Mathf.Abs(rb.linearVelocity.x) > 0.1f)
                {
                    ChangeAnimationState(PLAYER_RUN);
                }
                else
                {
                    ChangeAnimationState(PLAYER_IDLE);
                }
            }
        }

        private void ChangeAnimationState(int newStateHash)
        {
            if (currentAnimationHash == newStateHash) return;

            // Crossfade with 0 duration as requested for immediate overwrite
            animator.CrossFade(newStateHash, 0f);
            currentAnimationHash = newStateHash;
        }

        private void FlipSprite()
        {
            // LOCK direction to input only. This ensures facing persists in Idle.
            if (moveInput.x > 0.01f)
            {
                isFacingRight = true;
            }
            else if (moveInput.x < -0.01f)
            {
                isFacingRight = false;
            }

            if (visualContainer != null)
            {
                Vector3 scale = visualContainer.localScale;
                float targetX = isFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                
                if (!Mathf.Approximately(scale.x, targetX))
                {
                    scale.x = targetX;
                    visualContainer.localScale = scale;
                }
            }
        }


        private void FixedUpdate()
        {
            ApplyMovement();
            ApplyGravityScale();
        }

        private void CheckGround()
        {
            wasGroundedLastFrame = isGrounded;
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            if (isGrounded)
            {
                groundExitTimer = GROUND_BUFFER;
                coyoteTimeCounter = coyoteTime;
                
                if (!wasGroundedLastFrame && rb.linearVelocity.y <= 0.1f)
                {
                    landAnimTimer = 0.33f; // Duration matching the landing clip
                    isJumping = false;
                }
            }
            else
            {
                groundExitTimer -= Time.deltaTime;
            }
        }

        private void HandleTimers()
        {
            if (!isGrounded)
                coyoteTimeCounter -= Time.deltaTime;

            jumpBufferCounter -= Time.deltaTime;
        }

        private void HandleJump()
        {
            if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && !isJumping)
            {
                PerformJump();
            }
        }

        private void PerformJump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            isJumping = true;
            jumpStartAnimTimer = 0.33f; // Set to actual clip duration

            ChangeAnimationState(PLAYER_JUMP_START);
        }

        private void PerformJumpAction(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                jumpBufferCounter = jumpBufferTime;
            }

            if (context.canceled && rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
                coyoteTimeCounter = 0f;
            }
        }

        private void ApplyMovement()
        {
            float targetSpeed = moveInput.x * moveSpeed;
            float speedDif = targetSpeed - rb.linearVelocity.x;
            float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
            float movement = speedDif * accelRate;

            rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
        }

        private void ApplyGravityScale()
        {
            if (rb.linearVelocity.y < 0)
            {
                rb.gravityScale = gravityScale * fallGravityMultiplier;
            }
            else if (isJumping && Mathf.Abs(rb.linearVelocity.y) < hangTimeThreshold)
            {
                rb.gravityScale = gravityScale * hangTimeGravityMult;
            }
            else
            {
                rb.gravityScale = gravityScale;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }
        }
    }
}
