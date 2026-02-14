using UnityEngine;
using System.Collections;
using TimeRewind;

/// <summary>
/// Controls the Slime Enemy behavior, including:
/// 1. Physics-based jumping with manual animation frame control.
/// 2. Player detection and damage logic.
/// 3. Integration with the TimeRewind system.
/// </summary>
public class SlimeEnemy : EnemyBase, IRewindable
{
    [Header("Stats")]
    public float detectionRange = 5f;
    public float loseRange = 15f;
    public float moveSpeed = 2f;
    public float attackCooldown = 1.5f;
    public int damage = 1;
    public int health = 3;

    [Header("Hop Settings")]
    public float hopForce = 3f;
    public float hopCooldown = 1f;
    public float animationSpeed = 0.1f; // Speed for start/end frames

    [Header("Debug")]
    public float currentVelocityY;  // Visible in Inspector to debug falling speed
    public int currentFrameIndex;   // Shows which animation frame (0-8) is currently active
    public string currentStateLabel;
    public int degugground;

    public Transform player;

    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float lastAttackTime;
    private float lastHopTime;
    private bool playerInContact = false;
    
    // --- Ground Detection Variables ---
    private bool isGrounded = false;
    private int groundContacts = 0; // Tracks how many valid ground objects we are touching

    // Locks the Update loop during the custom Jump Coroutine so we don't interrupt the animation
    private bool isMidJumpSequence = false; 

