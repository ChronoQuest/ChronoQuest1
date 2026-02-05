using UnityEngine;
using TimeRewind;
public class FlyingEnemy : MonoBehaviour, IRewindable
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
    public enum State { Sleeping, Idle, Chase, Attack }
    public State currentState = State.Idle;

    private float lastAttackTime;
    private Rigidbody2D rb;
    private Vector3 originalScale;
    private Vector2 movement; // Store movement to apply in FixedUpdate
    private Collider2D playerCollider;
    private Animator animator;
    private bool isTouchingPlayer = false;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private bool _isRewinding;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        playerCollider = player.GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        animator.ResetTrigger("Chase");
        animator.ResetTrigger("Attack");
    }

    private void OnEnable()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }
    }

    private void OnDisable()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Unregister(this);
        }
    }

void Update()
    {
        if (_isRewinding) return;
        float distanceToPlayer = Vector2.Distance(transform.position, playerCollider.bounds.center);

        if (isTouchingPlayer) 
        {
            currentState = State.Attack;
        }
        else if (distanceToPlayer > detectionRange)
        {
            // If the enemy is sleeping and outside range, continue to sleep
            if (currentState == State.Sleeping) currentState = State.Sleeping;
            // If awake, continue to be awake
            else currentState = State.Idle;
        }
        else
        {
            // If enemy has awoken, increase detection range
            if (currentState == State.Sleeping) detectionRange += (float)2;
            currentState = State.Chase;
        }

        if (currentState == State.Chase)
        {
            FacePlayer();
        }
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject == player.gameObject)
        {
            isTouchingPlayer = true;
        }
    }

        private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject == player.gameObject)
        {
            isTouchingPlayer = false;
        }
    }

    void FixedUpdate()
    {
        if (_isRewinding) return;
        switch (currentState)
        {
            case State.Sleeping:
                break;
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
        animator.SetTrigger("Chase");
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
            animator.SetTrigger("Attack");

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

    public void OnStartRewind()
    {
        _isRewinding = true; // Sets the flag that stops Update/FixedUpdate

        // Make Rigidbody Kinematic so physics doesn't interfere
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void OnStopRewind()
    {
        _isRewinding = false;

        // Restore physics
        rb.bodyType = _originalBodyType;

        if (_originalBodyType == RigidbodyType2D.Dynamic)
        {
            rb.linearVelocity = _lastAppliedState.Velocity;
            rb.angularVelocity = _lastAppliedState.AngularVelocity;
        }
        if(currentState == State.Chase) animator.SetTrigger("Chase");
        else animator.ResetTrigger("Chase");
    }

public RewindState CaptureState()
    {
        // Create physics state
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            (rb != null) ? rb.linearVelocity : Vector2.zero,
            (rb != null) ? rb.angularVelocity : 0f,
            Time.time
        );

        // Save data using Dictionary
        state.Health = health;
        state.SetCustomData("EnemyState", (int)currentState);
        state.SetCustomData("DetectRange", detectionRange);
        state.SetCustomData("FacingDirection", transform.localScale);

        AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
        state.AnimatorStateHash = animInfo.shortNameHash;
        state.AnimatorNormalizedTime = animInfo.normalizedTime;
        
        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;

        health = state.Health;
        
        currentState = (State)state.GetCustomData<int>("EnemyState", (int)State.Idle);
        
        detectionRange = state.GetCustomData<float>("DetectRange", 10f);

        transform.localScale = state.GetCustomData<Vector3>("FacingDirection", new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z));

        animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}