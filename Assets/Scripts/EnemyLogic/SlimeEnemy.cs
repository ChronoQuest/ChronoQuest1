using UnityEngine;

public class SlimeEnemy : MonoBehaviour
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
    private bool playerInContact = false;
    private bool isGrounded = false;
    private SpriteRenderer spriteRenderer;


    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;

    void Start()
    {
    originalScale = transform.localScale;
    animator = GetComponent<Animator>();
    rb = GetComponent<Rigidbody2D>();
    spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
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

    void Idle()
    {
        animator.SetBool("isHopping", false);
    }

    void Chase()
    {
    Vector2 direction = (player.position - transform.position).normalized;
    
    // Flip sprite to face player
    if (direction.x > 0)
        spriteRenderer.flipX = false;
    else if (direction.x < 0)
        spriteRenderer.flipX = true;

    // Hop toward player
    if (isGrounded && Time.time >= lastHopTime + hopCooldown)
        {
        animator.SetBool("isHopping", true);
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

        // Check if landed on ground
        if (collision.contacts[0].normal.y > 0.5f)
        {
            isGrounded = true;
            animator.SetBool("isHopping", false);
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
        animator.SetTrigger("die");
        enabled = false;
        rb.linearVelocity = Vector2.zero;
        Destroy(gameObject, 0.6f);
    }
}