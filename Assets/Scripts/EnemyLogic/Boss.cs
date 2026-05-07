using UnityEngine;
using TimeRewind;
using System.Collections;

public class Boss : EnemyBase, IRewindable
{
    public Transform player;
    public BossAttackManager attackManager;
    public PlayerStrategyModel playerStrategyModel; 

    public int damage = 1;
    public float damageCooldown = 1.5f;
    public float playerPushSpeed = 3f;

    // spell cadence multiplier, set per phase from the GMM
    float tempoMultiplier = 1f;
    CameraShake cameraShake;

    bool _isRewinding;
    bool offActionSpawned;
    bool resActionSpawned;
    // stops fire columns + fire row spawning twice in one round
    bool bundleSpawned;
    bool fireRowSpawned;
    int facingDirection = 1;
    bool isGrounded;
    float lastDamageTime;

    bool isPlayingAttack1;
    bool isPlayingAttack2;

    // not captured in rewind state, so rewinding past the threshold doesnt demote the
    // boss back to phase1 + re-trigger the dialogue
    public enum FightStage { Waiting, Phase1, Phase2 }
    public FightStage fightStage = FightStage.Waiting;

    // BossFightController flips this during dialogue so Update freezes the boss
    // without needing Time.timeScale (which would freeze the player too)
    public bool dialoguePaused;

    enum BossPhase { Idle, Positional, Combat }
    enum PosMove { None, GroundPound, ChangeSides, Melee }
    enum MeleeSubPhase { WalkToPlayer, Attacking, WalkAway }
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
    OffMove lastLastOff = OffMove.None;
    ResMove lastRes = ResMove.None;

    // belief read picks a pool, then a random pair is drawn from it
    struct AttackPair { public OffMove off; public ResMove res; }

    static readonly AttackPair[] RecklessPool =
    {
        new AttackPair { off = OffMove.FireColumns,   res = ResMove.None },
        new AttackPair { off = OffMove.FireColumns,   res = ResMove.FireWave },
        new AttackPair { off = OffMove.FireExplosion, res = ResMove.FireRow },
    };
    static readonly AttackPair[] EvasivePool =
    {
        new AttackPair { off = OffMove.Fireballs,       res = ResMove.Enemy },
        new AttackPair { off = OffMove.Fireballs,       res = ResMove.FireWave },
        new AttackPair { off = OffMove.HomingFireballs, res = ResMove.Enemy },
    };
    static readonly AttackPair[] CautiousPool =
    {
        new AttackPair { off = OffMove.HomingFireballs, res = ResMove.FireWave },
        new AttackPair { off = OffMove.HomingFireballs, res = ResMove.FireRow },
        new AttackPair { off = OffMove.FireExplosion,   res = ResMove.FireWave },
    };
    // fallback when no belief axis dominates, or when the spoiler roll fires
    static readonly AttackPair[] MixedPool =
    {
        new AttackPair { off = OffMove.FireColumns,     res = ResMove.Enemy },
        new AttackPair { off = OffMove.Fireballs,       res = ResMove.FireRow },
        new AttackPair { off = OffMove.HomingFireballs, res = ResMove.None },
    };

    // below this, treat the player as unreadable and pick from MixedPool
    const float AxisDominanceThreshold = 0.40f;
    // random roll that ignores beliefs
    const float SpoilerChance = 0.20f;

    // 2-deep action queue. both slots are part of rewind state so rewinding past
    // the current attack still hits the same next roll on forward play
    bool nextIsPositional;
    PosMove nextPosMove;
    OffMove nextOff;
    ResMove nextRes;
    bool nextNextIsPositional;
    PosMove nextNextPosMove;
    OffMove nextNextOff;
    ResMove nextNextRes;

    // idle time between phases
    const float IdleDuration = 1.5f;

