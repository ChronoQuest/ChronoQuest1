using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using TimeRewind;

// 1. Add IForesightEnemy to the interface list
public class BatEnemyAI : Agent, IRewindable, IBossSpawnable, IForesightEnemy
{
    private EnemyBase enemy;
    private ForesightSystem foresightSystem;
    [Header("Mode")]
    public bool trainingMode = false;
    [Header("ML Agent Variables")]
    public bool controlsEnvironment = false;
    private BatEnemyAI partnerAgent;
    public Transform otherBat;
    public Transform obstacle;

    [Header("References")]
    [SerializeField] private Transform _player;

    public Transform player
    {
        get => _player;
        set => _player = value;
    }
    public void DoubleDetectionRange()
    {
        detectionRange *= 2f;
    }
    private PlayerCombat playerCombat;
    private PlayerSpellSystem playerSpells;
    [Header("Basic behaviour variables")]
    public float hoverFrequency = 2f; 
    public float hoverAmplitude = 0.5f; 
    public float moveSpeed = 5f;
    public float detectionRange = 1f;
    public int damage = 1;
    public float attackCooldown = 1.5f;
    public enum State { Sleeping, Idle, Chase }
    public State currentState = State.Idle;
    private bool isRewinding = false;
    private bool isDead = false;
    private bool isDodging = false;    
    private float dodgeDuration = 0.5f;
    private float dodgeTimer = 0f;
    private Vector2 calculatedDodgeVector;
    public LayerMask obstacleLayer;
    private float rewindStartTime;
    private Collider2D playerCollider;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Animator animator;
    private float lastAttackTime;
    private Vector3 originalScale;
    private bool hasForesight = false;
    public float dodgeTriggerDistance = 3.4f;
    public float dodgeCooldown = 1.0f;
    private float dodgeCooldownTimer = 0f;

    void Start()
    {
        enemy = GetComponent<EnemyBase>();
        foresightSystem = GetComponent<ForesightSystem>();
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
        if (dodgeCooldownTimer > 0)
        {
            dodgeCooldownTimer -= Time.deltaTime;
        }
        if (hasForesight && !isDodging && dodgeCooldownTimer <= 0)
        {
            CheckDodgeTriggers();
        }
    }
    private void CheckDodgeTriggers()
    {
        if (IsThreatClose())
        {
            // We just need a generic approach direction if the update loop catches it first
            Vector2 approachDirection = (playerCollider.bounds.center - transform.position).normalized;
            TriggerForesightDodge(approachDirection);
        }
    }

    void FixedUpdate()
    {
        if (isDead || isRewinding) return;

        if (isDodging)
        {
            rb.linearVelocity = calculatedDodgeVector * (moveSpeed * 2f);
            
            dodgeTimer -= Time.fixedDeltaTime;
            
            if (dodgeTimer <= 0) 
            {
                isDodging = false;
                rb.linearVelocity = Vector2.zero;
                RequestDecision();
            }
        }
    }

    // ---------------------- IForesightEnemy Implementation ----------------------
    public int GetPlayerAttackState()
    {
        if (playerCombat != null && playerCombat.isAttacking) return 1;
        if (playerSpells != null && playerSpells.isCasting) return 2;
        return 0;
    }

    public void PerformForesightDodge(Vector2 attackDirection) 
    {
        if (IsThreatClose()) TriggerForesightDodge(attackDirection);
    }
    public void PerformForesightLunge(Vector2 approachDirection) => TriggerForesightLunge(approachDirection);
    public void SetForesightState(bool state)
    {
        hasForesight = state;
        animator.SetBool("hasForesight", hasForesight);
    }
    public bool IsDead() => isDead;
    public bool IsRewinding() => isRewinding;
    public void TriggerForesightDodge(Vector2 attackDirection)
    {
        if (isDodging || isDead || isRewinding || dodgeCooldownTimer > 0) return;
        
        isDodging = true;
        dodgeTimer = dodgeDuration;
        dodgeCooldownTimer = dodgeCooldown;

        Vector2 dir1 = new Vector2(-attackDirection.y, attackDirection.x).normalized;
        Vector2 dir2 = -dir1; 

        float estimatedDodgeDistance = moveSpeed * 2f * dodgeDuration;

        RaycastHit2D hit1 = Physics2D.Raycast(transform.position, dir1, estimatedDodgeDistance, obstacleLayer);
        RaycastHit2D hit2 = Physics2D.Raycast(transform.position, dir2, estimatedDodgeDistance, obstacleLayer);

        float clearance1 = hit1.collider != null ? hit1.distance : float.MaxValue;
        float clearance2 = hit2.collider != null ? hit2.distance : float.MaxValue;

        if (clearance1 > clearance2) calculatedDodgeVector = dir1;
        else if (clearance2 > clearance1) calculatedDodgeVector = dir2;
        else
        {
            calculatedDodgeVector = Random.value > 0.5f ? dir1 : dir2;
        }
    }
    public void TriggerForesightLunge(Vector2 approachDirection)
    {
        if (isDodging || isDead || isRewinding || dodgeCooldownTimer > 0) return;
        
        isDodging = true;
        dodgeTimer = dodgeDuration;
        dodgeCooldownTimer = dodgeCooldown;
        calculatedDodgeVector = approachDirection;
    }

