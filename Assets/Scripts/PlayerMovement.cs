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

        if (isGrounded) coyoteTimeCounter = coyoteTime; // Reset timer while on ground
        else coyoteTimeCounter -= Time.deltaTime; // Count down when in the air
        
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

    // Called by Player Input Component (Jump Action)
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
            jumpBufferCounter = jumpBufferTime;
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
        // Flip the image based on movement direction
        if (horizontalInput > 0.1f)
        {
            spriteRenderer.flipX = false; // Facing Right
        }
        else if (horizontalInput < -0.1f)
        {
            spriteRenderer.flipX = true; // Facing Left
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