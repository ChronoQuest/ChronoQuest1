using TimeRewind;
using UnityEngine;
using System.Collections;
using NUnit.Framework.Internal.Commands;

public class Boss : MonoBehaviour, IRewindable
{
    public float attackCooldown = 1f;
    public float damageCooldown = 1.5f;
    public int damage = 1;
    public int health = 3;
    private bool isAttacking;

    public Transform player;
    public BossAttackManager attackManager;
    private bool _isRewinding;
    private Rigidbody2D rb;
    private float lastDamageTime;
    private float lastAttackTime;
    private Vector3 originalScale;
    private Animator animator;
    private bool playerInContact = false;

    void Start()
    {
        originalScale = transform.localScale;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Register(this);
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
                attackManager.raisePlatforms();
                // Allow time for player to react to platforms
                yield return wait2;
                attackManager.spawnFloorFire();
                yield return wait;
            }
        attackManager.lowerPlatforms();
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
    public void TakeDamage(int amount)
    {
        health -= amount;
        Debug.Log("Boss took damage! Health: " + health);

        if (health <= 0)
        {
            Die();
        }
    }
    void Die()
    {
        Debug.Log("Boss died!");
        Destroy(gameObject);
    }
    public void OnStartRewind()
    {
        _isRewinding = true; // Sets the flag that stops Update/FixedUpdate
    }
    public void OnStopRewind()
    {
        _isRewinding = false;
        StopAllCoroutines();
        StartCoroutine(AttackLoop());
    }
    public RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            (rb != null) ? rb.linearVelocity : Vector2.zero,
            (rb != null) ? rb.angularVelocity : 0f,
            Time.time
        );
        // Save data using Dictionary
        state.Health = health;
        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        health = state.Health;
    }
}