    private bool IsThreatClose()
    {
        if (Vector2.Distance(transform.position, playerCollider.bounds.center) < dodgeTriggerDistance)
            return true;

        GameObject spellObj = playerSpells.latestSpell;
        if (spellObj != null)
        {
            if (Vector2.Distance(transform.position, spellObj.GetComponent<Collider2D>().bounds.center) < dodgeTriggerDistance + 0.5f)
                return true;
        }

        return false;
    }

    // ---------------------- ---------------------- ----------------------

    public override void OnEpisodeBegin()
    {
        isDead = false;
        if (trainingMode)
        {
            rb.gravityScale = 0f;
            float randBatX = Random.Range(-6f, 13f);
            float randBatY = Random.Range(-2f, 8f);
            transform.localPosition = new Vector2(randBatX, randBatY);
            rb.linearVelocity = Vector2.zero;

            if (controlsEnvironment)
            {
                float randX;
                float randY = Random.Range(-4f, 2f);
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

                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                float randPlayerX;
                float randPlayerY;
                
                if (Random.value > 0.7f) 
                {
                    if(Random.value > 0.3f)
                    {
                        randPlayerY = randY + 6f;
                        if(newScale.x == 1){
                            randPlayerX = Random.Range(randX - 1.5f, randX + 1.5f) + 5f;
                        }
                        else
                        {
                            randPlayerX = Random.Range(randX - 1.5f, randX + 1.5f) + 3f;
                        }
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
        if (isDead || playerCollider == null)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }
        Vector2 toPlayer = playerCollider.bounds.center - transform.position;
        sensor.AddObservation(Mathf.Clamp(toPlayer.x, -20f, 20f));
        sensor.AddObservation(Mathf.Clamp(toPlayer.y, -20f, 20f));
        if (otherBat != null)
        {
            Vector2 otherBatToPlayer = playerCollider.bounds.center - otherBat.transform.position;
            sensor.AddObservation(Mathf.Clamp(otherBatToPlayer.x, -20f, 20f));
            sensor.AddObservation(Mathf.Clamp(otherBatToPlayer.y, -20f, 20f));
        }
        else
        {
            sensor.AddObservation(20f);
            sensor.AddObservation(20f);
        }
        if (rb != null)
        {
            sensor.AddObservation(rb.linearVelocity.x);
            sensor.AddObservation(rb.linearVelocity.y);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isDead || isDodging) return;
        float distToPlayer = Vector2.Distance(transform.position, playerCollider.bounds.center);
        
        if (!trainingMode && distToPlayer > detectionRange)
        {
            if (currentState == State.Sleeping) currentState = State.Sleeping;
            else 
            {
                currentState = State.Idle;
                Hover();
            }
            return;
        }
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
            AddReward(-0.001f);
            if(distToPlayer > 25f)
            {
                AddReward(-0.5f);
                EndEpisode();
                if(otherBat != null && partnerAgent.StepCount > 0){
                    partnerAgent.EndEpisode();
                }
                return;
            }
            AddReward(-0.0005f * distToPlayer);
        }
    }

    void Hover()
    {
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
                        AddReward(1.0f);
                    }
                }
                AddReward(1.0f);
                EndEpisode();
                if(otherBat != null && partnerAgent.StepCount > 0){
                    partnerAgent.EndEpisode();
                }
            }
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
        if (trainingMode && collision.gameObject.CompareTag("Ground"))
        {
            AddReward(-0.01f);
        }
    }
    void Attack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
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
        
        isDead = true;
        ForesightSystem.enemyDiedPreviously = true; // Update static variable on the system!
        
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

        isDodging = false;
        hasForesight = false;
        dodgeTimer = 0f;
        dodgeCooldownTimer = 0f;
        animator.SetBool("hasForesight", false);
        
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
        
        if (foresightSystem != null)
        {
            // Calculate how much time passed in the real world while we were rewinding
            float timeRewound = Time.time - rewindStartTime; 
            int statesErased = Mathf.RoundToInt(timeRewound / foresightSystem.recordInterval);
            foresightSystem.HandleRewindStop(statesErased);
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