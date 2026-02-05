using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TimeRewind;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerPlatformer : MonoBehaviour
{
    // =========================================================
    // ENUMS
    // =========================================================
    private enum AnimAuthority
    {
        Base,       // Idle / Run
        Airborne,   // Falling / physics mid-air
        Jump,       // Jump start / landing
        Action      // Dash / attack / etc
    }

    private AnimAuthority animAuthority = AnimAuthority.Base;

    // =========================================================
    // MOVEMENT
    // =========================================================
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private int extraJumps = 1;

    private int extraJumpsRemaining;
    public float horizontalInput;

    // =========================================================
    // COYOTE TIME
    // =========================================================
    [Header("Leeway")]
    [SerializeField] private float coyoteTime = 0.15f;
    private float coyoteCounter;

    // =========================================================
    // COMPONENTS
    // =========================================================
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D col;

    // =========================================================
    // GROUNDING (COLLISION BASED)
    // =========================================================
    public bool isGrounded { get; private set; }
    private int groundContacts;

    // =========================================================
    // ANIMATION
    // =========================================================
    [Header("Animation")]
    [SerializeField] private float animationStepSpeed = 0.05f;
    [SerializeField] private float totalJumpFrames = 8f;

    // =========================================================
    // DASH (example override)
    // =========================================================
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private LayerMask dashPhaseLayers;
    private bool isDashing;

    // =========================================================
    // UNITY
    // =========================================================
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            return;

        horizontalInput = Keyboard.current != null
            ? (Keyboard.current.aKey.isPressed ? -1f : 0f) + (Keyboard.current.dKey.isPressed ? 1f : 0f)
            : 0f;

        UpdateCoyoteTime();
        UpdateAnimatorBase();
        UpdateAirborneAnimation();
        UpdateLanding();
        FlipSprite();
    }

    void FixedUpdate()
    {
        if (isDashing) return;
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }

    // =========================================================
    // INPUT
    // =========================================================
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (animAuthority == AnimAuthority.Action) return;

        // Ground jump
        if (isGrounded || coyoteCounter > 0f)
        {
            StartCoroutine(JumpRoutine(false));
            return;
        }

        // Double jump
        if (extraJumpsRemaining > 0)
        {
            extraJumpsRemaining--;
            StartCoroutine(JumpRoutine(true));
        }
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || isDashing) return;
        StartCoroutine(DashRoutine());
    }

    // =========================================================
    // JUMP ROUTINE (SHORT + SAFE)
    // =========================================================
    IEnumerator JumpRoutine(bool isDoubleJump)
    {
        animAuthority = AnimAuthority.Jump;
        anim.SetTrigger("Jump");

        if (!isDoubleJump)
        {
            anim.SetBool("isGrounded", true); 
            SetFrame(0); // brace
            yield return new WaitForSeconds(animationStepSpeed);
        }
        else
        {
            SetFrame(2); // mid-air start
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        isGrounded = false;
        coyoteCounter = 0f;

        yield return null;
        animAuthority = AnimAuthority.Airborne;
    }

    // =========================================================
    // AIRBORNE ANIMATION (GLOBAL)
    // =========================================================
    void UpdateAirborneAnimation()
    {
        if (animAuthority != AnimAuthority.Airborne) return;
        if (isGrounded) return;

        anim.SetBool("isGrounded", false);

        float vy = rb.linearVelocity.y;

        if (vy > 5f)            SetFrame(1);
        else if (vy > 0.1f)     SetFrame(2);
        else if (vy > -0.1f)    SetFrame(3);
        else if (vy > -5f)      SetFrame(4);
        else                    SetFrame(5);
    }

    // =========================================================
    // LANDING
    // =========================================================
    void UpdateLanding()
    {
        if (isGrounded && animAuthority == AnimAuthority.Airborne)
        {
            StartCoroutine(LandingRoutine());
        }
    }

    IEnumerator LandingRoutine()
    {
        animAuthority = AnimAuthority.Jump;

        SetFrame(6);
        yield return new WaitForSeconds(animationStepSpeed);
        SetFrame(7);
        yield return new WaitForSeconds(animationStepSpeed);

        animAuthority = AnimAuthority.Base;
        anim.SetBool("isGrounded", true);
    }

    // =========================================================
    // DASH (OVERRIDES AIR)
    // =========================================================
    IEnumerator DashRoutine()
    {
        animAuthority = AnimAuthority.Action;
        isDashing = true;

        anim.SetTrigger("Dash");
        float gravity = rb.gravityScale;
        rb.gravityScale = 0f;
        SetDashPhasing(true);

        float dir = spriteRenderer.flipX ? -1f : 1f;
        rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

        yield return new WaitForSeconds(dashDuration);
        SetDashPhasing(false);

        rb.gravityScale = gravity;
        isDashing = false;
        animAuthority = isGrounded ? AnimAuthority.Base : AnimAuthority.Airborne;
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

    // =========================================================
    // GROUNDED — FLOOR ONLY (NO CEILINGS)
    // =========================================================
    void OnCollisionEnter2D(Collision2D col)
    {
        foreach (ContactPoint2D c in col.contacts)
        {
            if (c.normal.y > 0.7f)
            {
                groundContacts++;
                isGrounded = true;
                extraJumpsRemaining = extraJumps;
            }
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        foreach (ContactPoint2D c in col.contacts)
        {
            if (c.normal.y > 0.7f)
                groundContacts--;
        }

        if (groundContacts <= 0)
        {
            groundContacts = 0;
            isGrounded = false;
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================
    void UpdateCoyoteTime()
    {
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.deltaTime;
    }

    void UpdateAnimatorBase()
    {
        if (animAuthority != AnimAuthority.Base) return;
        anim.SetFloat("Speed", Mathf.Abs(horizontalInput));
        anim.SetBool("isGrounded", isGrounded);
    }

    void SetFrame(int frame)
    {
        anim.SetFloat("VerticalNormal", frame / totalJumpFrames);
    }

    void FlipSprite()
    {
        if (horizontalInput > 0.1f) spriteRenderer.flipX = false;
        else if (horizontalInput < -0.1f) spriteRenderer.flipX = true;
    }
}


