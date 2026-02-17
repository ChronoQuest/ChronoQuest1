using System.Collections;
using UnityEngine;
using TimeRewind;

<<<<<<< feature/combat-logic
public class ArrowProjectile : MonoBehaviour, IRewindable
{
    [Header("Arrow Settings")]
    public float speed = 8f;
    public float lifetime = 5f;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private int damage;
    private bool isActive;
    private bool isRewinding;
=======
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
>>>>>>> dev

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
<<<<<<< feature/combat-logic
        spriteRenderer = GetComponent<SpriteRenderer>();

=======

        // Register immediately in Awake (before first enable/disable)
        // so the full history including pooled state is captured from game start.
>>>>>>> dev
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
    }

    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Unregister(this);
    }

<<<<<<< feature/combat-logic
    public void Launch(Vector2 direction, int arrowDamage)
    {
        damage = arrowDamage;
        isActive = true;

        rb.linearVelocity = direction.normalized * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        spriteRenderer.flipX = direction.x < 0;

        StartCoroutine(LifetimeRoutine());
=======
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
>>>>>>> dev
    }

    IEnumerator LifetimeRoutine()
    {
<<<<<<< feature/combat-logic
        col.enabled = false;
        yield return null;
        col.enabled = true;
=======
        // Disable collider for the first frame so the arrow doesn't immediately
        // trigger against the archer's own collider or the ground on spawn.
        if (col != null) col.enabled = false;
        yield return null;
        if (col != null) col.enabled = true;
>>>>>>> dev

        yield return new WaitForSeconds(lifetime);
        Deactivate();
    }

<<<<<<< feature/combat-logic
    void Deactivate()
    {
        isActive = false;
        rb.linearVelocity = Vector2.zero;
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        if (other.isTrigger) return;

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
=======
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
>>>>>>> dev

    public void OnStartRewind()
    {
        isRewinding = true;
<<<<<<< feature/combat-logic
        StopAllCoroutines();
=======
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
>>>>>>> dev
        rb.linearVelocity = Vector2.zero;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
<<<<<<< feature/combat-logic
=======
        // Velocity is restored by ApplyState; no need to restart lifetime coroutine
        // since the arrow will shortly move/hit something or be re-pooled by the game.
>>>>>>> dev
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
<<<<<<< feature/combat-logic

        state.SetCustomData("isActive", isActive);
=======
        state.SetCustomData("isActive", gameObject.activeSelf);
        state.SetCustomData("ownerDamage", ownerDamage);
>>>>>>> dev
        return state;
    }

    public void ApplyState(RewindState state)
    {
<<<<<<< feature/combat-logic
        transform.position = state.Position;
        transform.rotation = state.Rotation;

        bool shouldBeActive = state.GetCustomData<bool>("isActive");
        if (shouldBeActive != gameObject.activeSelf)
            gameObject.SetActive(shouldBeActive);

        isActive = shouldBeActive;
=======
        bool shouldBeActive = state.GetCustomData<bool>("isActive");

        // Activate/deactivate without triggering OnEnable/OnDisable registration logic
        if (gameObject.activeSelf != shouldBeActive)
            gameObject.SetActive(shouldBeActive);

        if (!shouldBeActive) return;

        transform.position = state.Position;
        transform.rotation = state.Rotation;
        rb.linearVelocity = state.Velocity;
        ownerDamage = state.GetCustomData<int>("ownerDamage");
>>>>>>> dev
    }
}
