using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TimeRewind;

public class NecromancerEnemy : EnemyBase, IForesightEnemy
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
    public int spellPoolSize = 6;

    [Header("References")]
    [SerializeField] private Transform _player;
    public Transform player
    {
        get => _player;
        set => _player = value;
    }
    public List<EnemyBase> minions = new List<EnemyBase>();

    private Animator animator;
    private Collider2D col;
    private Vector3 originalScale;

    private Collider2D playerCollider;
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;
    [Header("Foresight")]
    public float dodgeTriggerDistance = 5f;
    private bool isDodging = false;    
    private float dodgeDuration = 0.75f;
    private ForesightSystem foresightSystem;
    private float rewindStartTime;
    private bool hasForesight = false;
    private SpriteRenderer spriteRenderer;

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
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = player.GetComponent<Collider2D>();
        playerCombat = player.GetComponent<PlayerCombat>();
        playerSpells = player.GetComponent<PlayerSpellSystem>();
        foresightSystem = GetComponent<ForesightSystem>();
        
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

        if (wasDead || isDying || isAttacking || isReviving || isStunned || isDodging || isLaunched) return;
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
        if (isRewinding || wasDead || isDying || isStunned || isDodging || isLaunched) return;

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
        if (wasDead || isDying || isRewinding || isStunned || isLaunched) return;
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
    private void OnCollisionEnter2D(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.7f)
            {
                if (isLaunched && stunOnLand)
                {
                    stunTimer = 0.5f;
                    isLaunched = false; 
                }
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
        if (wasDead || isDying || isDodging) return;
        
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
    public override void ApplyKnockback(Vector2 force)
    {
        // Removed StopAllCoroutines() to prevent breaking the death fall sequence
        base.ApplyKnockback(force);

        if (animator != null && !isDying) 
        {
            animator.SetTrigger("Hit"); 
        }
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
        rewindStartTime = Time.time;
        StopAllCoroutines();
        isDying = false;
        if (col != null) col.enabled = true;
    }

    public override void OnStopRewind()
    {
        if (foresightSystem != null)
        {
            // Calculate how much time passed in the real world while we were rewinding
            float timeRewound = rewindStartTime - TimeRewindManager.Instance.CurrentRewindTime;
            int statesErased = Mathf.RoundToInt(timeRewound / foresightSystem.recordInterval);
            foresightSystem.HandleRewindStop(statesErased);
        }
        
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

    // =================== IForesightEnemy Implementation ===================

    public void DoubleDetectionRange()
    {
        detectionRange *= 2f;
    }

    public int GetPlayerAttackState()
    {
        if (playerCombat != null && playerCombat.isAttacking) return 1;
        if (playerSpells != null && playerSpells.isCasting) return 2;
        return 0;
    }

    public void SetForesightState(bool state)
    {
        hasForesight = state;
        if(hasForesight) detectionRange *= 2;
        animator.SetBool("hasForesight", hasForesight);
        if(foresightGlow != null) foresightGlow.SetActive(hasForesight);
        Vector2 direction = (player.position - transform.position).normalized;
        if (direction.x > 0) spriteRenderer.flipX = true;
        else if (direction.x < 0) spriteRenderer.flipX = false;
    }

    new public bool IsDead() => wasDead;
    
    public bool IsRewinding() => isRewinding;

    public void ExecuteLunge()
    {
        if (isDodging) return;
        
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        isDodging = true;

        lastAttackTime = Time.time;
        Vector2 dir = (player.position - transform.position).normalized;

        for (int i = -1; i <= 1; i++)
        {
            NecromancerSpell spell = GetPooledSpell();
            if (spell != null)
            {
                spell.transform.position = transform.position;
                spell.gameObject.SetActive(true);

                Vector2 spreadDir = Quaternion.Euler(0, 0, 15f * i) * dir;
                spell.Launch(spreadDir, attackDamage);
            }
        }
        
        isDodging = false;
    }

    public void ExecuteDodge()
    {
        if (isDodging) return; // Prevent dodging if already in a dodge state

        GameObject spellObj = playerSpells.latestSpell;
        bool shouldDodge = false;
        Vector2 jumpMove = new Vector2(0f, 0f);

        // Check if player or spell is close enough to trigger the dodge
        if (Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance - 1.5f)
        {
            shouldDodge = true;
            Vector2 awayDir = (transform.position - playerCollider.bounds.center).normalized;
            jumpMove = (awayDir + Vector2.up * 1.5f).normalized;
        }
        else if (spellObj != null)
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
                        shouldDodge = true;
                        jumpMove = new Vector2 (0f, 3f);
                    }
                }
            }
        }

        if (shouldDodge)
        {
            StartCoroutine(PhaseDodgeRoutine(jumpMove));
        }
    }

    IEnumerator PhaseDodgeRoutine(Vector2 jumpMove)
    {
        isDodging = true;
        int originalLayer = gameObject.layer;
        gameObject.layer = LayerMask.NameToLayer("EnemyDodging");
        
        animator.SetTrigger("DodgeJump");

        if (foresightGlow != null) foresightGlow.SetActive(true);

        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);

        // Do a little jump to show dodging
        rb.linearVelocity = jumpMove;

        yield return new WaitForSeconds(dodgeDuration);
        
        spriteRenderer.color = originalColor;
        
        if (!hasForesight && foresightGlow != null) 
        {
            foresightGlow.SetActive(false);
        }

        gameObject.layer = originalLayer;
        isDodging = false;
    }

    public float GetDistanceToPlayer()
    {
        return Vector2.Distance(transform.position, playerCollider.bounds.center);
    }

    public bool IsPerformingForesightAction()
    {
        return isDodging;
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