    // --- Rewind System Variables ---
    private bool isRewinding = false;
    private RigidbodyType2D originalBodyType;
    private bool wasDead = false;

    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        // Register this enemy with the Rewind Manager when it spawns/enables
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Unregister(this);
    }

    void Update()
    {
        // 1. Pause logic if rewinding time
        if (isRewinding) return;

        // 2. Update Debug values
        currentVelocityY = rb.linearVelocity.y;
        degugground = groundContacts;

        // 3. Jump Guard: If the Jump Coroutine is running, stop here.
        // The coroutine handles movement/animation while airborne.
        if (isMidJumpSequence) return; 

        // 4. Default State (Ground Logic)
        SetFrame(0); // Default to "Sitting" frame
        animator.SetBool("isGrounded", isGrounded);

        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // 5. Determine State based on distance/contact
        if (playerInContact) currentState = State.Attack;
        else if (distanceToPlayer < detectionRange) currentState = State.Chase;
        else if (currentState == State.Chase && distanceToPlayer < loseRange) currentState = State.Chase;
        else currentState = State.Idle;
        
        // Update Animator State Machine
        animator.SetInteger("state", (int)currentState);
        
        // 6. Execute State Behavior
        if (currentState == State.Chase) Chase();
        if (currentState == State.Attack) Attack();
    }

    void Chase()
    {
        Vector2 direction = (player.position - transform.position).normalized;

        // Face the player
        if (direction.x > 0) spriteRenderer.flipX = false;
        else if (direction.x < 0) spriteRenderer.flipX = true;

        // Trigger Jump if grounded and cooldown is ready
        if (isGrounded && Time.time >= lastHopTime + hopCooldown)
        {
            StartCoroutine(JumpRoutine(direction.x));
            lastHopTime = Time.time;
        }
    }

    /// <summary>
    /// Manually controls the jump sequence. 
    /// Instead of letting the Animator play automatically, we dictate specific frames
    /// based on physics velocity and timing.
    /// </summary>
    IEnumerator JumpRoutine(float xDir)
    {
        isMidJumpSequence = true; // Take control away from Update()
        currentStateLabel = "Anticipation";

        // Phase 1: Anticipation (Frames 0-2)
        // Play "Squash/Prepare" frames while still on the ground
        SetFrame(0); yield return new WaitForSeconds(animationSpeed);
        SetFrame(1); yield return new WaitForSeconds(animationSpeed);
        SetFrame(2); yield return new WaitForSeconds(animationSpeed);

        // Phase 2: Launch
        currentStateLabel = "Launching";
        animator.SetTrigger("hop");
        
        rb.linearVelocity = new Vector2(xDir * moveSpeed, hopForce);
        isGrounded = false;
        groundContacts = 0;

        yield return new WaitForSeconds(0.1f); // Wait to ensure physical liftoff

        // Phase 3: Air Loop (Physics Driven)
        float timeAirborne = 0f; 
        float timeMotionless = 0f; // Tracks how long we are stuck on a wall/corner
        
        // Loop runs until we hit ground OR get stuck OR timeout (3s)
        while (!isGrounded && timeMotionless < 0.2f && timeAirborne < 3.0f) 
        {
            currentStateLabel = "Air (Physics)";
            
            float vy = rb.linearVelocity.y;
            currentVelocityY = vy;
            timeAirborne += Time.deltaTime;

            // --- STUCK PROTECTION ---
            // If velocity is near zero (stuck on wall), start a timer to force landing
            if (Mathf.Abs(vy) < 0.01f)
            {
                timeMotionless += Time.deltaTime; 
            }
            else
            {
                timeMotionless = 0f; // Reset if moving
            }
            // ------------------------

            // Manual Frame Selection based on Vertical Velocity
            if (vy > 1.0f)       SetFrame(3); // Rising Fast
            else if (vy > -1.0f) SetFrame(4); // Peak / Hover (Zero G)
            else if (vy > -3.0f) SetFrame(5); // Falling
            else                 SetFrame(5); // Falling Fast (Clamped to frame 5)

            yield return null; // Wait for next frame
        }

        // Phase 4: Landing (Frames 6-8)
        currentStateLabel = "Landing";
        
        // Stop sliding physics
        rb.linearVelocity = Vector2.zero; 
        currentVelocityY = 0f;
        
        // Force grounded state so Update() picks it up correctly next frame
        isGrounded = true; 
        
        SetFrame(6); yield return new WaitForSeconds(animationSpeed);
        SetFrame(7); yield return new WaitForSeconds(animationSpeed);
        SetFrame(8); yield return new WaitForSeconds(animationSpeed);

        isMidJumpSequence = false; // Return control to Update()
        currentStateLabel = "Idle";
    }

    // --- COLLISION LOGIC ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 1. Hit Player: Attack immediately
        if (collision.gameObject.CompareTag("Player")) 
        {
            playerInContact = true;
            Attack(); 
            return; // EXIT: Do not count Player body as "Ground"
        }

        // 2. Hit Environment: Check if it's a floor
        foreach(ContactPoint2D contact in collision.contacts) {
            // Only surfaces pointing UP (> 0.7f normal) count as ground.
            // This ignores walls and steep slopes.
            if(contact.normal.y > 0.7f) {
                groundContacts++;
                isGrounded = true;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) 
        {
            playerInContact = false;
            return; 
        }

        // Reduce ground contact count
        foreach(ContactPoint2D contact in collision.contacts) {
            if(contact.normal.y > 0.7f) {
                groundContacts--;
            }
        }
        
        // Only set grounded to false if NO valid ground contacts remain
        if (groundContacts <= 0) {
            isGrounded = false;
            groundContacts = 0; // Safety reset
        }
    }
    // ----------------------

    /// <summary>
    /// Helper to drive the "Motion Time" parameter in the Animator.
    /// Maps Frame Index (0-8) to a normalized float (0.0-1.0).
    /// </summary>
    void SetFrame(int frameIndex)
    {
        currentFrameIndex = frameIndex;
        // 9 Frames total means we divide by 8 to get the 0-1 range.
        float normalized = (float)frameIndex / 8f; 
        animator.SetFloat("VerticalNormal", normalized);
    }

    void Attack()
    {
        // Check cooldown logic
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null) playerHealth.ModifyHealth(-damage);
        }
    }

    public override void ApplyKnockback(Vector2 force)
    {
        if (isMidJumpSequence)
        {
            StopAllCoroutines();
            isMidJumpSequence = false;
        }

        base.ApplyKnockback(force);
    }

    protected override void Die()
    {
        wasDead = true;
        
        // 1. Critical: Stop any active jump coroutine immediately
        StopAllCoroutines(); 
        isMidJumpSequence = false;

        // 2. Play Death Animation
        animator.SetTrigger("die");
        
        // 3. Disable Script Logic
        enabled = false; 

        // 4. Physics Cleanup
        // Stop X movement but allow gravity (falling death)
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        
        // Disable collider so player can walk through corpse
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        Destroy(gameObject, 0.6f);
    }

    // --- REWIND INTERFACE IMPLEMENTATION ---
    // Handles saving and restoring state for the TimeRewind system.

    public void OnStartRewind()
    {
        isRewinding = true;
        StopAllCoroutines(); // Stop animations so they don't fight the rewind
        isMidJumpSequence = false;
        
        // Physics to Kinematic to prevent fighting the rewind positioning
        originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        
        CancelInvoke();
        
        // Revive logic if rewinding from death
        if (!enabled) { enabled = true; wasDead = false; GetComponent<Collider2D>().enabled = true; }
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        isMidJumpSequence = false;
        rb.bodyType = originalBodyType;
    }

    public RewindState CaptureState()
    {
        // Save Physics State
        var state = RewindState.CreateWithPhysics(transform.position, transform.rotation, rb.linearVelocity, rb.angularVelocity, Time.time);
        state.Health = health;
        
        // Save Custom Logic State
        state.SetCustomData("flipX", spriteRenderer.flipX);
        state.SetCustomData("isGrounded", isGrounded);
        state.SetCustomData("midJump", isMidJumpSequence);
        state.SetCustomData("frameIndex", currentFrameIndex);
        
        // Save Animation State
        var animState = animator.GetCurrentAnimatorStateInfo(0);
        state.AnimatorStateHash = animState.fullPathHash;
        state.AnimatorNormalizedTime = animState.normalizedTime;
        return state;
    }

    public void ApplyState(RewindState state)
    {
        // Restore Physics
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        health = state.Health;
        
        // Restore Logic
        spriteRenderer.flipX = state.GetCustomData<bool>("flipX");
        isGrounded = state.GetCustomData<bool>("isGrounded");
        isMidJumpSequence = state.GetCustomData<bool>("midJump");

        // Restore the animation frame
        int frameIndex = state.GetCustomData<int>("frameIndex");
        SetFrame(frameIndex);
        
        // Restore Animation
        animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
        animator.Update(0f);
    }
}