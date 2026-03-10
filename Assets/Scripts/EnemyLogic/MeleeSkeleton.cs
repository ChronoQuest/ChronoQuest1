using UnityEngine;
using System.Collections;
using TimeRewind;

public class MeleeSkeleton : EnemyBase
{
    [Header("Stats")]
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public float moveSpeed = 2.5f;
    public float attackCooldown = 1.2f;
    public int damage = 1;

    [Header("Attack Hitbox")]
    public float hitboxRadius = 0.6f;
    public float hitboxOffset = 0.8f;

    public Transform player;

    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float lastAttackTime;
    private bool isAttacking;

    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;
    private bool isHitStunned;

    protected override void Awake()
    {
        health = 3;
        knockbackResistance = 3f;
        base.Awake();
    }

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        if (isRewinding || wasDead) return;
        if (isAttacking || isHitStunned) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist <= attackRange && Time.time >= lastAttackTime + attackCooldown)
            currentState = State.Attack;
        else if (dist < detectionRange)
            currentState = State.Chase;
        else
            currentState = State.Idle;

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                animator.SetBool("isRunning", false);
                break;

            case State.Chase:
                Vector2 dir = (player.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * moveSpeed, rb.linearVelocity.y);
                spriteRenderer.flipX = dir.x < 0;
                animator.SetBool("isRunning", true);
                break;

            case State.Attack:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                animator.SetBool("isRunning", false);
                StartCoroutine(AttackRoutine());
                break;
        }
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        animator.SetTrigger("Attack");
        // Wait long enough for the animation to finish before allowing another attack
        yield return new WaitForSeconds(attackCooldown * 0.9f);
        isAttacking = false;
    }

    public override void TakeDamage(int amount)
    {
        if (wasDead) return;
        animator.SetTrigger("Hit");
        StartCoroutine(HitStunRoutine());
        base.TakeDamage(amount);
    }

    IEnumerator HitStunRoutine()
    {
        isHitStunned = true;
        yield return new WaitForSeconds(0.2f);
        isHitStunned = false;
    }

    // Called by Animation Event on the attack clip at the swing frame
    public void MeleeHit()
    {
        if (wasDead || isRewinding || player == null) return;

        float dir = spriteRenderer.flipX ? -1f : 1f;
        Vector2 hitPos = (Vector2)transform.position + new Vector2(dir * hitboxOffset, 0);

        if (Vector2.Distance(hitPos, player.position) <= hitboxRadius)
        {
            player.GetComponent<PlayerHealth>()?.ModifyHealth(-damage);
        }
    }

    public override void Die()
    {
        isAttacking = false;
        animator?.SetTrigger("Die");
        base.Die();          // handles wasDead, Kinematic, zero velocity, collider, DeathRoutine
        StopAllCoroutines(); // cancel DeathRoutine so bones stay visible (same as SkeletonArcher)
    }

    // ================= REVIVE =================

    [Header("Revive")]
    public float reviveAnimDuration = 0.9f;

    public override void Revive()
    {
        base.Revive();
        isAttacking = false;
        isHitStunned = false;
        rb.gravityScale = 1f; // Die() sets this to 0
        spriteRenderer.enabled = true;
        animator?.SetTrigger("Revive");
        StartCoroutine(ReviveStunRoutine());
    }

    IEnumerator ReviveStunRoutine()
    {
        isHitStunned = true;
        yield return new WaitForSeconds(reviveAnimDuration);
        isHitStunned = false;
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
        state.SetCustomData("isAttacking", isAttacking);
        state.SetCustomData("spriteEnabled", spriteRenderer != null && spriteRenderer.enabled);

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
        isAttacking = state.GetCustomData<bool>("isAttacking");

        if (spriteRenderer != null)
            spriteRenderer.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }

    void OnDrawGizmosSelected()
    {
        float dir = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + new Vector2(dir * hitboxOffset, 0), hitboxRadius);
    }
}
