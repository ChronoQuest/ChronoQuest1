using TimeRewind;
using UnityEngine;
using System.Collections;
using NUnit.Framework.Internal.Commands;

public class Boss : EnemyBase
{
    public float attackCooldown = 1f;
    public float damageCooldown = 1.5f;
    public int damage = 1;
    private bool platformsRaised = false;

    public Transform player;
    public BossAttackManager attackManager;
    private bool _isRewinding;
    private float lastDamageTime;

    void Start()
    {
        StartCoroutine(AttackLoop());
    }

    void Update()
    {
        if (player == null) return;
        if (_isRewinding) return;
    }

    IEnumerator AttackLoop()
    {   
        while (health > 0){
            yield return new WaitForSeconds(2f);
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
        float rand = Random.value;
        while (_isRewinding) yield return null;
        if(rand > 0.5f)
        {
            if(rand > 0.75f) yield return StartCoroutine(FireRow());
            else yield return StartCoroutine(FireWave()); 
        } else if (rand > 0.25f) yield return StartCoroutine(Enemy());
        else yield return StartCoroutine(Platforms());
    }

    IEnumerator OffensiveMove()
    {
        while (_isRewinding) yield return null;
        if(Random.value > 0.5f){
            yield return StartCoroutine(Fireballs());
        }
        else yield return StartCoroutine(FireColumns());
    }

    IEnumerator Fireballs()
    {
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        while (_isRewinding) yield return null;
        WaitForSeconds wait = new WaitForSeconds(0.5f);
        for(int i = 0; i < 15; i++) {
            // If player health is less than 3, only spawn every other fireball
            if(i % 2 == 0 && playerHealth.CurrentHealth < 3){
                yield return wait;
                continue;
            } else
            {
                // Spawn a fireball
                if(!_isRewinding) attackManager.spawnFireball();
            }
            // Wait 0.5 second
            yield return wait;
            // Repeat 15 times
        }
    }

    IEnumerator FireColumns()
    {
        while (_isRewinding) yield return null;
        if(!_isRewinding) attackManager.spawnFireColumns();
        WaitForSeconds wait = new WaitForSeconds(5f);
        yield return wait;
    }

    IEnumerator FireRow()
    {
        while (_isRewinding) yield return null;
        if(!_isRewinding) attackManager.spawnFireRow();
        WaitForSeconds wait = new WaitForSeconds(7f);
        yield return wait;
    }

    IEnumerator FireWave()
    {
        while (_isRewinding) yield return null;
        WaitForSeconds wait = new WaitForSeconds(1f);
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        for(int i = 0; i < 7; i++) {
            // Spawn every other fire wave if health < 3
            if(i % 2 == 0 && playerHealth.CurrentHealth < 3){
                yield return wait;
                continue;
            } else
            {
                // Spawn a fireball
                if(!_isRewinding) attackManager.spawnFireWave();
            }
            // Wait 2 seconds
            yield return wait;
            // Repeat 4 times
        }
    }

    IEnumerator Platforms()
    {
        while (_isRewinding) yield return null;
        WaitForSeconds wait = new WaitForSeconds(7f);
        WaitForSeconds wait2 = new WaitForSeconds(1f);
            if(!_isRewinding) {
                platformsRaised = true;
                attackManager.raisePlatforms();
                // Allow time for player to react to platforms
                yield return wait2;
                attackManager.spawnFloorFire();
                yield return wait;
            }
        attackManager.lowerPlatforms();
        platformsRaised = false;
    }

    IEnumerator Enemy()
    {
        while (_isRewinding) yield return null;
        if(!_isRewinding) attackManager.spawnEnemy();
        WaitForSeconds wait = new WaitForSeconds(7f);
        yield return wait;
    }

    void Damage()
    {
        if (Time.time >= lastDamageTime + damageCooldown)
        {
            Debug.Log("Boss damages!");
            lastDamageTime = Time.time;

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ModifyHealth(-damage);
            }
        }
    }

    // Called when colliders touch
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Damage();
        }
    }
    public override void OnStartRewind()
    {
        _isRewinding = true;
    }
    public override void OnStopRewind()
    {
        _isRewinding = false;
        StopAllCoroutines();
        if(platformsRaised){
            attackManager.lowerPlatforms();
            platformsRaised = false;
        }
        StartCoroutine(AttackLoop());
    }
}