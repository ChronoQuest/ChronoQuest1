using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using TimeRewind;
using log4net.DateFormatter;
using UnityEditor.Rendering;
using Unity.Collections;
public class BatEnemyAI : Agent, IRewindable
{
    [Header("Mode")]
    [Tooltip("Training mode")]
    public bool trainingMode = false;

    [Header("References")]
    public Transform player;
    public float hoverFrequency = 2f; // Bob speed
    public float hoverAmplitude = 0.5f; // Max bob height
    private Collider2D playerCollider;
    // Eventually to be used for 'flanking'
    public Transform otherBat;

    private Rigidbody2D rb;
    private Vector3 startPos;

    public float moveSpeed = 5f;
    private Animator animator;
    public float detectionRange = 1f;
    public int damage = 1;
    public int health = 3;
    public float attackCooldown = 1.5f;
    private float lastAttackTime;
    private bool _isRewinding;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private Vector3 originalScale;

    public enum State { Sleeping, Idle, Chase }
    public State currentState = State.Idle;
    public Transform obstacle;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        playerCollider = player.GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        animator.ResetTrigger("Chase");
        animator.ResetTrigger("Attack");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Unregister(this);
        }
    }
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // Disable gravity
            rb.gravityScale = 0f;
            // Start bat in a random position every time, in the air
            float randBatX = Random.Range(-6f, 14f);
            float randBatY = Random.Range(1f, 6f);
            transform.localPosition = new Vector2(randBatX, randBatY);
            rb.linearVelocity = Vector2.zero;

            // Spawn the player in a random position, just above the floor
            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            playerRb.gravityScale = 0f; 
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
            float randPlayerX = Random.Range(-6f, 14f);
            // Player height slightly skewed to bottom, as that is most common
            float randPlayerY = Random.Range(1f, 6f);
            player.localPosition = new Vector2(randPlayerX, randPlayerY);

            // Move the obstacle to a random location
            float randX = Random.Range(-6f, 10f);
            float randY = Random.Range(1f, 6f);
            // 50% chance to flip obstacle
            Vector3 newScale = obstacle.localScale;
            if (randY <= 3.5f) {
                newScale.x = -1f;
            } else newScale.x = 1f;
            obstacle.localScale = newScale; 
            obstacle.localPosition = new Vector2(randX, randY);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Get distance to player
        Vector2 toPlayer = playerCollider.bounds.center - transform.position;
        Vector2 otherBatToPlayer = playerCollider.bounds.center - otherBat.transform.position;

        bool either_side_of_player =  transform.localScale != otherBat.transform.localScale;
    
        // Give the AI access to the player's position
        sensor.AddObservation(toPlayer.x); 
        sensor.AddObservation(toPlayer.y);
        // Access to other bat's position
        sensor.AddObservation(otherBatToPlayer.x);
        sensor.AddObservation(otherBatToPlayer.y);

        // Let the bat know its speed
        sensor.AddObservation(rb.linearVelocity.x);
        sensor.AddObservation(rb.linearVelocity.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_isRewinding) return;

        float distToPlayer = Vector2.Distance(transform.position, playerCollider.bounds.center);
        
        if (!trainingMode && distToPlayer > detectionRange)
        {
            // If the enemy is sleeping and outside range, continue to sleep
            if (currentState == State.Sleeping) currentState = State.Sleeping;
            // If awake, continue to be awake
            else {
                currentState = State.Idle;
                Hover();
            }
            return;
        }
        // Sleeping bat has been awoken!
        if (currentState == State.Sleeping) detectionRange += 5;
        
        currentState = State.Chase;

        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];

        Vector2 dir = new Vector2(moveX, moveY);
        rb.linearVelocity = dir * moveSpeed;

        FacePlayer();
        animator.SetTrigger("Chase");

        if (trainingMode)
        {
            // Make sure speed is taken into account (longer, less reward)
            AddReward(-0.001f);
            // Reset and punish if bat gets too far away
            if(distToPlayer > 15f)
            {
                AddReward(-0.5f);
                EndEpisode();
                return;
            }
            float batOffsetToPlayerX = transform.position.x - playerCollider.bounds.center.x;
            float otherBatOffsetToPlayerX = otherBat.transform.position.x - playerCollider.bounds.center.x;
            // Bonus for flanking. A bat is no longer punished if the other gets stuck
            if (batOffsetToPlayerX * otherBatOffsetToPlayerX < 0f)
            {
                AddReward(0.002f);
            }
            // Reward for being close to player
            AddReward(-0.001f * distToPlayer);
        }
    }
    void Hover()
    {
        // Simple Sine wave bobbing effect
        float newY = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        rb.linearVelocity = new Vector2(0, newY); 
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(trainingMode){
            // The main goal is to damage the player, so reward them and end episode
            if (collision.gameObject.CompareTag("Player"))
            {
                AddReward(1.0f);
                EndEpisode();
            }
        }
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!trainingMode && collision.gameObject.CompareTag("Player"))
        {
            rb.linearVelocity = Vector2.zero; 
            Attack();
        }
        if (trainingMode)
        {
            // If the bat hits a wall, punish them continuously
            if (collision.gameObject.CompareTag("Ground"))
            {
                AddReward(-0.01f);
            }
        }
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

    void FacePlayer()
    {
        if (player.position.x > transform.position.x)
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        else
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
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