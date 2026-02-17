using System.Collections;
using UnityEngine;
using TimeRewind;

public class SkeletonArcher : EnemyBase
{
    [Header("Detection")]
    public float detectionRange = 8f;
    public float shootRange = 6f;
    public float safeDistance = 3f;

    [Header("Movement")]
    public float retreatSpeed = 2f;

    [Header("Shooting")]
    public int damage = 1;
    public float shootCooldown = 2f;

    [Header("References")]
    public Transform player;
    public GameObject arrowPrefab;

    [Header("Arrow Pool")]
    public int arrowPoolSize = 5;

    [Header("Arrow Spawn")]
    public Vector2 arrowSpawnOffset = new Vector2(0.3f, 0.5f);

    private Animator animator;
    private Collider2D col;

    private enum State { Idle, Retreat, Shoot, Hit, Dead }
    [SerializeField] private State currentState = State.Idle;

    private float lastShootTime;
    private Vector3 originalScale;
    private Vector2 pendingArrowDirection;

    private ArrowProjectile[] arrowPool;
    private RewindState? _lastAppliedState;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        originalScale = transform.localScale;

        BuildArrowPool();
    }

    void BuildArrowPool()
    {
        if (arrowPrefab == null) return;

        arrowPool = new ArrowProjectile[arrowPoolSize];
        for (int i = 0; i < arrowPoolSize; i++)
        {
            GameObject obj = Instantiate(arrowPrefab, transform.position, Quaternion.identity);
            arrowPool[i] = obj.GetComponent<ArrowProjectile>();
            obj.SetActive(false);
        }
    }

    void Update()
    {
        if (isRewinding || wasDead || isStunned) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > detectionRange)
            currentState = State.Idle;
        else if (dist < safeDistance)
            currentState = State.Retreat;
        else if (dist <= shootRange)
            currentState = State.Shoot;
        else
            currentState = State.Idle;

        if (animator != null)
            animator.SetFloat("Speed", currentState == State.Retreat ? 1f : 0f);
    }

    void FixedUpdate()
    {
        if (isRewinding || wasDead || isStunned) return;

        switch (currentState)
        {
            case State.Idle:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;
            case State.Retreat:
                Retreat();
                break;
            case State.Shoot:
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                TryShoot();
                break;
        }
    }

    void Retreat()
    {
        if (player == null) return;
        Vector2 dir = ((Vector2)transform.position - (Vector2)player.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * retreatSpeed, rb.linearVelocity.y);
        FaceDirection(dir.x);
    }

    void TryShoot()
    {
        if (Time.time < lastShootTime + shootCooldown) return;

        pendingArrowDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        FaceDirection(pendingArrowDirection.x);

        lastShootTime = Time.time;
        if (animator != null) animator.SetTrigger("Shoot");
    }

    /// <summary>
    /// Called by Animation Event on the attack clip at the arrow-release frame.
    /// </summary>
    public void FireArrow()
    {
        if (wasDead || isRewinding) return;

        ArrowProjectile arrow = GetPooledArrow();
        if (arrow == null) return;

        Vector3 spawnPos = transform.position
            + Vector3.up * arrowSpawnOffset.y
            + (Vector3)(pendingArrowDirection * arrowSpawnOffset.x);

        arrow.transform.position = spawnPos;
        arrow.transform.rotation = Quaternion.identity;
        arrow.gameObject.SetActive(true);
        arrow.Launch(pendingArrowDirection, damage);
    }

    ArrowProjectile GetPooledArrow()
    {
        if (arrowPool == null) return null;
        foreach (var arrow in arrowPool)
        {
            if (arrow != null && !arrow.gameObject.activeSelf)
                return arrow;
        }
        return null;
    }

    void FaceDirection(float dirX)
    {
        if (dirX > 0)
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        else if (dirX < 0)
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
    }

    // ================= DEATH =================

    protected override void Die()
    {
        base.Die();
        StopAllCoroutines();
        if (animator != null) animator.SetTrigger("Dead");
    }

    // ================= REWIND =================

    public override void OnStartRewind()
    {
        base.OnStartRewind();
        StopAllCoroutines();
    }

    public override void OnStopRewind()
    {
        base.OnStopRewind();
    }

    public override RewindState CaptureState()
    {
        var state = base.CaptureState();

        state.SetCustomData("EnemyState", (int)currentState);
        state.SetCustomData("FacingDirection", transform.localScale);
        if (animator != null)
        {
            AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animInfo.shortNameHash;
            state.AnimatorNormalizedTime = animInfo.normalizedTime;
        }

        return state;
    }

    public override void ApplyState(RewindState state)
    {
        base.ApplyState(state);
        _lastAppliedState = state;

        currentState = (State)state.GetCustomData<int>("EnemyState", (int)State.Idle);
        transform.localScale = state.GetCustomData<Vector3>("FacingDirection", originalScale);

        if (animator != null)
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
    }
}
