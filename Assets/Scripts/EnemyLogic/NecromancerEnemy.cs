using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TimeRewind;

public class NecromancerEnemy : EnemyBase
{
    // ──────────────────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Movement")]
    public float moveSpeed = 2f;

    [Header("Physics & Environment")]
    public LayerMask groundLayer;
    [Tooltip("Child transform positioned at the Necromancer's feet — centre of the ground check capsule.")]
    public Transform groundCheck;
    [Tooltip("Half-width of the ground check capsule (horizontal extent from centre).")]
    public float groundCheckWidth = 0.3f;
    [Tooltip("Half-height of the ground check capsule (vertical extent — keep small).")]
    public float groundCheckHeight = 0.1f;
    [Tooltip("Extra distance below collider extents used by the death-land detection (keep at ~1.3).")]
    public float groundDetectionOffset = 1.3f;

    // ── Flee – Waypoints ──────────────────────────────────────────────────────
    [Header("Flee – Waypoints")]
    [Tooltip("Ordered waypoints the Necromancer flees toward. Each needs a WaypointZone component with a Collider2D trigger sized as desired.")]
    public WaypointZone[] fleeWaypoints;

    // ── Flee – Distance Management ────────────────────────────────────────────
    [Header("Flee – Distance Management")]
    [Tooltip("Distance at which the Necromancer stops and waits for the player (~1.5× camera width).")]
    public float maxFleeDistance = 18f;
    [Tooltip("Player must close to this distance before the Necromancer resumes fleeing.")]
    public float resumeFleeDistance = 14f;

    // ── Jump ──────────────────────────────────────────────────────────────────
    [Header("Jump")]
    [Tooltip("Maximum vertical impulse that can be applied for any jump.")]
    public float maxJumpForce = 20f;
    [Tooltip("Extra height added above the obstacle top to guarantee clearance.")]
    public float jumpClearanceBuffer = 0.5f;
    [Tooltip("Horizontal lookahead distance for obstacle detection.")]
    public float wallAheadDistance = 0.9f;
    [Tooltip("Vertical step size when scanning to find the obstacle top.")]
    public float wallTopScanStep = 0.25f;
    [Tooltip("Maximum obstacle height the Necromancer will attempt to jump.")]
    public float wallTopScanMax = 5f;
    [Tooltip("Minimum gap between consecutive jumps.")]
    public float jumpCooldown = 0.6f;
    [Tooltip("Waypoint must be at least this many units above the Necromancer to trigger an upward jump.")]
    public float jumpUpThreshold = 0.8f;
    [Tooltip("Necromancer must be within this X distance of the waypoint to attempt an upward jump.")]
    public float jumpUpXRange = 2.5f;
    [Tooltip("Must match the totalJumpFrames value used in the Animator blend tree.")]
    public float totalJumpFrames = 9f;
    [Tooltip("Speed multiplier applied to horizontal movement while airborne (gap-jump momentum and post-clearance steering).")]
    public float midAirSpeedMultiplier = 1.8f;
    [Tooltip("Obstacle height (above feet) at or above which the high-jump clearance bonus is applied.")]
    public float highJumpHeightThreshold = 2f;
    [Tooltip("Extra clearance buffer added on top of jumpClearanceBuffer for obstacles at or above highJumpHeightThreshold. Compensates for physics imprecision near maxJumpForce.")]
    public float highJumpClearanceBonus = 0.5f;
    [Tooltip("How far left and right FindLaunchPositionDir scans (in world units) to find a clear vertical column to jump from. Independent of wallTopScanMax.")]
    public float launchSeekRange = 20f;

    // ── Cornered ──────────────────────────────────────────────────────────────
    [Header("Cornered")]
    [Tooltip("Horizontal distance ahead to check for a blocking wall.")]
    public float corneredWallCheckDist = 1.8f;
    [Tooltip("Downward distance to detect a ledge drop that would allow escape.")]
    public float ledgeDropCheckDist = 3f;
    [Tooltip("Reduced attack cooldown used when cornered.")]
    public float corneredAttackCooldown = 1.2f;
    [Tooltip("Minimum time the Necromancer stays cornered before attempting to flee again.")]
    public float corneredMinDuration = 3f;

    // ── Stuck Failsafe ────────────────────────────────────────────────────────
    [Header("Stuck Failsafe")]
    [Tooltip("Seconds without meaningful movement (while fleeing) before falling back to Cornered.")]
    public float stuckTimeThreshold = 2.5f;
    [Tooltip("Minimum displacement per stuckTimeThreshold to count as moving.")]
    public float stuckDistanceThreshold = 0.3f;
    [Tooltip("If the Necromancer has not reached the current waypoint within this many seconds it gives up and enters Final Stand. Set to 0 to disable.")]
    public float waypointTimeoutDuration = 30f;

    // ── Flee Interrupts ───────────────────────────────────────────────────────
    [Header("Flee Interrupts")]
    [Tooltip("Minimum seconds between opportunistic actions (spell / revive) while fleeing.")]
    public float fleeCastIntervalMin = 4f;
    [Tooltip("Maximum seconds between opportunistic actions while fleeing.")]
    public float fleeCastIntervalMax = 9f;

    // ── Revive ────────────────────────────────────────────────────────────────
    [Header("Revive")]
    public float reviveCooldown = 5f;
    public float reviveAnimDuration = 1.2f;
    [Tooltip("How long a minion must have been dead before it can be revived.")]
    public float minionDeadRequiredTime = 3f;
    [Tooltip("Health gained by the Necromancer each time it successfully revives a minion. Set to 0 to disable.")]
    public int   reviveHealthBonus = 1;

    // ── Attack ────────────────────────────────────────────────────────────────
    [Header("Attack")]
    public float attackCooldown = 3f;
    public float attackAnimDuration = 0.8f;
    public int   attackDamage = 1;
    public GameObject spellPrefab;
    public int   spellPoolSize = 3;

    // ── References ────────────────────────────────────────────────────────────
    [Header("References")]
    public Transform        player;
    public List<EnemyBase>  minions = new List<EnemyBase>();

    // ──────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE  (all rewind-serialised — see CaptureState / ApplyState)
    // ──────────────────────────────────────────────────────────────────────────

    private Animator   animator;
    private Collider2D col;
    private Vector3    originalScale;

    // Waypoint navigation
    private int   currentWaypointIndex  = 0;
    private bool  finalStand            = false; // true once all waypoints visited — permanent Cornered
    private float waypointFleeTimer     = 0f;    // seconds spent in Flee state toward the current waypoint

    // Stuck / cornered
    private float   stuckTimer        = 0f;
    private Vector2 lastCheckedPos;
    private float   corneredUntilTime = 0f;

    // Jump
    private bool  isGrounded           = false;
    private bool  isJumping            = false;
    private float lastJumpTime         = -99f;
    private float seekLaunchDir        = 0f;    // direction we're walking to find a launch spot
    private float jumpTargetSurfaceY   = float.MinValue; // world Y of the platform we're jumping onto
    private float jumpMoveDir          = 0f;    // horizontal direction to apply once feet clear the surface

    // Flee interrupts
    private float nextFleeCastTime = 0f;

    // Revive
    private float lastReviveTime = -99f;
    private bool  isReviving     = false;
    private float reviveTimer    = 0f;

    // Attack
    private NecromancerSpell[] spellPool;
    private float   lastAttackTime        = -99f;
    private bool    isAttacking           = false;
    private float   attackTimer           = 0f;
    private Vector2 pendingSpellDirection;

    // Minions
    private float[] minionDeadTimers;

    // Death
    private bool isDying = false;

    // State machine
    private enum State { Idle, Flee, Wait, Cornered }
    [SerializeField] private State currentState = State.Idle;

    // ──────────────────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ──────────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        animator      = GetComponent<Animator>();
        col           = GetComponent<Collider2D>();
        originalScale = transform.localScale;
        BuildSpellPool();
        lastCheckedPos   = transform.position;
        waypointFleeTimer = 0f;
        ScheduleNextFleeCast();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SPELL POOL
    // ──────────────────────────────────────────────────────────────────────────