    // Movement Tracking
    Vector2 moveStart;
    Vector2 movePeak;
    Vector2 moveTarget;
    // arena bounds. was ±11 but moved to ±12 so theres no strip behind the boss
    // to cheese ranged fights from
    const float ArenaMinX = -12f;
    const float ArenaMaxX =  12f;
    // if boss x exceeds this, teleport it back. kept 1 past the clamp so normal
    // movement doesnt trip it
    const float OffSceneThreshold = 13f;
    // beyond this |x|, dont shove the player further toward the edge on a jump-landing hit
    const float SafePushEdgeX = 9.5f;
    const float JumpAttackPushSpeed = 1.5f;
    float finalTargetX;
    float jumpLateral;
    private Vector3 originalScale;
    private Animator animator;
    private float groundedY;

    // melee state: snapshot player x, walk there, swing, retreat to nearest edge.
    // all three sub-phases share posTimer
    MeleeSubPhase meleeSubPhase;
    float meleeTargetX;
    float meleeRetreatX;
    [Header("Melee Movement")]
    [Tooltip("Speed when charging toward the player")]
    public float meleeRunSpeed = 63f;
    public float meleeStopBuffer = 0.6f;
    // extra horizontal reach beyond the stop distance. damage registers if player is
    // within (bossHalfWidth + playerHalfWidth + meleeStopBuffer + meleeHitReach)
    public float meleeHitReach = 1.5f;
    const float MeleeReachTolerance = 0.15f;
    const float MeleeSwingDuration = 0.7f;
    const float MeleeHitTime = 0.3f;
    bool meleeHitApplied;
    [Header("Audio")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private float jumpVolume = 1f;
    [SerializeField] private AudioClip smashClip;
    [SerializeField] private float smashVolume = 0.4f;
    [SerializeField] private AudioClip swingClip;
    [SerializeField] private float swingVolume = 0.8f;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float footstepVolume = 0.5f;
    

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        cameraShake = Camera.main.GetComponent<CameraShake>();
        originalScale = transform.localScale;
        groundedY = transform.position.y;
        animator = GetComponent<Animator>();
        playerStrategyModel = PlayerStrategyModel.Instance; 
        FacePlayer();
        musicController = FindFirstObjectByType<RewindMusicController>();
        EndPhase();
        InitializeActionQueue();
        if (attackManager != null) attackManager.boss = transform;
        // music + health bar wait until BeginFight() so the intro dialogue plays first
    }

    // called by BossFightController after the intro dialogue ends. before this,
    // fightStage is Waiting and Update early-returns
    public void BeginFight(FightStage stage)
    {
        fightStage = stage;
        if (musicController != null) musicController.PlayBossFightMusic();
        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.StartFight();
    }

    // flip to phase 2 and re-roll the queue so upcoming plans use the GMM branch
    public void AdvanceToPhase2()
    {
        fightStage = FightStage.Phase2;
        InitializeActionQueue();
    }

    public override void Update()
    {
        base.Update();
        if (fightStage == FightStage.Waiting || dialoguePaused || _isRewinding || player == null || wasDead) return;

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

    // if a stray platform launches the boss off-screen, snap it back to the same-side
    // edge facing inward, wipe lingering platforms, reset to Idle
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
        animator.SetBool("isRunning", false);
    }

    // fills both queue slots, called once from Start
    void InitializeActionQueue()
    {
        RollPlan(out nextIsPositional, out nextPosMove, out nextOff, out nextRes);
        RollPlan(out nextNextIsPositional, out nextNextPosMove, out nextNextOff, out nextNextRes);
    }

    // consume next*, shift nextNext into its place, roll a fresh back slot
    void AdvanceQueue()
    {
        nextIsPositional = nextNextIsPositional;
        nextPosMove = nextNextPosMove;
        nextOff = nextNextOff;
        nextRes = nextNextRes;
        RollPlan(out nextNextIsPositional, out nextNextPosMove, out nextNextOff, out nextNextRes);
    }

