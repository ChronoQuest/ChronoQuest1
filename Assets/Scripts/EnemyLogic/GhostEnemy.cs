using UnityEngine;
using System.Collections;
using TimeRewind;

public class GhostEnemy : EnemyBase
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

    public Transform player;

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

    protected override void Awake()
    {
        health = 2;
        base.Awake();
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
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
        yield return new WaitForSeconds(phaseInDuration);

        if (col != null) col.enabled = true;
        isTeleporting = false;
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
        if (collision.gameObject.CompareTag("Player"))
            isTouchingPlayer = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            isTouchingPlayer = false;
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

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isTouchingPlayer = false;
        isTeleporting = false;
        if (col != null) col.enabled = true;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("hasDetected", hasDetected);
        state.SetCustomData("isTeleporting", isTeleporting);
        state.SetCustomData("lastTeleportTime", lastTeleportTime);
        state.SetCustomData("colEnabled", col != null && col.enabled);
        state.SetCustomData("spriteEnabled", sprite != null && sprite.enabled);

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

        if (animator != null)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}
