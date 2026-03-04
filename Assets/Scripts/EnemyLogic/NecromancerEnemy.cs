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

    [Header("References")]
    public Transform player;
    public List<EnemyBase> minions = new List<EnemyBase>();

    private Animator animator;
    private Collider2D col;
    private Vector3 originalScale;

    private float lastReviveTime = -99f;
    private bool isReviving;

    private enum State { Idle, BackAway, Revive }
    private State currentState = State.Idle;

    protected override void Awake()
    {
        health = 3;
        base.Awake();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        originalScale = transform.localScale;
    }

    void Update()
    {
        if (isRewinding || wasDead || isReviving) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist < safeDistance)
            currentState = State.BackAway;
        else if (CanRevive())
            currentState = State.Revive;
        else
            currentState = State.Idle;

        // Always face the player when detected
        if (dist <= detectionRange)
            FacePlayer();

        animator?.SetBool("isWalking", currentState == State.BackAway);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isStunned || isReviving) return;

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
            if (m != null && m.wasDead) return true;
        return false;
    }

    IEnumerator ReviveRoutine()
    {
        isReviving = true;
        rb.linearVelocity = Vector2.zero;
        animator?.SetTrigger("Revive");
        // ReviveMinion() is called by Animation Event mid-clip
        yield return new WaitForSeconds(reviveAnimDuration);
        lastReviveTime = Time.time;
        isReviving = false;
    }

    // Called by Animation Event on the NecromancerRevive clip at the peak frame
    public void ReviveMinion()
    {
        if (wasDead || isRewinding) return;
        foreach (var m in minions)
        {
            if (m != null && m.wasDead)
            {
                m.Revive();
                return; // revive one at a time
            }
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
        wasDead = true;
        StopAllCoroutines();
        isReviving = false;

        animator?.SetTrigger("Die");
        rb.linearVelocity = Vector2.zero;

        if (col != null) col.enabled = false;

        StartCoroutine(base.DeathRoutine());
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
        isReviving = false;
        if (col != null) col.enabled = true;
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();
        state.SetCustomData("lastReviveTime", lastReviveTime);
        state.SetCustomData("isReviving", isReviving);
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
        transform.localScale = state.GetCustomData<Vector3>("localScale", originalScale);

        if (col != null)
            col.enabled = state.GetCustomData<bool>("colEnabled", true);

        if (sprite != null)
            sprite.enabled = state.GetCustomData<bool>("spriteEnabled", true);

        if (animator != null)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}
