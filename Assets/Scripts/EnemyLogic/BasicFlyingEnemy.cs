using UnityEngine;
using TimeRewind;

public class FlyingEnemy : EnemyBase
{
    [Header("Flying Stats")]
    public float detectionRange = 10f;
    public float attackRange = 1f;
    public float moveSpeed = 5f;
    public float attackCooldown = 1.5f;
    public int damage = 1;

    [Header("Hovering")]
    public float hoverFrequency = 2f; 
    public float hoverAmplitude = 0.5f; 
    
    public enum State { Sleeping, Idle, Chase, Attack }
    public State currentState = State.Idle;

    [Header("References")]
    public Transform player;
    private Animator animator;
    private Collider2D playerCollider;

    private float lastAttackTime;
    private Vector3 originalScale;
    private bool isTouchingPlayer = false;
    private RewindState? _lastAppliedState;

    void Start()
    {
        // rb, sprite, and flash are inherited and assigned in EnemyBase.Awake()
        originalScale = transform.localScale;
        playerCollider = player.GetComponent<Collider2D>();
        animator = GetComponent<Animator>();

        animator.ResetTrigger("Chase");
        animator.ResetTrigger("Attack");
    }

    // No OnEnable/OnDisable needed here; EnemyBase handles Rewind registration

    void Update()
    {
        if (isRewinding || isStunned) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerCollider.bounds.center);

        if (isTouchingPlayer) 
        {
            currentState = State.Attack;
        }
        else if (distanceToPlayer > detectionRange)
        {
            if (currentState != State.Sleeping) currentState = State.Idle;
        }
        else
        {
            if (currentState == State.Sleeping) detectionRange += 2f;
            currentState = State.Chase;
        }

        if (currentState == State.Chase) FacePlayer();
    }

    void FixedUpdate()
    {
        if (isRewinding || isStunned) return;

        switch (currentState)
        {
            case State.Idle:
                Hover();
                break;
            case State.Chase:
                Chase();
                break;
            case State.Attack:
                rb.linearVelocity = Vector2.zero; 
                Attack();
                break;
        }
    }

    void Hover()
    {
        float newY = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        rb.linearVelocity = new Vector2(0, newY); 
    }

    void Chase()
    {
        animator.SetTrigger("Chase");
        Vector2 newPosition = Vector2.MoveTowards(rb.position, playerCollider.bounds.center, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);
    }

    void FacePlayer()
    {
        // Flip scale based on player position relative to us
        float flip = (player.position.x > transform.position.x) ? -1 : 1;
        transform.localScale = new Vector3(flip * Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    void Attack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            animator.SetTrigger("Attack");

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null) playerHealth.ModifyHealth(-damage);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) isTouchingPlayer = true;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) isTouchingPlayer = false;
    }

    // ================= REWIND OVERRIDES =================

    public override void OnStartRewind()
    {
        base.OnStartRewind(); // Handles kinematic switch and velocity reset
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind(); // Restores original body type
        
        if (originalBodyType == RigidbodyType2D.Dynamic && _lastAppliedState != null)
        {
            rb.linearVelocity = _lastAppliedState.Value.Velocity;
        }
        
        if(currentState == State.Chase) animator.SetTrigger("Chase");
    }

    public override RewindState CaptureState()
    {
        // Start with the base state (Physics/Health)
        var state = base.CaptureState();

        // Add Bat-specific data
        state.SetCustomData("EnemyState", (int)currentState);
        state.SetCustomData("DetectRange", detectionRange);
        state.SetCustomData("FacingDirection", transform.localScale);

        AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
        state.AnimatorStateHash = animInfo.shortNameHash;
        state.AnimatorNormalizedTime = animInfo.normalizedTime;
        
        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state); // Restores Pos/Rot/Health
        _lastAppliedState = state;

        currentState = (State)state.GetCustomData<int>("EnemyState", (int)State.Idle);
        detectionRange = state.GetCustomData<float>("DetectRange", 10f);
        transform.localScale = state.GetCustomData<Vector3>("FacingDirection", originalScale);

        animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}