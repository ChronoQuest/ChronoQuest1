using System.Collections;
using UnityEngine;
using TimeRewind;

public class ArrowProjectile : MonoBehaviour, IRewindable
{
    [Header("Arrow Settings")]
    public float speed = 8f;
    public float lifetime = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private int damage;
    private bool isActive;
    private bool isRewinding;
    private float elapsedLifetime;
    private RigidbodyType2D originalBodyType;
    [Header("Homing")]
    public bool isHoming = false;
    public Transform homingTarget;
    public float homingTurnSpeed = 6f;
    public float homingTime = 1.5f;
    private float homingTimer;
    [Header("Audio")]
    public AudioClip impactClip;
    public float impactVolume = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalBodyType = rb.bodyType;

        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
    }
    void FixedUpdate()
    {
        if (!isActive || isRewinding) return;

        if (isHoming)
            {
                homingTimer -= Time.fixedDeltaTime;
                if (homingTimer <= 0f){
                    isHoming = false;
                    animator.SetBool("hasForesight", false);
                }
            }

        if (isHoming && homingTarget != null)
        {
            Vector2 targetDir = ((Vector2)homingTarget.position - rb.position).normalized;
            Vector2 newVelocity = Vector2.Lerp(rb.linearVelocity.normalized, targetDir, homingTurnSpeed * Time.fixedDeltaTime);

            rb.linearVelocity = newVelocity.normalized * speed * 0.5f;

            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Unregister(this);
    }

    public void Launch(Vector2 direction, int arrowDamage)
    {
        damage = arrowDamage;
        isActive = true;
        elapsedLifetime = 0f;

        isHoming = false;
        homingTarget = null;

        rb.linearVelocity = direction.normalized * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        StartCoroutine(EnableColliderNextFrame());
    }
    public void LaunchHoming(Vector2 direction, int arrowDamage, Transform target)
    {
        Launch(direction, arrowDamage);

        homingTimer = homingTime;
        animator.SetBool("hasForesight", true);

        isHoming = true;
        homingTarget = target;
    }

    IEnumerator EnableColliderNextFrame()
    {
        col.enabled = false;
        yield return null;
        col.enabled = true;
    }

    void Update()
    {
        if (!isActive || isRewinding) return;
        elapsedLifetime += Time.deltaTime;
        if (elapsedLifetime >= lifetime)
            Deactivate();
    }

    void Deactivate()
    {
        if(impactClip != null)
        {
            AudioSource.PlayClipAtPoint(impactClip, transform.position, impactVolume);
        }
        isActive = false;
        isHoming = false;
        homingTarget = null;
        rb.linearVelocity = Vector2.zero;
        StopAllCoroutines();
        animator.SetBool("hasForesight", false);
        gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        if (other.isTrigger) return;

        // Pass through enemies
        if (other.GetComponent<EnemyBase>() != null) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ModifyHealth(-damage);
            Deactivate();
            return;
        }

        Deactivate();
    }

    // ================= REWIND =================

    public void OnStartRewind()
    {
        isRewinding = true;
        StopAllCoroutines();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        rb.bodyType = originalBodyType;
    }

    public RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            rb.linearVelocity,
            rb.angularVelocity,
            Time.time
        );

        state.SetCustomData("visible", gameObject.activeSelf);
        state.SetCustomData("isActive", isActive);
        state.SetCustomData("elapsedLifetime", elapsedLifetime);
        state.SetCustomData("IdleTimer", homingTimer);
        state.SetCustomData("IsFlipped", isHoming);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        rb.linearVelocity = state.Velocity;

        bool shouldBeVisible = state.GetCustomData<bool>("visible");
        bool wasInactive = !gameObject.activeSelf;
        if (shouldBeVisible != gameObject.activeSelf)
            gameObject.SetActive(shouldBeVisible);

        isActive = state.GetCustomData<bool>("isActive");
        elapsedLifetime = state.GetCustomData<float>("elapsedLifetime");
        homingTimer = state.GetCustomData<float>("IdleTimer", 0);
        isHoming = state.GetCustomData<bool>("IsFlipped", false);

        if (shouldBeVisible && wasInactive)
            col.enabled = true;
    }
}
