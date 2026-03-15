using System.Collections;
using UnityEngine;
using TimeRewind;

public class SkeletonArcher : EnemyBase, IBossSpawnable, IForesightEnemy
{
    [Header("Detection")]
    public float detectionRange = 8f;
    public float shootRange = 6f;
    public float safeDistance = 3f;

    [Header("Movement")]
    public float retreatSpeed = 2f;

    [Header("Shooting")]
    public int damage = 1;
    public float shootCooldown = 2f;

    [Header("References")]
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
    public GameObject arrowPrefab;
    [Header("Arrow Pool")]
    public int arrowPoolSize = 5;

    [Header("Arrow Spawn")]
    public Vector2 arrowSpawnOffset = new Vector2(0.3f, 0.5f);

    private Animator animator;
    private Collider2D col;

    private enum State { Idle, Retreat, Shoot, Hit, Dead }
    [SerializeField] private State currentState = State.Idle;

    private float lastShootTime;
    private Vector3 originalScale;
    private Vector2 pendingArrowDirection;

    private ArrowProjectile[] arrowPool;
    private RewindState? _lastAppliedState;
    private Collider2D playerCollider;
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;
    [Header("Foresight")]
    public float dodgeTriggerDistance = 5f;
    public GameObject foresightGlow;
    private bool isDodging = false;    
    private float dodgeDuration = 0.5f;
    private ForesightSystem foresightSystem;
    private float rewindStartTime;
    private bool hasForesight = false;
    private bool isMidJumpSequence = false;
    private bool isGrounded = true;
    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = player.GetComponent<Collider2D>();
        playerCombat = player.GetComponent<PlayerCombat>();
        playerSpells = player.GetComponent<PlayerSpellSystem>();
        foresightSystem = GetComponent<ForesightSystem>();
        originalScale = transform.localScale;
        stunOnLand = true;
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
        if (isRewinding || wasDead || isStunned || isLaunched) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist < safeDistance)
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
        if (isRewinding || wasDead || isStunned || isLaunched) return;

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
        if (wasDead || isRewinding || isLaunched || isStunned) return;

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

    // ================= REVIVE =================

    [Header("Revive")]
    public float reviveAnimDuration = 0.9f;

    public override void Revive()
    {
        base.Revive();
        if (animator != null) animator.SetTrigger("Revive");
        StartCoroutine(ReviveStunRoutine());
    }

    IEnumerator ReviveStunRoutine()
    {
        isStunned = true;
        yield return new WaitForSeconds(reviveAnimDuration);
        isStunned = false;
    }

    // ================= DEATH =================

    public override void Die()
    {
        base.Die();
        StopAllCoroutines();
        if (animator != null) animator.SetTrigger("Dead");
    }

    // ================= DAMAGE =================
    public override void TakeDamage(int amount)
    {
        if (isDodging) return;
        base.TakeDamage(amount);
        if (foresightSystem != null) foresightSystem.NotifyDamage();
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
        if(hasForesight) detectionRange *= 2;
        animator.SetBool("hasForesight", hasForesight);
        if(foresightGlow != null) foresightGlow.SetActive(hasForesight);
        Vector2 direction = (player.position - transform.position).normalized;
        if (direction.x > 0) spriteRenderer.flipX = false;
        else if (direction.x < 0) spriteRenderer.flipX = true;
    }
    new public bool IsDead() => wasDead;
    public bool IsRewinding() => isRewinding;
    public void ExecuteLunge()
    {
        if (isDodging) return;
        isDodging = true;
        if (Time.time < lastShootTime + shootCooldown)
            return;

        ArrowProjectile arrow = GetPooledArrow();
        if (arrow == null) return;

        lastShootTime = Time.time;

        Vector3 spawnPos = transform.position + Vector3.up * arrowSpawnOffset.y;

        arrow.transform.position = spawnPos;
        arrow.gameObject.SetActive(true);

        Vector2 dir = (player.position - transform.position).normalized;

        arrow.LaunchHoming(dir, damage, player);
        isDodging = false;
    }
public void ExecuteDodge()
    {
        if (isDodging) return; // Prevent dodging if already in a dodge state

        GameObject spellObj = playerSpells.latestSpell;
        bool shouldDodge = false;

        // Check if player or spell is close enough to trigger the dodge
        if (Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance - 1.5f)
        {
            shouldDodge = true;
        }
        else if (spellObj != null)
        {
            Vector2 spellPos = spellObj.GetComponent<Collider2D>().bounds.center;
            if (Vector2.Distance(transform.position, spellPos) < dodgeTriggerDistance + 1.5f)
            {
                shouldDodge = true;
            }
        }

        if (shouldDodge)
        {
            StopAllCoroutines(); 
            StartCoroutine(PhaseDodgeRoutine());
        }
    }

    IEnumerator PhaseDodgeRoutine()
    {
        isDodging = true;
        int originalLayer = gameObject.layer;
        gameObject.layer = LayerMask.NameToLayer("EnemyDodging");
        
        animator.SetBool("hasForesight", true);
        if (foresightGlow != null) foresightGlow.SetActive(true);

        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);

        // Do a little jump to show dodging
        rb.linearVelocity = new Vector2(0f, 3f);

        yield return new WaitForSeconds(dodgeDuration);

        
        spriteRenderer.color = originalColor;
        animator.SetBool("hasForesight", false);
        if (foresightGlow != null) foresightGlow.SetActive(false);
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

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        rewindStartTime = Time.time;
        StopAllCoroutines();
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
        if (foresightSystem != null)
        {
            // Calculate how much time passed in the real world while we were rewinding
            float timeRewound = rewindStartTime - TimeRewindManager.Instance.CurrentRewindTime;
            int statesErased = Mathf.RoundToInt(timeRewound / foresightSystem.recordInterval);
            foresightSystem.HandleRewindStop(statesErased);
        }
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();

        state.SetCustomData("EnemyState", (int)currentState);
        state.SetCustomData("FacingDirection", transform.localScale);
        if (animator != null)
        {
            AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animInfo.shortNameHash;
            state.AnimatorNormalizedTime = animInfo.normalizedTime;
        }

        return state;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if we hit a floor layer or something tagged as ground
        foreach (ContactPoint2D contact in collision.contacts)
        {
            // If the surface normal is pointing up, it's a floor
            if (contact.normal.y > 0.7f)
            {
                // If we were flying from a knockback, trigger the stun now
                if (isLaunched && stunOnLand)
                {
                    StartCoroutine(HitStunRoutine(0.5f)); // This sets isStunned = true and isLaunched = false
                }
                isGrounded = true;
            }
        }
    }

    public override void ApplyKnockback(Vector2 force)
    {
        if (isDodging) return;
        StopAllCoroutines();
    
        base.ApplyKnockback(force);

        if (animator != null) 
        {
            animator.SetTrigger("Hit"); 
        }
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);
        _lastAppliedState = state;

        currentState = (State)state.GetCustomData<int>("EnemyState", (int)State.Idle);
        transform.localScale = state.GetCustomData<Vector3>("FacingDirection", originalScale);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}
