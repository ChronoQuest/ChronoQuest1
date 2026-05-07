using UnityEngine;
using System.Collections;
using TimeRewind;

public class DeathKnightEnemy : EnemyBase, IForesightEnemy
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
    public Transform _player;
    public Transform player
    {
        get => _player;
        set => _player = value;
    }

    private Animator animator;

    private float lastAttackTime = -99f;
    private float lastRangedAttackTime = -99f;
    private bool isAttacking;
    private SpriteRenderer spriteRenderer;
    private DeathKnightOrb[] orbPool;
    private Vector2 pendingOrbDirection;

    private enum State { Idle, Chase, Attack, RangedAttack }
    private State currentState = State.Idle;
    
    [Header("Audio")]
    public AudioClip swingClip;
    [Range(0f, 1f)] public float swingVolume = 1f;

    [Header("Foresight")]
    public float dodgeTriggerDistance = 5f;
    private bool isDodging = false;
    private float dodgeDuration = 0.75f;
    private ForesightSystem foresightSystem;
    private float rewindStartTime;
    private bool hasForesight = false;

    private Collider2D playerCollider;
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;

    protected override void Awake()
    {
        knockbackResistance = 4f;
        deathAnimationDuration = 1.1f;
        base.Awake();
    }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        BuildOrbPool();

        // --- Foresight Setup ---
        foresightSystem = GetComponent<ForesightSystem>();
        if (player != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
            playerCombat = player.GetComponent<PlayerCombat>();
            playerSpells = player.GetComponent<PlayerSpellSystem>();
        }
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
        // Added isDodging block
        if (isRewinding || wasDead || isDodging) return; 
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
        // Added isDodging block
        if (isRewinding || wasDead || isStunned || isAttacking || isDodging) return;

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

    // melee

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        animator?.SetTrigger("Attack");
        // MeleeHit() called by Animation Event at the swing frame
        yield return new WaitForSeconds(attackAnimDuration);
        isAttacking = false;
    }

    public void MeleeHit()
    {
        if (wasDead || isRewinding || player == null) return;
        float dir = sprite.flipX ? -1f : 1f;
        Vector2 hitPos = (Vector2)transform.position + new Vector2(dir * hitboxOffset, 0);
        if (Vector2.Distance(hitPos, player.position) <= hitboxRadius)
            player.GetComponent<PlayerHealth>()?.ModifyHealth(-attackDamage);
    }

    // ranged

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

    public void FireOrb()
    {
        if (wasDead || isRewinding) return;
        DeathKnightOrb orb = GetPooledOrb();
        if (orb == null) return;
        orb.transform.position = transform.position;
        orb.gameObject.SetActive(true);
        orb.Launch(pendingOrbDirection, rangedAttackDamage);
    }

    // shared

    void FacePlayer()
    {
        sprite.flipX = player.position.x < transform.position.x;
    }

    public override void TakeDamage(int amount)
    {
        if (wasDead || isDodging) return; // Ignore damage if dodging
        base.TakeDamage(amount);
        if (!wasDead)
            animator?.SetTrigger("Hit");
    }

    public override void Die()
    {
        isAttacking = false;
        isDodging = false;
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
        isDodging = false;
    }

    public override IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathAnimationDuration);
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        rewindStartTime = Time.time; // Log rewind time for Foresight math
        StopAllCoroutines();
        isAttacking = false;
        isDodging = false;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
        
        // Feed the gap to the foresight system
        if (foresightSystem != null)
        {
            float timeRewound = rewindStartTime - TimeRewindManager.Instance.CurrentRewindTime;
            int statesErased = Mathf.RoundToInt(timeRewound / foresightSystem.recordInterval);
            foresightSystem.HandleRewindStop(statesErased);
        }

        isAttacking = false;
        isDodging = false;
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("lastAttackTime",       lastAttackTime);
        state.SetCustomData("lastRangedAttackTime", lastRangedAttackTime);
        state.SetCustomData("spriteEnabled",        sprite != null && sprite.enabled);
        state.SetCustomData("isDodging",            isDodging);
        state.SetCustomData("hasForesight",         hasForesight);

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
        isDodging            = state.GetCustomData<bool>("isDodging");
        hasForesight         = state.GetCustomData<bool>("hasForesight");

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

    // ================= FORESIGHT SYSTEM METHODS =================
    
    public int GetPlayerAttackState()
    {
        if (playerCombat != null && playerCombat.isAttacking) return 1;
        if (playerSpells != null && playerSpells.isCasting) return 2;
        return 0;
    }
    public void SetForesightState(bool state)
    {
        hasForesight = state;
        if (hasForesight) detectionRange *= 2;
        
        if (animator != null) animator.SetBool("hasForesight", hasForesight);
        if (foresightGlow != null) foresightGlow.SetActive(hasForesight);
        
        if (player != null)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            if (direction.x > 0) sprite.flipX = false;
            else if (direction.x < 0) sprite.flipX = true;
        }
    }
    new public bool IsDead() => wasDead;
    public bool IsRewinding() => isRewinding;
    public float GetDistanceToPlayer()
    {
        if (playerCollider != null) return Vector2.Distance(transform.position, playerCollider.bounds.center);
        return Vector2.Distance(transform.position, player.position);
    }

    public bool IsPerformingForesightAction()
    {
        return isDodging;
    }
    public void ExecuteLunge()
    {
        if (isDodging || isAttacking || wasDead) return;
        
        // Verify standard melee cooldown
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        StartCoroutine(LungeRoutine());
    }
    private IEnumerator LungeRoutine()
    {
        isAttacking = true; 
        lastAttackTime = Time.time;

        float timer = 0f;
        float lungeTime = 1f; 

        while (timer < lungeTime)
        {
            if (player != null && !wasDead && !isStunned)
            {
                animator.SetBool("isRunning", true);
                float dist = Vector2.Distance(transform.position, player.position);

                // Stop lunging and use native Death Knight attack if we reach the player
                if (dist <= attackRange)
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    animator?.SetBool("isRunning", false);
                    StartCoroutine(AttackRoutine()); // Directly swaps to native attack routine
                    yield break;
                }

                Vector2 dir = (player.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * moveSpeed * 2.5f, rb.linearVelocity.y);
                
                sprite.flipX = dir.x < 0;
            }

            timer += Time.deltaTime;
            yield return null;
        }
        animator.SetBool("isRunning", false);
        isAttacking = false;
    }
    public void ExecuteDodge()
    {
        if (isDodging) return; // Prevent dodging if already in a dodge state

        GameObject spellObj = playerSpells != null ? playerSpells.latestSpell : null;
        bool shouldDodge = false;
        Vector2 jumpMove = new Vector2(0f, 0f);

        // Check if player or spell is close enough to trigger the dodge
        if (Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance - 1.5f)
        {
            shouldDodge = true;
            Vector2 awayDir = (transform.position - playerCollider.bounds.center).normalized;
            jumpMove = (awayDir + Vector2.up * 1.5f).normalized;
        }
        else if (spellObj != null && spellObj.activeInHierarchy)
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
        
        animator.SetBool("hasForesight", true);
        if (foresightGlow != null) foresightGlow.SetActive(true);

        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);

        // Do a little jump to show dodging
        
        rb.linearVelocity = jumpMove;
        animator.SetBool("isGrounded", false);
        yield return new WaitForSeconds(dodgeDuration);
        animator.SetBool("isGrounded", true);

        
        spriteRenderer.color = originalColor;
        gameObject.layer = originalLayer;
        isDodging = false;
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
    public void playSwing()
    {
        if(swingClip != null && audioSource != null) audioSource.PlayOneShot(swingClip, swingVolume);
    }
}