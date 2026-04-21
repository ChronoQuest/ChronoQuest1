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

    [Header("References")]
    public Transform player;

    private Animator animator;

    private float lastAttackTime = -99f;
    private float lastRangedAttackTime = -99f;
    private bool isAttacking;

    private DeathKnightOrb[] orbPool;
    private Vector2 pendingOrbDirection;

    private enum State { Idle, Chase, Attack, RangedAttack }
    private State currentState = State.Idle;

    protected override void Awake()
    {
        knockbackResistance = 4f;
        deathAnimationDuration = 1.1f;
        base.Awake();
    }

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
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

    public override void Update()
    {
        base.Update();
        if (isRewinding || wasDead) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (!isAttacking)
        {
            if (dist > detectionRange)
                currentState = State.Idle;
            else if (dist <= attackRange && CanAttack())
            {
                currentState = State.Attack;
                StartCoroutine(AttackRoutine());
            }
            else if (dist > rangedMinRange && dist <= rangedAttackRange && CanRangedAttack())
            {
                currentState = State.RangedAttack;
                StartCoroutine(RangedAttackRoutine());
            }
            else
                currentState = State.Chase;
        }

        if (dist <= detectionRange)
            FacePlayer();

        animator?.SetBool("isRunning", currentState == State.Chase);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isStunned || isAttacking) return;

        switch (currentState)
        {
            case State.Chase:
                Vector2 dir = (player.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * moveSpeed, rb.linearVelocity.y);
                break;

            default:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
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
            player.GetComponent<PlayerHealth>()?.ModifyHealth(-attackDamage);
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
        base.TakeDamage(amount);
        if (!wasDead)
            animator?.SetTrigger("Hit");
    }

    public override void Die()
    {
        isAttacking = false;
        StopAllCoroutines();
        animator?.SetTrigger("Die");
        base.Die();
        rb.simulated = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        Collider2D col = GetComponent<Collider2D>();
        col.enabled = true;
        if (player != null)
            foreach (var pc in player.GetComponents<Collider2D>())
                Physics2D.IgnoreCollision(col, pc, true);
    }

    public override void Revive()
    {
        base.Revive();
        rb.simulated = true;
    }

    public override IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathAnimationDuration);
        // Intentionally leave sprite visible — body stays as a corpse
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
        state.SetCustomData("lastAttackTime",       lastAttackTime);
        state.SetCustomData("lastRangedAttackTime", lastRangedAttackTime);
        state.SetCustomData("spriteEnabled",        sprite != null && sprite.enabled);

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
        isAttacking          = false;
        lastAttackTime       = state.GetCustomData<float>("lastAttackTime");
        lastRangedAttackTime = state.GetCustomData<float>("lastRangedAttackTime");

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);

        if (justBecameAlive)
        {
            rb.simulated = true;
            if (player != null)
            {
                Collider2D col = GetComponent<Collider2D>();
                foreach (var pc in player.GetComponents<Collider2D>())
                    Physics2D.IgnoreCollision(col, pc, false);
            }
        }
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
