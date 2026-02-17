using System.Collections;
using UnityEngine;
using TimeRewind;

<<<<<<< feature/combat-logic
public class SkeletonArcher : EnemyBase
{
=======
/// <summary>
/// Skeleton Archer enemy. Keeps distance from the player and fires arrows.
///
/// Behaviour:
///   - Idle    : Player not detected (outside detectionRange)
///   - Retreat : Player is too close (inside safeDistance) — moves away
///   - Shoot   : Player in shootRange and outside safeDistance — fires an arrow
///   - Hit     : Brief stun after taking damage
///   - Dead    : Death animation then destroy
///
/// Animator Parameters required:
///   Speed  (float)   — 0 = idle/shoot, >0 = retreating
///   Shoot  (trigger) — plays the shooting animation
///   Hit    (trigger) — plays the hit reaction
///   Dead   (trigger) — plays the death animation
/// </summary>
public class SkeletonArcher : MonoBehaviour, IRewindable
{
    [Header("Stats")]
    public int health = 3;
    public int damage = 1;

>>>>>>> dev
    [Header("Detection")]
    public float detectionRange = 8f;
    public float shootRange = 6f;
    public float safeDistance = 3f;

    [Header("Movement")]
    public float retreatSpeed = 2f;

    [Header("Shooting")]
<<<<<<< feature/combat-logic
    public int damage = 1;
    public float shootCooldown = 2f;
=======
    public float shootCooldown = 2f;
    public float hitStunDuration = 0.3f;
>>>>>>> dev

    [Header("References")]
    public Transform player;
    public GameObject arrowPrefab;

    [Header("Arrow Pool")]
    public int arrowPoolSize = 5;

    [Header("Arrow Spawn")]
<<<<<<< feature/combat-logic
    public Vector2 arrowSpawnOffset = new Vector2(0.3f, 0.5f);

    private Animator animator;
    private Collider2D col;

=======
    [Tooltip("Offset from the archer's transform where arrows spawn. Tweak X (forward) and Y (height) to match the sprite.")]
    public Vector2 arrowSpawnOffset = new Vector2(0.3f, 0.5f);

    // --- Components ---
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;

    // --- State ---
>>>>>>> dev
    private enum State { Idle, Retreat, Shoot, Hit, Dead }
    [SerializeField] private State currentState = State.Idle;

    private float lastShootTime;
<<<<<<< feature/combat-logic
    private bool isDead;
    private Vector3 originalScale;
    private Vector2 pendingArrowDirection;

    private ArrowProjectile[] arrowPool;
    private RewindState? _lastAppliedState;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
=======
    private bool isHitStunned;
    private bool isDead;
    private Vector3 originalScale;
    private Vector2 pendingArrowDirection; // Direction locked in when TryShoot fires, used by FireArrow()

    // --- Ground detection (same pattern as SlimeEnemy) ---
    private bool isGrounded;
    private int groundContacts;

    // --- Arrow Pool ---
    private ArrowProjectile[] arrowPool;

    // --- Rewind ---
    private bool isRewinding;
    private RigidbodyType2D originalBodyType;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
>>>>>>> dev
        col = GetComponent<Collider2D>();
        originalScale = transform.localScale;

        BuildArrowPool();
    }

<<<<<<< feature/combat-logic
=======
    void OnEnable()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Unregister(this);
    }

>>>>>>> dev
    void BuildArrowPool()
    {
        if (arrowPrefab == null) return;

        arrowPool = new ArrowProjectile[arrowPoolSize];
        for (int i = 0; i < arrowPoolSize; i++)
        {
            GameObject obj = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
            arrowPool[i] = obj.GetComponent<ArrowProjectile>();
<<<<<<< feature/combat-logic
            obj.SetActive(false);
=======
            obj.SetActive(false); // Awake has already run and registered with the manager
>>>>>>> dev
        }
    }

    void Update()
    {
<<<<<<< feature/combat-logic
        if (isRewinding || isDead || isStunned) return;
=======
        if (isRewinding || isDead) return;

>>>>>>> dev
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

<<<<<<< feature/combat-logic
        if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist < safeDistance)
            currentState = State.Retreat;
        else if (dist <= shootRange)
            currentState = State.Shoot;
        else
            currentState = State.Idle;

        if (animator != null)
            animator.SetFloat("Speed", currentState == State.Retreat ? 1f : 0f);
=======
        // Determine state
        if (isHitStunned)
        {
            currentState = State.Hit;
        }
        else if (dist > detectionRange)
        {
            currentState = State.Idle;
        }
        else if (dist < safeDistance)
        {
            currentState = State.Retreat;
        }
        else if (dist <= shootRange)
        {
            currentState = State.Shoot;
        }
        else
        {
            currentState = State.Idle;
        }

        // Update animator speed parameter
        animator.SetFloat("Speed", currentState == State.Retreat ? 1f : 0f);
>>>>>>> dev
    }

    void FixedUpdate()
    {
<<<<<<< feature/combat-logic
        if (isRewinding || isDead || isStunned) return;
=======
        if (isRewinding || isDead) return;
>>>>>>> dev

        switch (currentState)
        {
            case State.Idle:
<<<<<<< feature/combat-logic
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;
            case State.Retreat:
                Retreat();
                break;
=======
            case State.Hit:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;

            case State.Retreat:
                Retreat();
                break;

>>>>>>> dev
            case State.Shoot:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                TryShoot();
                break;
        }
    }

    void Retreat()
    {
        if (player == null) return;
<<<<<<< feature/combat-logic
        Vector2 dir = ((Vector2)transform.position - (Vector2)player.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * retreatSpeed, rb.linearVelocity.y);
=======

        // Move away from the player
        Vector2 dir = ((Vector2)transform.position - (Vector2)player.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * retreatSpeed, rb.linearVelocity.y);

        // Face away from player (flip toward retreat direction)
>>>>>>> dev
        FaceDirection(dir.x);
    }

    void TryShoot()
    {
        if (Time.time < lastShootTime + shootCooldown) return;

        pendingArrowDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        FaceDirection(pendingArrowDirection.x);

        lastShootTime = Time.time;
        if (animator != null) animator.SetTrigger("Shoot");
    }

    /// <summary>
