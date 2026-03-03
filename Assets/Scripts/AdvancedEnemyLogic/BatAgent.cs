using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using TimeRewind;
using System.Collections.Generic;
public class BatEnemyAI : Agent, IRewindable
{
    private EnemyBase enemy;
    [Header("Mode")]
    public bool trainingMode = false;
    [Header("ML Agent Variables")]
    public bool controlsEnvironment = false;
    private BatEnemyAI partnerAgent;
    public Transform otherBat;
    public Transform obstacle;

    [Header("References")]
    public Transform player;
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;
    [Header("Basic behaviour variables")]
    public float hoverFrequency = 2f; // Bob speed
    public float hoverAmplitude = 0.5f; // Max bob height
    public float moveSpeed = 5f;
    public float detectionRange = 1f;
    public int damage = 1;
    public float attackCooldown = 1.5f;
    public enum State { Sleeping, Idle, Chase }
    public State currentState = State.Idle;
    private bool isRewinding = false;
    private bool isDead = false;
    private bool isDodging = false;
    private bool hasForesight;
    private float dodgeDuration = 0.5f;
    private float dodgeTimer = 0f;
    private Vector2 calculatedDodgeVector;
    private float sequenceSimilarity = 0f;
    [Header("Foresight AI Variables")]
    public float foresightThreshold = 0.75f;
    public float dodgeTriggerDistance = 3.4f;
    // So the bat won't dodge into walls and floors!
    public LayerMask obstacleLayer;
    private int memorySize = 30;
    private Queue<PlayerState> currentTimeline = new Queue<PlayerState>();
    private Queue<PlayerState> previousTimeline = new Queue<PlayerState>();
    private float rewindStartTime;
    public float recordInterval = 0.5f;
    private float recordTimer = 0f;
    private Collider2D playerCollider;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Animator animator;
    private float lastAttackTime;
    private Vector3 originalScale;
    // Possible 'tactics' a player could be employing
    public struct PlayerState
    {
        public Vector2 relativeDirection;
        public float distance;
        public Vector2 velocity;
        public int attackType; // 0: Not attacking, 1: Melee, 2: Spell
    }
    public float weightDistance = 0.5f;
    public float weightVelocity = 0.2f;
    
