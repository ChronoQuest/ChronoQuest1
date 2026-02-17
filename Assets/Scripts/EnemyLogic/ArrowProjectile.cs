using System.Collections;
using UnityEngine;
using TimeRewind;

/// <summary>
/// A pooled arrow projectile fired by the SkeletonArcher.
/// Registers with the TimeRewindManager in Awake so its full history
/// (including times when it is inactive/pooled) is tracked from the start.
/// </summary>
public class ArrowProjectile : MonoBehaviour, IRewindable
{
    [Header("Stats")]
    public float arrowSpeed = 10f;
    public float lifetime = 3f;
    public int damage = 1;

    private Rigidbody2D rb;
    private Collider2D col;
    private Coroutine lifetimeCoroutine;
    private bool isRewinding;
    private int ownerDamage; // Damage value set by the archer on launch

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Register immediately in Awake (before first enable/disable)
        // so the full history including pooled state is captured from game start.
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
    }

    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Unregister(this);
    }

    /// <summary>
    /// Called by SkeletonArcher to fire this arrow in a direction.
    /// </summary>
    public void Launch(Vector2 direction, int damage)
    {
        ownerDamage = damage;
        rb.linearVelocity = direction.normalized * arrowSpeed;

        // Rotate sprite to face travel direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (lifetimeCoroutine != null)
            StopCoroutine(lifetimeCoroutine);
        lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
    }

    IEnumerator LifetimeRoutine()
    {
        // Disable collider for the first frame so the arrow doesn't immediately
        // trigger against the archer's own collider or the ground on spawn.
        if (col != null) col.enabled = false;
        yield return null;
        if (col != null) col.enabled = true;

        yield return new WaitForSeconds(lifetime);
        Deactivate();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isRewinding) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.ModifyHealth(-ownerDamage);

            Deactivate();
        }
        else if (!other.isTrigger)
        {
            // Hit environment or any solid collider
            Deactivate();
        }
    }

    void Deactivate()
    {
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
        rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);
    }

    // --- IRewindable Implementation ---

    public void OnStartRewind()
    {
        isRewinding = true;
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
        rb.linearVelocity = Vector2.zero;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        // Velocity is restored by ApplyState; no need to restart lifetime coroutine
        // since the arrow will shortly move/hit something or be re-pooled by the game.
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
        state.SetCustomData("isActive", gameObject.activeSelf);
        state.SetCustomData("ownerDamage", ownerDamage);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        bool shouldBeActive = state.GetCustomData<bool>("isActive");

        // Activate/deactivate without triggering OnEnable/OnDisable registration logic
        if (gameObject.activeSelf != shouldBeActive)
            gameObject.SetActive(shouldBeActive);

        if (!shouldBeActive) return;

        transform.position = state.Position;
        transform.rotation = state.Rotation;
        rb.linearVelocity = state.Velocity;
        ownerDamage = state.GetCustomData<int>("ownerDamage");
    }
}