    // rolls a single action plan. positional/combat split first, then specifics
    void RollPlan(out bool isPositional, out PosMove posMove, out OffMove off, out ResMove res)
    {
        /* TEST: force every attack to be Melee. Remove this block to restore normal rolls.
        isPositional = true;
        posMove = PosMove.Melee;
        off = OffMove.None;
        res = ResMove.None;
        return; */

        if (fightStage != FightStage.Phase2)
        {
            RollPlanDumb(out isPositional, out posMove, out off, out res);
            return;
        }

        isPositional = Random.value > 0.8f;
        if (isPositional)
        {
            // aggressive players press close, so boss relocates (ChangeSides)
            // cautious players camp, so boss drops on them (GroundPound)
            // cautious/evasive players who keep distance invite the Melee chase
            ReadBeliefs(out float reckless, out float evasive, out float cautious, out float idle);
            float meleeChance = Mathf.Clamp01(0.33f + 0.2f * cautious + 0.1f * evasive - 0.2f * reckless);
            if (Random.value < meleeChance)
            {
                posMove = PosMove.Melee;
            }
            else
            {
                float groundPoundChance = Mathf.Clamp01(0.5f + 0.3f * cautious - 0.3f * reckless);
                posMove = Random.value < groundPoundChance ? PosMove.GroundPound : PosMove.ChangeSides;
            }
            off = OffMove.None;
            res = ResMove.None;
        }
        else
        {
            posMove = PosMove.None;
            RollCombatMoves(out off, out res);
        }
    }

    // phase 1 roller: 20% positional, otherwise equal-chance off/res pair. no beliefs,
    // no tempo modulation
    void RollPlanDumb(out bool isPositional, out PosMove posMove, out OffMove off, out ResMove res)
    {
        isPositional = Random.value < 0.2f;
        if (isPositional)
        {
            PosMove[] pool = new[] { PosMove.GroundPound, PosMove.ChangeSides, PosMove.Melee };
            posMove = pool[Random.Range(0, pool.Length)];
            off = OffMove.None;
            res = ResMove.None;
            return;
        }

        posMove = PosMove.None;
        int pair = Random.Range(0, 3);
        if (pair == 0)      { off = OffMove.FireColumns;     res = ResMove.None; }
        else if (pair == 1) { off = OffMove.Fireballs;       res = ResMove.Enemy; }
        else                { off = OffMove.HomingFireballs; res = ResMove.FireWave; }
    }

    void RollCombatMoves(out OffMove off, out ResMove res)
    {
        ReadBeliefs(out float reckless, out float evasive, out float cautious, out float idle);

        AttackPair[] pool = SelectPool(reckless, evasive, cautious, idle);

        // block triples only (three same Off in a row). doubles are fine so the
        // pool's 2:1 ratio actually shows up
        AttackPair picked = pool[Random.Range(0, pool.Length)];
        for (int tries = 0; tries < 6 && picked.off == lastOff && picked.off == lastLastOff; tries++)
        {
            picked = pool[Random.Range(0, pool.Length)];
        }
        // if the pool somehow cant break a triple, pull a differing pair from MixedPool
        if (picked.off == lastOff && picked.off == lastLastOff)
        {
            for (int i = 0; i < MixedPool.Length; i++)
            {
                if (MixedPool[i].off != lastOff) { picked = MixedPool[i]; break; }
            }
        }

        off = picked.off;
        res = picked.res;
        lastLastOff = lastOff;
        lastOff = off;
        lastRes = res;
    }

    // picks a pool. spoiler roll fires no matter what; otherwise if no axis clears the
    // dominance threshold, fall back to MixedPool
    AttackPair[] SelectPool(float reckless, float evasive, float cautious, float idle)
    {
        if (Random.value < SpoilerChance) return MixedPool;

        float max = Mathf.Max(reckless, Mathf.Max(evasive, cautious));
        if (max < AxisDominanceThreshold) return MixedPool;

        if (reckless >= evasive && reckless >= cautious) return RecklessPool;
        if (evasive    >= cautious)                          return EvasivePool;
        return CautiousPool;
    }

