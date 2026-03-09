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
    private float lastDamageTime;
    public PlayerStrategyModel playerStrategyModel; 

    void Start()
    {
        StartCoroutine(AttackLoop());
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

        Coroutine restrict = StartCoroutine(RestrictiveMove());
        Coroutine attack = StartCoroutine(OffensiveMove());

        yield return restrict;
        yield return attack;
    }

    IEnumerator RestrictiveMove()
    {
        while (_isRewinding) yield return null;

        var strategy = playerStrategyModel.GetDominantStrategy();

        Debug.Log("Boss reacting to strategy: " + strategy); 

        if (strategy == PlayerStrategyModel.StrategyType.AggressivePlayer)
            yield return StartCoroutine(Platforms());

        else if (strategy == PlayerStrategyModel.StrategyType.DefensivePlayer)
            yield return StartCoroutine(Enemy());
        
        else 
            yield return StartCoroutine(FireWave()); 
    }

    IEnumerator OffensiveMove()
    {
        while (_isRewinding) yield return null;

        var strategy = playerStrategyModel.GetDominantStrategy();

        Debug.Log("Boss offensive decision for strategy: " + strategy);

        if (strategy == PlayerStrategyModel.StrategyType.AggressivePlayer)
            yield return StartCoroutine(FireColumns());

        else
            yield return StartCoroutine(Fireballs());
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
                    attackManager.spawnFireball();
            }

            yield return StartCoroutine(WaitOffensive(0.5f));
        }
    }

    IEnumerator FireColumns()
    {
        while (_isRewinding) yield return null;

        if(!_isRewinding) 
            attackManager.spawnFireColumns();

        yield return StartCoroutine(WaitOffensive(5f));
    }

    IEnumerator FireRow()
    {
        while (_isRewinding) yield return null;

        if(!_isRewinding) 
            attackManager.spawnFireRow();

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
                    attackManager.spawnFireWave();
            }

            yield return StartCoroutine(WaitRestrictive(1f));
        }
    }

    IEnumerator Platforms()
    {
        while (_isRewinding) yield return null;
        attackManager.spawnPlatforms();
        yield return StartCoroutine(WaitRestrictive(0.5f));
        attackManager.spawnFloorFire();
        PlatformController platform = FindFirstObjectByType<PlatformController>();
        while (platform != null && !platform.cycleComplete)
            yield return null;
    }

    IEnumerator Enemy()
    {
        while (_isRewinding) yield return null;

        if(!_isRewinding) 
            attackManager.spawnEnemy();

        yield return StartCoroutine(WaitRestrictive(7f));
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
        if (collision.gameObject.CompareTag("Player"))
            Damage();
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
