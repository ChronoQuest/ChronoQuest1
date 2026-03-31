using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TimeRewind;

public class NecromancerEnemy : EnemyBase
{
    [Header("Stats")]
    public float detectionRange = 10f;
    public float safeDistance = 4f;
    public float moveSpeed = 2f;

    [Header("Physics & Environment")]
    public LayerMask groundLayer;
    [Tooltip("How far below the feet to look for the ground when dying.")]
    public float groundDetectionOffset = 1.3f;

    [Header("Ledge Detection")]
    public float ledgeCheckOffset = 1.5f;
    public float ledgeCheckDepth = 2f;

    [Header("Revive")]
    public float reviveCooldown = 5f;
    public float reviveAnimDuration = 1.2f; 
    [Tooltip("How many seconds a minion must be dead before the Necromancer can revive it.")]
    public float minionDeadRequiredTime = 3f;

    [Header("Attack")]
    public float attackRange = 8f;
    public float attackCooldown = 3f;
    public float attackAnimDuration = 0.8f; 
    public int attackDamage = 1;
    public GameObject spellPrefab;
    public int spellPoolSize = 3;

    [Header("References")]
    public Transform player;
    public List<EnemyBase> minions = new List<EnemyBase>();

    private Animator animator;
    private Collider2D col;
    private Vector3 originalScale;

    // --- REWIND SAFE VARIABLES ---
    private float lastReviveTime = -99f;
    private bool isReviving;
    private float reviveTimer = 0f; 

    private NecromancerSpell[] spellPool;
    private float lastAttackTime = -99f;
    private bool isAttacking;
    private float attackTimer = 0f; 
    
    // Dynamic array to track how long each specific minion has been dead
    private float[] minionDeadTimers;

    private bool isDying = false;
    
    private Vector2 pendingSpellDirection;

    private enum State { Idle, BackAway, Revive, Attack }
    [SerializeField] private State currentState = State.Idle;

    protected override void Awake()
    {
       
        base.Awake();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        originalScale = transform.localScale;
        BuildSpellPool();
    }

    void BuildSpellPool()
    {
        if (spellPrefab == null) return;
        spellPool = new NecromancerSpell[spellPoolSize];
        for (int i = 0; i < spellPoolSize; i++)
        {
            GameObject obj = Instantiate(spellPrefab, transform.position, Quaternion.identity);
            spellPool[i] = obj.GetComponent<NecromancerSpell>();
            obj.SetActive(false);
        }
    }

    NecromancerSpell GetPooledSpell()
    {
        if (spellPool == null) return null;
        foreach (var s in spellPool)
            if (s != null && !s.gameObject.activeSelf) return s;
        return null;
    }

