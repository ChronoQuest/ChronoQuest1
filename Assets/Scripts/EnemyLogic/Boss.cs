using UnityEngine;
using TimeRewind;

public class Boss : EnemyBase, IRewindable
{
    public Transform player;
    public BossAttackManager attackManager;
    public PlayerStrategyModel playerStrategyModel; 

    public int damage = 1;
    public float damageCooldown = 1.5f;
    CameraShake cameraShake;

    bool _isRewinding;
    bool offActionSpawned;
    bool resActionSpawned;
    int facingDirection = 1;
    bool isGrounded;
    float lastDamageTime;

    bool isPlayingAttack1;
    bool isPlayingAttack2;

    enum BossPhase { Idle, Positional, Combat }
    enum PosMove { None, GroundPound, ChangeSides }
    enum OffMove { None, Fireballs, FireColumns, HomingFireballs, FireExplosion }
    enum ResMove { None, FireRow, FireWave, Platforms, Enemy }
    public bool isDead = false;
    BossPhase currentPhase;
    PosMove currentPos;
    OffMove currentOff;
    ResMove currentRes;

    // Separate timers so attacks don't block each other
    float idleTimer;
    float posTimer;
    float offTimer;
    float resTimer;

    int offIndex;
    int resIndex;
    private RewindMusicController musicController;

    OffMove lastOff = OffMove.None;
    ResMove lastRes = ResMove.None;
    int repeatCount = 0;

