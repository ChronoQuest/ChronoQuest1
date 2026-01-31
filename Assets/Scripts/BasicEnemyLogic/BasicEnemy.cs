using UnityEngine;

public class BasicEnemy : MonoBehaviour
{
    public float detectionRange = 5f;
    public float attackRange = 1f;
    public float moveSpeed = 2f;
    public float attackCooldown = 1.5f;
    public int damage = 1;
    public int health = 3;

    public Transform player;

    private float lastAttackTime;
    private Vector3 originalScale;
    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;

    void Start()
    {
        originalScale = transform.localScale;
    }

    void Update()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer > detectionRange)
        {
            currentState = State.Idle;
        }
        else if (distanceToPlayer > attackRange)
        {
            currentState = State.Chase;
        }
        else
        {
            currentState = State.Attack;
        }

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
    }

    void Chase()
    {
        Vector2 direction = (player.position - transform.position).normalized;
        transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);

        // flip sprite while keeping original scale
        if (direction.x > 0)
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        else if (direction.x < 0)
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    void Attack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Debug.Log("Enemy attacks!");
            lastAttackTime = Time.time;

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