    public override void Update()
    {
        base.Update();
        if (isRewinding) return;

        // --- TIMER UPDATES ---
        if (isReviving)
        {
            reviveTimer -= Time.deltaTime;
            if (reviveTimer <= 0)
            {
                isReviving = false;
                lastReviveTime = Time.time;
            }
        }

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                isAttacking = false;
                lastAttackTime = Time.time;
            }
        }

        // --- DYNAMIC MINION DEAD TIMERS ---
        // Ensure array matches minion count (in case minions are added dynamically)
        if (minionDeadTimers == null || minionDeadTimers.Length != minions.Count)
            minionDeadTimers = new float[minions.Count];

        for (int i = 0; i < minions.Count; i++)
        {
            if (minions[i] != null && minions[i].IsDead)
                minionDeadTimers[i] += Time.deltaTime;
            else
                minionDeadTimers[i] = 0f; // Reset if alive
        }

        if (wasDead || isDying || isAttacking || isReviving || isStunned) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (CanRevive())
        {
            currentState = State.Revive;
        }
        else if (dist > detectionRange)
        {
            currentState = State.Idle;
        }
        else if (dist < safeDistance)
        {
            float retreatDirX = transform.position.x - player.position.x;
            
            if (IsNearLedge(retreatDirX))
            {
                if (CanAttack())
                    currentState = State.Attack;
                else
                    currentState = State.Idle;
            }
            else
            {
                currentState = State.BackAway;
            }
        }
        else if (dist <= attackRange && CanAttack())
        {
            currentState = State.Attack;
        }
        else
        {
            currentState = State.Idle;
        }

        if (dist <= detectionRange)
            FacePlayer();

        animator?.SetBool("isWalking", currentState == State.BackAway);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isDying || isStunned) return;

        if (isReviving || isAttacking)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;

            case State.BackAway:
                Vector2 away = ((Vector2)transform.position - (Vector2)player.position).normalized;
                rb.linearVelocity = new Vector2(away.x * moveSpeed, rb.linearVelocity.y);
                break;

            case State.Revive:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                StartRevive(); 
                break;

            case State.Attack:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                StartAttack(); 
                break;
        }
    }

    bool IsNearLedge(float desiredMoveDirectionX)
    {
        float dir = Mathf.Sign(desiredMoveDirectionX); 
        Vector2 checkPosition = (Vector2)transform.position + new Vector2(dir * ledgeCheckOffset, 0);
        RaycastHit2D hit = Physics2D.Raycast(checkPosition, Vector2.down, ledgeCheckDepth, groundLayer);
        return hit.collider == null; 
    }

    // --- REFACTORED REVIVE CHECKS ---
    bool CanRevive() => !isReviving && Time.time >= lastReviveTime + reviveCooldown && HasRevivableMinion();

    bool HasRevivableMinion()
    {
        for (int i = 0; i < minions.Count; i++)
        {
            // Only return true if a minion has been dead longer than the required time threshold
            if (minions[i] != null && minions[i].IsDead && minionDeadTimers[i] >= minionDeadRequiredTime) 
                return true;
        }
        return false;
    }

    void StartRevive()
    {
        isReviving = true;
        reviveTimer = reviveAnimDuration;
        animator?.SetTrigger("Revive");
    }

    bool CanAttack() => !isAttacking && Time.time >= lastAttackTime + attackCooldown;

    void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackAnimDuration;
        pendingSpellDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        animator?.SetTrigger("Attack");
    }

    public void FireSpell()
    {
        if (wasDead || isDying || isRewinding || isStunned) return;
        NecromancerSpell spell = GetPooledSpell();
        if (spell == null) return;
        spell.transform.position = transform.position;
        spell.gameObject.SetActive(true);
        spell.Launch(pendingSpellDirection, attackDamage);
    }

    public void ReviveMinion()
    {
        if (wasDead || isDying || isRewinding) return;
        
        for (int i = 0; i < minions.Count; i++)
        {
            // Only revive the ones that meet the threshold! (Prevents instant-resurrection)
            if (minions[i] != null && minions[i].IsDead && minionDeadTimers[i] >= minionDeadRequiredTime)
            {
                minions[i].Revive();
                minionDeadTimers[i] = 0f; // Reset their specific death timer
            }
        }
    }

    void FacePlayer()
    {
        if (player.position.x > transform.position.x)
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    public override void TakeDamage(int amount)
    {
        if (wasDead || isDying) return;
        
        animator?.SetBool("isWalking", false);
        if (health - amount > 0)
        {
            animator?.SetTrigger("Hit");
        }
        
        base.TakeDamage(amount);
    }

    // ================= REFACTORED DEATH =================
    public override void Die()
    {
        if (wasDead || isDying) return;
        
        wasDead = true;
        isDying = true;
        isAttacking = false;
        isReviving = false;
        animator?.SetBool("isWalking", false);
        
        if (col != null) col.enabled = false;
        OnDeath?.Invoke();         
        
        StartCoroutine(HandleNecromancerDeath());
    }

    private IEnumerator HandleNecromancerDeath()
    {
        if (animator != null) animator.SetTrigger("Die");

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
        
        isDying = false; 
    }

    // ================= REVIVE / REWIND LOGIC =================

    public override void Revive()
    {
        StopAllCoroutines(); 
        
        base.Revive();
        isDying = false;
        isAttacking = false;
        isReviving = false;
        
        rb.bodyType = originalBodyType; 
        rb.gravityScale = 1f; 
        if (col != null) col.enabled = true;
        
        if (sprite != null) sprite.enabled = true;
    }

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isDying = false;
        if (col != null) col.enabled = true;
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
        
        state.SetCustomData("lastReviveTime", lastReviveTime);
        state.SetCustomData("isReviving", isReviving);
        state.SetCustomData("reviveTimer", reviveTimer);

        state.SetCustomData("lastAttackTime", lastAttackTime);
        state.SetCustomData("isAttacking", isAttacking);
        state.SetCustomData("attackTimer", attackTimer);
        
        state.SetCustomData("isDying", isDying);
        
        // Save the array of minion dead timers
        state.SetCustomData("minionDeadTimers", minionDeadTimers != null ? (float[])minionDeadTimers.Clone() : new float[0]);

        state.SetCustomData("colEnabled", col != null && col.enabled);
        state.SetCustomData("spriteEnabled", sprite != null && sprite.enabled);
        state.SetCustomData("localScale", transform.localScale);

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
        
        lastReviveTime = state.GetCustomData<float>("lastReviveTime");
        isReviving = state.GetCustomData<bool>("isReviving");
        reviveTimer = state.GetCustomData<float>("reviveTimer");

        lastAttackTime = state.GetCustomData<float>("lastAttackTime");
        isAttacking = state.GetCustomData<bool>("isAttacking");
        attackTimer = state.GetCustomData<float>("attackTimer");
        
        isDying = state.GetCustomData<bool>("isDying");
        
        // Restore the array of minion dead timers
        float[] savedTimers = state.GetCustomData<float[]>("minionDeadTimers");
        if (savedTimers != null) minionDeadTimers = (float[])savedTimers.Clone();

        transform.localScale = state.GetCustomData<Vector3>("localScale", originalScale);

        if (col != null)
            col.enabled = state.GetCustomData<bool>("colEnabled", true);

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector2 rightCheck = (Vector2)transform.position + new Vector2(ledgeCheckOffset, 0);
        Vector2 leftCheck = (Vector2)transform.position + new Vector2(-ledgeCheckOffset, 0);
        Gizmos.DrawLine(rightCheck, rightCheck + Vector2.down * ledgeCheckDepth);
        Gizmos.DrawLine(leftCheck, leftCheck + Vector2.down * ledgeCheckDepth);
        
        // Ground detection Gizmo
        Collider2D localCol = GetComponent<Collider2D>(); 
        if (localCol != null)
        {
            Gizmos.color = Color.yellow;
            float groundDist = localCol.bounds.extents.y + groundDetectionOffset;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3.down * groundDist));
        }
    }
}