    void BuildSpellPool()
    {
        if (spellPrefab == null) return;
        spellPool = new NecromancerSpell[spellPoolSize];
        for (int i = 0; i < spellPoolSize; i++)
        {
            var obj = Instantiate(spellPrefab, transform.position, Quaternion.identity);
            spellPool[i] = obj.GetComponent<NecromancerSpell>();
            obj.SetActive(false);
        }
    }

    NecromancerSpell GetPooledSpell()
    {
        if (spellPool == null) return null;
        foreach (var s in spellPool)
            if (s != null && !s.gameObject.activeSelf) return s;
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  UPDATE
    // ──────────────────────────────────────────────────────────────────────────

    public override void Update()
    {
        base.Update();
        if (isRewinding) return;

        TickTimers();
        isGrounded = CheckGrounded();
        UpdateMinionDeadTimers();

        if (wasDead || isDying || isStunned || player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        TickStuckCheck();

        // State transitions and per-state actions (only when not mid-action)
        if (!isAttacking && !isReviving)
        {
            DetermineState(dist);
            ExecuteStateActions();
        }

        FaceDirection();
        UpdateAnimation();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  FIXED UPDATE
    // ──────────────────────────────────────────────────────────────────────────

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isDying || isStunned) return;

        // While locked in an action, stop horizontal movement but preserve gravity
        if (isReviving || isAttacking)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // Refresh grounded here too so the jump-arc guard is never stale
        isGrounded = CheckGrounded();
        if (isJumping)
        {
            if (isGrounded)
            {
                isJumping     = false;  // landed — resume normal movement next frame
                seekLaunchDir = 0f;     // re-evaluate seek direction fresh next time
            }
            else
            {
                float feetY = col != null ? col.bounds.min.y : transform.position.y;

                if (feetY >= jumpTargetSurfaceY)
                {
                    // Feet have cleared the platform surface — steer toward it.
                    // Cast from the leading edge so detection is reliable even when touching.
                    float hDir   = jumpMoveDir;
                    float edgeX  = col != null ? col.bounds.center.x + col.bounds.extents.x * hDir : transform.position.x;
                    bool wallAtMid  = col != null && Physics2D.Raycast(
                        new Vector2(edgeX, col.bounds.center.y),
                        Vector2.right * hDir, 0.2f, groundLayer).collider != null;
                    bool wallAtHead = col != null && Physics2D.Raycast(
                        new Vector2(edgeX, col.bounds.max.y - 0.05f),
                        Vector2.right * hDir, 0.2f, groundLayer).collider != null;

                    if (wallAtMid || wallAtHead)
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        // If descending and still blocked, bail out of the jump state so
                        // we don't stay pinned against the wall — HandleFlee re-evaluates on landing.
                        if (rb.linearVelocity.y <= 0f)
                            isJumping = false;
                    }
                    else
                        rb.linearVelocity = new Vector2(hDir * moveSpeed * midAirSpeedMultiplier, rb.linearVelocity.y);
                }
                // else: still rising through the gap — no horizontal movement yet
                return;
            }
        }

        switch (currentState)
        {
            case State.Flee:
                HandleFlee();
                break;

            case State.Idle:
            case State.Wait:
            case State.Cornered:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  STATE MACHINE
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Pure state-transition logic. No side effects beyond setting currentState.</summary>
    void DetermineState(float dist)
    {
        // ── Final stand — all waypoints visited, lock into Cornered permanently ──
        if (finalStand)
        {
            currentState = State.Cornered;
            return;
        }

        // ── Too far → wait ────────────────────────────────────────────────────
        if (dist > maxFleeDistance)
        {
            currentState = State.Wait;
            return;
        }

        // ── Currently waiting → leave only when player is close enough ────────
        if (currentState == State.Wait)
        {
            if (dist <= resumeFleeDistance)
            {
                currentState = State.Flee;
                ResetStuckCheck();
            }
            return;
        }

        // ── Currently cornered → leave only after min duration + unblocked ────
        if (currentState == State.Cornered)
        {
            if (Time.time >= corneredUntilTime && !IsCornered(GetCurrentFleeDir()))
            {
                seekLaunchDir = 0f;   // force fresh scan on the next SeekLaunchPos
                currentState = State.Flee;
                ResetStuckCheck();
            }
            return;
        }

        // Cornered entry is handled by the stuck failsafe (TickStuckCheck) rather than
        // here, to prevent the necromancer from declaring itself cornered the instant it
        // spawns near a wall before it has taken a single step.
        currentState = State.Flee;
    }

    /// <summary>Triggers actions appropriate to the current state.</summary>
    void ExecuteStateActions()
    {
        switch (currentState)
        {
            case State.Wait:
                // Prioritise reviving idle minions while we wait
                if (CanRevive()) StartRevive();
                break;

            case State.Cornered:
                // Aggressively cast spells
                if (CanAttackCornered()) StartAttack();
                break;

            case State.Flee:
                // Occasional interrupt: revive or cast to give player a chance to catch up
                if (Time.time >= nextFleeCastTime)
                    TryFleeInterrupt();
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  FLEE MOVEMENT (FixedUpdate)
    // ──────────────────────────────────────────────────────────────────────────

    void HandleFlee()
    {
        // Fallback: no waypoints configured — flee directly away from player
        if (fleeWaypoints == null || fleeWaypoints.Length == 0)
        {
            float fallbackDir = Mathf.Sign(transform.position.x - player.position.x);
            if (fallbackDir == 0f) fallbackDir = 1f;
            rb.linearVelocity = new Vector2(fallbackDir * moveSpeed, rb.linearVelocity.y);
            return;
        }

        WaypointZone wp = CurrentWaypoint();
        if (wp == null) return;

        // Reached when our centre is inside the waypoint's trigger collider
        if (wp.IsInside(transform.position))
        {
            AdvanceWaypoint();
            if (finalStand) return;
            wp = CurrentWaypoint();
            if (wp == null) return;
        }

        // ── Determine desired horizontal direction toward waypoint ─────────────
        // Always move toward the waypoint regardless of where the player is.
        // The player is never a navigation obstacle — we jump over them if needed.
        float wpX    = wp.transform.position.x;
        float wpY    = wp.transform.position.y;
        float xDiff  = wpX - transform.position.x;
        float yDiff  = wpY - transform.position.y;
        float moveDir = xDiff != 0f ? Mathf.Sign(xDiff) : (yDiff > 0f ? 1f : -1f);
        if (moveDir == 0f) moveDir = 1f;

        // ── Per-frame navigation decision ─────────────────────────────────────
        // Only make jump decisions when grounded and cooldown has elapsed.
        if (isGrounded && Time.time >= lastJumpTime + jumpCooldown)
        {
            NavDecision decision = EvaluateNavDecision(moveDir, yDiff);

            switch (decision)
            {
                case NavDecision.JumpOverWall:
                    float wallTop = GetWallTopHeight(moveDir);
                    PerformCalculatedJump(moveDir, wallTop, moveSpeed);
                    return;

                case NavDecision.JumpUp:
                    // We are already in a clear position (IsClearAbove passed).
                    // Jump with reduced horizontal speed so we rise through the gap
                    // rather than clipping the platform edge. Mid-air wall avoidance
                    // will stop horizontal movement if we graze anything on the way up.
                    float platformH = GetPlatformEdgeAboveHeight(moveDir);
                    if (platformH < 0f) platformH = Mathf.Min(yDiff, wallTopScanMax);
                    PerformCalculatedJump(moveDir, platformH, moveSpeed * 0.4f);
                    return;

                case NavDecision.SeekLaunchPos:
                {
                    float seekDir = FindLaunchPositionDir(yDiff, moveDir);

                    // Once we're standing in a clear spot, zero any residual vertical
                    // velocity and immediately attempt the jump — don't walk further.
                    if (IsClearAbove(yDiff))
                    {
                        // Stop horizontal drift and kill any lingering vertical velocity
                        // so we don't clip the platform edge on the first frame of the jump.
                        rb.linearVelocity = Vector2.zero;

                        float platformH2 = GetPlatformEdgeAboveHeight(seekDir);
                        if (platformH2 < 0f) platformH2 = Mathf.Min(yDiff, wallTopScanMax);
                        PerformCalculatedJump(seekDir, platformH2, moveSpeed * 0.4f);
                        seekLaunchDir = 0f;   // reset so next seek re-evaluates fresh
                        return;
                    }

                    // Not clear yet — keep walking toward the clear spot
                    rb.linearVelocity = new Vector2(seekDir * moveSpeed, rb.linearVelocity.y);
                    return;
                }

                case NavDecision.JumpGap:
                    // Run-jump: carry horizontal momentum through the gap.
                    // PerformCalculatedJump sets vertical impulse and zeros X, then we
                    // immediately restore horizontal so the necromancer doesn't stop at the edge.
                    PerformCalculatedJump(moveDir, jumpClearanceBuffer, moveSpeed);
                    // Enable horizontal from launch — feet are already at the target surface Y
                    jumpTargetSurfaceY = col != null ? col.bounds.min.y : transform.position.y;
                    rb.linearVelocity = new Vector2(moveDir * moveSpeed * midAirSpeedMultiplier, rb.linearVelocity.y);
                    return;

                case NavDecision.WalkOffLedge:
                    rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);
                    return;

                case NavDecision.Walk:
                    break; // falls through to the velocity line below
            }
        }

        // Default: walk horizontally.
        // Mid-air: use the faster air speed, but don't push into walls.
        float airMult = isGrounded ? 1f : midAirSpeedMultiplier;
        if (!isGrounded && HasWallAhead(moveDir))
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(moveDir * moveSpeed * airMult, rb.linearVelocity.y);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  NAVIGATION DECISION SYSTEM
    // ──────────────────────────────────────────────────────────────────────────

    private enum NavDecision { Walk, JumpOverWall, JumpUp, JumpGap, WalkOffLedge, SeekLaunchPos }

    /// <summary>
    /// Reads the immediate environment and returns what the necromancer should do
    /// this frame to make progress toward the waypoint.
    ///
    ///  JumpOverWall  — a wall is blocking at foot level and is jumpable
    ///  JumpUp        — the waypoint is above and a climbable platform edge is
    ///                  within reach in one jump in the move direction
    ///  WalkOffLedge  — the waypoint is below / same level, ground ends ahead
    ///  Walk          — clear path, just move
    /// </summary>
    NavDecision EvaluateNavDecision(float moveDir, float yDiff)
    {
        bool wallAhead  = HasWallAhead(moveDir);
        bool groundAhead = HasGroundAhead(moveDir);

        // ── Wall blocking horizontal path ──────────────────────────────────────
        if (wallAhead)
        {
            float wallTop = GetWallTopHeight(moveDir);
            if (wallTop >= 0f && IsClearAbove(wallTop))
                return NavDecision.JumpOverWall;
            // Wall too tall or ceiling blocked — can't jump, cornered logic handles it
            return NavDecision.Walk;
        }

        // ── Waypoint is above ─────────────────────────────────────────────────
        if (yDiff > jumpUpThreshold)
        {
            float platformH = GetPlatformEdgeAboveHeight(moveDir);
            if (platformH >= 0f && IsClearAbove(platformH))
                return NavDecision.JumpUp;

            // Can't jump from here — either we're under an overhang blocking the path
            // up, or there's no visible platform edge yet. Move to a clear launch spot.
            return NavDecision.SeekLaunchPos;
        }

        // ── No ground ahead — gap or intentional drop ────────────────────────
        if (!groundAhead)
        {
            float gapWidth = MeasureGapWidth(moveDir);
            if (gapWidth >= 0f)
                return NavDecision.JumpGap;   // narrow enough to jump across
            return NavDecision.WalkOffLedge;  // wide gap / intentional drop
        }

        return NavDecision.Walk;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  ENVIRONMENT SENSORS
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Returns true if there is a solid ground-layer wall at foot level ahead.
    /// Only checks groundLayer so the player's collider is never mistaken for a wall.</summary>
    bool HasWallAhead(float moveDir)
    {
        if (col == null) return false;
        Vector2 footOrigin = new Vector2(col.bounds.center.x, col.bounds.min.y + 0.05f);
        // Also check at mid-body height — a wall that starts above feet still blocks us
        Vector2 midOrigin  = new Vector2(col.bounds.center.x, col.bounds.center.y);
        return Physics2D.Raycast(footOrigin, Vector2.right * moveDir, wallAheadDistance, groundLayer).collider != null
            && Physics2D.Raycast(midOrigin,  Vector2.right * moveDir, wallAheadDistance, groundLayer).collider != null;
    }

    /// <summary>Returns true if there is ground ahead (no ledge drop).</summary>
    bool HasGroundAhead(float moveDir)
    {
        if (col == null) return true;
        // Check from a point half a step in front of the collider's outer edge
        float   edgeX      = col.bounds.center.x + col.bounds.extents.x * moveDir;
        Vector2 probeOrigin = new Vector2(edgeX + moveDir * 0.3f, col.bounds.min.y);
        return Physics2D.Raycast(probeOrigin, Vector2.down, ledgeDropCheckDist, groundLayer).collider != null;
    }

    /// <summary>
    /// Scans forward from the ledge edge to find where ground resumes.
    /// Returns the horizontal gap width if it is jumpable (landing platform is at
    /// roughly the same height and within reach), or -1 if it is a drop/too wide.
    /// </summary>
    float MeasureGapWidth(float moveDir)
    {
        if (col == null) return -1f;
        float footY  = col.bounds.min.y;
        float edgeX  = col.bounds.center.x + col.bounds.extents.x * moveDir;
        float scanY  = footY + 0.1f; // scan at just above foot level

        for (float dist = 0.3f; dist <= wallTopScanMax * 2f; dist += 0.2f)
        {
            float   probeX  = edgeX + moveDir * dist;
            Vector2 probe   = new Vector2(probeX, scanY);
            var     hit     = Physics2D.Raycast(probe, Vector2.down, ledgeDropCheckDist, groundLayer);

            if (hit.collider != null)
            {
                // Found ground on the other side — check it's roughly level (not a big drop)
                float landingY = hit.point.y;
                float dropDiff = footY - landingY;
                if (dropDiff > 2f) return -1f;  // too far down, treat as drop

                // Check we can physically jump the gap: horizontal distance vs jump arc
                float g         = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
                float vy        = maxJumpForce;
                float hangTime  = 2f * vy / g;                      // time in air at max jump
                float maxRange  = hangTime * moveSpeed;              // horizontal distance covered
                if (dist <= maxRange) return dist;                   // jumpable gap
                return -1f;                                          // too wide
            }
        }
        return -1f; // no ground found within scan range
    }

    /// <summary>
    /// Scans upward from foot level to find the height at which a wall ahead clears.
    /// Returns the clearance height above feet, or -1 if no wall or wall is too tall.
    /// </summary>
    float GetWallTopHeight(float moveDir)
    {
        if (col == null) return -1f;
        Vector2 footOrigin = new Vector2(col.bounds.center.x, col.bounds.min.y);

        if (!Physics2D.Raycast(footOrigin, Vector2.right * moveDir, wallAheadDistance, groundLayer).collider)
            return -1f;

        for (float h = wallTopScanStep; h <= wallTopScanMax; h += wallTopScanStep)
        {
            Vector2 scanOrigin = footOrigin + Vector2.up * h;
            if (!Physics2D.Raycast(scanOrigin, Vector2.right * moveDir, wallAheadDistance, groundLayer).collider)
                return h;
        }

        return -1f;  // wall too tall
    }

    /// <summary>
    /// Scans upward from the top of the collider (in the move direction) to find
    /// the first top surface the Necromancer could land on after a jump.
    /// Returns the height above the Necromancer's feet of that surface, or -1.
    ///
    /// Three filters are applied to each candidate hit:
    ///   1. Normal must face upward (rules out undersides and cave-wall tops).
    ///   2. Hit point must be above the Necromancer's head (rules out ground-level
    ///      roughness or raised tiles at body height detected through the probe window).
    ///   3. Required impulse must not exceed maxJumpForce (reachability gate).
    /// </summary>
    float GetPlatformEdgeAboveHeight(float moveDir)
    {
        if (col == null) return -1f;

        float halfW   = col.bounds.extents.x;
        float centerX = col.bounds.center.x;
        float topY    = col.bounds.max.y;
        float footY   = col.bounds.min.y;

        for (float h = wallTopScanStep; h <= wallTopScanMax; h += wallTopScanStep)
        {
            float   probeY = topY + h;
            Vector2 origin = new Vector2(centerX + moveDir * halfW, probeY);

            // Reject probes whose origin is above a ceiling — the Necromancer cannot
            // reach that space by jumping from below.  Cast upward from our head to the
            // probe origin at the same X; any ground-layer hit means a ceiling intervenes.
            if (Physics2D.Raycast(new Vector2(origin.x, topY), Vector2.up, h, groundLayer).collider != null)
                continue;

            var hit = Physics2D.Raycast(origin, Vector2.down, wallTopScanStep * 2f, groundLayer);
            if (hit.collider == null) continue;

            // Reject undersides (probe started inside a thin collider and exited through
            // its bottom) and near-vertical surfaces (cave wall edges).
            if (hit.normal.y < 0.5f) continue;

            // Reject surfaces at or below head level — these are ground roughness or
            // raised tiles next to the Necromancer, not meaningful jump targets.
            if (hit.point.y <= topY) continue;

            // Use the actual surface Y from the hit, not the probe start — avoids the
            // up-to-wallTopScanStep*2 overestimate that causes excess jump force.
            float heightAboveFeet = hit.point.y - footY;
            float g     = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
            float bonus = heightAboveFeet >= highJumpHeightThreshold ? highJumpClearanceBonus : 0f;
            float vy    = Mathf.Sqrt(2f * g * (heightAboveFeet + jumpClearanceBuffer + bonus));
            if (vy <= maxJumpForce)
                return heightAboveFeet;
            else
                return -1f; // platform exists but is out of jump range
        }

        return -1f;
    }

    /// <summary>
    /// Checks that the full collider width is unobstructed upward for the required
    /// height, so the necromancer doesn't attempt a jump it can't fit through.
    /// </summary>
    bool IsClearAbove(float requiredHeight)
    {
        if (col == null) return true;
        float halfWidth = col.bounds.extents.x;
        float centerX   = col.bounds.center.x;
        float topY      = col.bounds.max.y;
        float castDist  = requiredHeight + jumpClearanceBuffer;

        Vector2 left   = new Vector2(centerX - halfWidth + 0.05f, topY);
        Vector2 center = new Vector2(centerX,                      topY);
        Vector2 right  = new Vector2(centerX + halfWidth - 0.05f, topY);

        return !Physics2D.Raycast(left,   Vector2.up, castDist, groundLayer).collider &&
               !Physics2D.Raycast(center, Vector2.up, castDist, groundLayer).collider &&
               !Physics2D.Raycast(right,  Vector2.up, castDist, groundLayer).collider;
    }

    /// <summary>
    /// Calculates the exact vertical impulse to clear <paramref name="obstacleTopHeight"/>,
    /// caps it at maxJumpForce, and applies it with the given horizontal speed.
    /// </summary>
    void PerformCalculatedJump(float moveDir, float obstacleTopHeight, float hSpeed)
    {
        // High obstacles get extra clearance on top of the base buffer to compensate
        // for physics imprecision when the required impulse approaches maxJumpForce.
        float bonus       = obstacleTopHeight >= highJumpHeightThreshold ? highJumpClearanceBonus : 0f;
        float clearHeight = obstacleTopHeight + jumpClearanceBuffer + bonus;

        float g  = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
        float vy = Mathf.Sqrt(2f * g * clearHeight);
        vy = Mathf.Min(vy, maxJumpForce);

        // Store the world-Y of the surface we're clearing so FixedUpdate knows
        // when to start applying horizontal movement toward the platform
        jumpTargetSurfaceY = col != null
            ? col.bounds.min.y + obstacleTopHeight   // feet Y + platform height
            : transform.position.y + obstacleTopHeight;
        jumpMoveDir = moveDir;

        // Launch vertically only — horizontal movement is applied mid-air once clear
        rb.linearVelocity = new Vector2(0f, vy);
        isJumping    = true;
        lastJumpTime = Time.time;
    }

    /// <summary>
    /// Returns the direction to walk to find a clear launch position under a platform.
    /// On the first call (seekLaunchDir == 0) both directions are scanned simultaneously
    /// and the one whose nearest clear column is closer wins; preferredDir (toward the
    /// waypoint) is used as the tie-breaker and the fallback when neither side finds
    /// anything within range.  Once committed the direction is locked for the rest of
    /// this SeekLaunchPos session — no per-frame re-scanning, no oscillation.
    /// seekLaunchDir is reset to 0 on landing (FixedUpdate) and when re-entering Flee
    /// after Cornered (DetermineState), so the decision is always fresh.
    /// </summary>
    float FindLaunchPositionDir(float requiredHeight, float preferredDir = 0f)
    {
        // Already committed — keep walking in the chosen direction without re-scanning.
        if (seekLaunchDir != 0f) return seekLaunchDir;

        // Establish candidate directions.
        if (preferredDir == 0f)
            preferredDir = Mathf.Sign(transform.position.x - player.position.x);
        if (preferredDir == 0f) preferredDir = 1f;
        float oppDir = -preferredDir;

        if (col == null)
        {
            seekLaunchDir = preferredDir;
            return seekLaunchDir;
        }

        float topY     = col.bounds.max.y;
        float centerX  = col.bounds.center.x;
        float castDist = requiredHeight + jumpClearanceBuffer;
        float scanMax  = launchSeekRange;

        // Scan both directions at once, recording the distance to the nearest clear
        // vertical column in each.  Stop as soon as both are resolved.
        float distPreferred = float.MaxValue;
        float distOpposite  = float.MaxValue;

        for (float dist = 0f; dist <= scanMax; dist += 0.25f)
        {
            if (distPreferred == float.MaxValue)
            {
                float probeX = centerX + preferredDir * dist;
                if (!Physics2D.Raycast(new Vector2(probeX, topY), Vector2.up, castDist, groundLayer).collider)
                    distPreferred = dist;
            }
            if (distOpposite == float.MaxValue)
            {
                float probeX = centerX + oppDir * dist;
                if (!Physics2D.Raycast(new Vector2(probeX, topY), Vector2.up, castDist, groundLayer).collider)
                    distOpposite = dist;
            }
            if (distPreferred != float.MaxValue && distOpposite != float.MaxValue) break;
        }

        // Choose the closer opening.  preferredDir wins ties and no-find cases
        // so we default to walking toward the waypoint when uncertain.
        seekLaunchDir = (distOpposite < distPreferred) ? oppDir : preferredDir;
        return seekLaunchDir;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CORNERED / STUCK
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when a wall blocks the flee direction AND there is no ledge
    /// drop available as an escape route.
    /// </summary>
    bool IsCornered(float fleeDir)
    {
        bool wallAhead = Physics2D.Raycast(
            transform.position,
            Vector2.right * fleeDir,
            corneredWallCheckDist,
            groundLayer).collider != null;

        if (!wallAhead) return false;

        // A ledge drop is a valid escape — if ground disappears ahead, not cornered
        Vector2 ledgeCheckPos = (Vector2)transform.position + Vector2.right * fleeDir * corneredWallCheckDist;
        bool canDropOff = !Physics2D.Raycast(ledgeCheckPos, Vector2.down, ledgeDropCheckDist, groundLayer).collider;

        return !canDropOff;
    }

    void EnterCornered()
    {
        currentState      = State.Cornered;
        corneredUntilTime = Time.time + corneredMinDuration;
    }

    void TickStuckCheck()
    {
        if (currentState != State.Flee) { ResetStuckCheck(); return; }

        // Only count time actually spent fleeing — pauses in Wait/Cornered don't penalise.
        waypointFleeTimer += Time.deltaTime;
        if (waypointTimeoutDuration > 0f && waypointFleeTimer >= waypointTimeoutDuration)
        {
            finalStand = true;
            return;
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer < stuckTimeThreshold) return;

        // Only enter Cornered if we genuinely haven't moved AND a wall is blocking us
        bool hasntMoved = Vector2.Distance(transform.position, lastCheckedPos) < stuckDistanceThreshold;
        if (hasntMoved && IsCornered(GetCurrentFleeDir()))
            EnterCornered();

        ResetStuckCheck();
    }

    void ResetStuckCheck()
    {
        stuckTimer     = 0f;
        lastCheckedPos = transform.position;
    }

    float GetCurrentFleeDir()
    {
        WaypointZone wp = CurrentWaypoint();
        if (wp != null)
            return Mathf.Sign(wp.transform.position.x - transform.position.x);

        // Fallback: flee away from player
        return Mathf.Sign(transform.position.x - player.position.x);
    }

    bool CanAttackCornered() =>
        !isAttacking && Time.time >= lastAttackTime + corneredAttackCooldown;

    // ──────────────────────────────────────────────────────────────────────────
    //  FLEE INTERRUPTS
    // ──────────────────────────────────────────────────────────────────────────

    void TryFleeInterrupt()
    {
        if (CanRevive())      StartRevive();
        else if (CanAttack()) StartAttack();
        ScheduleNextFleeCast();
    }

    void ScheduleNextFleeCast() =>
        nextFleeCastTime = Time.time + Random.Range(fleeCastIntervalMin, fleeCastIntervalMax);

    // ──────────────────────────────────────────────────────────────────────────
    //  REVIVE / ATTACK
    // ──────────────────────────────────────────────────────────────────────────

    bool CanRevive() =>
        !isReviving && Time.time >= lastReviveTime + reviveCooldown && HasRevivableMinion();

    bool HasRevivableMinion()
    {
        for (int i = 0; i < minions.Count; i++)
            if (minions[i] != null && minions[i].IsDead && minionDeadTimers[i] >= minionDeadRequiredTime)
                return true;
        return false;
    }

    void StartRevive()
    {
        isReviving  = true;
        reviveTimer = reviveAnimDuration;
        animator?.SetTrigger("Revive");
    }

    bool CanAttack() =>
        !isAttacking && Time.time >= lastAttackTime + attackCooldown;

    void StartAttack()
    {
        isAttacking           = true;
        attackTimer           = attackAnimDuration;
        pendingSpellDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        animator?.SetTrigger("Attack");
    }

    /// <summary>Called by an Animation Event on the Attack animation.</summary>
    public void FireSpell()
    {
        if (wasDead || isDying || isRewinding || isStunned) return;
        NecromancerSpell spell = GetPooledSpell();
        if (spell == null) return;
        spell.transform.position = transform.position;
        spell.gameObject.SetActive(true);
        spell.Launch(pendingSpellDirection, attackDamage);
    }

    /// <summary>Called by an Animation Event on the Revive animation.</summary>
    public void ReviveMinion()
    {
        if (wasDead || isDying || isRewinding) return;
        for (int i = 0; i < minions.Count; i++)
        {
            if (minions[i] != null && minions[i].IsDead && minionDeadTimers[i] >= minionDeadRequiredTime)
            {
                minions[i].Revive();
                minionDeadTimers[i] = 0f;
                health += reviveHealthBonus;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    bool CheckGrounded()
    {
        Vector2 centre = groundCheck != null
            ? (Vector2)groundCheck.position
            : (col != null ? new Vector2(col.bounds.center.x, col.bounds.min.y) : (Vector2)transform.position);

        return Physics2D.OverlapCapsule(centre, new Vector2(groundCheckWidth * 2f, groundCheckHeight * 2f),
            CapsuleDirection2D.Horizontal, 0f, groundLayer) != null;
    }

    WaypointZone CurrentWaypoint() =>
        (fleeWaypoints != null && fleeWaypoints.Length > 0 && currentWaypointIndex < fleeWaypoints.Length)
            ? fleeWaypoints[currentWaypointIndex]
            : null;

    void AdvanceWaypoint()
    {
        if (fleeWaypoints == null || fleeWaypoints.Length == 0) return;
        if (currentWaypointIndex >= fleeWaypoints.Length - 1)
        {
            finalStand = true;  // all waypoints visited — trigger permanent Cornered
            return;
        }
        currentWaypointIndex++;
        waypointFleeTimer = 0f;  // reset timeout clock for the new waypoint
    }

    void FaceDirection()
    {
        if (currentState == State.Wait || currentState == State.Cornered)
        {
            FacePlayer();
        }
        else
        {
            WaypointZone wp = CurrentWaypoint();
            if (wp == null) return;
            float dir = wp.transform.position.x - transform.position.x;
            if (Mathf.Abs(dir) < 0.05f) return;
            SetFacing(dir > 0f);
        }
    }

    void FacePlayer()
    {
        SetFacing(player.position.x >= transform.position.x);
    }

    void SetFacing(bool facingRight)
    {
        transform.localScale = facingRight
            ? new Vector3( Mathf.Abs(originalScale.x), originalScale.y, originalScale.z)
            : new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    /// <summary>
    /// Drives the Animator using physics-based parameters mirroring the player approach.
    /// Requires Animator parameters: isGrounded (bool), isWalking (bool),
    /// VerticalNormal (float), and triggers: Revive, Attack, Hit, Die.
    /// </summary>
    void UpdateAnimation()
    {
        bool walking = currentState == State.Flee && isGrounded && !isReviving && !isAttacking;
        animator?.SetBool("isWalking", walking);
        animator?.SetBool("isGrounded", isGrounded);

        if (!isGrounded)
        {
            // Map vertical velocity to a normalised frame index — same scheme as the player
            float vy    = rb.linearVelocity.y;
            float frame = vy > 5f    ? 2f :
                          vy > 0.1f  ? 3f :
                          vy > -0.1f ? 3f :
                          vy > -10f  ? 4f : 5f;
            animator?.SetFloat("VerticalNormal", frame / totalJumpFrames);
        }

        // isJumping is cleared in FixedUpdate when grounded is confirmed
    }

    void TickTimers()
    {
        if (isReviving)
        {
            reviveTimer -= Time.deltaTime;
            if (reviveTimer <= 0f) { isReviving = false; lastReviveTime = Time.time; }
        }

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f) { isAttacking = false; lastAttackTime = Time.time; }
        }
    }

    void UpdateMinionDeadTimers()
    {
        if (minionDeadTimers == null || minionDeadTimers.Length != minions.Count)
            minionDeadTimers = new float[minions.Count];

        for (int i = 0; i < minions.Count; i++)
        {
            if (minions[i] != null && minions[i].IsDead) minionDeadTimers[i] += Time.deltaTime;
            else                                          minionDeadTimers[i]  = 0f;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  DAMAGE / DEATH
    // ──────────────────────────────────────────────────────────────────────────

    public override void TakeDamage(int amount)
    {
        if (wasDead || isDying) return;
        animator?.SetBool("isWalking", false);
        if (health - amount > 0) animator?.SetTrigger("Hit");
        base.TakeDamage(amount);
    }

    public override void Die()
    {
        if (wasDead || isDying) return;
        wasDead     = true;
        isDying     = true;
        isAttacking = false;
        isReviving  = false;
        animator?.SetBool("isWalking", false);
        if (col != null) col.enabled = false;
        OnDeath?.Invoke();
        StartCoroutine(HandleNecromancerDeath());
    }

    private IEnumerator HandleNecromancerDeath()
    {
        animator?.SetTrigger("Die");

        if (col != null)
        {
            float checkDist = col.bounds.extents.y + groundDetectionOffset;
            while (!Physics2D.Raycast(transform.position, Vector2.down, checkDist, groundLayer))
                yield return null;
        }

        rb.linearVelocity  = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType        = RigidbodyType2D.Kinematic;
        isDying            = false;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  REVIVE / REWIND
    // ──────────────────────────────────────────────────────────────────────────

    public override void Revive()
    {
        StopAllCoroutines();
        base.Revive();
        isDying     = false;
        isAttacking = false;
        isReviving  = false;
        rb.bodyType     = originalBodyType;
        rb.gravityScale = 1f;
        if (col    != null) col.enabled    = true;
        if (sprite != null) sprite.enabled = true;
    }

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isDying = false;
        if (col != null) col.enabled = true;
    }

    public override void OnStopRewind()
    {
        isRewinding = false;
        rb.bodyType = wasDead ? RigidbodyType2D.Kinematic : originalBodyType;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  REWIND STATE
    // ──────────────────────────────────────────────────────────────────────────

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();

        // ── Existing fields ───────────────────────────────────────────────────
        state.SetCustomData("lastReviveTime",   lastReviveTime);
        state.SetCustomData("isReviving",       isReviving);
        state.SetCustomData("reviveTimer",      reviveTimer);
        state.SetCustomData("lastAttackTime",   lastAttackTime);
        state.SetCustomData("isAttacking",      isAttacking);
        state.SetCustomData("attackTimer",      attackTimer);
        state.SetCustomData("isDying",          isDying);
        state.SetCustomData("minionDeadTimers", minionDeadTimers != null
            ? (float[])minionDeadTimers.Clone() : new float[0]);
        state.SetCustomData("colEnabled",    col    != null && col.enabled);
        state.SetCustomData("spriteEnabled", sprite != null && sprite.enabled);
        state.SetCustomData("localScale",    transform.localScale);

        // ── New fields ────────────────────────────────────────────────────────
        state.SetCustomData("currentWaypointIndex", currentWaypointIndex);
        state.SetCustomData("finalStand",           finalStand);
        state.SetCustomData("waypointFleeTimer",    waypointFleeTimer);
        state.SetCustomData("stuckTimer",           stuckTimer);
        state.SetCustomData("lastCheckedPos",        (Vector2)lastCheckedPos);
        state.SetCustomData("corneredUntilTime",     corneredUntilTime);
        state.SetCustomData("isJumping",             isJumping);
        state.SetCustomData("lastJumpTime",          lastJumpTime);
        state.SetCustomData("seekLaunchDir",        seekLaunchDir);
        state.SetCustomData("jumpTargetSurfaceY",   jumpTargetSurfaceY);
        state.SetCustomData("jumpMoveDir",          jumpMoveDir);
        state.SetCustomData("nextFleeCastTime",      nextFleeCastTime);

        if (animator != null)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash      = info.shortNameHash;
            state.AnimatorNormalizedTime = info.normalizedTime;
        }

        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);

        // ── Existing fields ───────────────────────────────────────────────────
        lastReviveTime = state.GetCustomData<float>("lastReviveTime");
        isReviving     = state.GetCustomData<bool> ("isReviving");
        reviveTimer    = state.GetCustomData<float>("reviveTimer");
        lastAttackTime = state.GetCustomData<float>("lastAttackTime");
        isAttacking    = state.GetCustomData<bool> ("isAttacking");
        attackTimer    = state.GetCustomData<float>("attackTimer");
        isDying        = state.GetCustomData<bool> ("isDying");

        float[] savedTimers = state.GetCustomData<float[]>("minionDeadTimers");
        if (savedTimers != null) minionDeadTimers = (float[])savedTimers.Clone();

        transform.localScale = state.GetCustomData<Vector3>("localScale", originalScale);
        if (col    != null) col.enabled    = state.GetCustomData<bool>("colEnabled",    true);
        if (sprite != null) sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        // ── New fields ────────────────────────────────────────────────────────
        currentWaypointIndex = state.GetCustomData<int>    ("currentWaypointIndex");
        finalStand           = state.GetCustomData<bool>   ("finalStand");
        waypointFleeTimer    = state.GetCustomData<float>  ("waypointFleeTimer");
        stuckTimer           = state.GetCustomData<float>  ("stuckTimer");
        lastCheckedPos       = state.GetCustomData<Vector2>("lastCheckedPos");
        corneredUntilTime    = state.GetCustomData<float>  ("corneredUntilTime");
        isJumping            = state.GetCustomData<bool>   ("isJumping");
        lastJumpTime         = state.GetCustomData<float>  ("lastJumpTime");
        seekLaunchDir        = state.GetCustomData<float>  ("seekLaunchDir");
        jumpTargetSurfaceY   = state.GetCustomData<float>  ("jumpTargetSurfaceY", float.MinValue);
        jumpMoveDir          = state.GetCustomData<float>  ("jumpMoveDir");
        nextFleeCastTime     = state.GetCustomData<float>  ("nextFleeCastTime");

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  GIZMOS
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Collider2D gizmoCol = col != null ? col : GetComponent<Collider2D>();
        float gizmoDir      = Application.isPlaying ? GetCurrentFleeDir() : 1f;

        // ── Ground check ──────────────────────────────────────────────────────
        {
            Color gcColour = (Application.isPlaying && isGrounded) ? Color.green : Color.red;
            Gizmos.color   = gcColour;

            Vector2 centre = groundCheck != null
                ? (Vector2)groundCheck.position
                : (gizmoCol != null ? new Vector2(gizmoCol.bounds.center.x, gizmoCol.bounds.min.y) : (Vector2)transform.position);

            // Draw the capsule as two end-circles + connecting lines
            float endOffset = Mathf.Max(0f, groundCheckWidth - groundCheckHeight);
            Vector3 cL = centre + Vector2.left  * endOffset;
            Vector3 cR = centre + Vector2.right * endOffset;
            float   r  = groundCheckHeight;

            Gizmos.DrawWireSphere(cL, r);
            Gizmos.DrawWireSphere(cR, r);
            Gizmos.DrawLine(cL + Vector3.up    * r, cR + Vector3.up    * r);
            Gizmos.DrawLine(cL + Vector3.down  * r, cR + Vector3.down  * r);
#if UNITY_EDITOR
            string groundLabel = Application.isPlaying ? (isGrounded ? "grounded" : "airborne") : "ground check";
            UnityEditor.Handles.Label(centre + Vector2.down * (r + 0.15f),
                groundLabel, new GUIStyle { normal = { textColor = gcColour }, fontSize = 10 });
#endif
        }

        // ── Navigation sensors ────────────────────────────────────────────────
        if (gizmoCol != null)
        {
            float footY    = gizmoCol.bounds.min.y;
            float topY     = gizmoCol.bounds.max.y;
            float halfW    = gizmoCol.bounds.extents.x;
            float centerX  = gizmoCol.bounds.center.x;
            float edgeX    = centerX + halfW * gizmoDir;

            // Wall-ahead ray (magenta) — foot level horizontal
            Gizmos.color = Color.magenta;
            Vector3 footOrigin = new Vector3(centerX, footY + 0.05f, 0f);
            Gizmos.DrawLine(footOrigin, footOrigin + Vector3.right * gizmoDir * wallAheadDistance);

            // Wall-top scan rays (faded magenta) — vertical scan to find wall height
            for (float h = wallTopScanStep; h <= wallTopScanMax; h += wallTopScanStep)
            {
                float t = h / wallTopScanMax;
                Gizmos.color = new Color(1f, 0f, 1f, Mathf.Lerp(0.45f, 0.08f, t));
                Vector3 scanOrigin = footOrigin + Vector3.up * h;
                Gizmos.DrawLine(scanOrigin, scanOrigin + Vector3.right * gizmoDir * wallAheadDistance);
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(footOrigin + Vector3.right * gizmoDir * (wallAheadDistance + 0.1f),
                "wall\nahead", new GUIStyle { normal = { textColor = Color.magenta }, fontSize = 9 });
#endif

            // Ground-ahead ray (orange) — checks for ledge drop
            Vector3 ledgeProbe = new Vector3(edgeX + gizmoDir * 0.3f, footY, 0f);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawLine(ledgeProbe, ledgeProbe + Vector3.down * ledgeDropCheckDist);
            Gizmos.DrawWireSphere(ledgeProbe + Vector3.down * ledgeDropCheckDist, 0.06f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(ledgeProbe + Vector3.right * gizmoDir * 0.1f,
                "ledge\ncheck", new GUIStyle { normal = { textColor = new Color(1f, 0.5f, 0f) }, fontSize = 9 });
#endif

            // Platform-above scan rays (cyan) — finds platform to jump onto
            for (float h = wallTopScanStep; h <= wallTopScanMax; h += wallTopScanStep)
            {
                float t = h / wallTopScanMax;
                Gizmos.color = new Color(0f, 0.8f, 1f, Mathf.Lerp(0.5f, 0.1f, t));
                Vector3 probeOrigin = new Vector3(edgeX, topY + h, 0f);
                Gizmos.DrawLine(probeOrigin, probeOrigin + Vector3.down * (wallTopScanStep * 2f));
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(new Vector3(edgeX + gizmoDir * 0.1f, topY + wallTopScanMax * 0.5f),
                "platform\nscanner", new GUIStyle { normal = { textColor = new Color(0f, 0.8f, 1f) }, fontSize = 9 });
#endif

        }

        // ── Cornered wall check ───────────────────────────────────────────────
        Vector3 corneredEnd = transform.position + Vector3.right * gizmoDir * corneredWallCheckDist;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, corneredEnd);
        Gizmos.DrawWireSphere(corneredEnd, 0.08f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(corneredEnd + Vector3.up * 0.15f,
            $"wall check\n({corneredWallCheckDist}u)",
            new GUIStyle { normal = { textColor = Color.red }, fontSize = 9 });
#endif

        // ── Ledge drop check (from cornered end) ──────────────────────────────
        Vector3 ledgeEnd = corneredEnd + Vector3.down * ledgeDropCheckDist;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawLine(corneredEnd, ledgeEnd);
        Gizmos.DrawWireSphere(ledgeEnd, 0.08f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(ledgeEnd + Vector3.right * 0.1f,
            $"ledge drop\n({ledgeDropCheckDist}u)",
            new GUIStyle { normal = { textColor = new Color(1f, 0.5f, 0f) }, fontSize = 9 });
#endif

        // ── Distance rings ────────────────────────────────────────────────────
        Gizmos.color = Color.yellow;
        DrawGizmoCircle(transform.position, maxFleeDistance);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.right * maxFleeDistance + Vector3.up * 0.2f,
            $"wait\n({maxFleeDistance}u)",
            new GUIStyle { normal = { textColor = Color.yellow }, fontSize = 10 });
#endif

        Gizmos.color = Color.cyan;
        DrawGizmoCircle(transform.position, resumeFleeDistance);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.right * resumeFleeDistance + Vector3.up * 0.2f,
            $"resume\n({resumeFleeDistance}u)",
            new GUIStyle { normal = { textColor = Color.cyan }, fontSize = 10 });
#endif

        // ── Waypoints ─────────────────────────────────────────────────────────
        if (fleeWaypoints != null)
        {
            for (int i = 0; i < fleeWaypoints.Length; i++)
            {
                if (fleeWaypoints[i] == null) continue;

                bool    isCurrent = Application.isPlaying && i == currentWaypointIndex;
                Vector3 wpPos     = fleeWaypoints[i].transform.position;

                // The WaypointZone's own OnDrawGizmos draws the collider shape —
                // draw a connecting arrow here and a label above it
                Gizmos.color = isCurrent ? Color.yellow : Color.green;
                Gizmos.DrawWireSphere(wpPos, 0.2f);  // small dot at centre

                if (i + 1 < fleeWaypoints.Length && fleeWaypoints[i + 1] != null)
                {
                    Gizmos.color = Color.green;
                    DrawGizmoArrow(wpPos, fleeWaypoints[i + 1].transform.position);
                }
#if UNITY_EDITOR
                string wpLabel = isCurrent ? $"[{i}]  ← next" : $"[{i}]";
                UnityEditor.Handles.Label(wpPos + Vector3.up * 0.4f, wpLabel,
                    new GUIStyle
                    {
                        normal    = { textColor = isCurrent ? Color.yellow : Color.green },
                        fontSize  = 11,
                        fontStyle = FontStyle.Bold
                    });
#endif
            }
        }

        // ── IsClearAbove rays ─────────────────────────────────────────────────
        if (gizmoCol != null)
        {
            float halfW   = gizmoCol.bounds.extents.x;
            float centerX = gizmoCol.bounds.center.x;
            float topY    = gizmoCol.bounds.max.y;
            float castD   = wallTopScanMax + jumpClearanceBuffer;
            Gizmos.color  = new Color(0f, 0.8f, 1f, 0.6f);
            Gizmos.DrawLine(new Vector3(centerX - halfW + 0.05f, topY), new Vector3(centerX - halfW + 0.05f, topY + castD));
            Gizmos.DrawLine(new Vector3(centerX,                 topY), new Vector3(centerX,                 topY + castD));
            Gizmos.DrawLine(new Vector3(centerX + halfW - 0.05f, topY), new Vector3(centerX + halfW - 0.05f, topY + castD));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(new Vector3(centerX + halfW, topY + castD * 0.5f), "ceiling\ncheck",
                new GUIStyle { normal = { textColor = new Color(0f, 0.8f, 1f) }, fontSize = 9 });
#endif
        }

        // ── Jump arc ──────────────────────────────────────────────────────────
        // Green arc  = vertical-only phase (rising to clear obstacle).
        // Yellow arc = horizontal-movement phase (after feet clear surface).
        // Cyan sphere  = clearance point where horizontal kicks in.
        // Orange sphere = predicted landing.
        // Yellow dashed = peak height.   Cyan dashed = surface clearance level.
        if (Application.isPlaying && col != null && rb != null)
        {
            float g = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;

            if (isJumping)
            {
                // Live arc from current position + velocity.
                DrawJumpArcGizmo(transform.position, rb.linearVelocity, g,
                    jumpTargetSurfaceY, jumpMoveDir * moveSpeed * midAirSpeedMultiplier);
            }
            else if (currentState == State.Flee && isGrounded && !isAttacking && !isReviving
                     && Time.time >= lastJumpTime + jumpCooldown)
            {
                // Predict the jump we would take this frame.
                WaypointZone predWp = CurrentWaypoint();
                if (predWp != null)
                {
                    float xDiff = predWp.transform.position.x - transform.position.x;
                    float yDiff = predWp.transform.position.y - transform.position.y;
                    float mDir  = xDiff != 0f ? Mathf.Sign(xDiff) : (yDiff > 0f ? 1f : -1f);
                    if (mDir == 0f) mDir = 1f;

                    NavDecision pred = EvaluateNavDecision(mDir, yDiff);
                    float launchVy = 0f;
                    float surfY    = col.bounds.min.y;

                    switch (pred)
                    {
                        case NavDecision.JumpOverWall:
                        {
                            float wallTop = GetWallTopHeight(mDir);
                            if (wallTop >= 0f)
                            {
                                float bonus  = wallTop >= highJumpHeightThreshold ? highJumpClearanceBonus : 0f;
                                float clearH = wallTop + jumpClearanceBuffer + bonus;
                                launchVy = Mathf.Min(Mathf.Sqrt(2f * g * clearH), maxJumpForce);
                                surfY    = col.bounds.min.y + wallTop;
                            }
                            break;
                        }
                        case NavDecision.JumpUp:
                        case NavDecision.SeekLaunchPos:
                        {
                            float platformH = GetPlatformEdgeAboveHeight(mDir);
                            if (platformH < 0f) platformH = Mathf.Min(yDiff, wallTopScanMax);
                            float bonus  = platformH >= highJumpHeightThreshold ? highJumpClearanceBonus : 0f;
                            float clearH = platformH + jumpClearanceBuffer + bonus;
                            launchVy = Mathf.Min(Mathf.Sqrt(2f * g * clearH), maxJumpForce);
                            surfY    = col.bounds.min.y + platformH;
                            break;
                        }
                        case NavDecision.JumpGap:
                        {
                            float bonus  = jumpClearanceBuffer >= highJumpHeightThreshold ? highJumpClearanceBonus : 0f;
                            float clearH = jumpClearanceBuffer + bonus;
                            launchVy = Mathf.Min(Mathf.Sqrt(2f * g * clearH), maxJumpForce);
                            surfY    = col.bounds.min.y;   // horizontal starts immediately
                            break;
                        }
                    }

                    if (launchVy > 0f)
                        DrawJumpArcGizmo(transform.position, new Vector2(0f, launchVy), g,
                            surfY, mDir * moveSpeed * midAirSpeedMultiplier);
                }
            }
        }

        // ── Runtime-only overlay ──────────────────────────────────────────────
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            // Current state + active flags
            string stateText = currentState.ToString().ToUpper();
            if (finalStand)  stateText += "  [FINAL STAND]";
            if (isReviving)  stateText += "  [reviving]";
            if (isAttacking) stateText += "  [attacking]";
            if (isJumping)   stateText += "  [jumping]";

            Color stateColor = currentState switch
            {
                State.Flee     => Color.green,
                State.Wait     => Color.cyan,
                State.Cornered => Color.red,
                _              => Color.white
            };

            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, stateText,
                new GUIStyle
                {
                    normal    = { textColor = stateColor },
                    fontSize  = 13,
                    fontStyle = FontStyle.Bold
                });

            // Stuck timer progress
            if (currentState == State.Flee && stuckTimeThreshold > 0f)
            {
                float pct = Mathf.Clamp01(stuckTimer / stuckTimeThreshold);
                Color stuckColor = Color.Lerp(Color.white, Color.red, pct);
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f,
                    $"stuck {pct * 100f:0}%  ({stuckTimer:0.0} / {stuckTimeThreshold:0.0}s)",
                    new GUIStyle { normal = { textColor = stuckColor }, fontSize = 10 });
            }

            // Cornered cooldown
            if (currentState == State.Cornered)
            {
                float remaining = Mathf.Max(0f, corneredUntilTime - Time.time);
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f,
                    $"cornered lock {remaining:0.0}s",
                    new GUIStyle { normal = { textColor = Color.red }, fontSize = 10 });
            }

            // Next flee cast countdown
            if (currentState == State.Flee)
            {
                float castIn = Mathf.Max(0f, nextFleeCastTime - Time.time);
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.3f,
                    $"next interrupt {castIn:0.0}s",
                    new GUIStyle { normal = { textColor = Color.grey }, fontSize = 9 });
            }
        }
#endif
    }

    void DrawGizmoArrow(Vector3 from, Vector3 to)
    {
        Gizmos.DrawLine(from, to);
        Vector3 dir   = (to - from).normalized;
        Vector3 right = Quaternion.Euler(0f, 0f,  30f) * (-dir) * 0.35f;
        Vector3 left  = Quaternion.Euler(0f, 0f, -30f) * (-dir) * 0.35f;
        Gizmos.DrawLine(to, to + right);
        Gizmos.DrawLine(to, to + left);
    }

    void DrawGizmoCircle(Vector3 centre, float radius, int segments = 36)
    {
        float   step = 360f / segments;
        Vector3 prev = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float   angle = i * step * Mathf.Deg2Rad;
            Vector3 next  = centre + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    /// <summary>
    /// Simulates and draws the jump arc as a sequence of line segments.
    ///   Green  segments = vertical-only phase (rising before feet clear surfaceY).
    ///   Yellow segments = horizontal-movement phase (after feet clear surfaceY).
    ///   Cyan sphere     = clearance point where horizontal kicks in.
    ///   Orange sphere   = predicted landing.
    ///   Yellow line     = peak height above launch.
    ///   Cyan line       = surface clearance level (where horizontal starts).
    /// </summary>
    void DrawJumpArcGizmo(Vector2 startPos, Vector2 startVel, float g,
                          float surfaceY, float hSpeedAfterClear)
    {
        // Feet sit below the transform pivot by this offset (negative value).
        float footOffset = col != null ? col.bounds.min.y - transform.position.y : 0f;

        const float dt       = 0.04f;
        const int   maxSteps = 300;

        Vector2 pos  = startPos;
        Vector2 vel  = startVel;
        Vector2 prev = startPos;

        bool horizontalStarted = (startPos.y + footOffset) >= surfaceY;
        if (horizontalStarted) vel.x = hSpeedAfterClear;

        float   peakY     = startPos.y;
        bool    peakFound = false;
        Vector2 clearPos  = Vector2.zero;
        bool    hadClear  = horizontalStarted;

        for (int i = 0; i < maxSteps; i++)
        {
            vel.y -= g * dt;
            pos   += vel * dt;

            float feetY = pos.y + footOffset;

            if (!horizontalStarted && feetY >= surfaceY)
            {
                horizontalStarted = true;
                hadClear          = true;
                clearPos          = pos;
                vel.x             = hSpeedAfterClear;
            }

            if (!peakFound && vel.y < 0f)
            {
                peakY    = pos.y;
                peakFound = true;
            }

            Gizmos.color = horizontalStarted
                ? new Color(1f, 0.85f, 0f, 0.9f)   // yellow — horizontal phase
                : new Color(0.2f, 1f, 0.3f, 0.9f); // green  — vertical phase
            Gizmos.DrawLine(prev, pos);
            prev = pos;

            // Stop once we've clearly fallen below the launch point.
            if (peakFound && pos.y < startPos.y - 0.5f) break;
        }

        // ── Peak height line ──────────────────────────────────────────────
        float lineMinX = Mathf.Min(startPos.x, prev.x) - 0.5f;
        float lineMaxX = Mathf.Max(startPos.x, prev.x) + 0.5f;

        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawLine(new Vector3(lineMinX, peakY, 0f), new Vector3(lineMaxX, peakY, 0f));

        // ── Surface clearance line ────────────────────────────────────────
        float footYAtLaunch = startPos.y + footOffset;
        if (surfaceY > footYAtLaunch + 0.05f)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawLine(new Vector3(lineMinX, surfaceY, 0f), new Vector3(lineMaxX, surfaceY, 0f));
        }

        // ── Key spheres ───────────────────────────────────────────────────
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);   // landing
        Gizmos.DrawWireSphere(prev, 0.13f);

        if (hadClear && clearPos != Vector2.zero)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.9f); // clearance point
            Gizmos.DrawWireSphere(clearPos, 0.1f);
        }

#if UNITY_EDITOR
        GUIStyle arcStyle = new GUIStyle { fontSize = 9 };

        float peakAboveStart = peakY - startPos.y;
        arcStyle.normal.textColor = Color.yellow;
        UnityEditor.Handles.Label(
            new Vector3(lineMinX - 0.1f, peakY, 0f),
            $"peak +{peakAboveStart:0.00}u", arcStyle);

        if (surfaceY > footYAtLaunch + 0.05f)
        {
            float clearHeight = surfaceY - footYAtLaunch;
            arcStyle.normal.textColor = Color.cyan;
            UnityEditor.Handles.Label(
                new Vector3(lineMinX - 0.1f, surfaceY, 0f),
                $"clear @{clearHeight:0.00}u", arcStyle);
        }
#endif
    }
}
