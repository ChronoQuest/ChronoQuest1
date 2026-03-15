using UnityEngine;
using System.Collections;
using TimeRewind;

public class GhostEnemy : EnemyBase, IBossSpawnable, IForesightEnemy
{
    [Header("Stats")]
    public float detectionRange = 7f;
    public float moveSpeed = 2f;
    public float attackCooldown = 1f;
    public int damage = 1;

    [Header("Hover")]
    public float hoverAmplitude = 0.3f;
    public float hoverFrequency = 1.5f;

    [Header("Teleport")]
    public float teleportInterval = 3f;
    public float teleportOffset = 2f;     // how far from player to reappear
    public float teleportYOffset = 1f;    // raise spawn point above ground surface
    public float phaseOutDuration = 0.5f; // match your PhaseOut clip length
    public float phaseInDuration = 0.5f;  // match your PhaseIn clip length

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
    private Collider2D col;
    private float lastAttackTime;
    private float lastTeleportTime;
    private bool isTouchingPlayer;
    private bool hasDetected;
    private bool isHitStunned;
    private bool isTeleporting;

    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;
    private Collider2D playerCollider;
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;
    [Header("Foresight")]
    public float dodgeTriggerDistance = 5f;
    public GameObject foresightGlow;
    private bool isDodging = false;    
    private float dodgeCooldown = 1.5f;
    private float dodgeTimer = 0f;
    private float dodgeDuration = 0.5f;
    private ForesightSystem foresightSystem;
    private float rewindStartTime;
    private bool hasForesight = false;
    private bool isMidJumpSequence = false;
    private bool isGrounded = true;
    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        health = 2;
        base.Awake();
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = player.GetComponent<Collider2D>();
        playerCombat = player.GetComponent<PlayerCombat>();
        playerSpells = player.GetComponent<PlayerSpellSystem>();
        foresightSystem = GetComponent<ForesightSystem>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        if (isRewinding || wasDead || isTeleporting) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (isTouchingPlayer)
            currentState = State.Attack;
        else if (dist < detectionRange)
        {
            hasDetected = true;
            currentState = State.Chase;
        }
        else
            currentState = State.Idle;

        // Face player
        if (player.position.x > transform.position.x)
            sprite.flipX = false;
        else
            sprite.flipX = true;

