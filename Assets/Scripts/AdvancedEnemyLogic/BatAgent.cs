using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using TimeRewind;
using System.Collections.Generic;
using System.Linq;
public class BatEnemyAI : Agent, IRewindable
{
    private EnemyBase enemy;
    [Header("Mode")]
    [Tooltip("Training mode")]
    public bool trainingMode = false;

    [Header("References")]
    public Transform player;
    public float hoverFrequency = 2f; // Bob speed
    public float hoverAmplitude = 0.5f; // Max bob height
    private bool isRewinding = false;
    private bool isDead = false;
    private bool isDodging = false;
    private bool hasForesight = true;
    private float dodgeDuration = 0.5f;
    private float dodgeTimer = 0f;
    private Vector2 calculatedDodgeVector;
    public float sequenceSimilarity = 0f;
    public float foresightThreshold = 0.75f;
    public float dodgeTriggerDistance = 3f;
    private float previousPlayerDistance;
    private List<PlayerTactic> currentPlayerTimeline;
    private List<PlayerTactic> previousPlayerTimeline = new List<PlayerTactic>();
    public float recordInterval = 0.5f;
    private float recordTimer = 0f;
    private Collider2D playerCollider;
    private SpriteRenderer spriteRenderer;
    public Transform otherBat;

    private Rigidbody2D rb;
    public float moveSpeed = 5f;
    private Animator animator;
    public float detectionRange = 1f;
    public int damage = 1;
    public float attackCooldown = 1.5f;
    private float lastAttackTime;
    private Vector3 originalScale;

    public enum State { Sleeping, Idle, Chase }
    public State currentState = State.Idle;
    public Transform obstacle;
    public bool controlsEnvironment = false;
    private BatEnemyAI partnerAgent;
    // Possible 'tactics' a player could be employing
    public enum PlayerTactic {Idle, Approaching, Retreating, AttackingClose, AttackingFar, Airborne}
    private PlayerPlatformer playerPlatformer;
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;


