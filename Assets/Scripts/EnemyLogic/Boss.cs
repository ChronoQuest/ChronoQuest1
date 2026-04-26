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

    // Two-phase fight progression. Waiting = pre-dialogue, boss idle, no music/health
    // bar. Phase1 = dumber equal-weight rolls, no GMM. Phase2 = full GMM-driven behavior.
    // Intentionally NOT captured in rewind state so rewinding past the 80% threshold
    // doesn't demote the boss back to Phase1 (which would also re-trigger the dialogue
    // via the controller's one-way latch).
    public enum FightStage { Waiting, Phase1, Phase2 }
    public FightStage fightStage = FightStage.Waiting;

    // Set by BossFightController while a mid-fight dialogue is up, so Update() freezes
    // the boss without us having to touch Time.timeScale (which would also halt the
    // player's idle animation). Not rewind-captured — purely transient UI state.
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

    // Combat pair pools. Belief read picks a pool; a random pair is drawn from it.
    // Each pool intentionally contains at least two distinct Off moves so the
    // "no back-to-back Off" guard always has a fallback without falling out of pool.
    struct AttackPair { public OffMove off; public ResMove res; }

    static readonly AttackPair[] AggressivePool =
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
    // Picked when no belief axis dominates, or when the spoiler roll fires. Three
    // distinct Off moves so it's maximally unpredictable.
    static readonly AttackPair[] MixedPool =
    {
        new AttackPair { off = OffMove.FireColumns,     res = ResMove.Enemy },
        new AttackPair { off = OffMove.Fireballs,       res = ResMove.FireRow },
        new AttackPair { off = OffMove.HomingFireballs, res = ResMove.None },
    };

    // If max belief falls below this, treat the player as unreadable and pick from MixedPool.
    const float AxisDominanceThreshold = 0.40f;
    // Unconditional "keep them guessing" roll that ignores beliefs entirely.
    const float SpoilerChance = 0.20f;

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
    // Pushed to the stage edge (was ±11) so there's no behind-boss strip for
    // the player to stand on and cheese ranged fights. Positional attacks
    // (ChangeSides / GroundPound / Melee) and TeleportToSafeEdge all clamp to
    // these, so their behaviour scales with the bounds — the Melee ±1 buffer
    // is still 1 unit from the new edge.
    const float ArenaMinX = -12f;
    const float ArenaMaxX =  12f;
    // Safety net: if the boss's x exceeds this (e.g. launched off a stray platform),
    // it gets teleported back to the matching arena edge. Kept one unit past the
    // clamp so normal movement never trips it.
    const float OffSceneThreshold = 13f;
    // Beyond this |x|, we don't shove the player further toward the edge on a jump-landing hit.
    const float SafePushEdgeX = 9.5f;
    const float JumpAttackPushSpeed = 1.5f;
    float finalTargetX;
    float jumpLateral;
    private Vector3 originalScale;
    private Animator animator;
    private float groundedY;

    // Melee attack state: the boss snapshots the player's x at the start, walks there,
    // swings, then retreats to the nearest arena edge. All three sub-phases share posTimer.
    MeleeSubPhase meleeSubPhase;
    float meleeTargetX;
    float meleeRetreatX;
    [Header("Melee Movement")]
    [Tooltip("Speed when charging toward the player")]
    public float meleeRunSpeed = 70f;
    public float meleeStopBuffer = 0.6f;
    // Extra horizontal reach of the swing beyond the stop distance — damage registers
    // if the player is within (bossHalfWidth + playerHalfWidth + meleeStopBuffer + meleeHitReach).
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
        FacePlayer();
        musicController = FindFirstObjectByType<RewindMusicController>();
        EndPhase();
        InitializeActionQueue();
        if (attackManager != null) attackManager.boss = transform;
        // Music + health bar are deferred to BeginFight() so the intro dialogue plays first.
    }

    // Called by BossFightController once the intro dialogue completes. Before this
    // runs, fightStage is Waiting and Update() early-returns, so the boss just stands
    // idle in the scene with no music and no visible health bar.
    public void BeginFight(FightStage stage)
    {
        fightStage = stage;
        if (musicController != null) musicController.PlayBossFightMusic();
        BossHealthBarDriver driver = GetComponent<BossHealthBarDriver>();
        if (driver != null) driver.StartFight();
    }

    // Flip to phase 2 and re-roll the lookahead queue so upcoming plans use the GMM
    // branch instead of the phase-1 equal-weight roller.
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
        animator.SetBool("isRunning", false);
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
            // Aggressive players press close → boss relocates (ChangeSides).
            // Cautious players camp → boss drops on them (GroundPound).
            // Cautious/evasive players who maintain distance invite the Melee chase.
            ReadBeliefs(out float aggressive, out float evasive, out float cautious);
            float meleeChance = Mathf.Clamp01(0.33f + 0.2f * cautious + 0.1f * evasive - 0.2f * aggressive);
            if (Random.value < meleeChance)
            {
                posMove = PosMove.Melee;
            }
            else
            {
                float groundPoundChance = Mathf.Clamp01(0.5f + 0.3f * cautious - 0.3f * aggressive);
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

    // Phase-1 roller: 20% positional (equal weight across the three positional moves),
    // otherwise equal chance among the three canonical off/res pairs that the GMM
    // would normally bias between. No beliefs read, no tempo modulation.
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
        ReadBeliefs(out float aggressive, out float evasive, out float cautious);

        AttackPair[] pool = SelectPool(aggressive, evasive, cautious);

        // Block only triples (three same Off in a row). Doubles are allowed so each
        // pool's 2:1 ratio actually manifests — aggressive reads show mostly FireColumns
        // with FireExplosion breaks, rather than being forced into strict alternation.
        AttackPair picked = pool[Random.Range(0, pool.Length)];
        for (int tries = 0; tries < 6 && picked.off == lastOff && picked.off == lastLastOff; tries++)
        {
            picked = pool[Random.Range(0, pool.Length)];
        }
        // Safety fallback: if the pool can't break a triple, pull any differing pair
        // from MixedPool (shouldn't trigger given pool construction, but guard anyway).
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

    // Chooses which pair pool to draw from. Spoiler roll fires unconditionally; otherwise,
    // if no axis clears the dominance threshold, the player is treated as unreadable and
    // gets MixedPool — avoids the boss committing to a weak read.
    AttackPair[] SelectPool(float aggressive, float evasive, float cautious)
    {
        if (Random.value < SpoilerChance) return MixedPool;

        float max = Mathf.Max(aggressive, Mathf.Max(evasive, cautious));
        if (max < AxisDominanceThreshold) return MixedPool;

        if (aggressive >= evasive && aggressive >= cautious) return AggressivePool;
        if (evasive    >= cautious)                          return EvasivePool;
        return CautiousPool;
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
        // Snapshot the player's x so the boss commits to a fixed strike point; rewind-friendly
        // because it's captured in state and we don't re-read player.position mid-attack.
        float playerX = player.position.x;

        // Pull the strike point back by the sum of the two colliders' half-widths plus a small
        // buffer, so the boss stops just short of touching the player instead of overrunning them.
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
                // Partway through the swing, check once if the player is within reach and
                // apply damage directly — the collision hit no longer fires because the boss
                // deliberately stops short of the player.
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
                    // Force out of Melee even if the clip hasn't reached its exit time yet —
                    // otherwise the slowed Melee clip keeps playing while the boss runs back.
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

    // Shared horizontal mover for both melee walk sub-phases. Faces the direction of travel
    // and clamps to groundedY so the boss can't drift off the floor mid-walk.
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

        // Phase 1 uses default tempo + targeting (no GMM). Phase 2 pulls from beliefs.
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
        // Animator events keep firing even when Update() is gated by dialoguePaused,
        // so a late Attack1 event could spawn an enemy/fireball mid phase-2 dialogue —
        // after BossFightController.ClearBossAttacks already wiped the scene.
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
        DeathSound();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        // Every collider off — body contact, attack hit, everything. Include children
        // so any separate hitbox objects parented under the boss also get disabled.
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

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

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        _isRewinding = true;
        // Freeze the animator so Play()/Update(0f) in ApplyState fully drives the
        // visible state each rewind tick. Without this, the animator keeps
        // advancing forward between ticks and every state bleeds back to Idle.
        if (animator != null) animator.speed = 0f;
    }
    //public override void OnStopRewind() { base.OnStopRewind(); _isRewinding = false; }
    public override void OnStopRewind()
    {
        base.OnStopRewind();
        _isRewinding = false;
        if (animator != null) animator.speed = 1f;

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

        state.SetCustomData("MeleeSub", (int)meleeSubPhase);
        state.SetCustomData("MeleeTargetX", meleeTargetX);
        state.SetCustomData("MeleeRetreatX", meleeRetreatX);
        state.SetCustomData("MeleeHitApplied", meleeHitApplied);

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
        state.SetCustomData("LastLastOff", (int)lastLastOff);
        state.SetCustomData("LastRes", (int)lastRes);

        // Capture animator state so the boss's animations rewind the same way the
        // player's do. Use the built-in top-level fields on RewindState — those
        // survive RewindState.Lerp; custom-data keys do not unless Lerp is taught
        // about them explicitly.
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

        // Base handles wasDead + the first collider + sprite; boss needs the Death animator
        // bool and every collider synced too, plus the health bar re-shown on revive.
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

        // Keep the run-bool in sync with the restored melee state so the walk clip resumes
        // (or stops) correctly when rewind lands mid-attack.
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

        // Restore animator state so the boss's animations rewind like the
        // player's. Bump speed to 1 briefly so Play + Update(0f) actually
        // commits the state change (speed=0 leaves it uncommitted), then
        // freeze again so no transitions fire before the next rewind tick.
        if (animator != null && state.AnimatorStateHash != 0)
        {
            animator.speed = 1f;
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
            animator.Update(0f);
            if (_isRewinding) animator.speed = 0f;
        }

        // Keep health bar in sync during rewind scrubbing
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
        // Default to footstepVolume if unspecified
        if (vol == -1) vol = footstepVolume;
        if (footstepClips != null && footstepClips.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, footstepClips.Length);
            Debug.Log("HELLO");
            audioSource.PlayOneShot(footstepClips[randomIndex], vol);
        }
    }
}