    // Movement Tracking
    Vector2 moveStart;
    Vector2 movePeak;
    Vector2 moveTarget;
    const float ArenaMinX = -11f;
    const float ArenaMaxX =  11f;
    float finalTargetX;
    float jumpLateral;
    private Vector3 originalScale;
    private Animator animator;
    

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        cameraShake = Camera.main.GetComponent<CameraShake>();
        originalScale = transform.localScale;
        animator = GetComponent<Animator>();
        FacePlayer();
        musicController = FindFirstObjectByType<RewindMusicController>();
        if (musicController != null)
        {
            musicController.PlayBossFightMusic();
        }
        EndPhase();
        // Start the boss health bar
        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.StartFight();
    }

    public override void Update()
    {
        base.Update();
        if (_isRewinding || player == null || wasDead) return;

        switch (currentPhase)
        {
            case BossPhase.Idle: UpdateIdle(); break;
            case BossPhase.Positional: UpdatePositional(); break;
            case BossPhase.Combat: UpdateCombat(); break;
        }
    }

    void EndPhase()
    {
        currentPhase = BossPhase.Idle;
        currentPos = PosMove.None;
        currentOff = OffMove.None;
        currentRes = ResMove.None;
        idleTimer = 0f;

        animator.SetBool("isGrounded", true);
    }

    void FacePlayer()
    {
        if (player == null) return;
        
        int dir = player.position.x > transform.position.x ? 1 : -1;
        
        transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * dir, originalScale.y, originalScale.z);        
    }

    void UpdateIdle()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer < 1f) return;

        if (Random.value > 0.8f) StartPositional();
        else StartCombat();
    }

    void StartPositional()
    {
        currentPhase = BossPhase.Positional;
        posTimer = 0f;

        if (Random.value > 0.5f) StartGroundPound();
        else StartChangeSides();
    }

    void UpdatePositional()
    {
        if (currentPos == PosMove.GroundPound) UpdateGroundPound();
        else if (currentPos == PosMove.ChangeSides) UpdateChangeSides();
    }

    void StartChangeSides()
    {
        animator.SetTrigger("ChangeSides");
        animator.SetBool("isGrounded", false); 

        currentPos = PosMove.ChangeSides;
        Vector2 start = transform.position;
        moveStart = start;
        //moveTarget = new Vector2(-start.x, start.y);
        moveTarget = new Vector2(Mathf.Clamp(-start.x, ArenaMinX, ArenaMaxX), start.y);

        float jumpHeight = 8f;
        movePeak = start + new Vector2((moveTarget.x - start.x) / 2, jumpHeight);
    }

    void UpdateChangeSides()
    {
        posTimer += Time.deltaTime;
        float duration = 1.8f;

        if (posTimer < duration)
        {
            float t = posTimer / duration;
            Vector2 a = Vector2.Lerp(moveStart, movePeak, t);
            Vector2 b = Vector2.Lerp(movePeak, moveTarget, t);
            rb.MovePosition(Vector2.Lerp(a, b, t));
        }
        else
        {
            rb.MovePosition(moveTarget);
            facingDirection *= -1;
            EndPhase();
        }
    }

    void StartGroundPound()
    {
        currentPos = PosMove.GroundPound;
        finalTargetX = Mathf.Clamp(-transform.position.x, ArenaMinX, ArenaMaxX);

        float totalDist = Mathf.Abs(finalTargetX - transform.position.x);
        int currentJumps = Mathf.CeilToInt(totalDist / 4f);
        int newJumps = Mathf.Max(currentJumps - 1, 1);
        jumpLateral = totalDist / newJumps;

        CalculateNextJump();
    }

    void CalculateNextJump()
    {
        posTimer = 0f;
        float jumpHeight = 7f;
        float remaining = Mathf.Abs(finalTargetX - transform.position.x);
        float step = Mathf.Min(jumpLateral, remaining);
        float lateral = -step * facingDirection;

        moveStart = transform.position;
        movePeak = moveStart + new Vector2(lateral, jumpHeight);
        moveTarget = new Vector2(movePeak.x, moveStart.y);

        animator.SetTrigger("GroundPound");
        animator.SetBool("isGrounded", false);
    }

    void UpdateGroundPound()
    {
        posTimer += Time.deltaTime;
        float rise = 0.8f;
        float fall = 0.4f;
        float pause = 0.6f; // Pause between jumps
        float total = rise + fall + pause;

        if (posTimer < rise)
        {
            float t = posTimer / rise;
            rb.MovePosition(Vector2.Lerp(moveStart, movePeak, t));
            isGrounded = false;
        }
        else if (posTimer < rise + fall)
        {
            float t = (posTimer - rise) / fall;
            rb.MovePosition(Vector2.Lerp(movePeak, moveTarget, t));
        }
        else
        {
            rb.MovePosition(moveTarget);

            if (!isGrounded)
            {
                cameraShake.Shake(0.25f, 0.2f);
                isGrounded = true;
                animator.SetBool("isGrounded", true);
            }

            // Once the pause is over, check if we loop or end
            if (posTimer >= total)
            {
                if (Mathf.Abs(transform.position.x - finalTargetX) <= 0.1f)
                {
                    facingDirection *= -1;
                    EndPhase();
                }
                else
                {
                    CalculateNextJump();
                }
            }
        }
    }

    void StartCombat()
    {
        isPlayingAttack1 = false;
        isPlayingAttack2 = false;
        FacePlayer();
        facingDirection = player.position.x > transform.position.x ? -1 : 1; 

        currentPhase = BossPhase.Combat;
        offTimer = 0f; resTimer = 0f;
        offIndex = 0; resIndex = 0;
            
        offActionSpawned = false; 
        resActionSpawned = false;
            
        /* var strategy = playerStrategyModel.GetDominantStrategy();

        Debug.Log($"Strategy: {strategy}"); 

        Debug.Log("Boss reacting to strategy");

        // if (strategy == PlayerStrategyModel.StrategyType.AggressivePlayer)
        // {
        //     currentOff = OffMove.FireColumns;
        //     currentRes = ResMove.Platforms;
        // }
        // else if (strategy == PlayerStrategyModel.StrategyType.DefensivePlayer)
        // {
        //     currentOff = OffMove.Fireballs;
        //     currentRes = ResMove.Enemy;
        // }
        // else 
        // {
        //     currentOff = OffMove.HomingFireballs;
        //     currentRes = ResMove.FireWave;
        // }

        float counterChance = 0.7f; // 70% chance to use counter moves, adjust to taste

        if (Random.value < counterChance)
        {
            // Use the counter strategy
            if (strategy == PlayerStrategyModel.StrategyType.AggressivePlayer)
            {
                currentOff = OffMove.FireColumns;
                currentRes = ResMove.Platforms;
            }
            else if (strategy == PlayerStrategyModel.StrategyType.DefensivePlayer)
            {
                currentOff = OffMove.Fireballs;
                currentRes = ResMove.Enemy;
            }
            else
            {
                currentOff = OffMove.HomingFireballs;
                currentRes = ResMove.FireWave;
            }
        }
        else
        {
            // Pick randomly from the other moves
            OffMove[] otherOff = strategy == PlayerStrategyModel.StrategyType.AggressivePlayer
                ? new[] { OffMove.Fireballs, OffMove.HomingFireballs, OffMove.FireExplosion }
                : strategy == PlayerStrategyModel.StrategyType.DefensivePlayer
                ? new[] { OffMove.FireColumns, OffMove.HomingFireballs, OffMove.FireExplosion }
                : new[] { OffMove.Fireballs, OffMove.FireColumns, OffMove.FireExplosion };

            ResMove[] otherRes = strategy == PlayerStrategyModel.StrategyType.AggressivePlayer
                ? new[] { ResMove.FireRow, ResMove.FireWave, ResMove.Enemy }
                : strategy == PlayerStrategyModel.StrategyType.DefensivePlayer
                ? new[] { ResMove.FireRow, ResMove.FireWave, ResMove.Platforms }
                : new[] { ResMove.FireRow, ResMove.Enemy, ResMove.Platforms };

            currentOff = otherOff[Random.Range(0, otherOff.Length)];
            currentRes = otherRes[Random.Range(0, otherRes.Length)];
        }

        else if (strategy == playerStrategyModel.StrategyType.AbilityFocusedPlayer)
        {
            currentOff = OffMove.HomingFireballs;
            currentRes = ResMove.FireWave;
        } */ 

        // -- EXPERIMENT EDITS --
        var tactics = playerStrategyModel.playerTacticalModel.tacticBeliefs;

        float aggressive = tactics[PlayerTacticalModel.TacticType.Aggressive];
        float evasive    = tactics[PlayerTacticalModel.TacticType.Evasive];
        float cautious   = tactics[PlayerTacticalModel.TacticType.Cautious];

        float roll = Random.value;

        if (roll < aggressive)
        {
            currentOff = OffMove.FireColumns;
            currentRes = ResMove.Platforms;
        }
        else if (roll < aggressive + evasive)
        {
            currentOff = OffMove.Fireballs;
            currentRes = ResMove.Enemy;     
        }
        else


        //Don't want too much repetition
        if (currentOff == lastOff && currentRes == lastRes)
        {
            repeatCount++;
            if (repeatCount >= 2)
            {
                OffMove[] allOff = new[] { OffMove.Fireballs, OffMove.FireColumns, OffMove.HomingFireballs, OffMove.FireExplosion };
                do { currentOff = allOff[Random.Range(0, allOff.Length)]; }
                while (currentOff == lastOff);

                ResMove[] allRes = new[] { ResMove.FireRow, ResMove.FireWave, ResMove.Platforms, ResMove.Enemy };
                do { currentRes = allRes[Random.Range(0, allRes.Length)]; }
                while (currentRes == lastRes);

                repeatCount = 0;
            }
        }
        else
        {
            repeatCount = 0;
        }

        lastOff = currentOff;
        lastRes = currentRes;
    }

    void UpdateCombat()
    {
        bool offDone = UpdateOffensive();
        bool resDone = UpdateRestrictive();

        // Idle only when both attacks are done
        if (offDone && resDone)
        {
            EndPhase();
        }
    }

    bool UpdateOffensive()
    {
        offTimer += Time.deltaTime;
        if (currentOff == OffMove.Fireballs)
        {
            if (offIndex >= 15) return true;
            PlayerHealth playerHealth = attackManager.player.GetComponent<PlayerHealth>();
            float spawnDelay = (playerHealth != null && playerHealth.CurrentHealth < 3) ? 1.0f : 0.5f;

            if (offTimer > spawnDelay)
            {
                if (isPlayingAttack1) return false;
                offTimer = 0f;
                animator.SetTrigger("Attack1");
                isPlayingAttack1 = true;
                offIndex++;
            }
            return false;
        }
        else if (currentOff == OffMove.FireColumns)
        {
            if (!offActionSpawned)
            {
                if (isPlayingAttack2) return false;
                animator.SetTrigger("Attack2");
                isPlayingAttack2 = true;
                offActionSpawned = true;
            }
            return offTimer > 5f;
        }
        else if (currentOff == OffMove.HomingFireballs)
        {
            if (offIndex >= 14) return true;
            PlayerHealth playerHealth = attackManager.player.GetComponent<PlayerHealth>();
            float spawnDelay = (playerHealth != null && playerHealth.CurrentHealth < 3) ? 2f : 1f;

            if (offTimer > spawnDelay)
            {
                if (isPlayingAttack1) return false;
                offTimer = 0f;
                animator.SetTrigger("Attack1");
                isPlayingAttack1 = true;
                offIndex++;
            }
            return false;
        }
        else if (currentOff == OffMove.FireExplosion)
        {
            if (offIndex >= 15) return true;
            PlayerHealth playerHealth = attackManager.player.GetComponent<PlayerHealth>();
            float spawnDelay = (playerHealth != null && playerHealth.CurrentHealth < 3) ? 1f : 0.5f;

            if (offTimer > spawnDelay)
            {
                if (isPlayingAttack2) return false;
                offTimer = 0f;
                animator.SetTrigger("Attack2");
                isPlayingAttack2 = true;
                offIndex++;
            }
            return false;
        }
        return true;
    }

    bool UpdateRestrictive()
    {
        resTimer += Time.deltaTime;

        if (resTimer < 1.5f) return false;
        else if (currentRes == ResMove.FireRow)
        {
            if (!resActionSpawned && resTimer > 1.5f && resTimer < 1.6f)
            {
                if (isPlayingAttack2) return false;
                animator.SetTrigger("Attack2");
                isPlayingAttack2 = true;
                resActionSpawned = true;
            }
            return resTimer > 8.5f;
        }
        else if (currentRes == ResMove.Enemy)
        {
            if (!resActionSpawned)
            {
                if (isPlayingAttack1) return false;
                animator.SetTrigger("Attack1");
                isPlayingAttack1 = true;
                resActionSpawned = true;
            }
            return resTimer > 8.5f;
        }
        else if (currentRes == ResMove.FireWave)
        {
            if (currentOff == OffMove.HomingFireballs) return offIndex >= 14;
            if (resIndex >= 7) return true;
            PlayerHealth playerHealth = attackManager.player.GetComponent<PlayerHealth>();
            float baseDelay = (playerHealth != null && playerHealth.CurrentHealth < 3) ? 2.0f : 1.0f;
            float padding = (currentOff == OffMove.Fireballs) ? 0.5f : 1.5f;
            float spawnDelay = baseDelay + padding;

            if (resTimer > (spawnDelay + 1.5f))
            {
                if (isPlayingAttack2) return false;
                resTimer = 1.5f;
                animator.SetTrigger("Attack2");
                isPlayingAttack2 = true;
                resIndex++;
            }
            return false;
        }
        else if (currentRes == ResMove.Platforms)
        {
            // Platforms, FloorFire and FireColumns are a combined attack — spawned together in AnimEvent_Attack2
            PlatformController platform = FindFirstObjectByType<PlatformController>();
            if (platform == null) return currentOff != OffMove.FireColumns;
            return platform.cycleComplete;
        }
        return true;
    }

    public void AnimEvent_Attack1() 
    {
        // Check if we should spawn an Offensive attack
        if (currentOff == OffMove.Fireballs)
        {
            attackManager.spawnFireball(facingDirection);
        }
        else if (currentOff == OffMove.HomingFireballs)
        {
            attackManager.spawnHomingFireball(facingDirection);
        }

        // Separately check if we should spawn a Restrictive attack
        // if (currentRes == ResMove.Enemy)
        // {
        //     attackManager.spawnEnemy(facingDirection);
        // }

        if (currentRes == ResMove.Enemy && currentOff != OffMove.Fireballs && currentOff != OffMove.HomingFireballs)
            attackManager.spawnEnemy(facingDirection); // standalone enemy spawn, no fireball combo
        else if (currentRes == ResMove.Enemy && offIndex % 3 == 0)
            attackManager.spawnEnemy(facingDirection); // paired with fireballs, throttled

    }

    public void AnimEvent_Attack2()
    {
        // FireColumns + Platforms + FloorFire are a combined attack, spawn all together
        if (currentOff == OffMove.FireColumns)
        {
            attackManager.spawnFireColumns(facingDirection);
            attackManager.spawnPlatforms(facingDirection);
            attackManager.spawnFloorFire(facingDirection);
        }
        else if (currentOff == OffMove.FireExplosion)
        {
            attackManager.spawnFireExplosion(facingDirection);
        }

        // Separately check if we should spawn a Restrictive attack
        if (currentRes == ResMove.FireRow)
        {
            attackManager.spawnFireRow(facingDirection);
        }
        // else if (currentRes == ResMove.FireWave)
        // {
        //     attackManager.spawnFireWave(facingDirection);
        // }
        else if (currentRes == ResMove.FireWave)
            attackManager.spawnFireWave(facingDirection);
    }

    public void AnimEvent_Attack1Complete()
    {
        isPlayingAttack1 = false;
    }

    public void AnimEvent_Attack2Complete()
    {
        isPlayingAttack2 = false;
    }

    void Damage()
    {
        if (Time.time >= lastDamageTime + damageCooldown)
        {
            lastDamageTime = Time.time;
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.ModifyHealth(-damage);
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (_isRewinding) return;
        if (col.gameObject.CompareTag("Player")) Damage();
        if (col.gameObject.CompareTag("Ground")) 
        {
            isGrounded = true;
            animator.SetBool("isGrounded", true);
        }
    }

    public override void OnStartRewind() { base.OnStartRewind(); _isRewinding = true; }
    //public override void OnStopRewind() { base.OnStopRewind(); _isRewinding = false; }
    public override void OnStopRewind() 
    { 
        base.OnStopRewind(); 
        _isRewinding = false; 

        // Snap health bar to rewound health value
        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.SyncAfterRewind();
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();

        state.SetCustomData("Phase", (int)currentPhase);
        state.SetCustomData("PosType", (int)currentPos);
        state.SetCustomData("OffType", (int)currentOff);
        state.SetCustomData("ResType", (int)currentRes);

        state.SetCustomData("IdleTimer", idleTimer);
        state.SetCustomData("PosTimer", posTimer);
        state.SetCustomData("OffTimer", offTimer);
        state.SetCustomData("ResTimer", resTimer);

        state.SetCustomData("OffIndex", offIndex);
        state.SetCustomData("ResIndex", resIndex);
        state.SetCustomData("Facing", facingDirection);

        state.SetCustomData("TargetX", finalTargetX);
        state.SetCustomData("MoveStart", moveStart);
        state.SetCustomData("MovePeak", movePeak);
        state.SetCustomData("MoveTarget", moveTarget);

        state.SetCustomData("OffSpawned", offActionSpawned);
        state.SetCustomData("ResSpawned", resActionSpawned);

        // Capture animator state so walk animation reverses properly during rewind
        if (animator != null)
        {
            AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
            state.SetCustomData("AnimStateHash", animInfo.fullPathHash);
            state.SetCustomData("AnimNormalizedTime", animInfo.normalizedTime);
        }

        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);

        currentPhase = (BossPhase)state.GetCustomData<int>("Phase", 0);
        currentPos = (PosMove)state.GetCustomData<int>("PosType", 0);
        currentOff = (OffMove)state.GetCustomData<int>("OffType", 0);
        currentRes = (ResMove)state.GetCustomData<int>("ResType", 0);

        idleTimer = state.GetCustomData<float>("IdleTimer", 0);
        posTimer = state.GetCustomData<float>("PosTimer", 0);
        offTimer = state.GetCustomData<float>("OffTimer", 0);
        resTimer = state.GetCustomData<float>("ResTimer", 0);

        offIndex = state.GetCustomData<int>("OffIndex", 0);
        resIndex = state.GetCustomData<int>("ResIndex", 0);
        facingDirection = state.GetCustomData<int>("Facing", 1);

        finalTargetX = state.GetCustomData<float>("TargetX", 0);
        moveStart = state.GetCustomData<Vector2>("MoveStart", Vector2.zero);
        movePeak = state.GetCustomData<Vector2>("MovePeak", Vector2.zero);
        moveTarget = state.GetCustomData<Vector2>("MoveTarget", Vector2.zero);

        offActionSpawned = state.GetCustomData<bool>("OffSpawned", false);
        resActionSpawned = state.GetCustomData<bool>("ResSpawned", false);

        // Restore animator state so walk animation plays in reverse during rewind
        if (animator != null)
        {
            int animStateHash = state.GetCustomData<int>("AnimStateHash", 0);
            float animNormalizedTime = state.GetCustomData<float>("AnimNormalizedTime", 0f);
            
            if (animStateHash != 0)
            {
                animator.Play(animStateHash, 0, animNormalizedTime);
            }
            animator.Update(0f);  // Apply the animation state without time progression
        }

        // Keep health bar in sync during rewind scrubbing
        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.SyncAfterRewind();
    }
}