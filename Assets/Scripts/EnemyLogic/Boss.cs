using UnityEngine;
using TimeRewind;

public class Boss : EnemyBase, IRewindable
{
    public Transform player;
    public BossAttackManager attackManager;

    public int damage = 1;
    public float damageCooldown = 1.5f;
    CameraShake cameraShake;

    bool _isRewinding;
    bool offActionSpawned;
    bool resActionSpawned;
    int facingDirection = 1;
    bool isGrounded;
    float lastDamageTime;

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

    // Movement Tracking
    Vector2 moveStart;
    Vector2 movePeak;
    Vector2 moveTarget;
    float finalTargetX;
    private Vector3 originalScale;
    private Animator animator;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        cameraShake = Camera.main.GetComponent<CameraShake>();
        originalScale = transform.localScale;
        animator = GetComponent<Animator>();
        EndPhase();
    }

    void Update()
    {
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
    }

    void UpdateIdle()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer < 1f) return;

        if (Random.value > 0.7f) StartPositional();
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
        currentPos = PosMove.ChangeSides;
        Vector2 start = transform.position;
        moveStart = start;
        moveTarget = new Vector2(-start.x, start.y);

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
        finalTargetX = -transform.position.x; 
        CalculateNextJump();
    }

    void CalculateNextJump()
    {
        posTimer = 0f;
        float jumpHeight = 7f;
        float lateral = -4f * facingDirection;

        if (Mathf.Abs(finalTargetX - transform.position.x) < Mathf.Abs(lateral))
        {
            lateral = finalTargetX - transform.position.x;
        }

        moveStart = transform.position;
        movePeak = moveStart + new Vector2(lateral, jumpHeight);
        moveTarget = new Vector2(movePeak.x, moveStart.y);
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
            currentPhase = BossPhase.Combat;
            offTimer = 0f; resTimer = 0f;
            offIndex = 0; resIndex = 0;
            
            offActionSpawned = false; 
            resActionSpawned = false;
            float rand = Random.value;

            if (rand < 0.25f)  currentOff = OffMove.Fireballs;
            else if (rand < 0.5f) currentOff = OffMove.FireColumns;
            else if (rand < 0.75f) currentOff = OffMove.HomingFireballs;
            else currentOff = OffMove.FireExplosion;

            rand = Random.value;
            if (rand < 0.25f) currentRes = ResMove.FireRow;
            else if (rand < 0.5f) currentRes = ResMove.FireWave;
            else if (rand < 0.75f) currentRes = ResMove.Platforms;
            else currentRes = ResMove.Enemy;
            currentRes = ResMove.Platforms;
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
                offTimer = 0f;
                attackManager.spawnFireball(facingDirection);
                offIndex++;
            }
            return false;
        }
        else if (currentOff == OffMove.FireColumns)
        {
            if (!offActionSpawned) 
            {
                attackManager.spawnFireColumns(facingDirection);
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
                offTimer = 0f;
                attackManager.spawnHomingFireball(facingDirection);
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
                offTimer = 0f;
                attackManager.spawnFireExplosion(facingDirection);
                offIndex++;
            }
            return false;
        }
        return true;
    }
    bool UpdateRestrictive()
    {
        resTimer += Time.deltaTime;

        if (currentRes == ResMove.FireRow)
        {
            if (!resActionSpawned)
            {
                attackManager.spawnFireRow(facingDirection);
                resActionSpawned = true;
            }
            return resTimer > 7f;
        }
        else if (currentRes == ResMove.Enemy)
        {
            if (!resActionSpawned)
            {
                attackManager.spawnEnemy(facingDirection);
                resActionSpawned = true;
            }
            return resTimer > 7f;
        }
        else if (currentRes == ResMove.FireWave)
        {
            if (resIndex >= 7) return true;
            PlayerHealth playerHealth = attackManager.player.GetComponent<PlayerHealth>();
            float spawnDelay = (playerHealth != null && playerHealth.CurrentHealth < 3) ? 2.0f : 1.0f;

            if (resTimer > spawnDelay)
            {
                resTimer = 0f;
                attackManager.spawnFireWave(facingDirection);
                resIndex++;
            }
            return false;
        }
        else if (currentRes == ResMove.Platforms)
        {
            if (!resActionSpawned) 
            {
                attackManager.spawnPlatforms(facingDirection);
                attackManager.spawnFloorFire(facingDirection);
                resActionSpawned = true;
            }
            PlatformController platform = FindFirstObjectByType<PlatformController>();
            return platform == null || platform.cycleComplete;
        }
        return true;
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
        if (col.gameObject.CompareTag("Ground")) isGrounded = true;
    }


    public override void OnStartRewind() { base.OnStartRewind(); _isRewinding = true; }
    public override void OnStopRewind() { base.OnStopRewind(); _isRewinding = false; }

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
    }
}