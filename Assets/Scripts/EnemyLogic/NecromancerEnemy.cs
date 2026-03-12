using System.Collections.Generic;
using UnityEngine;
using TimeRewind;

public class NecromancerEnemy : EnemyBase
{
    [Header("Stats")]
    public float detectionRange = 10f;
    public float safeDistance = 4f;
    public float moveSpeed = 2f;

    [Header("Ledge Detection")]
    [Tooltip("How far behind the enemy to check for the ledge.")]
    public float ledgeCheckOffset = 1.5f;
    [Tooltip("How far down to cast the ray to look for the floor.")]
    public float ledgeCheckDepth = 2f;
    [Tooltip("The layer(s) that represent the solid ground/platforms.")]
    public LayerMask groundLayer;

    [Header("Revive")]
    public float reviveCooldown = 5f;
    public float reviveAnimDuration = 1.2f; 

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

    private float lastReviveTime = -99f;
    private bool isReviving;
    private float reviveTimer = 0f; 

    private NecromancerSpell[] spellPool;
    private float lastAttackTime = -99f;
    private bool isAttacking;
    private float attackTimer = 0f; 
    
    private Vector2 pendingSpellDirection;

    private enum State { Idle, BackAway, Revive, Attack }
    private State currentState = State.Idle;

    protected override void Awake()
    {
        health = 3;
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

    void Update()
    {
        if (isRewinding || wasDead) return;

        // --- REWIND-SAFE TIMERS ---
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
        // ------------------------------

        if (isReviving || isAttacking || player == null) return;

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
            // Determine which direction we want to retreat (away from player)
            float retreatDirX = transform.position.x - player.position.x;
            
            // Check if there is a ledge in the direction we want to run
            if (IsNearLedge(retreatDirX))
            {
                // Cornered! Stop retreating. Fight back if cooldown allows, else wait.
                if (CanAttack())
                    currentState = State.Attack;
                else
                    currentState = State.Idle;
            }
            else
            {
                // Path is clear, keep backing up
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
        if (isRewinding || wasDead || isStunned) return;

        // If locked in animation, freeze movement
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

    // --- LEDGE DETECTION LOGIC ---
    bool IsNearLedge(float desiredMoveDirectionX)
    {
        // Get 1 or -1 depending on which way we want to go
        float dir = Mathf.Sign(desiredMoveDirectionX); 
        
        // Calculate the point slightly behind the enemy
        Vector2 checkPosition = (Vector2)transform.position + new Vector2(dir * ledgeCheckOffset, 0);
        
        // Shoot a ray straight down from that point
        RaycastHit2D hit = Physics2D.Raycast(checkPosition, Vector2.down, ledgeCheckDepth, groundLayer);
        
        // If the ray hits NOTHING (collider is null), it means there is a drop-off
        return hit.collider == null; 
    }

    bool CanRevive() => !isReviving && Time.time >= lastReviveTime + reviveCooldown && HasDeadMinion();

    bool HasDeadMinion()
    {
        foreach (var m in minions)
            if (m != null && m.IsDead) return true;
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
        if (wasDead || isRewinding) return;
        NecromancerSpell spell = GetPooledSpell();
        if (spell == null) return;
        spell.transform.position = transform.position;
        spell.gameObject.SetActive(true);
        spell.Launch(pendingSpellDirection, attackDamage);
    }

    public void ReviveMinion()
    {
        if (wasDead || isRewinding) return;
        foreach (var m in minions)
        {
            if (m != null && m.IsDead)
                m.Revive();
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
        if (wasDead) return;
        animator?.SetTrigger("Hit");
        base.TakeDamage(amount);
    }

    public override void Die()
    {
        isReviving = false;
        isAttacking = false;
        animator?.SetTrigger("Die");
        base.Die();          
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        if (col != null) col.enabled = true;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
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

        transform.localScale = state.GetCustomData<Vector3>("localScale", originalScale);

        if (col != null)
            col.enabled = state.GetCustomData<bool>("colEnabled", true);

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }

    // This makes it easy to see and tune your ledge detection in the Unity Editor!
    private void OnDrawGizmosSelected()
    {
        // Draw the Ledge Raycasts in Cyan
        Gizmos.color = Color.cyan;
        Vector2 rightCheck = (Vector2)transform.position + new Vector2(ledgeCheckOffset, 0);
        Vector2 leftCheck = (Vector2)transform.position + new Vector2(-ledgeCheckOffset, 0);
        
        Gizmos.DrawLine(rightCheck, rightCheck + Vector2.down * ledgeCheckDepth);
        Gizmos.DrawLine(leftCheck, leftCheck + Vector2.down * ledgeCheckDepth);
    }
}