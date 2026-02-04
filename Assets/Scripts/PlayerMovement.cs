using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TimeRewind;

[RequireComponent(typeof(PlayerRewindController))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerPlatformer : MonoBehaviour
{
    [Header("Leeway Settings")]
    [SerializeField] private float coyoteTime = 0.2f; // How long you can jump after falling
    private float coyoteTimeCounter;
    [SerializeField] private float jumpBufferTime = 0.2f; // How early you can press jump before landing
    private float jumpBufferCounter;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    private bool canDash = true;
    private bool isDashing;

    private Rigidbody2D rb;
    public float horizontalInput;
    private bool jumpPressed;

    [Header("Visuals")]
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    public bool isGrounded { get; private set; }

    [Header("Wall Slide")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private LayerMask wallLayer; // can be = GroundLayer
    private bool isTouchingWall;
    private bool isWallSliding;

    [Header("Double Jump")]
    [SerializeField] private int extraJumps = 1; // Number of mid-air jumps allowed
    private int extraJumpsRemaining;
    private BoxCollider2D playerCollider;

    [Header("Wall Polish")]
    [SerializeField] private float wallCoyoteTime = 0.15f;
    private float wallCoyoteTimeCounter;

    [Header("Wall Jump Settings")]
    [SerializeField] private Vector2 wallJumpPower = new Vector2(10f, 15f); // X is away, Y is up
    [SerializeField] private float wallJumpDuration = 0.2f;
    private bool isWallJumping;

    [Header("Wall Jump Logic")]
    [SerializeField] private float wallJumpLockoutTime = 0.2f; // Time before you can grab a wall again
    private float wallJumpLockoutCounter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Auto-assign components if they weren't dragged into the Inspector
        if (anim == null) anim = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding) return;
        FlipSprite();
        if (isDashing) return;
        // Check if feet are touching the ground layer
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            anim.ResetTrigger("Jump");
        }

        if (isGrounded){
            coyoteTimeCounter = coyoteTime;
            extraJumpsRemaining = extraJumps;
        }
        else{
            coyoteTimeCounter -= Time.deltaTime;
        }
        
        if (wallJumpLockoutCounter > 0) wallJumpLockoutCounter -= Time.deltaTime;
        if (jumpBufferCounter > 0) jumpBufferCounter -= Time.deltaTime;

        float senseDirection = spriteRenderer.flipX ? -1f : 1f;
        float wallDistance = 0.5f;

        // This calculates the position manually, ignoring PPU/Child shifts
        Vector3 manualWallCheckPos = transform.position + new Vector3(senseDirection * wallDistance, 0.8f, 0);

        isTouchingWall = Physics2D.OverlapCircle(manualWallCheckPos, 0.45f, wallLayer);

        //if player is pushing towards wall -> actually slide
        bool isPushingWall = (horizontalInput > 0 && !spriteRenderer.flipX) || (horizontalInput < 0 && spriteRenderer.flipX);
        //bool isPushingWall = true;

        if (isTouchingWall && !isGrounded && rb.linearVelocity.y < 0 && isPushingWall && wallJumpLockoutCounter <= 0)
        { 
            float xOffset = spriteRenderer.flipX ? -0.10f : 0.10f;
            playerCollider.offset = new Vector2(xOffset, playerCollider.offset.y);
            isWallSliding = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -wallSlideSpeed, float.MaxValue));
        }
        else
        {
            isWallSliding = false;
        }

        // Update Animator Parameters
        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(horizontalInput));
            anim.SetBool("isGrounded", isGrounded);
            anim.SetBool("isWallSliding", isWallSliding);
        }
        
        // Jump Logic
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;            
            coyoteTimeCounter = 0f; // Prevent double jumping with coyote time
            if (anim != null) anim.SetTrigger("Jump");
        }

        if (isTouchingWall) 
        {
            wallCoyoteTimeCounter = wallCoyoteTime;
        }
        else 
        {
            wallCoyoteTimeCounter -= Time.deltaTime;
        }
    }
    
    private void OnDrawGizmos()
    {
        float senseDirection = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
        Vector3 debugPos = transform.position + new Vector3(senseDirection * 0.5f, 0.8f, 0);

        Gizmos.color = isTouchingWall ? Color.green : Color.blue;
        Gizmos.DrawWireSphere(debugPos, 0.45f);
    }

    private void FixedUpdate()
    {
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding) return;

        if (isDashing || isWallSliding || isWallJumping) return;
        // Apply horizontal movement while preserving falling/jumping speed
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }

    // Called by Player Input Component (Move Action)
    public void OnMove(InputAction.CallbackContext context)
    {
        horizontalInput = context.ReadValue<Vector2>().x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // 1. Only do a Wall Jump if the player is ACTUALLY sliding
            // This prevents the "kick back" when just walking into a wall on the ground
            if (isWallSliding)
            {
                StartCoroutine(WallJumpLogic());
            }
            // 2. Normal/Double Jump Logic
            else if (coyoteTimeCounter > 0f || extraJumpsRemaining > 0)
            {
                if (coyoteTimeCounter <= 0f) extraJumpsRemaining--;
                ExecuteJump();
            }
        
            jumpBufferCounter = jumpBufferTime;
        }
    }

    private void ExecuteJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    
        // Clear buffers so we don't trigger two jumps at once
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;

        if (anim != null) 
        {
            anim.ResetTrigger("Dash"); // Clear dash so it doesn't fire after jump
            anim.SetTrigger("Jump");
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed && canDash)
        {
            StartCoroutine(Dash());
        }
    }

    private IEnumerator WallJumpLogic()
    {
        isWallJumping = true; // Use this to ignore OnMove input in FixedUpdate
        wallCoyoteTimeCounter = 0; // Use it up immediately

        wallJumpLockoutCounter = wallJumpLockoutTime;

        float jumpDirection = spriteRenderer.flipX ? 1f : -1f;
        rb.linearVelocity = new Vector2(jumpDirection * wallJumpPower.x, wallJumpPower.y);

        if (anim != null) anim.SetTrigger("Jump"); // Or "WallJump" if you have it
    
        yield return new WaitForSeconds(wallJumpDuration);    
        isWallJumping = false;
    }

    private IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;

        if (anim != null) 
        {
            anim.ResetTrigger("Jump"); // Clear jump so it doesn't fire after dash
            anim.SetTrigger("Dash");
        }
        
        // Save current gravity, then set to 0 so we don't drop while dashing
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // Apply dash velocity based on the direction the player is facing
        float dashDirection = spriteRenderer.flipX ? -1f : 1f;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);

        if (anim != null) anim.SetTrigger("Dash");

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void FlipSprite()
    {

        if (horizontalInput > 0.1f) spriteRenderer.flipX = false;
        else if (horizontalInput < -0.1f) spriteRenderer.flipX = true;

        if (!spriteRenderer.flipX) // Facing Right
        {
            playerCollider.offset = new Vector2(-0.06f, 0.007f); 
            wallCheck.localPosition = new Vector2(0.5f, 0.8f); 
        }
        else // Facing Left
        {
            playerCollider.offset = new Vector2(0.05f, 0.007f); 
            wallCheck.localPosition = new Vector2(-0.5f, 0.8f);
        }
    }

    // Visualization for the Ground Check in the Scene View
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    public void TriggerJumpTest()
    {
        jumpBufferCounter = jumpBufferTime;
    }
}
