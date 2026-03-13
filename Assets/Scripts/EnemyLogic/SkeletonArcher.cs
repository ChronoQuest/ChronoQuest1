using System.Collections;
using UnityEngine;
using TimeRewind;

public class SkeletonArcher : EnemyBase
{
    [Header("Detection")]
    public float detectionRange = 8f;
    public float shootRange = 6f;
    public float safeDistance = 3f;

    [Header("Movement")]
    public float retreatSpeed = 2f;

    [Header("Physics & Environment")]
    public LayerMask groundLayer;
    [Tooltip("How far below the skeleton's feet to look for the ground when dying. Increase this if it falls into the floor.")]
    public float groundDetectionOffset = 1.3f;

    [Header("Behavior")]
    [Tooltip("If true, the skeleton will not retreat and will shoot from its position.")]
    public bool isStatic = false;

    [Header("Shooting")]
    public int damage = 1;
    public float shootCooldown = 2f;

    [Header("References")]
    public Transform player;
    public GameObject arrowPrefab;

    [Header("Arrow Pool")]
    public int arrowPoolSize = 5;

    [Header("Arrow Spawn")]
    public Vector2 arrowSpawnOffset = new Vector2(0.3f, 0.5f);
    
    [Header("Revive")]
    public float reviveAnimDuration = 0.9f;

    private Animator animator;
    private Collider2D col;

    private enum State { Idle, Retreat, Shoot }
    [SerializeField] private State currentState = State.Idle;

    private float lastShootTime;
    private Vector3 originalScale;
    private Vector2 pendingArrowDirection;

    private ArrowProjectile[] arrowPool;

    // --- REWIND SAFE VARIABLES ---
    private bool isDying = false;
    private bool isReviving;
    private float reviveTimer;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        originalScale = transform.localScale;
        stunOnLand = true; // Keeps your existing OnCollisionEnter stun logic intact
        BuildArrowPool();
    }

    void BuildArrowPool()
    {
        if (arrowPrefab == null) return;

        arrowPool = new ArrowProjectile[arrowPoolSize];
        for (int i = 0; i < arrowPoolSize; i++)
        {
            GameObject obj = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
            arrowPool[i] = obj.GetComponent<ArrowProjectile>();
            obj.SetActive(false);
        }
    }

    void Update()
    {
        if (isRewinding) return;

        // --- TIMER UPDATES ---
        if (isReviving)
        {
            reviveTimer -= Time.deltaTime;
            if (reviveTimer <= 0) isReviving = false;
        }

        // If dead, dying, reviving, launched, or stunned -> Do nothing.
        if (wasDead || isDying || isReviving || isStunned || isLaunched) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
            currentState = State.Idle;
        else if (!isStatic && dist < safeDistance)
            currentState = State.Retreat;
        else if (dist <= shootRange)
            currentState = State.Shoot;
        else
            currentState = State.Idle;

        if (animator != null)
            animator.SetFloat("Speed", currentState == State.Retreat ? 1f : 0f);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isDying || isReviving || isStunned || isLaunched) return;

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;
            case State.Retreat:
                Retreat();
                break;
            case State.Shoot:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                TryShoot();
                break;
        }
    }

    void Retreat()
    {
        if (player == null) return;
        Vector2 dir = ((Vector2)transform.position - (Vector2)player.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * retreatSpeed, rb.linearVelocity.y);
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
    /// Called by Animation Event on the attack clip at the arrow-release frame.
    /// </summary>
    public void FireArrow()
    {
        if (wasDead || isDying || isRewinding || isLaunched || isStunned) return;

        ArrowProjectile arrow = GetPooledArrow();
        if (arrow == null) return;

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
        return null;
    }

    void FaceDirection(float dirX)
    {
        if (dirX > 0)
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        else if (dirX < 0)
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    // ================= DAMAGE & STUN =================

    public override void TakeDamage(int amount)
    {
        if (wasDead || isDying) return; 
        
        animator?.SetTrigger("Hit");
        base.TakeDamage(amount);
    }

    public override void ApplyKnockback(Vector2 force)
    {
        // Removed StopAllCoroutines() to prevent breaking the death fall sequence if hit immediately upon death
        base.ApplyKnockback(force);

        if (animator != null && !isDying) 
        {
            animator.SetTrigger("Hit"); 
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // This keeps your existing airborne landing stun logic intact
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.7f)
            {
                if (isLaunched && stunOnLand)
                {
                    StartCoroutine(HitStunRoutine(0.5f)); 
                }
            }
        }
    }

    // ================= REFACTORED DEATH =================

    public override void Die()
    {
        if (wasDead || isDying) return;
        
        wasDead = true;
        isDying = true;
        
        if (animator != null) animator.SetFloat("Speed", 0f);
        
        if (col != null) col.enabled = false;

        StartCoroutine(HandleSkeletonDeath());
    }

    private IEnumerator HandleSkeletonDeath()
    {
        // Note: Your original script used "Dead" instead of "Die" for the trigger string. 
        if (animator != null) animator.SetTrigger("Dead");

        if (col != null)
        {
            float checkDist = col.bounds.extents.y + groundDetectionOffset;
            while (!Physics2D.Raycast(transform.position, Vector2.down, checkDist, groundLayer))
            {
                yield return null;
            }
        }

        rb.linearVelocity = Vector2.zero; 
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic; 
        rb.gravityScale = 0f;
        
        isDying = false; 
    }

    // ================= REVIVE =================

    public override void Revive()
    {
        StopAllCoroutines(); 
        
        base.Revive();
        isDying = false;
        
        isReviving = true;
        reviveTimer = reviveAnimDuration;
        
        rb.bodyType = originalBodyType; 
        rb.gravityScale = 1f; 
        if (col != null) col.enabled = true;
        
        if (sprite != null) sprite.enabled = true;
        animator?.SetTrigger("Revive");
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isDying = false; 
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();

        state.SetCustomData("EnemyState", (int)currentState);
        state.SetCustomData("FacingDirection", transform.localScale);
        
        state.SetCustomData("isReviving", isReviving);
        state.SetCustomData("reviveTimer", reviveTimer);
        
        state.SetCustomData("isDying", isDying);
        state.SetCustomData("spriteEnabled", sprite != null && sprite.enabled);
        state.SetCustomData("colEnabled", col != null && col.enabled);

        if (animator != null)
        {
            AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animInfo.shortNameHash;
            state.AnimatorNormalizedTime = animInfo.normalizedTime;
        }

        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);

        currentState = (State)state.GetCustomData<int>("EnemyState", (int)State.Idle);
        transform.localScale = state.GetCustomData<Vector3>("FacingDirection", originalScale);
        
        isReviving = state.GetCustomData<bool>("isReviving");
        reviveTimer = state.GetCustomData<float>("reviveTimer");
        
        isDying = state.GetCustomData<bool>("isDying");

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);
            
        if (col != null)
            col.enabled = state.GetCustomData<bool>("colEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }

    void OnDrawGizmosSelected()
    {
        // Draw the ground detection raycast
        Collider2D localCol = GetComponent<Collider2D>(); 
        if (localCol != null)
        {
            Gizmos.color = Color.cyan;
            float checkDist = localCol.bounds.extents.y + groundDetectionOffset;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3.down * checkDist));
        }
    }
}