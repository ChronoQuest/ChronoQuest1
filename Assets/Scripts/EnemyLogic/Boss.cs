using TimeRewind;
using UnityEngine;

public class Boss : MonoBehaviour, IRewindable
{
    public float attackCooldown = 1f;
    public float damageCooldown = 1.5f;
    public int damage = 1;
    public int health = 3;

    public Transform player;
    public BossAttackManager attackManager;
    private bool _isRewinding;
    private Rigidbody2D rb;
    private float lastDamageTime;
    private float lastAttackTime;
    private Vector3 originalScale;
    private Animator animator;
    private bool playerInContact = false;
    private enum State { Fight, Damage }
    private State currentState = State.Fight;

    void Start()
    {
        originalScale = transform.localScale;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Register(this);
    }

    void Update()
    {
        if (player == null) return;
        if (_isRewinding) return;
        // Use collision for attack, distance for chase
        if (playerInContact)
            currentState = State.Damage;

        switch (currentState)
        {
            case State.Fight:
                Fight();
                break;
            case State.Damage:
                Damage();
                break;
        }
    }

    void Fight()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            Attack();
        }
    }

    void Attack()
    {
        Fireballs();
    }

    void Fireballs()
    {
        attackManager.spawnFireball();
    }

    void Damage()
    {
        if (Time.time >= lastDamageTime + damageCooldown)
        {
            Debug.Log("Boss damages!");
            lastDamageTime = Time.time;

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ModifyHealth(-damage);
            }
        }
    }

    // Called when colliders touch
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerInContact = true;
        }
    }

    // Called when colliders separate
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
        Debug.Log("Boss took damage! Health: " + health);

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Boss died!");
        Destroy(gameObject);
    }

    public void OnStartRewind()
    {
        _isRewinding = true; // Sets the flag that stops Update/FixedUpdate
    }

    public void OnStopRewind()
    {
        _isRewinding = false;
    }

    public RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            (rb != null) ? rb.linearVelocity : Vector2.zero,
            (rb != null) ? rb.angularVelocity : 0f,
            Time.time
        );

        // Save data using Dictionary
        state.Health = health;
        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        health = state.Health;

    }
}