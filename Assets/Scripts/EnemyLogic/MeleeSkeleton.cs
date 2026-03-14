using UnityEngine;
using System.Collections;
using TimeRewind;

public class MeleeSkeleton : EnemyBase, IBossSpawnable
{
    [Header("Stats")]
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public float moveSpeed = 2.5f;
    public float attackCooldown = 1.2f;
    public int damage = 1;

    [Header("Physics & Environment")]
    public LayerMask groundLayer;
    [Tooltip("How far below the skeleton's feet to look for the ground when dying. Increase this if the skeleton falls into the floor.")]
    public float groundDetectionOffset = 1.3f;

    [Header("Attack Hitbox")]
    public float hitboxRadius = 0.6f;
    public float hitboxOffset = 0.8f;

    [Header("Revive")]
    public float reviveAnimDuration = 0.9f;

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
    private Collider2D col;

    // --- REWIND SAFE TIMERS ---
    private float lastAttackTime = -99f;
    private bool isAttacking;
    private float attackTimer;

    private bool isReviving;
    private float reviveTimer;

    private bool isDying = false; 

    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        if (isRewinding) return;

        // --- TIMER UPDATES ---
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0) isAttacking = false;
        }

        if (isReviving)
        {
            reviveTimer -= Time.deltaTime;
            if (reviveTimer <= 0) isReviving = false;
        }

        // If dead, dying, locked in an attack, reviving, or stunned -> Do nothing.
        if (wasDead || isDying || isAttacking || isReviving || isStunned) return;
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
                StartAttack();
                break;
        }
    }

    void StartAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        attackTimer = attackCooldown * 0.9f;
        animator?.SetTrigger("Attack");
    }

    public override void TakeDamage(int amount)
    {
        if (wasDead || isDying) return; 
        
        animator?.SetBool("isRunning", false);
        if (health - amount > 0)
        {
            animator?.SetTrigger("Hit");
        }
        
        base.TakeDamage(amount);
    }

    public void MeleeHit()
    {
        if (wasDead || isDying || isRewinding || player == null) return;

        float dir = spriteRenderer.flipX ? -1f : 1f;
        Vector2 hitPos = (Vector2)transform.position + new Vector2(dir * hitboxOffset, 0);

        if (Vector2.Distance(hitPos, player.position) <= hitboxRadius)
        {
            player.GetComponent<PlayerHealth>()?.ModifyHealth(-damage);
        }
    }

    // ================= REFACTORED DEATH =================
    public override void Die()
    {
        if (wasDead || isDying) return;
        
        wasDead = true;
        isDying = true;
        isAttacking = false;
        animator?.SetBool("isRunning", false);
        
        if (col != null) col.enabled = false;

        StartCoroutine(HandleSkeletonDeath());
    }

    private IEnumerator HandleSkeletonDeath()
    {
        if (animator != null) animator.SetTrigger("Die");

        if (col != null)
        {
            // We add the groundDetectionOffset here to check slightly lower than the actual collider bounds.
            float checkDist = col.bounds.extents.y + groundDetectionOffset;
            
            while (!Physics2D.Raycast(transform.position, Vector2.down, checkDist, groundLayer))
            {
                yield return null;
            }
        }

        rb.linearVelocity = Vector2.zero; 
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic; 
        
        isDying = false; 
    }

    // ================= REVIVE =================
    public override void Revive()
    {
        StopAllCoroutines(); 
        
        base.Revive();
        isDying = false;
        isAttacking = false;
        
        isReviving = true;
        reviveTimer = reviveAnimDuration;
        
        rb.bodyType = originalBodyType; 
        rb.gravityScale = 1f; 
        if (col != null) col.enabled = true;
        
        spriteRenderer.enabled = true;
        animator?.SetTrigger("Revive");
    }

    // ================= REWIND =================
    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines(); 
        isDying = false; 
    }
        public override void OnStopRewind()
    {
        isRewinding = false;
        // Restore alive body type if living, keep frozen if still dead
        rb.bodyType = wasDead ? RigidbodyType2D.Kinematic : originalBodyType;
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        
        state.SetCustomData("isAttacking", isAttacking);
        state.SetCustomData("attackTimer", attackTimer);
        
        state.SetCustomData("isReviving", isReviving);
        state.SetCustomData("reviveTimer", reviveTimer);
        
        state.SetCustomData("isDying", isDying);
        state.SetCustomData("spriteEnabled", spriteRenderer != null && spriteRenderer.enabled);
        state.SetCustomData("colEnabled", col != null && col.enabled);

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
        attackTimer = state.GetCustomData<float>("attackTimer");
        
        isReviving = state.GetCustomData<bool>("isReviving");
        reviveTimer = state.GetCustomData<float>("reviveTimer");
        
        isDying = state.GetCustomData<bool>("isDying");

        if (spriteRenderer != null)
            spriteRenderer.enabled = state.GetCustomData<bool>("spriteEnabled", true);
            
        if (col != null)
            col.enabled = state.GetCustomData<bool>("colEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }

    void OnDrawGizmosSelected()
    {
        float dir = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + new Vector2(dir * hitboxOffset, 0), hitboxRadius);
        
        // Draw the ground detection raycast so you can easily see it in the Scene view!
        if (col != null)
        {
            Gizmos.color = Color.cyan;
            float checkDist = col.bounds.extents.y + groundDetectionOffset;
            Gizmos.DrawRay(transform.position, Vector2.down * checkDist);
        }
    }
}