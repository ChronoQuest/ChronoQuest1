using UnityEngine;
using System.Collections;
using TimeRewind;

public class MeleeSkeleton : EnemyBase, IBossSpawnable, IForesightEnemy
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

    [Header("Landed-On-Player Nudge")]
    // Fired when the skeleton's collider rests on top of the player — small push so it
    // slides off the head and gravity drops it to real ground.
    public float pushOffXSpeed = 1.5f;
    public float pushOffYSpeed = 1f;
    public float pushOffDuration = 0.25f;

    [Header("Revive")]
    public float reviveAnimDuration = 0.9f;

    [SerializeField] private Transform _player;
    public Transform player
    {
        get => _player;
        set => _player = value;
    }
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
    private float pushOffTimer;

    private bool isReviving;
    private float reviveTimer;

    private bool isDying = false; 

    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;
    [Header("Audio")]
    public AudioClip swingClip;
    [Range(0f, 1f)] public float swingVolume = 1f;

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
        playerCollider = player.GetComponent<Collider2D>();
        playerCombat = player.GetComponent<PlayerCombat>();
        playerSpells = player.GetComponent<PlayerSpellSystem>();
        foresightSystem = GetComponent<ForesightSystem>();
    }

    public override void Update()
    {
        base.Update();
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

        if (pushOffTimer > 0f) pushOffTimer -= Time.deltaTime;

        if (wasDead || isDying || isAttacking || isReviving || isStunned || isDodging || pushOffTimer > 0f) return;
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
        if (wasDead || isDying || isDodging) return; 
        
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

    // The attack hitbox only probes horizontally (see MeleeHit), so if the skeleton lands
    // on the player's head it never connects. Catch that case via contact normal and deal
    // damage + a small nudge so the skeleton slides off rather than walking on the player.
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (wasDead || isDying || isRewinding) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        bool skeletonOnPlayer = false;
        bool playerOnSkeleton = false;
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.7f) skeletonOnPlayer = true;
            if (contact.normal.y < -0.7f) playerOnSkeleton = true;
        }

        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();

        if (skeletonOnPlayer)
        {
            if (playerHealth != null) playerHealth.ModifyHealth(-damage);
            float pushDir = Mathf.Sign(transform.position.x - collision.transform.position.x);
            if (Mathf.Approximately(pushDir, 0f))
                pushDir = (spriteRenderer != null && spriteRenderer.flipX) ? 1f : -1f;
            rb.linearVelocity = new Vector2(pushDir * pushOffXSpeed, pushOffYSpeed);
            pushOffTimer = pushOffDuration;
        }

        if (playerOnSkeleton)
        {
            if (playerHealth != null) playerHealth.ModifyHealth(-damage);
            Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                float pushDir = Mathf.Sign(collision.transform.position.x - transform.position.x);
                if (Mathf.Approximately(pushDir, 0f))
                    pushDir = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
                playerRb.linearVelocity = new Vector2(pushDir * pushOffXSpeed, pushOffYSpeed);
            }
        }
    }

    // ================= REFACTORED DEATH =================
    public override void Die()
    {
        if (wasDead || isDying) return;

        base.DeathSound();

        // Kill any running coroutines (e.g. BlockRoutine, LungeRoutine) before starting death
        StopAllCoroutines();
        isDodging = false;

        wasDead = true;
        isDying = true;
        isAttacking = false;

        if (animator != null)
        {
            animator.SetBool("isRunning", false);
            animator.SetBool("hasForesight", false);
        }
        if (foresightGlow != null) foresightGlow.SetActive(false);

        // Cache collider metrics BEFORE disabling (disabled collider returns zero bounds)
        // Use actual distance from pivot to collider bottom, not extents.y, in case the collider is offset
        float feetOffset = (col != null) ? transform.position.y - col.bounds.min.y : 0f;
        float groundCheckDist = feetOffset + groundDetectionOffset;

        if (col != null) col.enabled = false;

        OnDeath?.Invoke();
        StartCoroutine(HandleSkeletonDeath(groundCheckDist, feetOffset));
    }

    private IEnumerator HandleSkeletonDeath(float groundCheckDist, float feetOffset)
    {
        if (animator != null) animator.SetTrigger("Die");

        RaycastHit2D hit = default;
        while (true)
        {
            hit = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDist, groundLayer);
            if (hit.collider != null) break;
            yield return null;
        }

        // Snap so feet sit exactly on the ground surface
        transform.position = new Vector3(transform.position.x, hit.point.y + feetOffset, transform.position.z);

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
        rewindStartTime = Time.time;
        StopAllCoroutines(); 
        isDying = false; 
    }

    public override void OnStopRewind()
    {
        if (foresightSystem != null)
        {
            float timeRewound = rewindStartTime - TimeRewindManager.Instance.CurrentRewindTime;
            int statesErased = Mathf.RoundToInt(timeRewound / foresightSystem.recordInterval);
            foresightSystem.HandleRewindStop(statesErased);
        }
        isRewinding = false;
        rb.bodyType = wasDead ? RigidbodyType2D.Kinematic : originalBodyType;
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        
        state.SetCustomData("isAttacking", isAttacking);
        state.SetCustomData("attackTimer", attackTimer);
        state.SetCustomData("pushOffTimer", pushOffTimer);
        
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
        pushOffTimer = state.GetCustomData<float>("pushOffTimer");
        
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
        animator.SetBool("hasForesight", hasForesight);
        if (foresightGlow != null) foresightGlow.SetActive(hasForesight);
        
        Vector2 direction = (player.position - transform.position).normalized;
        if (direction.x > 0) spriteRenderer.flipX = false;
        else if (direction.x < 0) spriteRenderer.flipX = true;
    }

    new public bool IsDead() => wasDead;
    public bool IsRewinding() => isRewinding;
    
    public void ExecuteLunge()
    {
        if (isDodging || isAttacking) return;
        
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
            if (player != null && !wasDead && !isDying && !isStunned)
            {
                float dist = Vector2.Distance(transform.position, player.position);

                // Stop lunging and attack if we reach the player
                if (dist <= attackRange)
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    animator.SetBool("isRunning", false);
                    StartAttack(); 
                    yield break;
                }

                Vector2 dir = (player.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(dir.x * moveSpeed * 2.5f, rb.linearVelocity.y);
                
                spriteRenderer.flipX = dir.x < 0;
            }

            timer += Time.deltaTime;
            yield return null;
        }
        isAttacking = false;
    }

    public void ExecuteDodge()
    {
        if (isDodging) return;

        GameObject spellObj = playerSpells.latestSpell;
        bool shouldBlock = false;

        if (Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance - 1.5f)
        {
            shouldBlock = true;
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
                        shouldBlock = true;
                    }
                }
            }
        }

        if (shouldBlock)
        {
            StartCoroutine(BlockRoutine());
        }
    }

    private IEnumerator BlockRoutine()
    {
        isDodging = true;
        
        float originalKnockbackResist = knockbackResistance;
        knockbackResistance = 10f; 

        animator?.SetTrigger("Block");

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

        if (foresightGlow != null) foresightGlow.SetActive(true);

        yield return new WaitForSeconds(dodgeDuration);
        
        if (!hasForesight && foresightGlow != null) 
        {
            foresightGlow.SetActive(false);
        }

        knockbackResistance = originalKnockbackResist;
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

    void OnDrawGizmosSelected()
    {
        float dir = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + new Vector2(dir * hitboxOffset, 0), hitboxRadius);
        
        if (col != null)
        {
            Gizmos.color = Color.cyan;
            float checkDist = col.bounds.extents.y + groundDetectionOffset;
            Gizmos.DrawRay(transform.position, Vector2.down * checkDist);
        }
    }
    public void playSwing()
    {
        if(swingClip != null && audioSource != null) audioSource.PlayOneShot(swingClip, swingVolume);
    }
}