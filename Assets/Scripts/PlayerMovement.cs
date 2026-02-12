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

    [Header("Animation")]
    [SerializeField] private float totalJumpFrames = 9f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private LayerMask dashPhaseLayers;
    private bool canDash = true;
    private bool isDashing;
    private bool _isRewinding = false;
    private PlayerRewindController rewindController;

    private Rigidbody2D rb;
    public float horizontalInput;

    [Header("Visuals")]
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer spriteRenderer;
    

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.35f;
    [SerializeField] private LayerMask groundLayer;

    public bool isGrounded { get; private set; }

    [Header("Wall Slide")]
    // [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private LayerMask wallLayer; 
    [SerializeField] private float wallCheckDistance = 0.8f;
    [SerializeField] private bool isTouchingWall;
    [SerializeField] private bool isWallSliding;

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

    [Header("Input Keys")]
    [SerializeField] private Key moveLeftKey = Key.A;
    [SerializeField] private Key moveRightKey = Key.D;
    [SerializeField] private Key jumpKey = Key.Space;

    private bool jumpPressedThisFrame;
    private bool wasGrounded;
    private bool isLanding;

    private TutorialManager tutorialManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Auto-assign components if they weren't dragged into the Inspector
        if (anim == null) anim = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<BoxCollider2D>();

        rewindController = GetComponent<PlayerRewindController>();
        if (rewindController != null)
        {
            rewindController.OnRewindStarted += OnStartRewind;
            rewindController.OnRewindStopped += OnStopRewind;
        }
    }

    private void Update()
    {
        bool isDead = GetComponent<PlayerHealth>()?.IsDead ?? false; 

        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position, 
            groundCheckRadius, 
            groundLayer
        ); 

        if (anim != null) 
            anim.SetBool("isGrounded", isGrounded); 
        
        if (isDead)
        {
            return; 
        }
        
        if (isDashing) return;
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            return;
        FlipSprite();
        float kb = 0f;
        if (Keyboard.current != null)
            kb = (Keyboard.current.aKey.isPressed ? -1f : 0f) + (Keyboard.current.dKey.isPressed ? 1f : 0f);
        float gp = 0f;
        if (Gamepad.current != null)
            gp = Gamepad.current.leftStick.x.ReadValue();
        horizontalInput = Mathf.Clamp(kb + gp, -1f, 1f);


        // Check if feet are touching the ground layer
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded && !wasGrounded && !isLanding)
        {
            StartCoroutine(LandingRoutine());
        }
        wasGrounded = isGrounded;

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


        float direction = spriteRenderer.flipX ? -1f : 1f;

        // Raise the origin to "Chest Height" (e.g., +0.5f Y)
        // This is CRITICAL: It ensures we don't hit the floor and think it's a wall.
        Vector2 wallOrigin = new Vector2(transform.position.x, transform.position.y + 0.5f);

        RaycastHit2D wallHit = Physics2D.Raycast(
            wallOrigin, 
            Vector2.right * direction, 
            wallCheckDistance, 
            groundLayer // Using the same layer!
        );

        isTouchingWall = wallHit.collider != null;

        

        //if player is pushing towards wall -> actually slide
        bool isPushingWall = (horizontalInput > 0 && !spriteRenderer.flipX) || (horizontalInput < 0 && spriteRenderer.flipX);
        //bool isPushingWall = true;

        if (isTouchingWall && !isGrounded && rb.linearVelocity.y < 0 && isPushingWall && wallJumpLockoutCounter <= 0)
        { 
            float xOffset = spriteRenderer.flipX ? -0.10f : 0.10f;
            playerCollider.offset = new Vector2(xOffset, playerCollider.offset.y);
            isWallSliding = true;
            extraJumpsRemaining = 1;
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
            if (!isLanding) 
            {
                anim.SetBool("isGrounded", isGrounded);
                // Only update airborne frames if we are actually in the air
                if (!isGrounded) UpdateAirborneAnimation(); 
            }
            anim.SetBool("isWallSliding", isWallSliding);
            UpdateAirborneAnimation();
        }
        
        // Jump Logic
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

            jumpBufferCounter = 0f;            
            coyoteTimeCounter = 0f; // Prevent double jumping with coyote time
            if (anim != null) anim.SetTrigger("Jump");
            tutorialManager?.OnPlayerJump();
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
        if (GetComponent<PlayerHealth>()?.IsDead == true)
            return;

        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            return;

        if (isDashing || isWallSliding || isWallJumping) return;
        // Apply horizontal movement while preserving falling/jumping speed
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }


    public void OnJump(InputAction.CallbackContext context)
    {
        if (GetComponent<PlayerHealth>()?.IsDead == true)
            return;
        
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
                StartCoroutine(JumpRoutine(extraJumpsRemaining));
            }
        
            jumpBufferCounter = jumpBufferTime;
        }
    }

    // =========================================================
    // JUMP ROUTINE (SHORT + SAFE)
    // =========================================================
    IEnumerator JumpRoutine(int extraJumpsRemaining)
    {
        // Clear buffers so we don't trigger two jumps at once
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        anim.SetTrigger("Jump");

        if (extraJumpsRemaining>0)
        {
            anim.SetBool("isGrounded", true); 
            SetFrame(0); // brace
            yield return new WaitForSeconds(0.02f);
            SetFrame(1);
        }
        else
        {
            SetFrame(2); // mid-air start
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        isGrounded = false;
    

        yield return null;
    }
    // =========================================================
    // AIRBORNE ANIMATION (GLOBAL)
    // =========================================================
    void UpdateAirborneAnimation()
    {
        if (isGrounded) return;

        

        float vy = rb.linearVelocity.y;

        if (vy > 5f)            SetFrame(2);
        else if (vy > 0.1f)     SetFrame(3);
        else if (vy > -0.1f)    SetFrame(3);
        else if (vy > -10f)      SetFrame(4);
        else                    SetFrame(5);
    }

    // =========================================================
    // LANDING
    // =========================================================
    IEnumerator UpdateLanding()
    {
        isLanding = true; // Lock the Update loop out of the animator
    
        // Keep the animator in "Air" state so SetFrame works
        anim.SetBool("isGrounded", false); 

        SetFrame(6); // Impact Frame
        yield return new WaitForSeconds(0.1f); // 0.5s is too long for a landing! Reduced to 0.1f
        
        SetFrame(7); // Recovery Frame
        yield return new WaitForSeconds(0.1f);

        isLanding = false; // Release the lock
        anim.SetBool("isGrounded", true); // Return to idle
    }

    IEnumerator LandingRoutine()
    {

        SetFrame(6);
        yield return new WaitForSeconds(0.05f);
        SetFrame(7);
        yield return new WaitForSeconds(0.09f);
        SetFrame(8);
        yield return new WaitForSeconds(0.09f);

        anim.SetBool("isGrounded", true);
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (GetComponent<PlayerHealth>()?.IsDead == true)
            return;
        
        if (!context.performed || isDashing || _isRewinding) return;
        var gamepad = Gamepad.current;
        if (gamepad != null && context.control?.device == gamepad && gamepad.leftTrigger.ReadValue() > 0.5f)
            return;
        if (canDash) StartCoroutine(Dash());
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

    IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;

        if (anim != null) 
        {
            anim.ResetTrigger("Jump"); // Clear jump so it doesn't fire after dash
            if (!_isRewinding) anim.SetTrigger("Dash");
        }
        
        float gravity = rb.gravityScale;
        rb.gravityScale = 0f;
        SetDashPhasing(true);

        float dir = spriteRenderer.flipX ? -1f : 1f;
        if (isTouchingWall) dir = -dir;
        rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

        yield return new WaitForSeconds(dashDuration);
        SetDashPhasing(false);

        rb.gravityScale = gravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
    // Helper to toggle collision with specific layers
    private void SetDashPhasing(bool ignore)
    {
        int playerLayer = gameObject.layer;
        
        // Loop through all 32 possible layers
        for (int i = 0; i < 32; i++)
        {
            // If the layer 'i' is selected in our LayerMask...
            if ((dashPhaseLayers.value & (1 << i)) != 0)
            {
                // Toggle collision between Player and that Layer
                Physics2D.IgnoreLayerCollision(playerLayer, i, ignore);
            }
        }
    }

    void FlipSprite()
    {

        if (horizontalInput > 0.1f) spriteRenderer.flipX = false;
        else if (horizontalInput < -0.1f) spriteRenderer.flipX = true;

        if (!spriteRenderer.flipX) // Facing Right
        {
            playerCollider.offset = new Vector2(-0.06f, 0.007f); 
            // wallCheck.localPosition = new Vector2(0.5f, 0.8f); 
        }
        else // Facing Left
        {
            playerCollider.offset = new Vector2(0.05f, 0.007f); 
            // wallCheck.localPosition = new Vector2(-0.5f, 0.8f);
        }
    }
    void SetFrame(int frame)
    {
        anim.SetFloat("VerticalNormal", frame / totalJumpFrames);
    }

    // Visualization for the Ground Check in the Scene View
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        // Draw Wall Check (Blue Line)
    float direction = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
    Vector3 wallOrigin = transform.position + new Vector3(0, 0.5f, 0);
    
    Gizmos.color = isTouchingWall ? Color.green : Color.blue;
    Gizmos.DrawLine(wallOrigin, wallOrigin + (Vector3.right * direction * wallCheckDistance));
    }

    public void TriggerJumpTest()
    {
        jumpBufferCounter = jumpBufferTime;
    }

    void OnStartRewind()
    {
        _isRewinding = true;
    }

    void OnStopRewind()
    {
        _isRewinding = false;
    }
}