<<<<<<< feature/combat-logic
    /// Called by Animation Event on the attack clip at the arrow-release frame.
=======
    /// Called by an Animation Event on the Attack1 clip at the arrow-release frame.
    /// Add an event in the Unity Animation window at the frame the bow is fully drawn.
>>>>>>> dev
    /// </summary>
    public void FireArrow()
    {
        if (isDead || isRewinding) return;

        ArrowProjectile arrow = GetPooledArrow();
        if (arrow == null) return;

<<<<<<< feature/combat-logic
=======
        // Spawn at the archer's position + configurable offset (height + forward)
>>>>>>> dev
        Vector3 spawnPos = transform.position
            + Vector3.up * arrowSpawnOffset.y
            + (Vector3)(pendingArrowDirection * arrowSpawnOffset.x);

        arrow.transform.position = spawnPos;
        arrow.transform.rotation = Quaternion.identity;
        arrow.gameObject.SetActive(true);
        arrow.Launch(pendingArrowDirection, damage);
    }

    ArrowProjectile GetPooledArrow()
    {
        if (arrowPool == null) return null;
        foreach (var arrow in arrowPool)
        {
            if (arrow != null && !arrow.gameObject.activeSelf)
                return arrow;
        }
<<<<<<< feature/combat-logic
        return null;
    }

=======
        return null; // All arrows in flight — skip this shot
    }

    /// <summary>Flips sprite scale so the archer faces the given horizontal direction.</summary>
>>>>>>> dev
    void FaceDirection(float dirX)
    {
        if (dirX > 0)
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        else if (dirX < 0)
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

<<<<<<< feature/combat-logic
    // ================= DEATH =================

    protected override void Die()
    {
        isDead = true;
        StopAllCoroutines();

        if (animator != null) animator.SetTrigger("Dead");
=======
    // --- Ground Detection (same pattern as SlimeEnemy) ---

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.7f)
            {
                groundContacts++;
                isGrounded = true;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.7f)
                groundContacts--;
        }

        if (groundContacts <= 0)
        {
            isGrounded = false;
            groundContacts = 0;
        }
    }

    // --- Damage & Death ---

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        health -= amount;

        if (health <= 0)
        {
            Die();
        }
        else
        {
            animator.SetTrigger("Hit");
            StartCoroutine(HitStunRoutine());
        }
    }

    IEnumerator HitStunRoutine()
    {
        isHitStunned = true;
        yield return new WaitForSeconds(hitStunDuration);
        isHitStunned = false;
    }

    void Die()
    {
        isDead = true;
        StopAllCoroutines();
        isHitStunned = false;

        animator.SetTrigger("Dead");
>>>>>>> dev
        rb.linearVelocity = Vector2.zero;

        if (col != null) col.enabled = false;
        enabled = false;

        Destroy(gameObject, 1.5f);
    }

<<<<<<< feature/combat-logic
    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();

=======
    // --- IRewindable Implementation ---

    public void OnStartRewind()
    {
        isRewinding = true;
        StopAllCoroutines();
        isHitStunned = false;

        originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        // Revive if currently dead
>>>>>>> dev
        if (isDead)
        {
            isDead = false;
            enabled = true;
            if (col != null) col.enabled = true;
        }
    }

<<<<<<< feature/combat-logic
    public override void OnStopRewind()
    {
        base.OnStopRewind();
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();

        state.SetCustomData("EnemyState", (int)currentState);
        state.SetCustomData("FacingDirection", transform.localScale);
        state.SetCustomData("isDead", isDead);

        if (animator != null)
        {
            AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animInfo.shortNameHash;
            state.AnimatorNormalizedTime = animInfo.normalizedTime;
        }
=======
    public void OnStopRewind()
    {
        isRewinding = false;
        rb.bodyType = originalBodyType;
    }

    public RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            rb.linearVelocity,
            rb.angularVelocity,
            Time.time
        );

        state.Health = health;
        state.SetCustomData("EnemyState", (int)currentState);
        state.SetCustomData("FacingDirection", transform.localScale);
        state.SetCustomData("isGrounded", isGrounded);
        state.SetCustomData("isDead", isDead);

        AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
        state.AnimatorStateHash = animInfo.fullPathHash;
        state.AnimatorNormalizedTime = animInfo.normalizedTime;
>>>>>>> dev

        return state;
    }

<<<<<<< feature/combat-logic
    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);
        _lastAppliedState = state;

        currentState = (State)state.GetCustomData<int>("EnemyState", (int)State.Idle);
        transform.localScale = state.GetCustomData<Vector3>("FacingDirection", originalScale);
=======
    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        health = state.Health;

        currentState = (State)state.GetCustomData<int>("EnemyState");
        transform.localScale = state.GetCustomData<Vector3>("FacingDirection", originalScale);
        isGrounded = state.GetCustomData<bool>("isGrounded");
>>>>>>> dev

        bool wasDead = state.GetCustomData<bool>("isDead");
        if (wasDead != isDead)
        {
            isDead = wasDead;
            enabled = !isDead;
            if (col != null) col.enabled = !isDead;
        }

<<<<<<< feature/combat-logic
        if (animator != null)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
=======
        animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
        animator.Update(0f);
>>>>>>> dev
    }
}