    public float weightAttack = 2.0f;
    // Controlls how many player states we look at when determining similarity
    // public int windowSize = 4; - Only used by Hamming Weight func
    // We need at least minTimelineSize samples before we consider foresight
    public int minTimelineSize = 2;
    public int bandWidth = 3;


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
        playerCombat = player.GetComponent<PlayerCombat>();
        playerSpells = player.GetComponent<PlayerSpellSystem>();
        animator = GetComponent<Animator>();
        animator.ResetTrigger("Chase");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("die");
        if(otherBat != null) partnerAgent = otherBat.GetComponent<BatEnemyAI>();
    }
    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Unregister(this);
        }
    }

    void Update()
    {
        if (isDead || isRewinding || trainingMode) return;

        recordTimer += Time.deltaTime;
        if (recordTimer >= recordInterval)
        {
            // Record state of the player
            PlayerState state = GetCurrentPlayerState();
            if (currentTimeline.Count >= memorySize) currentTimeline.Dequeue();
            currentTimeline.Enqueue(state);
            sequenceSimilarity = CalculateDTWSimilarity();
            Debug.Log(sequenceSimilarity);
            recordTimer = 0f;
        }

        // Player is behaving similarly (within a threshold) to before
        if (sequenceSimilarity >= foresightThreshold)
        {
            if (!hasForesight)
            {
                hasForesight = true;
                animator.SetBool("hasForesight", true);
            }
            GameObject spellObj = playerSpells.latestSpell;

            if (!isDodging && Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance)
            {
                Vector2 approachDirection = (playerCollider.bounds.center - transform.position).normalized;
                
                TriggerForesightDodge(approachDirection);
            }
            if (!isDodging && spellObj != null)
            {
                if (Vector2.Distance(transform.position, spellObj.GetComponent<Collider2D>().bounds.center) < dodgeTriggerDistance + 0.5f){
                    Rigidbody2D spellRb = spellObj.GetComponent<Rigidbody2D>();
                    Vector2 approachDirection = (spellRb.position - (Vector2)transform.position).normalized;
                    TriggerForesightDodge(approachDirection);
                }
            }
        }
        else
        {
            // Player is behaving differently
            if (hasForesight && !isDodging)
            {
                hasForesight = false;
                animator.SetBool("hasForesight", false);
            }
        }
    }

    // Heuristic for calculating distance between tactics
    float GetPlayerStateDistance(PlayerState a, PlayerState b)
    {
        // Differences squared
        float distDiffSq = Mathf.Pow(a.distance - b.distance, 2);
        float velDiffSq = (a.velocity - b.velocity).sqrMagnitude;
        float attackDiffSq = Mathf.Pow(a.attackType - b.attackType, 2);
        
        // Weighted distance: attacking is most important, then distance, then velocity
        float distance = Mathf.Sqrt(
            (weightDistance * distDiffSq) + 
            (weightVelocity * velDiffSq) +
            (weightAttack * attackDiffSq)
        );

        return distance;
    }
    // Use Dynamic Time Warp algorithm to calculate similarity between current and previous timeline
    float CalculateDTWSimilarity()
    {
        // We need both timelines to have at least the window size number of samples
        if (currentTimeline.Count < minTimelineSize || previousTimeline.Count < minTimelineSize) return 0f;

        // Convert both queues into arrays for easier manipulation
        var current = currentTimeline.ToArray();
        var previous = previousTimeline.ToArray();
        int c_len = current.Length;
        int p_len = previous.Length;

        // Band must be able to reach the final cell!
        int w = Mathf.Max(bandWidth, Mathf.Abs(c_len - p_len));

        float[,] dtw = new float[c_len + 1, p_len + 1];

        // Initialise table
        for (int i = 0; i <= c_len; i++)
            for (int j = 0; j <= p_len; j++)
                dtw[i, j] = float.PositiveInfinity;

        dtw[0, 0] = 0;

        // Populate matrix, using Sakoe-Chiba Band
        for (int i = 1; i <= c_len; i++)
            {
                // Calculate dynamic start and end bounds for the inner loop
                int startJ = Mathf.Max(1, i - w);
                int endJ = Mathf.Min(p_len, i + w);

                for (int j = startJ; j <= endJ; j++)
                {
                    float cost = GetPlayerStateDistance(current[i - 1], previous[j - 1]);
                    // D(i,j) = d(i,j) + min(D(i-1,j), D(i,j-1), D(i-1,j-1))
                    dtw[i, j] = cost + Mathf.Min(dtw[i - 1, j], Mathf.Min(dtw[i, j - 1], dtw[i - 1, j - 1]));
                }
            }
        // Normalise score
        float maxPossibleDistance = c_len * 5.0f; 
        return 1.0f - Mathf.Clamp01(dtw[c_len, p_len] / maxPossibleDistance);
    }

    // Old Hamming-weight distance function
    // float CalculateSimilarity()
    // {
    //     if (currentTimeline.Count < windowSize || previousTimeline.Count < windowSize)
    //         return 0f;

    //     var currentArray = currentTimeline.ToArray();
    //     var previousArray = previousTimeline.ToArray();

    //     int matches = 0;
    //     for (int i = 0; i < windowSize; i++)
    //     {
    //         PlayerTactic current = currentArray[currentArray.Length - 1 - i];
    //         PlayerTactic previous = previousArray[previousArray.Length - 1 - i];
    //         if (current == previous) matches++;
    //     }

    //     return (float)matches / windowSize;
    // }
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
        if (isDead) return;
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
                        if (dodgeTimer <= 0) isDodging = false;
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
            if (dodgeTimer <= 0) isDodging = false;
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
        hasForesight = true;
        isDodging = true;
        dodgeTimer = dodgeDuration;

        animator.SetBool("hasForesight", true);

        // Calculate both potential perpendicular escape routes
        Vector2 dir1 = new Vector2(-attackDirection.y, attackDirection.x).normalized;
        Vector2 dir2 = -dir1; // The exact opposite direction

        // Calculate how far the bat will travel during the dodge
        float estimatedDodgeDistance = moveSpeed * 2f * dodgeDuration;

        // Cast a ray in both directions to look for walls
        RaycastHit2D hit1 = Physics2D.Raycast(transform.position, dir1, estimatedDodgeDistance, obstacleLayer);
        RaycastHit2D hit2 = Physics2D.Raycast(transform.position, dir2, estimatedDodgeDistance, obstacleLayer);

        float clearance1 = hit1.collider != null ? hit1.distance : float.MaxValue;
        float clearance2 = hit2.collider != null ? hit2.distance : float.MaxValue;

        // Pick the direction that has more space
        if (clearance1 > clearance2) calculatedDodgeVector = dir1;
        else if (clearance2 > clearance1) calculatedDodgeVector = dir2;
        else
        {
            // Random fallback
            calculatedDodgeVector = Random.value > 0.5f ? dir1 : dir2;
        }
    }

    private PlayerState GetCurrentPlayerState()
    {
        PlayerState state = new PlayerState();
        Vector2 offset = player.position - transform.position;
        state.relativeDirection = offset.normalized;
        state.distance = offset.magnitude;
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        state.velocity = playerRb.linearVelocity;
        
        if (playerCombat.isAttacking) state.attackType = 1; // Melee
        else if (playerSpells.isCasting) state.attackType = 2; // Spell
        else state.attackType = 0; // Not attacking
        
        return state;
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

        rewindStartTime = Time.time;

        // if (currentTimeline.Count > 0)
        // {
        //     previousTimeline = new Queue<PlayerState>(currentTimeline);
        //     currentTimeline.Clear();
        // }
        
        animator.ResetTrigger("die");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Chase");
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

        float timeRewound = rewindStartTime - TimeRewindManager.Instance.CurrentRewindTime; 
        int statesErased = Mathf.RoundToInt(timeRewound / recordInterval);
        
        statesErased = Mathf.Clamp(statesErased, 0, currentTimeline.Count);
        var currentArray = currentTimeline.ToArray();
        
        previousTimeline.Clear();
        int startIndex = currentArray.Length - statesErased;
        for (int i = startIndex; i < currentArray.Length; i++)
        {
            previousTimeline.Enqueue(currentArray[i]);
        }

        // Wipe current timeline
        currentTimeline.Clear();
        
        recordTimer = 0f; 
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