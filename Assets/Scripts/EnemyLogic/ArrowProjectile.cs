using System.Collections;
using UnityEngine;
using TimeRewind;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
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

        rb.linearVelocity = direction.normalized * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        StartCoroutine(LifetimeRoutine());
    }

    IEnumerator LifetimeRoutine()
    {
        col.enabled = false;
        yield return null;
        col.enabled = true;

        yield return new WaitForSeconds(lifetime);
        Deactivate();
    }

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

    public void OnStartRewind()
    {
        isRewinding = true;
        StopAllCoroutines();
        rb.linearVelocity = Vector2.zero;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
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

        state.SetCustomData("isActive", isActive);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;

        bool shouldBeActive = state.GetCustomData<bool>("isActive");
        if (shouldBeActive != gameObject.activeSelf)
            gameObject.SetActive(shouldBeActive);

        isActive = shouldBeActive;
    }
}