    void ReadBeliefs(out float reckless, out float evasive, out float cautious, out float idle)
    {
        // TEST OVERRIDE: (1,0,0)=Aggressive, (0,1,0)=Evasive, (0,0,1)=Cautious. Comment out to use the GMM.
        //(aggressive, evasive, cautious) = (0.33f, 0.33f, 0.34f); return;

        if (playerStrategyModel == null ||
            playerStrategyModel.playerTacticalModel == null ||
            playerStrategyModel.playerTacticalModel.tacticBeliefs == null)
        {
            reckless = evasive = cautious = idle = 0.25f;
            return;
        }

        var tactics = playerStrategyModel.playerTacticalModel.tacticBeliefs;
        reckless = tactics[PlayerTacticalModel.TacticType.Reckless];
        evasive = tactics[PlayerTacticalModel.TacticType.Evasive];
        cautious = tactics[PlayerTacticalModel.TacticType.Cautious];
        idle = tactics[PlayerTacticalModel.TacticType.Idle]; 
    }

    // pushes belief-derived tuning into combat timing + targeting, once per combat phase
    void ApplyBeliefModulation()
    {
        ReadBeliefs(out float reckless, out float evasive, out float cautious, out float idle);

        // aggressive and cautious both invite faster pressure. evasive players already
        // move a lot so keep their cadence near default
        tempoMultiplier = Mathf.Clamp(1f - 0.3f * reckless - 0.15f * cautious, 0.55f, 1.1f);

        // campers get spawns biased onto them, dashers get more random spread
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
        else if (nextPosMove == PosMove.Melee) StartMelee();
        else StartChangeSides();
    }

    void UpdatePositional()
    {
        if (currentPos == PosMove.GroundPound) UpdateGroundPound();
        else if (currentPos == PosMove.ChangeSides) UpdateChangeSides();
        else if (currentPos == PosMove.Melee) UpdateMelee();
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
                playSmashSound();
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

    void StartMelee()
    {
        currentPos = PosMove.Melee;
        meleeSubPhase = MeleeSubPhase.WalkToPlayer;
        // snapshot player x so the boss commits to a fixed strike point. rewind-safe
        // because its captured in state
        float playerX = player.position.x;

        // pull the strike point back by both half-widths + a buffer so the boss stops
        // just short of the player instead of overrunning
        float bossHalfWidth = 0f;
        var bossCol = GetComponent<Collider2D>();
        if (bossCol != null) bossHalfWidth = bossCol.bounds.extents.x;
        float playerHalfWidth = 0f;
        var playerCol = player.GetComponent<Collider2D>();
        if (playerCol != null) playerHalfWidth = playerCol.bounds.extents.x;
        float approachDir = playerX > transform.position.x ? 1f : -1f;
        float stopX = playerX - approachDir * (bossHalfWidth + playerHalfWidth + meleeStopBuffer);

        meleeTargetX = Mathf.Clamp(stopX, ArenaMinX + 1f, ArenaMaxX - 1f);
        posTimer = 0f;
        meleeHitApplied = false;
        animator.SetBool("isGrounded", true);
        animator.SetBool("isRunning", true);
    }

    void UpdateMelee()
    {
        posTimer += Time.deltaTime;

        switch (meleeSubPhase)
        {
            case MeleeSubPhase.WalkToPlayer:
                WalkMeleeTowards(meleeTargetX);
                if (Mathf.Abs(transform.position.x - meleeTargetX) <= MeleeReachTolerance)
                {
                    animator.SetBool("isRunning", false);
                    animator.SetTrigger("Melee");
                    meleeSubPhase = MeleeSubPhase.Attacking;
                    posTimer = 0f;
                }
                break;

            case MeleeSubPhase.Attacking:
                // partway through the swing, check once if the player is in reach and
                // apply damage directly. collision hit doesnt fire because the boss stops
                // short of the player
                if (!meleeHitApplied && posTimer >= MeleeHitTime)
                {
                    meleeHitApplied = true;
                    float bossHalfWidth = 0f;
                    var bossCol = GetComponent<Collider2D>();
                    if (bossCol != null) bossHalfWidth = bossCol.bounds.extents.x;
                    float playerHalfWidth = 0f;
                    var playerCol = player.GetComponent<Collider2D>();
                    if (playerCol != null) playerHalfWidth = playerCol.bounds.extents.x;
                    float dx = Mathf.Abs(player.position.x - transform.position.x);
                    if (dx <= bossHalfWidth + playerHalfWidth + meleeStopBuffer + meleeHitReach)
                    {
                        PlayerHealth ph = player.GetComponent<PlayerHealth>();
                        if (ph != null) ph.ModifyHealth(-damage);
                        lastDamageTime = Time.time;
                    }
                }
                if (posTimer >= MeleeSwingDuration)
                {
                    meleeRetreatX = transform.position.x <= 0f ? ArenaMinX : ArenaMaxX;
                    meleeSubPhase = MeleeSubPhase.WalkAway;
                    posTimer = 0f;
                    animator.SetBool("isRunning", true);
                    // force out of Melee even if the clip isnt at its exit time, otherwise
                    // the slowed Melee clip keeps playing while the boss runs back
                    animator.Play("Run", 0, 0f);
                }
                break;

            case MeleeSubPhase.WalkAway:
                WalkMeleeTowards(meleeRetreatX);
                if (Mathf.Abs(transform.position.x - meleeRetreatX) <= MeleeReachTolerance)
                {
                    animator.SetBool("isRunning", false);
                    FacePlayer();
                    facingDirection = player.position.x > transform.position.x ? -1 : 1;
                    EndPhase();
                }
                break;
        }
    }

    // shared horizontal mover for both melee walk sub-phases. clamps to groundedY so
    // the boss cant drift off the floor mid-walk
    void WalkMeleeTowards(float targetX)
    {
        float delta = targetX - transform.position.x;
        float dir = delta >= 0f ? 1f : -1f;

        transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * dir, originalScale.y, originalScale.z);
        facingDirection = dir > 0f ? -1 : 1;

        float step = meleeRunSpeed * Time.deltaTime;
        float newX = Mathf.Abs(delta) <= step
            ? targetX
            : transform.position.x + dir * step;
        rb.MovePosition(new Vector2(newX, groundedY));
    }

