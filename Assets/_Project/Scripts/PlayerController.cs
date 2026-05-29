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

        [Header("Dash Settings")]
        public float dashForce = 20f;
        public float dashDuration = 0.2f;
        public int maxDashes = 3;

        [Header("Ground Detection")]
        public Transform groundCheck;
        public float groundCheckRadius = 0.2f;
        public LayerMask groundLayer;

        private Rigidbody2D rb;
        private Vector2 moveInput;
        private bool isGrounded;
        private float coyoteTimeCounter;
        private float jumpBufferCounter;
        private bool isJumping;
        private int currentDashes;
        private bool isDashing;
        private float dashTimeLeft;

        private PlayerInput playerInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;
            currentDashes = maxDashes;
            playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            if (playerInput != null)
            {
                playerInput.onActionTriggered += HandleAction;
                
                // Force enable map and all actions in asset
                if (playerInput.actions != null) 
                {
                    playerInput.actions.Enable();
                    Debug.Log("Input Actions Enabled explicitly.");
                }
                
                if (playerInput.currentActionMap != null) 
                {
                    playerInput.currentActionMap.Enable();
                    Debug.Log($"Input Map '{playerInput.currentActionMap.name}' Enabled explicitly.");
                }
                else if (!string.IsNullOrEmpty(playerInput.defaultActionMap))
                {
                    playerInput.SwitchCurrentActionMap(playerInput.defaultActionMap);
                    playerInput.currentActionMap?.Enable();
                    Debug.Log($"Input Map Switched to '{playerInput.defaultActionMap}' and Enabled.");
                }
            }
        }

        private void OnDisable()
        {
            if (playerInput != null)
            {
                playerInput.onActionTriggered -= HandleAction;
            }
        }

        // For "Invoke CSharp Events"
        private void HandleAction(InputAction.CallbackContext context)
        {
            string actionName = context.action.name;

            if (actionName == "Move") PerformMove(context);
            else if (actionName == "Jump") PerformJumpAction(context);
            else if (actionName == "Dash") PerformDash(context);
        }

        // For "Send Messages"
        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
            Debug.Log($"Input (Message): Move {moveInput}");
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                jumpBufferCounter = jumpBufferTime;
                Debug.Log("Input (Message): Jump Pressed");
            }
        }

        public void OnDash(InputValue value)
        {
            if (value.isPressed && currentDashes > 0 && !isDashing)
            {
                StartDash();
                Debug.Log("Input (Message): Dash Pressed");
            }
        }

        private void Update()
        {
            CheckGround();
            HandleTimers();
            HandleJump();
        }

        private void FixedUpdate()
        {
            if (isDashing)
            {
                HandleDashPhysics();
            }
            else
            {
                ApplyMovement();
                ApplyGravityScale();
            }
        }

        private void CheckGround()
        {
            bool wasGrounded = isGrounded;
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            if (isGrounded)
            {
                coyoteTimeCounter = coyoteTime;
                if (!wasGrounded)
                {
                    isJumping = false;
                    currentDashes = maxDashes; // Reset dashes on ground
                }
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
            Debug.Log("Jump Performed");
        }

        public void PerformMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
            Debug.Log($"Move Input Updated: {moveInput}");
        }

        public void PerformJumpAction(InputAction.CallbackContext context)
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

        public void PerformDash(InputAction.CallbackContext context)
        {
            if (context.started && currentDashes > 0 && !isDashing)
            {
                StartDash();
            }
        }

        private void StartDash()
        {
            isDashing = true;
            dashTimeLeft = dashDuration;
            currentDashes--;
            rb.gravityScale = 0f;
            
            float dashDir = moveInput.x != 0 ? Mathf.Sign(moveInput.x) : transform.localScale.x;
            rb.linearVelocity = new Vector2(dashDir * dashForce, 0f);
        }

        private void HandleDashPhysics()
        {
            dashTimeLeft -= Time.fixedDeltaTime;
            if (dashTimeLeft <= 0)
            {
                isDashing = false;
                rb.gravityScale = gravityScale;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y);
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
                // Hang Time logic
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
