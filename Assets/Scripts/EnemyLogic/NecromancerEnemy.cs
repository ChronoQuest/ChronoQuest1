using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TimeRewind;

public class NecromancerEnemy : EnemyBase
{
    [Header("Stats")]
    public float detectionRange = 10f;
    public float safeDistance = 4f;
    public float moveSpeed = 2f;

    [Header("Revive")]
    public float reviveCooldown = 5f;
    public float reviveAnimDuration = 1.2f; // match NecromancerRevive clip length

    [Header("Attack")]
    public float attackRange = 8f;
    public float attackCooldown = 3f;
    public float attackAnimDuration = 0.8f; // match NecromancerAttack clip length
    public int attackDamage = 1;
    public GameObject spellPrefab;
    public int spellPoolSize = 3;

    [Header("References")]
    public Transform player;
    public List<EnemyBase> minions = new List<EnemyBase>();

    private Animator animator;
    private Collider2D col;
    private Vector3 originalScale;

    private float lastReviveTime = -99f;
    private bool isReviving;

    private NecromancerSpell[] spellPool;
    private float lastAttackTime = -99f;
    private bool isAttacking;
    private Vector2 pendingSpellDirection;

    private enum State { Idle, BackAway, Revive, Attack }
    private State currentState = State.Idle;

    protected override void Awake()
    {
        health = 3;
        base.Awake();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        originalScale = transform.localScale;
        BuildSpellPool();
    }

    void BuildSpellPool()
    {
        if (spellPrefab == null) return;
        spellPool = new NecromancerSpell[spellPoolSize];
        for (int i = 0; i < spellPoolSize; i++)
        {
            GameObject obj = Instantiate(spellPrefab, transform.position, Quaternion.identity);
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

    void Update()
    {
        if (isRewinding || wasDead || isReviving || isAttacking) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (CanRevive())
            currentState = State.Revive;
        else if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist < safeDistance)
            currentState = State.BackAway;
        else if (dist <= attackRange && CanAttack())
            currentState = State.Attack;
        else
            currentState = State.Idle;

        // Always face the player when detected
        if (dist <= detectionRange)
            FacePlayer();

        animator?.SetBool("isWalking", currentState == State.BackAway);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isStunned || isReviving || isAttacking) return;

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;

            case State.BackAway:
                Vector2 away = ((Vector2)transform.position - (Vector2)player.position).normalized;
                rb.linearVelocity = new Vector2(away.x * moveSpeed, rb.linearVelocity.y);
                break;

            case State.Revive:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                StartCoroutine(ReviveRoutine());
                break;

            case State.Attack:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                StartCoroutine(AttackRoutine());
                break;
        }
    }

    bool CanRevive()
    {
        return !isReviving
            && Time.time >= lastReviveTime + reviveCooldown
            && HasDeadMinion();
    }

    bool HasDeadMinion()
    {
        foreach (var m in minions)
            if (m != null && m.IsDead) return true;
        return false;
    }

    IEnumerator ReviveRoutine()
    {
        isReviving = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(reviveCooldown);
        animator?.SetTrigger("Revive");
        // ReviveMinion() is called by Animation Event mid-clip
        yield return new WaitForSeconds(reviveAnimDuration);
        lastReviveTime = Time.time;
        isReviving = false;
    }

    bool CanAttack() => !isAttacking && Time.time >= lastAttackTime + attackCooldown;

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        pendingSpellDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        animator?.SetTrigger("Attack");
        // FireSpell() is called by Animation Event mid-clip
        yield return new WaitForSeconds(attackAnimDuration);
        lastAttackTime = Time.time;
        isAttacking = false;
    }

    // Called by Animation Event on the NecromancerAttack clip at the cast frame
    public void FireSpell()
    {
        if (wasDead || isRewinding) return;
        NecromancerSpell spell = GetPooledSpell();
        if (spell == null) return;
        spell.transform.position = transform.position;
        spell.gameObject.SetActive(true);
        spell.Launch(pendingSpellDirection, attackDamage);
    }

    // Called by Animation Event on the NecromancerRevive clip at the peak frame
    public void ReviveMinion()
    {
        if (wasDead || isRewinding) return;
        foreach (var m in minions)
        {
            if (m != null && m.IsDead)
                m.Revive();
        }
    }

    void FacePlayer()
    {
        if (player.position.x > transform.position.x)
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    public override void TakeDamage(int amount)
    {
        if (wasDead) return;
        animator?.SetTrigger("Hit");
        base.TakeDamage(amount);
    }

    public override void Die()
    {
        isReviving = false;
        isAttacking = false;
        animator?.SetTrigger("Die");
        base.Die();          // handles wasDead, Kinematic, zero velocity, collider, DeathRoutine
        StopAllCoroutines(); // cancel DeathRoutine so corpse stays visible
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isReviving = false;
        isAttacking = false;
        if (col != null) col.enabled = true;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
        isReviving = false;
        isAttacking = false;
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("lastReviveTime", lastReviveTime);
        state.SetCustomData("isReviving", isReviving);
        state.SetCustomData("lastAttackTime", lastAttackTime);
        state.SetCustomData("colEnabled", col != null && col.enabled);
        state.SetCustomData("spriteEnabled", sprite != null && sprite.enabled);
        state.SetCustomData("localScale", transform.localScale);

        if (animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = info.shortNameHash;
            state.AnimatorNormalizedTime = info.normalizedTime;
        }

        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);
        lastReviveTime = state.GetCustomData<float>("lastReviveTime");
        isReviving = state.GetCustomData<bool>("isReviving");
        lastAttackTime = state.GetCustomData<float>("lastAttackTime");
        transform.localScale = state.GetCustomData<Vector3>("localScale", originalScale);

        if (col != null)
            col.enabled = state.GetCustomData<bool>("colEnabled", true);

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        if (animator != null && !justBecameAlive)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}
