using UnityEngine;
using TimeRewind;

public class SlimeEnemy : MonoBehaviour, IRewindable
{
    [Header("Stats")]
    public float detectionRange = 5f;
    public float loseRange = 15f;
    public float moveSpeed = 2f;
    public float attackCooldown = 1.5f;
    public int damage = 1;
    public int health = 3;

    [Header("Hop Settings")]
    public float hopForce = 3f;
    public float hopCooldown = 1f;

    public Transform player;

    private float lastAttackTime;
    private float lastHopTime;
    private Vector3 originalScale;
    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private bool playerInContact = false;
    private bool isGrounded = false;

    // Rewind
    private bool isRewinding = false;
    private RigidbodyType2D originalBodyType;
    private bool wasDead = false;

    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;

    void Start()
    {
        originalScale = transform.localScale;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        TimeRewindManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Unregister(this);
        }
    }

    void Update()
    {
        // Don't run AI during rewind
        if (isRewinding) return;
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (playerInContact)
            currentState = State.Attack;
        else if (distanceToPlayer < detectionRange)
            currentState = State.Chase;
        else if (currentState == State.Chase && distanceToPlayer < loseRange)
            currentState = State.Chase;
        else
            currentState = State.Idle;

        switch (currentState)
        {
            case State.Idle:
                Idle();
                break;
            case State.Chase:
                Chase();
                break;
            case State.Attack:
                Attack();
                break;
        }
    }

    void Idle() { }

    void Chase()
{
    Vector2 direction = (player.position - transform.position).normalized;

    if (direction.x > 0)
        spriteRenderer.flipX = false;
    else if (direction.x < 0)
        spriteRenderer.flipX = true;

    if (isGrounded && Time.time >= lastHopTime + hopCooldown)
    {
        animator.SetTrigger("hop");
        rb.linearVelocity = new Vector2(direction.x * moveSpeed, hopForce);
        lastHopTime = Time.time;
        isGrounded = false;
    }
}

    void Attack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Debug.Log("Slime attacks!");
            lastAttackTime = Time.time;

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ModifyHealth(-damage);
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerInContact = true;
        }

        if (collision.contacts[0].normal.y > 0.5f)
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerInContact = false;
        }
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        Debug.Log("Slime took damage! Health: " + health);

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        wasDead = true;
        animator.SetTrigger("die");
        enabled = false;
        rb.linearVelocity = Vector2.zero;
        Destroy(gameObject, 0.6f);
    }

    // ---- IRewindable Implementation ----

    public void OnStartRewind()
    {
        isRewinding = true;
        originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // Cancel any pending destroy
        CancelInvoke();

        // Re-enable if we were dead
        if (!enabled)
        {
            enabled = true;
            wasDead = false;
        }
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        rb.bodyType = originalBodyType;
    }

    public RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            rb.linearVelocity,
            rb.angularVelocity,
            Time.time
        );

        state.Health = health;
        state.SetCustomData("flipX", spriteRenderer.flipX);
        state.SetCustomData("isGrounded", isGrounded);

        // Save animator state
        var animState = animator.GetCurrentAnimatorStateInfo(0);
        state.AnimatorStateHash = animState.fullPathHash;
        state.AnimatorNormalizedTime = animState.normalizedTime;

        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        health = state.Health;

        spriteRenderer.flipX = state.GetCustomData<bool>("flipX");
        isGrounded = state.GetCustomData<bool>("isGrounded");

        // Restore animation
        animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
        animator.Update(0f);
    }
}