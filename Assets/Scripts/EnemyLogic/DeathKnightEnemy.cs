using UnityEngine;
using System.Collections;
using TimeRewind;

public class DeathKnightEnemy : EnemyBase
{
    [Header("Stats")]
    public float detectionRange = 8f;
    public float moveSpeed = 3f;

    [Header("Melee Attack")]
    public float attackRange = 1.3f;
    public float attackCooldown = 1.4f;
    public float attackAnimDuration = 0.85f; // match DeathKnightAttack clip length
    public int attackDamage = 1;
    public float hitboxRadius = 0.7f;
    public float hitboxOffset = 0.9f;

    [Header("Ranged Attack")]
    public float rangedAttackRange = 7f;
    public float rangedMinRange = 3f;   // won't shoot if player is closer than this
    public float rangedAttackCooldown = 4f;
    public float rangedAttackAnimDuration = 0.9f; // match DeathKnightRangedAttack clip length
    public int rangedAttackDamage = 1;
    public GameObject orbPrefab;
    public int orbPoolSize = 3;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("References")]
    public Transform player;

    private Animator animator;
    private bool isGrounded;
    private bool wasGrounded;

    private float lastAttackTime = -99f;
    private float lastRangedAttackTime = -99f;
    private bool isAttacking;

    private DeathKnightOrb[] orbPool;
    private Vector2 pendingOrbDirection;

    private enum State { Idle, Chase, Attack, RangedAttack }
    private State currentState = State.Idle;

    protected override void Awake()
    {
        health = 6;
        knockbackResistance = 4f;
        deathAnimationDuration = 1.1f;
        base.Awake();
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        BuildOrbPool();
    }

    void BuildOrbPool()
    {
        if (orbPrefab == null) return;
        orbPool = new DeathKnightOrb[orbPoolSize];
        for (int i = 0; i < orbPoolSize; i++)
        {
            GameObject obj = Instantiate(orbPrefab, transform.position, Quaternion.identity);
            orbPool[i] = obj.GetComponent<DeathKnightOrb>();
            obj.SetActive(false);
        }
    }

    DeathKnightOrb GetPooledOrb()
    {
        if (orbPool == null) return null;
        foreach (var o in orbPool)
            if (o != null && !o.gameObject.activeSelf) return o;
        return null;
    }

    void Update()
    {
        UpdateGroundedState();

        if (isRewinding || wasDead || isAttacking) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist <= attackRange && CanAttack())
            currentState = State.Attack;
        else if (dist > rangedMinRange && dist <= rangedAttackRange && CanRangedAttack())
            currentState = State.RangedAttack;
        else
            currentState = State.Chase;

        if (dist <= detectionRange)
            FacePlayer();

        animator?.SetBool("isRunning", currentState == State.Chase);
        UpdateAirborneAnimation();
    }

    void UpdateGroundedState()
    {
        if (groundCheck == null) return;
        bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        isGrounded  = grounded;
        wasGrounded = grounded;
        animator?.SetBool("isGrounded", isGrounded);
    }

    void UpdateAirborneAnimation()
    {
        if (isGrounded) return;
        animator?.SetBool("isFalling", rb.linearVelocity.y < -0.1f);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isStunned || isAttacking) return;

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;

            case State.Chase:
                Vector2 dir = (player.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * moveSpeed, rb.linearVelocity.y);
                break;

            case State.Attack:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                StartCoroutine(AttackRoutine());
                break;

            case State.RangedAttack:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                StartCoroutine(RangedAttackRoutine());
                break;
        }
    }

    bool CanAttack()       => !isAttacking && Time.time >= lastAttackTime       + attackCooldown;
    bool CanRangedAttack() => !isAttacking && Time.time >= lastRangedAttackTime + rangedAttackCooldown;

    // ─── Melee ───────────────────────────────────────────────────────────────

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        animator?.SetTrigger("Attack");
        // MeleeHit() called by Animation Event at the swing frame
        yield return new WaitForSeconds(attackAnimDuration);
        isAttacking = false;
    }

    // Called by Animation Event on the attack clip
    public void MeleeHit()
    {
        if (wasDead || isRewinding || player == null) return;
        float dir = sprite.flipX ? -1f : 1f;
        Vector2 hitPos = (Vector2)transform.position + new Vector2(dir * hitboxOffset, 0);
        if (Vector2.Distance(hitPos, player.position) <= hitboxRadius)
            Vector2 kbDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
            player.GetComponent<PlayerHealth>()?.ModifyHealth(-attackDamage, kbDir);
    }

    // ─── Ranged ──────────────────────────────────────────────────────────────

    IEnumerator RangedAttackRoutine()
    {
        isAttacking = true;
        lastRangedAttackTime = Time.time;
        pendingOrbDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        animator?.SetTrigger("RangedAttack");
        // FireOrb() called by Animation Event at the cast frame
        yield return new WaitForSeconds(rangedAttackAnimDuration);
        isAttacking = false;
    }

    // Called by Animation Event on the ranged attack clip
    public void FireOrb()
    {
        if (wasDead || isRewinding) return;
        DeathKnightOrb orb = GetPooledOrb();
        if (orb == null) return;
        orb.transform.position = transform.position;
        orb.gameObject.SetActive(true);
        orb.Launch(pendingOrbDirection, rangedAttackDamage);
    }

    // ─── Shared ──────────────────────────────────────────────────────────────

    void FacePlayer()
    {
        sprite.flipX = player.position.x < transform.position.x;
    }

    public override void TakeDamage(int amount)
    {
        if (wasDead) return;
        animator?.SetTrigger("Hit");
        base.TakeDamage(amount);
    }

    public override void Die()
    {
        isAttacking = false;
        animator?.SetTrigger("Die");
        base.Die();
        StopAllCoroutines();
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isAttacking = false;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
        isAttacking = false;
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("isAttacking",          isAttacking);
        state.SetCustomData("lastAttackTime",        lastAttackTime);
        state.SetCustomData("lastRangedAttackTime",  lastRangedAttackTime);
        state.SetCustomData("spriteEnabled",         sprite != null && sprite.enabled);

        if (animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = info.shortNameHash;
            state.AnimatorNormalizedTime = info.normalizedTime;
        }

        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);
        isAttacking          = state.GetCustomData<bool>("isAttacking");
        lastAttackTime       = state.GetCustomData<float>("lastAttackTime");
        lastRangedAttackTime = state.GetCustomData<float>("lastRangedAttackTime");

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }

    void OnDrawGizmosSelected()
    {
        float dir = (sprite != null && sprite.flipX) ? -1f : 1f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + new Vector2(dir * hitboxOffset, 0), hitboxRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
        Gizmos.DrawWireSphere(transform.position, rangedMinRange);
    }
}
