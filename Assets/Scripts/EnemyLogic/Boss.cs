using UnityEngine;
using TimeRewind;

public class Boss : EnemyBase, IRewindable
{
    public Transform player;
    public BossAttackManager attackManager;
    public PlayerStrategyModel playerStrategyModel; 

    public int damage = 1;
    public float damageCooldown = 1.5f;
    public float playerPushSpeed = 3f;

    // Belief-driven spell cadence multiplier, refreshed per combat phase from the GMM.
    float tempoMultiplier = 1f;
    CameraShake cameraShake;

    bool _isRewinding;
    bool offActionSpawned;
    bool resActionSpawned;
    // Once-per-phase spawn guards for the two "heavy / one-shot" payloads. FireExplosion and
    // FireWave spawn per Attack2 event on purpose (interleaving is the intended fantasy); only
    // the FireColumns bundle and FireRow must never stack within a single combat round.
    bool bundleSpawned;
    bool fireRowSpawned;
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

    // The boss keeps a 2-deep queue of upcoming actions ("next" and "nextNext"). Both slots
    // are part of the rewind state, so rewinding past the current attack still preserves the
    // next attack's identity — forward play will hit the same roll.
    bool nextIsPositional;
    PosMove nextPosMove;
    OffMove nextOff;
    ResMove nextRes;
    bool nextNextIsPositional;
    PosMove nextNextPosMove;
    OffMove nextNextOff;
    ResMove nextNextRes;

    // Seconds the boss stays idle between phases. Longer gives the player more room to rewind
    // into the idle window without losing the pre-rolled attack.
    const float IdleDuration = 1.5f;

