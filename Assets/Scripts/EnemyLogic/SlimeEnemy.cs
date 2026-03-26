using UnityEngine;
using System.Collections;
using TimeRewind;

/// <summary>
/// Controls the Slime Enemy behavior, including:
/// 1. Physics-based jumping with manual animation frame control.
/// 2. Player detection and damage logic.
/// 3. Integration with the TimeRewind system.
/// </summary>
public class SlimeEnemy : EnemyBase, IBossSpawnable, IForesightEnemy
{
    [Header("Stats")]
    public float detectionRange = 5f;
    public float loseRange = 15f;
    public float moveSpeed = 2f;
    public float attackCooldown = 1.5f;
    public int damage = 1;

    [Header("Hop Settings")]
    public float hopForce = 3f;
    public float hopCooldown = 1f;
    public float animationSpeed = 0.0833f; // Speed for start/end frames

    [Header("Debug")]
    public float currentVelocityY; // Visible in Inspector to debug falling speed
    public int currentFrameIndex; // Shows which animation frame (0-8) is currently active
    public string currentStateLabel;

    [SerializeField] private Transform _player;

    public Transform player
    {
        get => _player;
        set => _player = value;
    }
    public void DoubleDetectionRange()
    {
        detectionRange *= 2f;
    }

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
    private float liftoffTime = -1f; // Timestamp of last launch, used to guard OnCollisionStay2D
    private Collider2D playerCollider;
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;
    private bool hasForesight = false;
    [Header("Foresight")]
    public float dodgeTriggerDistance = 3.4f;
    public GameObject foresightGlow;
    private bool isDodging = false;    
    private ForesightSystem foresightSystem;
    private float rewindStartTime;
    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;
    private bool wasStunnedLastFrame = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = player.GetComponent<Collider2D>();
        playerCombat = player.GetComponent<PlayerCombat>();
        playerSpells = player.GetComponent<PlayerSpellSystem>();
        foresightSystem = GetComponent<ForesightSystem>();
    }

    protected override void Awake()
    {
        health = 10; // Slime unique HP
        base.Awake();
        stunOnLand = true;
    }


    public override void Update()
    {
        base.Update();
        bool justGotStunned = isStunned && !wasStunnedLastFrame;
        wasStunnedLastFrame = isStunned;
        if (justGotStunned)
        {
            animator.Play("Idle", 0, 0f); // snap to start of idle
            SetFrame(0);
        }
        // 1. Pause logic if rewinding time or dead
        if (isRewinding || wasDead || isLaunched) return;
        animator.speed = 1f;

        if (isStunned)
        {
            rb.linearVelocity = new Vector2(
                Mathf.Lerp(rb.linearVelocity.x, 0f, Time.deltaTime * 2f),
                rb.linearVelocity.y
            );

            animator.speed = isGrounded ? 0f : 1f;

            if (isGrounded)
            {
                SetFrame(0);
            }

            return;
        }

        // 2. Update Debug values
        currentVelocityY = rb.linearVelocity.y;

        // 3. Jump Guard: If the Jump Coroutine is running, stop here.
        // The coroutine handles movement/animation while airborne.
        if (isMidJumpSequence) return;
        if (isGrounded)
        {
        float friction = isMidJumpSequence ? 3f : 20f;
        rb.linearVelocity = new Vector2(Mathf.Lerp(rb.linearVelocity.x, 0f, Time.deltaTime * 3f), rb.linearVelocity.y);
        }
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
        StartCoroutine(JumpRoutine(direction.x, false, 1f, 1f));
        lastHopTime = Time.time;
        }
    }

    /// <summary>
    /// Manually controls the jump sequence.
    /// Instead of letting the Animator play automatically, we dictate specific frames
    /// based on physics velocity and timing.
    /// </summary>
    IEnumerator JumpRoutine(float xDir, bool dodge, float heightMultiplier = 1f, float distanceMultiplier = 1f)
    {
        Vector2 direction = (player.position - transform.position).normalized;
        if (direction.x > 0) spriteRenderer.flipX = false;
        else if (direction.x < 0) spriteRenderer.flipX = true;
        isMidJumpSequence = true; // Take control away from Update()
        currentStateLabel = "Anticipation";

        // Phase 1: Anticipation (Frames 0-2)
        // Play "Squash/Prepare" frames while still on the ground
        float timer = 0f;
        float phaseDuration = animationSpeed * 3f; // 3 frames total

        while (timer < phaseDuration)
        {
            if (timer < animationSpeed) SetFrame(0);
            else if (timer < animationSpeed * 2f) SetFrame(1);
            else SetFrame(2);
            
            timer += Time.deltaTime;
            yield return null; // Wait exactly 1 frame, then check again
        }

        // Phase 2: Launch
        int originalLayer = gameObject.layer;
        Color originalColor = spriteRenderer.color;
        if(dodge) {
            gameObject.layer = LayerMask.NameToLayer("EnemyDodging");
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
        }
        currentStateLabel = "Launching";
        animator.SetTrigger("hop");
        rb.linearVelocity = new Vector2(xDir * moveSpeed * distanceMultiplier, hopForce * heightMultiplier);
        isGrounded = false;
        groundContacts = 0;
        liftoffTime = Time.time;

        yield return new WaitForFixedUpdate(); 

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
            if (vy > 1.0f) SetFrame(3); // Rising Fast
            else if (vy > -1.0f) SetFrame(4); // Peak / Hover (Zero G)
            else if (vy > -3.0f) SetFrame(5); // Falling
            else SetFrame(5); // Falling Fast (Clamped to frame 5)

            yield return null; // Wait for next frame

        } 

        // Phase 4: Landing (Frames 6-8)
        currentStateLabel = "Landing";
        if (dodge)
        {
            gameObject.layer = originalLayer;
            spriteRenderer.color = originalColor;
        }

        rb.linearVelocity = Vector2.zero;
        currentVelocityY = 0f;
        isGrounded = true;

        // Reset timer for the landing phase
        timer = 0f; 

        while (timer < phaseDuration)
        {
            if (timer < animationSpeed) SetFrame(6);
            else if (timer < animationSpeed * 2f) SetFrame(7);
            else SetFrame(8);
            
            timer += Time.deltaTime;
            yield return null;
        }

        isMidJumpSequence = false;
        currentStateLabel = "Idle";
    }

    // --- COLLISION LOGIC ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 1. Hit Player: Attack immediately
        if (!isMidJumpSequence && !collision.gameObject.CompareTag("Player"))
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("touched player");
            playerInContact = true;
            Attack();
            return; // EXIT: Do not count Player body as "Ground"
        }

        // 2. Hit Environment: Check if it's a floor
        foreach(ContactPoint2D contact in collision.contacts) {
            // Only surfaces pointing UP (> 0.7f normal) count as ground.
            // This ignores walls and steep slopes.
            if(contact.normal.y > 0.7f) {

                if (isLaunched && stunOnLand)
                {
                    stunTimer = 0.5f;
                    isLaunched = false;

                    animator.Rebind();
                    animator.Update(0f);
                    SetFrame(0);
                    currentState = State.Idle; 
                }

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
    void OnCollisionStay2D(Collision2D collision)
    {
    if (collision.gameObject.CompareTag("Player")) return;

    // Ignore the first 0.2s after liftoff so we don't re-detect the floor we just left
    if (Time.time - liftoffTime < 0.2f) return;

    foreach (ContactPoint2D contact in collision.contacts)
    {
    if (contact.normal.y > 0.7f)
    {
    isGrounded = true;
    }
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
        //StopAllCoroutines();
        //isMidJumpSequence = false;

        if (isMidJumpSequence)
        {
            StopAllCoroutines();
            isMidJumpSequence = false;
        }

        //Vector2 knockbackDir = force.normalized;
        //float knockbackSpeed = 7f; 
        //float upwardPop = 2f;
        //rb.linearVelocity = new Vector2(knockbackDir.x * knockbackSpeed, upwardPop);
        base.ApplyKnockback(force);
        SetFrame(0); 
        currentStateLabel = "Launched";
    }

    public override void Die()
    {
        wasDead = true;
        // 1. Critical: Stop any active jump coroutine immediately
        StopAllCoroutines();
        isMidJumpSequence = false;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) 
        {
            PlayerMana pm = p.GetComponent<PlayerMana>();
            if (pm != null) pm.ModifyMana(15f); // Reward 15 mana
        }
        // 2. Play Death Animation
        animator.SetTrigger("die");

        // 3. Physics Cleanup: Stop X movement but allow gravity (falling death)
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

        // 4. Disable collider and hide sprite so enemy disappears
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        StartCoroutine(base.DeathRoutine());

        // Do not disable script or Destroy - stay registered so rewind can restore us
    }
    public override void TakeDamage(int amount)
    {
        base.TakeDamage(amount);
        if (foresightSystem != null) foresightSystem.NotifyDamage();
    }

    // ---------------------- IForesightEnemy Implementation ----------------------
    public int GetPlayerAttackState()
    {
        if (playerCombat != null && playerCombat.isAttacking) return 1;
        if (playerSpells != null && playerSpells.isCasting) return 2;
        return 0;
    }
    public void SetForesightState(bool state)
    {
        hasForesight = state;
        if(hasForesight) transform.localScale = new Vector3(0.66f, 0.64f, 0f);
        else transform.localScale = new Vector3(0.74f, 0.64f, 0f);
        animator.SetBool("hasForesight", hasForesight);
        if(foresightGlow != null) foresightGlow.SetActive(hasForesight);
        Vector2 direction = (player.position - transform.position).normalized;
        if (direction.x > 0) spriteRenderer.flipX = false;
        else if (direction.x < 0) spriteRenderer.flipX = true;
    }
    new public bool IsDead() => wasDead;
    public bool IsRewinding() => isRewinding;
    public void ExecuteLunge()
    {
        if (isDodging || !isGrounded || isMidJumpSequence) return;

        animator.SetBool("hasForesight", true);
        foresightGlow.SetActive(true);

        Vector2 approachDirection = (playerCollider.bounds.center - transform.position).normalized;

        float xDir = Mathf.Sign(approachDirection.x);

        StopAllCoroutines();
        StartCoroutine(JumpRoutine(xDir, false, 2f, 2f));
    }
    public void ExecuteDodge()
    {
        if (isMidJumpSequence) return; // slime cannot dodge mid-jump

        animator.SetBool("hasForesight", true);
        foresightGlow.SetActive(true);

        GameObject spellObj = playerSpells.latestSpell;

        if (Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance)
        {
            if (GetPlayerAttackState() != 0) 
            {
                Vector2 attackDirection = (playerCollider.bounds.center - transform.position).normalized;
                TriggerForesightDodge(attackDirection);
                return;
            }
        }

        if (spellObj != null)
        {
            SpriteRenderer spellSprite = spellObj.GetComponent<SpriteRenderer>();
            if (spellSprite != null && spellSprite.enabled) 
            {
                Collider2D spellCol = spellObj.GetComponent<Collider2D>();
                if (spellCol != null)
                {
                    Vector2 spellPos = spellCol.bounds.center;
                    if (Vector2.Distance(transform.position, spellPos) < dodgeTriggerDistance + 1.5f)
                    {
                        Rigidbody2D spellRb = spellObj.GetComponent<Rigidbody2D>();
                        Vector2 attackDirection = (spellRb.position - (Vector2)transform.position).normalized;
                        TriggerForesightDodge(attackDirection);
                    }
                }
            }
        }
    }
    public float GetDistanceToPlayer()
    {
        return Vector2.Distance(transform.position, playerCollider.bounds.center);
    }
    void TriggerForesightDodge(Vector2 attackDirection)
    {
        Vector2 dir1 = new Vector2(-attackDirection.y, attackDirection.x).normalized;
        Vector2 dir2 = -dir1;

        float dist1 = Physics2D.Raycast(transform.position, dir1, 3f).distance;
        float dist2 = Physics2D.Raycast(transform.position, dir2, 3f).distance;

        Vector2 dodgeDir = dist1 > dist2 ? dir1 : dir2;

        float xDir = Mathf.Sign(dodgeDir.x);
        
        // Slime must dodge away from the player
        float playerSide = Mathf.Sign(player.position.x - transform.position.x);
        if (xDir == playerSide)
            xDir *= -1f;

        StopAllCoroutines();
        StartCoroutine(JumpRoutine(xDir, true, 1.3f, 1.5f));
    }
    public bool IsPerformingForesightAction()
    {
        return isDodging;
    }

    // --- REWIND INTERFACE IMPLEMENTATION ---
    // Handles saving and restoring state for the TimeRewind system.

    public override void OnStartRewind()
    {
    base.OnStartRewind(); // IMPORTANT

    StopAllCoroutines();
    isMidJumpSequence = false;
    CancelInvoke();
    rewindStartTime = Time.time;

    // Revive logic
    if (!enabled)
    {
    enabled = true;
    wasDead = false;
    GetComponent<Collider2D>().enabled = true;
    if (spriteRenderer != null) spriteRenderer.enabled = true;
    }
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind(); // IMPORTANT
        isMidJumpSequence = false;
        if (foresightSystem != null)
        {
            // Calculate how much time passed in the real world while we were rewinding
            float timeRewound = rewindStartTime - TimeRewindManager.Instance.CurrentRewindTime;
            int statesErased = Mathf.RoundToInt(timeRewound / foresightSystem.recordInterval);
            foresightSystem.HandleRewindStop(statesErased);
        }
    }

    public override RewindState CaptureState()
    {
    var state = base.CaptureState();
    state.SetCustomData("isGrounded", isGrounded);
    state.SetCustomData("midJump", isMidJumpSequence);
    state.SetCustomData("frameIndex", currentFrameIndex);
    var animState = animator.GetCurrentAnimatorStateInfo(0);
    state.AnimatorStateHash = animState.fullPathHash;
    state.AnimatorNormalizedTime = animState.normalizedTime;
    return state;
    }

    public override void ApplyState(RewindState state)
    {
    base.ApplyState(state); // Restores position, rotation, velocity, health, flipX, and revives when state.Health > 0

    // Restore Slime-specific logic
    spriteRenderer.flipX = state.GetCustomData<bool>("flipX");
    isGrounded = state.GetCustomData<bool>("isGrounded");
    isMidJumpSequence = state.GetCustomData<bool>("midJump");

    int frameIndex = state.GetCustomData<int>("frameIndex");
    SetFrame(frameIndex);

    animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    animator.Update(0f);

    if (state.Health > 0)
    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }
}