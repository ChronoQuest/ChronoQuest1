using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

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
    private float horizontalInput;
    private bool jumpPressed;

    [Header("Visuals")]
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded;

    [Header("Wall Slide")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private LayerMask wallLayer; // can be = GroundLayer
    private bool isTouchingWall;
    private bool isWallSliding;

    [Header("Double Jump")]
    [SerializeField] private int extraJumps = 1; // Number of mid-air jumps allowed
    private int extraJumpsRemaining;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Auto-assign components if they weren't dragged into the Inspector
        if (anim == null) anim = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {

        if (isDashing) return;
        // Check if feet are touching the ground layer
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded){
            coyoteTimeCounter = coyoteTime;
            extraJumpsRemaining = extraJumps;
        }
        else{
            coyoteTimeCounter -= Time.deltaTime;
        }
        
        if (jumpBufferCounter > 0) jumpBufferCounter -= Time.deltaTime;

        float facingDirection = spriteRenderer.flipX ? -1f : 1f;
        Vector2 wallCheckPos = (Vector2)transform.position + new Vector2(facingDirection * 0.3f, 0.8f);        
        isTouchingWall = Physics2D.OverlapCircle(wallCheck.position, 0.3f, wallLayer);

        //if player is pushing towards wall -> actually slide
        bool isPushingWall = (horizontalInput > 0 && !spriteRenderer.flipX) || (horizontalInput < 0 && spriteRenderer.flipX);

        if (isTouchingWall && !isGrounded && rb.linearVelocity.y < 0 && isPushingWall)
        {
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

        FlipSprite();
    }
    
    private void OnDrawGizmos()
    {
        if (wallCheck != null)
        {
            Gizmos.color = isTouchingWall ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(wallCheck.position, 0.2f);
        }
    }

    private void FixedUpdate()
    {
        if (isDashing || isWallSliding) return;
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
            // If we are on the ground or in coyote time, the Update() method handles it via the buffer.
            // We set the buffer here:
            jumpBufferCounter = jumpBufferTime;

            // If we are ALREADY in the air and out of coyote time, try a double jump:
            if (coyoteTimeCounter <= 0f && !isWallSliding && extraJumpsRemaining > 0)
            {
                ExecuteJump();
                extraJumpsRemaining--; // Use up one of the mid-air jumps
            }
        }
    }

    private void ExecuteJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    
        // Clear buffers so we don't trigger two jumps at once
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;

        if (anim != null) anim.SetTrigger("Jump");
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed && canDash)
        {
            StartCoroutine(Dash());
        }
    }



    private IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;
        
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
    bool isMovingLeft = horizontalInput < -0.1f;
    bool isMovingRight = horizontalInput > 0.1f;

    if (isMovingRight && spriteRenderer.flipX)
    {
        spriteRenderer.flipX = false;
        // Flip the WallCheck position to the right side
        Vector3 newPos = wallCheck.localPosition;
        newPos.x = Mathf.Abs(newPos.x); 
        wallCheck.localPosition = newPos;
    }
    else if (isMovingLeft && !spriteRenderer.flipX)
    {
        spriteRenderer.flipX = true;
        // Flip the WallCheck position to the left side
        Vector3 newPos = wallCheck.localPosition;
        newPos.x = -Mathf.Abs(newPos.x); 
        wallCheck.localPosition = newPos;
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
}