        animator?.SetBool("isChasing", currentState == State.Chase);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isHitStunned || isTeleporting) return;

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude);
                break;

            case State.Chase:
                if (Time.time >= lastTeleportTime + teleportInterval)
                {
                    StartCoroutine(TeleportRoutine());
                    break;
                }
                Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
                rb.linearVelocity = dir * moveSpeed;
                break;

            case State.Attack:
                rb.linearVelocity = Vector2.zero;
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    lastAttackTime = Time.time;
                    animator?.SetTrigger("Attack");
                }
                break;
        }
    }

    // Fade out → teleport near player → fade in
    IEnumerator TeleportRoutine()
    {
        isTeleporting = true;
        rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = false;

        // Phase out — ghost sinks underground
        animator?.SetTrigger("PhaseOut");
        yield return new WaitForSeconds(phaseOutDuration);
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        // Ghost is now underground in the animation — safe to snap position
        float side = Random.value > 0.5f ? 1f : -1f;
        Vector2 targetX = (Vector2)player.position + new Vector2(side * teleportOffset, 1f);
        RaycastHit2D hit = Physics2D.Raycast(targetX, Vector2.down, 10f, LayerMask.GetMask("Ground"));
        Vector2 spawnPos = hit.collider != null
            ? hit.point + Vector2.up * teleportYOffset
            : (Vector2)player.position + new Vector2(side * teleportOffset, teleportYOffset);
        transform.position = spawnPos;

        // Phase in
        animator?.SetTrigger("PhaseIn");
        yield return null; 
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        yield return new WaitForSeconds(phaseInDuration);

        if (col != null) col.enabled = true;
        isTeleporting = false;
        lastTeleportTime = Time.time;
    }
    IEnumerator ForesightTeleportRoutine(bool isLunge, Vector2 escapeDirection)
    {
        isDodging = true;
        isTeleporting = true;
        rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = false;

        animator?.SetTrigger("PhaseOut");
        foresightGlow.SetActive(false);
        yield return new WaitForSeconds(phaseOutDuration);
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        Vector2 targetPos;

        if (isLunge)
        {
            float side = transform.position.x > player.position.x ? 1f : -1f;
            
            targetPos = (Vector2)player.position + new Vector2(side * 1.5f, 0.5f);
            dodgeTimer = dodgeCooldown;
        }
        else
        {
            targetPos = (Vector2)transform.position + (escapeDirection * teleportOffset * 1.5f);
            dodgeTimer = dodgeCooldown;
        }

        RaycastHit2D hit = Physics2D.Raycast(targetPos + Vector2.up * 2f, Vector2.down, 10f, LayerMask.GetMask("Ground"));
        
        Vector2 spawnPos = hit.collider != null
            ? hit.point + Vector2.up * teleportYOffset
            : targetPos + Vector2.up * teleportYOffset;

        transform.position = spawnPos;

        animator?.SetTrigger("PhaseIn");
        yield return null;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        yield return new WaitForSeconds(phaseInDuration);
        foresightGlow.SetActive(true);

        if (col != null) col.enabled = true;
        
        if (col != null && playerCollider != null)
        {
            isTouchingPlayer = Physics2D.IsTouching(col, playerCollider);
        }

        isTeleporting = false;
        isDodging = false;
        lastTeleportTime = Time.time;
    }

    public override void TakeDamage(int amount)
    {
        if (wasDead) return;
        animator?.SetTrigger("Hit");
        StartCoroutine(HitStunRoutine());
        base.TakeDamage(amount);
    }

    IEnumerator HitStunRoutine()
    {
        isHitStunned = true;
        yield return new WaitForSeconds(0.2f);
        isHitStunned = false;
    }

    // Called by Animation Event on the attack clip at the hit frame
    public void GhostDealDamage()
    {
        if (wasDead || isRewinding || !isTouchingPlayer || player == null) return;
        player.GetComponent<PlayerHealth>()?.ModifyHealth(-damage);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) isTouchingPlayer = true;
    }
    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) isTouchingPlayer = false;
    }

    public override void Die()
    {
        wasDead = true;
        StopAllCoroutines();

        animator?.SetTrigger("Die");
        rb.linearVelocity = Vector2.zero;

        if (col != null) col.enabled = false;

        StartCoroutine(base.DeathRoutine());
    }

    // =================== IForesightEnemy Implementation ===================
    public int GetPlayerAttackState()
    {
        if (playerCombat != null && playerCombat.isAttacking) return 1;
        if (playerSpells != null && playerSpells.isCasting) return 2;
        return 0;
    }
    public void SetForesightState(bool state)
    {
        hasForesight = state;
        //if(hasForesight) detectionRange *= 2;
        animator.SetBool("hasForesight", hasForesight);
        if(foresightGlow != null) foresightGlow.SetActive(hasForesight);
        Vector2 direction = (player.position - transform.position).normalized;
        if (direction.x > 0) spriteRenderer.flipX = false;
        else if (direction.x < 0) spriteRenderer.flipX = true;
    }
    new public bool IsDead() => wasDead;
    public bool IsRewinding() => isRewinding;
    public void ExecuteDodge()
    {
        if (wasDead || isRewinding || isTeleporting || isDodging || dodgeTimer > 0) return;

        Vector2 threatPos = playerCollider.bounds.center;
        GameObject spellObj = playerSpells.latestSpell;
        bool shouldDodge = false;

        if (spellObj != null)
        {
            Vector2 spellPos = spellObj.GetComponent<Collider2D>().bounds.center;
            float spellDist = Vector2.Distance(transform.position, spellPos);
            float playerDist = Vector2.Distance(transform.position, threatPos);

            if (spellDist < playerDist && spellDist < dodgeTriggerDistance + 2f)
            {
                threatPos = spellPos;
                shouldDodge = true;
            }
        }
        
        if (!shouldDodge && Vector2.Distance(transform.position, threatPos) < dodgeTriggerDistance)
        {
            shouldDodge = true;
        }

        if (shouldDodge)
        {
            Vector2 escapeDirection = ((Vector2)transform.position - threatPos).normalized;
            StartCoroutine(ForesightTeleportRoutine(false, escapeDirection));
        }
    }

    public void ExecuteLunge()
    {
        if (wasDead || isRewinding || isTeleporting || isDodging || dodgeTimer > 0) return;
        
        StartCoroutine(ForesightTeleportRoutine(true, Vector2.zero));
    }

    public float GetDistanceToPlayer()
    {
        return Vector2.Distance(transform.position, playerCollider.bounds.center);
    }
    public bool IsPerformingForesightAction()
    {
        return isDodging;
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        dodgeTimer = 0f;
        rewindStartTime = Time.time;
        StopAllCoroutines();
        isTouchingPlayer = false;
        isTeleporting = false;
        if (col != null) col.enabled = true;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
        isTeleporting = false;
        isHitStunned = false;
        isRewinding = false;
        if (foresightSystem != null)
        {
            // Calculate how much time passed in the real world while we were rewinding
            float timeRewound = rewindStartTime - TimeRewindManager.Instance.CurrentRewindTime;
            int statesErased = Mathf.RoundToInt(timeRewound / foresightSystem.recordInterval);
            foresightSystem.HandleRewindStop(statesErased);
        }
        if (!wasDead && col != null) col.enabled = true;

        // OnCollisionEnter2D won't fire for pre-existing overlaps after collider is re-enabled,
        // so manually check if the player is already touching the ghost.
        if (!wasDead && col != null && player != null)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            isTouchingPlayer = playerCol != null && Physics2D.IsTouching(col, playerCol);
        }
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("hasDetected", hasDetected);
        state.SetCustomData("isTeleporting", isTeleporting);
        state.SetCustomData("lastTeleportTime", lastTeleportTime);
        state.SetCustomData("colEnabled", col != null && col.enabled);
        state.SetCustomData("spriteEnabled", sprite != null && sprite.enabled);
        state.SetCustomData("isTouchingPlayer", isTouchingPlayer);

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
        hasDetected = state.GetCustomData<bool>("hasDetected");
        isTeleporting = state.GetCustomData<bool>("isTeleporting");
        lastTeleportTime = state.GetCustomData<float>("lastTeleportTime");

        if (col != null)
            col.enabled = state.GetCustomData<bool>("colEnabled", true);

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        isTouchingPlayer = state.GetCustomData<bool>("isTouchingPlayer");

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}
