using UnityEngine;
using System.Collections;
using TimeRewind;

public class Boss : EnemyBase, IRewindable
{
    public float attackCooldown = 1f;
    public float damageCooldown = 1.5f;
    public int damage = 1;
    public Transform player;
    public BossAttackManager attackManager;

    private bool _isRewinding;
    private float mainTimer;
    private float restrictiveTimer;
    private float offensiveTimer;
    private float positionalTimer;
    private float lastDamageTime;
    private int facingDirection = 1;
    private bool isGrounded;

    void Start()
    {
        StartCoroutine(AttackLoop());
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (player == null) return;
        if (_isRewinding) return;
    }

    IEnumerator WaitMain(float duration)
    {
        mainTimer = 0f;
        while (mainTimer < duration)
        {
            if (!_isRewinding) mainTimer += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitRestrictive(float duration)
    {
        restrictiveTimer = 0f;
        while (restrictiveTimer < duration)
        {
            if (!_isRewinding) restrictiveTimer += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitOffensive(float duration)
    {
        offensiveTimer = 0f;
        while (offensiveTimer < duration)
        {
            if (!_isRewinding) offensiveTimer += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitPositional(float duration)
    {
        positionalTimer = 0f;
        while (positionalTimer < duration)
        {
            if (!_isRewinding) positionalTimer += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator AttackLoop()
    {   
        while (health > 0)
        {
            yield return StartCoroutine(WaitMain(1f));
            while (_isRewinding) yield return null;
            yield return StartCoroutine(FullAttack());
        }
    }

    IEnumerator FullAttack()
    {
        while (_isRewinding) yield return null;
        float rand = Random.value;

        if(rand > 0.3f){
            Coroutine restrict = StartCoroutine(RestrictiveMove());
            Coroutine attack = StartCoroutine(OffensiveMove());
            yield return restrict;
            yield return attack;
        } else
        {
            Coroutine position = StartCoroutine(PositionalMove());
            yield return position;
        }
    }

    IEnumerator RestrictiveMove()
    {
        float rand = Random.value;

        while (_isRewinding) yield return null;

        if(rand > 0.5f)
        {
            if(rand > 0.75f) yield return StartCoroutine(FireRow());
            else yield return StartCoroutine(FireWave()); 
        } 
        else if (rand > 0.25f) 
            yield return StartCoroutine(Enemy());
        else 
            yield return StartCoroutine(Platforms());
    }

    IEnumerator OffensiveMove()
    {
        while (_isRewinding) yield return null;

        if(Random.value > 0.5f)
            yield return StartCoroutine(Fireballs());
        else 
            yield return StartCoroutine(FireColumns());
    }

    IEnumerator PositionalMove()
    {
        while (_isRewinding) yield return null;
        yield return StartCoroutine(ChangeSides());
    }

    IEnumerator Fireballs()
    {
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        while (_isRewinding) yield return null;

        for(int i = 0; i < 15; i++)
        {
            if(i % 2 == 0 && playerHealth.CurrentHealth < 3)
            {
                yield return StartCoroutine(WaitOffensive(0.5f));
                continue;
            }
            else
            {
                if(!_isRewinding) 
                    attackManager.spawnFireball(facingDirection);
            }

            yield return StartCoroutine(WaitOffensive(0.5f));
        }
    }

    IEnumerator FireColumns()
    {
        while (_isRewinding) yield return null;

        if(!_isRewinding) 
            attackManager.spawnFireColumns(facingDirection);

        yield return StartCoroutine(WaitOffensive(5f));
    }

    IEnumerator FireRow()
    {
        while (_isRewinding) yield return null;

        if(!_isRewinding) 
            attackManager.spawnFireRow(facingDirection);

        yield return StartCoroutine(WaitRestrictive(7f));
    }

    IEnumerator FireWave()
    {
        while (_isRewinding) yield return null;

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();

        for(int i = 0; i < 7; i++)
        {
            if(i % 2 == 0 && playerHealth.CurrentHealth < 3)
            {
                yield return StartCoroutine(WaitRestrictive(1f));
                continue;
            }
            else
            {
                if(!_isRewinding) 
                    attackManager.spawnFireWave(facingDirection);
            }

            yield return StartCoroutine(WaitRestrictive(1f));
        }
    }

    IEnumerator Platforms()
    {
        while (_isRewinding) yield return null;
        attackManager.spawnPlatforms(facingDirection);
        yield return StartCoroutine(WaitRestrictive(0.5f));
        attackManager.spawnFloorFire(facingDirection);
        PlatformController platform = FindFirstObjectByType<PlatformController>();
        while (platform != null && !platform.cycleComplete)
            yield return null;
    }

    IEnumerator Enemy()
    {
        while (_isRewinding) yield return null;

        if(!_isRewinding) 
            attackManager.spawnEnemy(facingDirection);

        yield return StartCoroutine(WaitRestrictive(7f));
    }

    IEnumerator ChangeSides()
    {
        Vector2 start = transform.position;
        // Arena is centred at (0,0), so get invert x to get other side
        Vector2 target = new Vector2(-start.x, start.y);

        float jumpHeight = 5f;
        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        float velocityY = Mathf.Sqrt(2 * gravity * jumpHeight);
        float timeToPeak = velocityY / gravity;
        float totalAirTime = timeToPeak * 2;
        float velocityX = (target.x - start.x) / totalAirTime;
        rb.linearVelocity = new Vector2(velocityX, velocityY);
        isGrounded = false;

        facingDirection *= -1;
        while(!isGrounded)
        {
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        // Ensure the boss lands at the correct position
        transform.position = new Vector2(target.x, transform.position.y);
        yield return StartCoroutine(WaitPositional(2f));
    }
    void Damage()
    {
        if (Time.time >= lastDamageTime + damageCooldown)
        {
            lastDamageTime = Time.time;

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.ModifyHealth(-damage);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) Damage();
        if (collision.gameObject.CompareTag("Ground")) isGrounded = true;
    }

    public override void ApplyKnockback(Vector2 force)
    {
        if (isRewinding || wasDead) return;

        // No knockback for boss (for now)
        //force /= knockbackResistance;
        //rb.linearVelocity = Vector2.zero;
        // Vector2 finalForce = new Vector2(force.x, Mathf.Max(Mathf.Abs(force.x), Mathf.Abs(force.y)) * knockbackUpMultiplier);
        // rb.AddForce(finalForce, ForceMode2D.Impulse);
        StartCoroutine(base.HitStunRoutine(0.25f));
    }

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        _isRewinding = true;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind(); 
        _isRewinding = false;
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("MainTimer", mainTimer);
        state.SetCustomData("RestrictTimer", restrictiveTimer);
        state.SetCustomData("OffenseTimer", offensiveTimer);
        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);
        mainTimer = state.GetCustomData<float>("MainTimer", 0f);
        restrictiveTimer = state.GetCustomData<float>("RestrictTimer", 0f);
        offensiveTimer = state.GetCustomData<float>("OffenseTimer", 0f);
    }
}