    // Movement Tracking
    Vector2 moveStart;
    Vector2 movePeak;
    Vector2 moveTarget;
    const float ArenaMinX = -11f;
    const float ArenaMaxX =  11f;
    // Safety net: if the boss's x exceeds this (e.g. launched off a stray platform),
    // it gets teleported back to the matching arena edge.
    const float OffSceneThreshold = 12f;
    // Beyond this |x|, we don't shove the player further toward the edge on a jump-landing hit.
    const float SafePushEdgeX = 9.5f;
    const float JumpAttackPushSpeed = 1.5f;
    float finalTargetX;
    float jumpLateral;
    private Vector3 originalScale;
    private Animator animator;
    private float groundedY;
    

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        cameraShake = Camera.main.GetComponent<CameraShake>();
        originalScale = transform.localScale;
        groundedY = transform.position.y;
        animator = GetComponent<Animator>();
        FacePlayer();
        musicController = FindFirstObjectByType<RewindMusicController>();
        if (musicController != null)
        {
            musicController.PlayBossFightMusic();
        }
        EndPhase();
        InitializeActionQueue();
        if (attackManager != null) attackManager.boss = transform;
        // Start the boss health bar
        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.StartFight();
    }

    public override void Update()
    {
        base.Update();
        if (_isRewinding || player == null || wasDead) return;

        if (Mathf.Abs(transform.position.x) > OffSceneThreshold)
        {
            TeleportToSafeEdge();
            return;
        }

        switch (currentPhase)
        {
            case BossPhase.Idle: UpdateIdle(); break;
            case BossPhase.Positional: UpdatePositional(); break;
            case BossPhase.Combat: UpdateCombat(); break;
        }
    }

    // Safety net for when stray platforms (or any other hazard) launch the boss off-screen.
    // Snaps the boss back to the arena edge on the same side it left from, facing inward,
    // wipes any lingering platforms, and resets to Idle so combat can resume cleanly.
    void TeleportToSafeEdge()
    {
        float edgeX = transform.position.x < 0f ? ArenaMinX : ArenaMaxX;
        Vector2 pos = new Vector2(edgeX, groundedY);

        rb.linearVelocity = Vector2.zero;
        rb.position = pos;
        transform.position = pos;
        isGrounded = true;

        FacePlayer();
        facingDirection = player.position.x > transform.position.x ? -1 : 1;

        foreach (var pc in FindObjectsByType<PlatformController>(FindObjectsSortMode.None))
        {
            Destroy(pc.gameObject);
        }

        EndPhase();
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

    // Fills both queue slots. Called once from Start so there's always a 2-move lookahead.
    void InitializeActionQueue()
    {
        RollPlan(out nextIsPositional, out nextPosMove, out nextOff, out nextRes);
        RollPlan(out nextNextIsPositional, out nextNextPosMove, out nextNextOff, out nextNextRes);
    }

    // Consume the front plan (next*) and shift nextNext into its place; roll a fresh back slot.
    // Called right after an action is kicked off in UpdateIdle.
    void AdvanceQueue()
    {
        nextIsPositional = nextNextIsPositional;
        nextPosMove = nextNextPosMove;
        nextOff = nextNextOff;
        nextRes = nextNextRes;
        RollPlan(out nextNextIsPositional, out nextNextPosMove, out nextNextOff, out nextNextRes);
    }

    // Rolls a single action plan. Positional-vs-combat split, then the specifics.
    // Writes into out params so the same routine can target either queue slot.
    void RollPlan(out bool isPositional, out PosMove posMove, out OffMove off, out ResMove res)
    {
        isPositional = Random.value > 0.8f;
        if (isPositional)
        {
            // Aggressive players press close → boss relocates (ChangeSides).
            // Cautious players camp → boss drops on them (GroundPound).
            ReadBeliefs(out float aggressive, out float _, out float cautious);
            float groundPoundChance = Mathf.Clamp01(0.5f + 0.3f * cautious - 0.3f * aggressive);
            posMove = Random.value < groundPoundChance ? PosMove.GroundPound : PosMove.ChangeSides;
            off = OffMove.None;
            res = ResMove.None;
        }
        else
        {
            posMove = PosMove.None;
            RollCombatMoves(out off, out res);
        }
    }

    void RollCombatMoves(out OffMove off, out ResMove res)
    {
        ReadBeliefs(out float aggressive, out float evasive, out float _);

        float roll = Random.value;

        if (roll < aggressive)
        {
            off = OffMove.FireColumns;
            res = ResMove.None;
        }
        else if (roll < aggressive + evasive)
        {
            off = OffMove.Fireballs;
            res = ResMove.Enemy;
        }
        else
        {
            // Cautious players camp and rewind — punish with tracking + area denial
            off = OffMove.HomingFireballs;
            res = ResMove.FireWave;
        }

        //Don't want too much repetition
        if (off == lastOff && res == lastRes)
        {
            repeatCount++;
            if (repeatCount >= 2)
            {
                OffMove[] allOff = new[] { OffMove.Fireballs, OffMove.FireColumns, OffMove.HomingFireballs, OffMove.FireExplosion };
                do { off = allOff[Random.Range(0, allOff.Length)]; }
                while (off == lastOff);

                ResMove[] allRes = new[] { ResMove.FireRow, ResMove.FireWave, ResMove.Enemy };
                do { res = allRes[Random.Range(0, allRes.Length)]; }
                while (res == lastRes);

                repeatCount = 0;
            }
        }
        else
        {
            repeatCount = 0;
        }

        lastOff = off;
        lastRes = res;
    }

    void ReadBeliefs(out float aggressive, out float evasive, out float cautious)
    {
        // TEST OVERRIDE: (1,0,0)=Aggressive, (0,1,0)=Evasive, (0,0,1)=Cautious. Comment out to use the GMM.
        //(aggressive, evasive, cautious) = (0.33f, 0.33f, 0.34f); return;

        if (playerStrategyModel == null ||
            playerStrategyModel.playerTacticalModel == null ||
            playerStrategyModel.playerTacticalModel.tacticBeliefs == null)
        {
            aggressive = evasive = cautious = 1f / 3f;
            return;
        }
        var tactics = playerStrategyModel.playerTacticalModel.tacticBeliefs;
        aggressive = tactics[PlayerTacticalModel.TacticType.Aggressive];
        evasive    = tactics[PlayerTacticalModel.TacticType.Evasive];
        cautious   = tactics[PlayerTacticalModel.TacticType.Cautious];
    }

    // Pushes belief-derived tuning into combat timing + targeting. Called once per combat
    // phase so the feel of the fight matches the GMM's current read on the player.
    void ApplyBeliefModulation()
    {
        ReadBeliefs(out float aggressive, out float evasive, out float cautious);

        // Aggressive + cautious both invite faster pressure; evasive players already move
        // plenty, so keep their cadence close to the default to avoid over-saturation.
        tempoMultiplier = Mathf.Clamp(1f - 0.3f * aggressive - 0.15f * cautious, 0.55f, 1.1f);

        // Campers get spawns biased onto them; dashers get more random spread.
        if (attackManager != null)
            attackManager.playerTargetBias = Mathf.Clamp01(0.75f + 0.2f * cautious - 0.2f * evasive);
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
        if (idleTimer < IdleDuration) return;

        if (nextIsPositional) StartPositional();
        else StartCombat();

        AdvanceQueue();
    }

    void StartPositional()
    {
        currentPhase = BossPhase.Positional;
        posTimer = 0f;

        if (nextPosMove == PosMove.GroundPound) StartGroundPound();
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

        ApplyBeliefModulation();

        currentPhase = BossPhase.Combat;
        offTimer = 0f; resTimer = 0f;
        offIndex = 0; resIndex = 0;
            
        offActionSpawned = false;
        resActionSpawned = false;
        bundleSpawned = false;
        fireRowSpawned = false;
            
        currentOff = nextOff;
        currentRes = nextRes;
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
            spawnDelay *= tempoMultiplier;

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
            // Full bundle mode (no res spell): wait for platforms to finish their cycle.
            if (currentRes == ResMove.None)
            {
                PlatformController platform = FindFirstObjectByType<PlatformController>();
                if (platform != null && !platform.cycleComplete) return false;
            }
            return offTimer > 5f;
        }
        else if (currentOff == OffMove.HomingFireballs)
        {
            if (offIndex >= 14) return true;
            PlayerHealth playerHealth = attackManager.player.GetComponent<PlayerHealth>();
            float spawnDelay = (playerHealth != null && playerHealth.CurrentHealth < 3) ? 2f : 1f;
            spawnDelay *= tempoMultiplier;

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
            spawnDelay *= tempoMultiplier;

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
            // Skip the res trigger entirely if FireRow was already spawned by the offensive
            // side's Attack2 event — prevents the boss playing a cast animation for nothing.
            if (!fireRowSpawned && !resActionSpawned && resTimer > 1.5f && resTimer < 1.6f)
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

            // Completion: sync to off phase when paired with HomingFireballs; otherwise cap at 7 waves.
            if (currentOff == OffMove.HomingFireballs) return offIndex >= 14;
            return resIndex >= 7;
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
        // Each payload decides its own spawn cadence. Bundle (FireColumns + Platforms +
        // FloorFire) and FireRow are one-shot per combat phase (stacking them is undodgeable).
        // FireExplosion and FireWave spawn on every Attack2 event — that's what gives the
        // "interleaved spells" feeling when both sides use Attack2.
        if (currentOff == OffMove.FireColumns && !bundleSpawned)
        {
            attackManager.spawnFireColumns(facingDirection);
            // Platforms + floor fire only come along as the full bundle — when no res
            // spell is paired. Otherwise FireColumns is just the columns alone.
            if (currentRes == ResMove.None)
            {
                attackManager.spawnPlatforms(facingDirection);
                attackManager.spawnFloorFire(facingDirection);
            }
            bundleSpawned = true;
        }
        else if (currentOff == OffMove.FireExplosion)
        {
            attackManager.spawnFireExplosion(facingDirection);
        }

        if (currentRes == ResMove.FireRow && !fireRowSpawned)
        {
            attackManager.spawnFireRow(facingDirection);
            fireRowSpawned = true;
        }
        else if (currentRes == ResMove.FireWave)
        {
            attackManager.spawnFireWave(facingDirection);
        }
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

    public override void Die()
    {
        if (wasDead) return;
        wasDead = true;
        isDead = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        // Every collider off — body contact, attack hit, everything.
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;

        if (foresightGlow != null) foresightGlow.SetActive(false);

        // Any-state → Die transition; the clip's last frame holds because we never clear the bool.
        if (animator != null) animator.SetBool("Death", true);

        OnDeath?.Invoke();
        // Intentionally skip DeathRoutine — boss stays visible on the final death frame instead
        // of vanishing like regular enemies.
    }

    public override void Revive()
    {
        base.Revive();
        foreach (var c in GetComponents<Collider2D>()) c.enabled = true;
        if (animator != null) animator.SetBool("Death", false);
        isDead = false;

        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.StartFight();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (_isRewinding || wasDead) return;
        if (col.gameObject.CompareTag("Player"))
        {
            Damage();
            Rigidbody2D playerRb = col.gameObject.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                float pushDir;
                float pushSpeed;
                bool isJumpAttack = currentPhase == BossPhase.Positional &&
                                    (currentPos == PosMove.GroundPound || currentPos == PosMove.ChangeSides);
                if (isJumpAttack)
                {
                    // Shove opposite to the boss's horizontal travel so the player clears
                    // the path of the continuing jump instead of eating a second hit.
                    float travel = moveTarget.x - moveStart.x;
                    pushDir = travel >= 0f ? -1f : 1f;
                    pushSpeed = JumpAttackPushSpeed;

                    // If this direction would send the player past the safe edge, cancel
                    // the horizontal shove so they can't get knocked off the stage.
                    float playerX = col.transform.position.x;
                    if ((pushDir > 0f && playerX > SafePushEdgeX) ||
                        (pushDir < 0f && playerX < -SafePushEdgeX))
                    {
                        pushSpeed = 0f;
                    }
                }
                else
                {
                    // Push toward whichever side of the stage has more room
                    pushDir = col.transform.position.x <= 0f ? 1f : -1f;
                    pushSpeed = playerPushSpeed;
                }
                playerRb.linearVelocity = new Vector2(pushDir * pushSpeed, playerRb.linearVelocity.y);
            }
        }
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
        state.SetCustomData("BundleSpawned", bundleSpawned);
        state.SetCustomData("FireRowSpawned", fireRowSpawned);

        state.SetCustomData("NextIsPositional", nextIsPositional);
        state.SetCustomData("NextPosMove", (int)nextPosMove);
        state.SetCustomData("NextOff", (int)nextOff);
        state.SetCustomData("NextRes", (int)nextRes);
        state.SetCustomData("NextNextIsPositional", nextNextIsPositional);
        state.SetCustomData("NextNextPosMove", (int)nextNextPosMove);
        state.SetCustomData("NextNextOff", (int)nextNextOff);
        state.SetCustomData("NextNextRes", (int)nextNextRes);
        state.SetCustomData("LastOff", (int)lastOff);
        state.SetCustomData("LastRes", (int)lastRes);
        state.SetCustomData("RepeatCount", repeatCount);

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
        bool wasDeadBefore = wasDead;
        base.ApplyState(state);

        // Base handles wasDead + the first collider + sprite; boss needs the Death animator
        // bool and every collider synced too, plus the health bar re-shown on revive.
        if (wasDead)
        {
            isDead = true;
            if (animator != null) animator.SetBool("Death", true);
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        }
        else
        {
            isDead = false;
            if (animator != null) animator.SetBool("Death", false);
            foreach (var c in GetComponents<Collider2D>()) c.enabled = true;
            if (wasDeadBefore)
            {
                BossHealthBarDriver reviveDriver = GetComponent<BossHealthBarDriver>();
                if (reviveDriver != null) reviveDriver.StartFight();
            }
        }

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
        bundleSpawned = state.GetCustomData<bool>("BundleSpawned", false);
        fireRowSpawned = state.GetCustomData<bool>("FireRowSpawned", false);

        nextIsPositional = state.GetCustomData<bool>("NextIsPositional", false);
        nextPosMove = (PosMove)state.GetCustomData<int>("NextPosMove", 0);
        nextOff = (OffMove)state.GetCustomData<int>("NextOff", 0);
        nextRes = (ResMove)state.GetCustomData<int>("NextRes", 0);
        nextNextIsPositional = state.GetCustomData<bool>("NextNextIsPositional", false);
        nextNextPosMove = (PosMove)state.GetCustomData<int>("NextNextPosMove", 0);
        nextNextOff = (OffMove)state.GetCustomData<int>("NextNextOff", 0);
        nextNextRes = (ResMove)state.GetCustomData<int>("NextNextRes", 0);
        lastOff = (OffMove)state.GetCustomData<int>("LastOff", 0);
        lastRes = (ResMove)state.GetCustomData<int>("LastRes", 0);
        repeatCount = state.GetCustomData<int>("RepeatCount", 0);

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