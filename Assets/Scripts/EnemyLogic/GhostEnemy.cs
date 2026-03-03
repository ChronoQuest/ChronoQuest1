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

    [Header("Phase-in")]
    public float phaseInDuration = 0.6f; // Fade in when first detecting player

    public Transform player;

    private Animator animator;
    private float lastAttackTime;
    private bool isTouchingPlayer;
    private bool hasDetected;
    private bool isHitStunned;

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
        // Ghosts float — no gravity
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        if (isRewinding || wasDead) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (isTouchingPlayer)
            currentState = State.Attack;
        else if (dist < detectionRange)
        {
            if (!hasDetected)
            {
                hasDetected = true;
                StartCoroutine(PhaseInRoutine());
            }
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
        if (isRewinding || wasDead || isHitStunned) return;

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude);
                break;

            case State.Chase:
                Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
                rb.linearVelocity = dir * moveSpeed;
                break;

            case State.Attack:
                rb.linearVelocity = Vector2.zero;
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    lastAttackTime = Time.time;
                    animator?.SetTrigger("Attack");
                    player.GetComponent<PlayerHealth>()?.ModifyHealth(-damage);
                }
                break;
        }
    }

    // Fade in from transparent when the ghost detects the player
    IEnumerator PhaseInRoutine()
    {
        Color c = sprite.color;
        c.a = 0f;
        sprite.color = c;

        float elapsed = 0f;
        while (elapsed < phaseInDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Clamp01(elapsed / phaseInDuration);
            sprite.color = c;
            yield return null;
        }

        c.a = 1f;
        sprite.color = c;
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

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(base.DeathRoutine());
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isTouchingPlayer = false;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("hasDetected", hasDetected);
        state.SetCustomData("spriteAlpha", sprite.color.a);
        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);
        hasDetected = state.GetCustomData<bool>("hasDetected");

        Color c = sprite.color;
        c.a = state.GetCustomData<float>("spriteAlpha");
        sprite.color = c;
    }
}