    void Start()
    {
        enemy = GetComponent<EnemyBase>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemy.OnDeath += HandleDeath;
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Unregister(enemy); 
            TimeRewindManager.Instance.Register(this);    
        }
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        playerCollider = player.GetComponent<Collider2D>();
        playerPlatformer = player.GetComponent<PlayerPlatformer>();
        playerCombat = player.GetComponent<PlayerCombat>();
        playerSpells = player.GetComponent<PlayerSpellSystem>();
        currentPlayerTimeline = new List<PlayerTactic>();
        previousPlayerDistance = Vector2.Distance(transform.position, player.position);
        animator = GetComponent<Animator>();
        animator.ResetTrigger("Chase");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("die");
        if(otherBat != null) partnerAgent = otherBat.GetComponent<BatEnemyAI>();
    }

    void Update()
    {
        if (isDead || isRewinding || trainingMode) return;
        hasForesight = true;
        animator.SetBool("hasForesight", true);

        if (currentState == State.Chase || true /* FOR TESTING */)
        {
            recordTimer += Time.deltaTime;
            if (recordTimer >= recordInterval)
            {
                PlayerTactic tactic = GetCurrentPlayerTactic();
                Debug.Log(tactic);
                currentPlayerTimeline.Add(tactic);
                sequenceSimilarity = CalculateSimilarity();
                recordTimer = 0f;
            }
        }

        // Player is behaving similarly (within a threshold) to before
        if (sequenceSimilarity >= foresightThreshold || hasForesight)
        {
            if (!hasForesight)
            {
                hasForesight = true;
                animator.SetBool("hasForesight", true);
            }
            GameObject spellObj = GameObject.FindWithTag("Spell");

            if (!isDodging && Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance)
            {
                Vector2 approachDirection = (playerCollider.bounds.center - transform.position).normalized;
                
                TriggerForesightDodge(approachDirection);
            }
            if (!isDodging && spellObj != null)
            {
                //Rigidbody2D spellRb = spellObj.GetComponent<Rigidbody2D>();
                //Vector2 approachDirection = (spellRb.position - (Vector2)transform.position).normalized;
                Vector2 approachDirection = (playerCollider.bounds.center - transform.position).normalized;
                TriggerForesightDodge(approachDirection);
            }
        }
        else
        {
            // Player is behaving differently
            if (hasForesight && !isDodging)
            {
                //hasForesight = false;
                //animator.SetBool("hasForesight", false);
            }
        }
    }

    float CalculateSimilarity()
    {
        int index = currentPlayerTimeline.Count - 1;
        if (index < 0 || index >= previousPlayerTimeline.Count)
            return 0f;
        PlayerTactic present = currentPlayerTimeline.Last();
        PlayerTactic past = previousPlayerTimeline[index];
        if (past == present) {
            return 1f;
        }
        return 0f;  
    }
    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Unregister(this);
        }
    }

    public override void OnEpisodeBegin()
    {
        isDead = false;
        if (trainingMode)
        {
            // Disable gravity
            rb.gravityScale = 0f;
            // Start bat in a random position every time, in the air
            float randBatX = Random.Range(-6f, 13f);
            float randBatY = Random.Range(-2f, 8f);
            transform.localPosition = new Vector2(randBatX, randBatY);
            rb.linearVelocity = Vector2.zero;

            if (controlsEnvironment)
            {
                // Move the obstacle to a random location
                float randX;
                float randY = Random.Range(-4f, 2f);
                // 50% chance to flip obstacle
                Vector3 newScale = obstacle.localScale;
                if (Random.value > 0.5f) {
                    newScale.x = -1f;
                    randX = Random.Range(1f, 13f);
                } else {
                    newScale.x = 1f;
                    randX = Random.Range(-6f, 6f);
                }
                obstacle.localScale = newScale; 
                obstacle.localPosition = new Vector2(randX, randY);

                // Spawn the player in a random position, just above the floor
                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                float randPlayerX;
                float randPlayerY;
                
                // 50% chance to be in the air
                if (Random.value > 0.7f) 
                {
                    // 70% chance to be on a platform in the air
                    if(Random.value > 0.3f)
                    {
                        randPlayerY = randY + 6f;
                        // Account for flipping of obstacle
                        if(newScale.x == 1){
                            randPlayerX = Random.Range(randX - 1.5f, randX + 1.5f) + 5f;
                        }
                        else
                        {
                            randPlayerX = Random.Range(randX - 1.5f, randX + 1.5f) + 3f;
                        }
                    // 30% chance to be flying (mid jump)
                    } else {
                        randPlayerX = Random.Range(-6f, 13f);
                        randPlayerY = Random.Range(0f, 6f);
                        playerRb.gravityScale = 0f; 
                    }
                }
                else 
                {
                    randPlayerX = Random.Range(-6f, 13f);
                    randPlayerY = -1.5f; 
                }
                player.localPosition = new Vector2(randPlayerX, randPlayerY);
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector2 toPlayer = playerCollider.bounds.center - transform.position;
        // Limit values so works in large rooms. 
        sensor.AddObservation(Mathf.Clamp(toPlayer.x, -20f, 20f));
        sensor.AddObservation(Mathf.Clamp(toPlayer.y, -20f, 20f));

        if(otherBat != null){
            Vector2 otherBatToPlayer = playerCollider.bounds.center - otherBat.transform.position;
            sensor.AddObservation(Mathf.Clamp(otherBatToPlayer.x, -20f, 20f));
            sensor.AddObservation(Mathf.Clamp(otherBatToPlayer.y, -20f, 20f));
        } else
        {
            sensor.AddObservation(20f);
            sensor.AddObservation(20f);
        }
        
        // Let the bat know its velocity
        sensor.AddObservation(rb.linearVelocity.x);
        sensor.AddObservation(rb.linearVelocity.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isDead) return;
        float distToPlayer = Vector2.Distance(transform.position, playerCollider.bounds.center);
        
        if (!trainingMode && distToPlayer > detectionRange)
        {
            // If the enemy is sleeping and outside range, continue to sleep
            if (currentState == State.Sleeping) currentState = State.Sleeping;
            // If awake, continue to be awake
            else {
                currentState = State.Idle;
                if (isDodging)
                    {
                        rb.linearVelocity = calculatedDodgeVector * (moveSpeed * 2f);
                        dodgeTimer -= Time.deltaTime;
                        if (dodgeTimer <= 0)
                        {
                            isDodging = false;
                            hasForesight = false;
                        }
                        return;
                    }
                Hover();
            }
            return;
        }
        // Sleeping bat has been awoken!
        if (currentState == State.Sleeping) detectionRange += 5;
        
        currentState = State.Chase;

        if (isDodging)
        {
            rb.linearVelocity = calculatedDodgeVector * (moveSpeed * 2f);
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0)
            {
                isDodging = false;
                hasForesight = false;
            }
            return;
        }

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
            if(distToPlayer > 25f)
            {
                AddReward(-0.5f);
                EndEpisode();
                if(otherBat != null){
                    if (partnerAgent.StepCount > 0)
                    {
                        partnerAgent.EndEpisode();
                    }
                }
                return;
            }
            // Reward for being close to player
            AddReward(-0.0005f * distToPlayer);
        }
    }

    public void TriggerForesightDodge(Vector2 attackDirection)
    {
        if (isDodging || isDead || isRewinding) return;
        Debug.Log("ACTUALLY TRIED TO DIDGE");
        hasForesight = true;
        isDodging = true;
        dodgeTimer = dodgeDuration;

        animator.SetBool("hasForesight", true);

        calculatedDodgeVector = new Vector2(-attackDirection.y, attackDirection.x).normalized;
        
        // Dodge either up or down
        if (Random.value > 0.5f) calculatedDodgeVector *= -1; 
    }

    private PlayerTactic GetCurrentPlayerTactic()
    {
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        bool isPlayerAttacking = playerCombat.isAttacking || playerSpells.isCasting;
        bool isPlayerInAir = !playerPlatformer.isGrounded;
        PlayerTactic tactic;
        if (isPlayerAttacking)
        {
            if(distToPlayer < 3f) tactic = PlayerTactic.AttackingClose;
            else tactic = PlayerTactic.AttackingFar;
        }
        else if (isPlayerInAir) tactic = PlayerTactic.Airborne;
        else if (Mathf.Abs(distToPlayer - previousPlayerDistance) < 0.1f) tactic = PlayerTactic.Idle;
        else if (distToPlayer < previousPlayerDistance) tactic = PlayerTactic.Approaching;
        else tactic = PlayerTactic.Retreating;
        previousPlayerDistance = distToPlayer;
        return tactic;
    }
    void Hover()
    {
        // Simple Sine wave bobbing effect
        float newY = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        rb.linearVelocity = new Vector2(0, newY); 
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isRewinding || isDead) return;
        if(trainingMode){
           if (collision.gameObject.CompareTag("Player"))
            {
                float batOffsetX = transform.position.x - collision.transform.position.x;
                if(otherBat != null){
                    float otherBatOffsetX = otherBat.transform.position.x - collision.transform.position.x;

                    if (batOffsetX * otherBatOffsetX < 0f) 
                    {
                        // Extra reward for flank
                        AddReward(1.0f);
                    }
                }
                AddReward(1.0f);
                EndEpisode();
                if(otherBat != null){
                    if (partnerAgent.StepCount > 0)
                    {
                        partnerAgent.EndEpisode();
                    }
                }
            }
            // We don't want the bats to crash into walls
            if (collision.gameObject.CompareTag("Ground"))
            {
                AddReward(-0.01f);
            }
        }
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isRewinding || isDead) return;
        if (!trainingMode && collision.gameObject.CompareTag("Player"))
        {
            rb.linearVelocity = Vector2.zero; 
            Attack();
        }
        if (trainingMode)
        {
            // If the bat is touching a wall, punish them continuously
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

private void HandleDeath()
    {
        if (isRewinding) return;
        Debug.Log("Bat death triggered");
        
        isDead = true;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        rb.linearVelocity = Vector2.zero;
        animator.SetTrigger("die");
        
        StartCoroutine(enemy.DeathRoutine()); 
    }

    public void OnStartRewind()
    {
        isRewinding = true;
        enemy.OnStartRewind();
        
        StopAllCoroutines(); 

        if (currentPlayerTimeline.Count > 0)
        {
            previousPlayerTimeline = new List<PlayerTactic>(currentPlayerTimeline);
            currentPlayerTimeline.Clear();
        }
        
        animator.ResetTrigger("die");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Chase");
        animator.ResetTrigger("Foresight");
        animator.speed = 0f;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        enemy.OnStopRewind();
        animator.speed = 1f;
        if (isDead)
        {
            spriteRenderer.enabled = false;
        }
    }

    public RewindState CaptureState()
    {
        var state = enemy.CaptureState(); 
        AnimatorStateInfo animState = animator.GetCurrentAnimatorStateInfo(0);
        state.AnimatorStateHash = animState.fullPathHash;
        state.AnimatorNormalizedTime = animState.normalizedTime;
        
        state.SetCustomData("spriteVisible", spriteRenderer.enabled);
        
        Collider2D col = GetComponent<Collider2D>();
        state.SetCustomData("colEnabled", col != null && col.enabled);
        
        return state;
    }

    public void ApplyState(RewindState state)
    {
        enemy.ApplyState(state); 
        isDead = state.Health <= 0; 
        if (spriteRenderer != null) spriteRenderer.enabled = state.GetCustomData<bool>("spriteVisible");
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = state.GetCustomData<bool>("colEnabled");
        animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
        animator.Update(0f);
    }
}