    void StartCombat()
    {
        isPlayingAttack1 = false;
        isPlayingAttack2 = false;
        FacePlayer();
        facingDirection = player.position.x > transform.position.x ? -1 : 1;

        // phase 1 uses default tempo + targeting, phase 2 pulls from beliefs
        if (fightStage == FightStage.Phase2) ApplyBeliefModulation();

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
            // full bundle (no res spell): wait for platforms to finish their cycle
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
            // skip the res trigger if FireRow was already spawned by the off side's Attack2
            // event, otherwise the boss plays a cast animation for nothing
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

            // sync to off phase when paired with HomingFireballs, otherwise cap at 7 waves
            if (currentOff == OffMove.HomingFireballs) return offIndex >= 14;
            return resIndex >= 7;
        }
        else if (currentRes == ResMove.Platforms)
        {
            // Platforms, FloorFire and FireColumns spawn together in AnimEvent_Attack2
            PlatformController platform = FindFirstObjectByType<PlatformController>();
            if (platform == null) return currentOff != OffMove.FireColumns;
            return platform.cycleComplete;
        }
        return true;
    }

    public void AnimEvent_Attack1()
    {
        // animator events keep firing even when Update is gated by dialoguePaused, so
        // a late Attack1 could spawn stuff mid phase-2 dialogue after the scene was wiped
        if (dialoguePaused || _isRewinding) return;

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
        if (dialoguePaused || _isRewinding) return;

        // bundle (FireColumns + Platforms + FloorFire) and FireRow only spawn once per
        // combat phase (stacking them would be undodgeable). FireExplosion and FireWave
        // can spawn on every Attack2
        if (currentOff == OffMove.FireColumns && !bundleSpawned)
        {
            attackManager.spawnFireColumns(facingDirection);
            // platforms + floor fire only come along as the full bundle, when no res
            // spell is paired. otherwise its just the columns
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
        DataCollectionService.Instance?.RecordEnemyKill();
        DeathSound();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        // every collider off, including children so any separate hitbox objects get
        // disabled too
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

        if (foresightGlow != null) foresightGlow.SetActive(false);

        // Any-state -> Die transition. clip's last frame holds because we never clear the bool
        if (animator != null) animator.SetBool("Death", true);

        OnDeath?.Invoke();
        // skip DeathRoutine so the boss stays visible on the death frame instead of
        // vanishing like regular enemies
    }

    public override void Revive()
    {
        base.Revive();
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
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
                    // shove opposite to the boss's travel so the player clears the path
                    // of the continuing jump instead of eating a second hit
                    float travel = moveTarget.x - moveStart.x;
                    pushDir = travel >= 0f ? -1f : 1f;
                    pushSpeed = JumpAttackPushSpeed;

                    // if the shove would send the player past the safe edge, cancel it
                    // so they cant get knocked off the stage
                    float playerX = col.transform.position.x;
                    if ((pushDir > 0f && playerX > SafePushEdgeX) ||
                        (pushDir < 0f && playerX < -SafePushEdgeX))
                    {
                        pushSpeed = 0f;
                    }
                }
                else
                {
                    // push toward whichever side has more room
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

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        _isRewinding = true;
        // freeze the animator so Play/Update(0f) in ApplyState drives the visible state
        // each tick. without this, every state bleeds back to Idle
        if (animator != null) animator.speed = 0f;
    }
    //public override void OnStopRewind() { base.OnStopRewind(); _isRewinding = false; }
    public override void OnStopRewind()
    {
        base.OnStopRewind();
        _isRewinding = false;
        if (animator != null) animator.speed = 1f;

        // snap health bar to rewound health
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

        state.SetCustomData("MeleeSub", (int)meleeSubPhase);
        state.SetCustomData("MeleeTargetX", meleeTargetX);
        state.SetCustomData("MeleeRetreatX", meleeRetreatX);
        state.SetCustomData("MeleeHitApplied", meleeHitApplied);

        state.SetCustomData("OffSpawned", offActionSpawned);
        state.SetCustomData("ResSpawned", resActionSpawned);
        state.SetCustomData("BundleSpawned", bundleSpawned);
        state.SetCustomData("FireRowSpawned", fireRowSpawned);

        // mirror animator attack-clip state. without these, a rewind landing between
        // attacks can leave isPlayingAttack* stuck true and stall combat
        state.SetCustomData("PlayingAtk1", isPlayingAttack1);
        state.SetCustomData("PlayingAtk2", isPlayingAttack2);

        state.SetCustomData("NextIsPositional", nextIsPositional);
        state.SetCustomData("NextPosMove", (int)nextPosMove);
        state.SetCustomData("NextOff", (int)nextOff);
        state.SetCustomData("NextRes", (int)nextRes);
        state.SetCustomData("NextNextIsPositional", nextNextIsPositional);
        state.SetCustomData("NextNextPosMove", (int)nextNextPosMove);
        state.SetCustomData("NextNextOff", (int)nextNextOff);
        state.SetCustomData("NextNextRes", (int)nextNextRes);
        state.SetCustomData("LastOff", (int)lastOff);
        state.SetCustomData("LastLastOff", (int)lastLastOff);
        state.SetCustomData("LastRes", (int)lastRes);

        // capture animator state so the boss animates back the same way the player does.
        // top-level fields survive RewindState.Lerp, custom-data keys dont
        if (animator != null)
        {
            AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animInfo.fullPathHash;
            state.AnimatorNormalizedTime = animInfo.normalizedTime;
        }

        return state;
    }

    public override void ApplyState(RewindState state)
    {
        bool wasDeadBefore = wasDead;
        base.ApplyState(state);

        // base handles wasDead + first collider + sprite. boss also needs Death bool,
        // every collider, and the health bar re-shown on revive
        if (wasDead)
        {
            isDead = true;
            if (animator != null) animator.SetBool("Death", true);
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
        }
        else
        {
            isDead = false;
            if (animator != null) animator.SetBool("Death", false);
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
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

        meleeSubPhase = (MeleeSubPhase)state.GetCustomData<int>("MeleeSub", 0);
        meleeTargetX = state.GetCustomData<float>("MeleeTargetX", 0f);
        meleeRetreatX = state.GetCustomData<float>("MeleeRetreatX", 0f);
        meleeHitApplied = state.GetCustomData<bool>("MeleeHitApplied", false);

        // keep the run bool in sync with the restored melee state so the walk clip
        // resumes/stops right when rewind lands mid-attack
        if (animator != null)
        {
            bool shouldRun = currentPos == PosMove.Melee &&
                             (meleeSubPhase == MeleeSubPhase.WalkToPlayer ||
                              meleeSubPhase == MeleeSubPhase.WalkAway);
            animator.SetBool("isRunning", shouldRun);
        }

        offActionSpawned = state.GetCustomData<bool>("OffSpawned", false);
        resActionSpawned = state.GetCustomData<bool>("ResSpawned", false);
        bundleSpawned = state.GetCustomData<bool>("BundleSpawned", false);
        fireRowSpawned = state.GetCustomData<bool>("FireRowSpawned", false);

        isPlayingAttack1 = state.GetCustomData<bool>("PlayingAtk1", false);
        isPlayingAttack2 = state.GetCustomData<bool>("PlayingAtk2", false);

        nextIsPositional = state.GetCustomData<bool>("NextIsPositional", false);
        nextPosMove = (PosMove)state.GetCustomData<int>("NextPosMove", 0);
        nextOff = (OffMove)state.GetCustomData<int>("NextOff", 0);
        nextRes = (ResMove)state.GetCustomData<int>("NextRes", 0);
        nextNextIsPositional = state.GetCustomData<bool>("NextNextIsPositional", false);
        nextNextPosMove = (PosMove)state.GetCustomData<int>("NextNextPosMove", 0);
        nextNextOff = (OffMove)state.GetCustomData<int>("NextNextOff", 0);
        nextNextRes = (ResMove)state.GetCustomData<int>("NextNextRes", 0);
        lastOff = (OffMove)state.GetCustomData<int>("LastOff", 0);
        lastLastOff = (OffMove)state.GetCustomData<int>("LastLastOff", 0);
        lastRes = (ResMove)state.GetCustomData<int>("LastRes", 0);

        // restore animator state. bump speed to 1 briefly so Play + Update(0f)
        // commits the change (speed=0 leaves it uncommitted), then freeze again
        if (animator != null && state.AnimatorStateHash != 0)
        {
            animator.speed = 1f;
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
            animator.Update(0f);
            if (_isRewinding) animator.speed = 0f;
        }

        // keep health bar in sync during rewind scrubbing
        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.SyncAfterRewind();
    }
    public void playJumpSound()
    {
        if(audioSource != null && jumpClip != null)
        {
            audioSource.PlayOneShot(jumpClip, jumpVolume);
        }
    }
    public void playSmashSound()
    {
        if(audioSource != null && smashClip != null)
        {
            audioSource.PlayOneShot(smashClip, smashVolume);
        }
    }
    public void playSwingSound()
    {
        if(audioSource != null && swingClip != null)
        {
            audioSource.PlayOneShot(swingClip, swingVolume);
        }
    }
    public void PlayFootstep(float vol = -1)
    {
        // default to footstepVolume if unspecified
        if (vol == -1) vol = footstepVolume;
        if (footstepClips != null && footstepClips.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, footstepClips.Length);
            Debug.Log("HELLO");
            audioSource.PlayOneShot(footstepClips[randomIndex], vol);
        }
    }
}