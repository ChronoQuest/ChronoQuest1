using UnityEngine;

public class FlyingEnemy : MonoBehaviour
{
    public float detectionRange = 10f;
    public float attackRange = 1f;
    public float moveSpeed = 5f;
    public float attackCooldown = 1.5f;
    public int damage = 1;
    public int health = 3;

    public Transform player;
    public float hoverFrequency = 2f; // Bob speed
    public float hoverAmplitude = 0.5f; // Max bob height

    private float lastAttackTime;
    private Rigidbody2D rb;
    private Vector3 originalScale;
    private Vector2 movement; // Store movement to apply in FixedUpdate
    private Collider2D playerCollider;
    
    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        playerCollider = player.GetComponent<Collider2D>();
    }

void Update()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, playerCollider.bounds.center);

        if (distanceToPlayer > detectionRange)
        {
            currentState = State.Idle;
        }
        else
        {
            currentState = State.Chase;
        }

        if (currentState == State.Chase)
        {
            FacePlayer();
        }
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject == player.gameObject)
        {
            Attack();
        }
    }

    void FixedUpdate()
    {
        switch (currentState)
        {
            case State.Idle:
                Hover();
                break;
            case State.Chase:
                Chase();
                break;
            case State.Attack:
                // Stop moving when attacking
                rb.linearVelocity = Vector2.zero; 
                Attack();
                break;
        }
    }

    void Hover()
    {
        // Simple Sine wave bobbing effect
        float newY = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        rb.linearVelocity = new Vector2(0, newY); 
    }

    void Chase()
    {
        // Enemy's body type should be kinematic - different movement system  
        Vector2 newPosition = Vector2.MoveTowards(rb.position, playerCollider.bounds.center, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);
    }

    void FacePlayer()
    {
         if (player.position.x > transform.position.x)
             transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
         else
             transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    void Attack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Debug.Log("Enemy attacks!");
            lastAttackTime = Time.time;

            // Assuming you have the same PlayerHealth script
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ModifyHealth(-damage);
            }
        }
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        Debug.Log("Enemy took damage! Health: " + health);

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Enemy died!");
        Destroy(gameObject);
    }
}