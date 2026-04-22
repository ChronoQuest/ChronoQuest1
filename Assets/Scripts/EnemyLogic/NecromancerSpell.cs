using UnityEngine;
using System.Collections;
using TimeRewind;

public class NecromancerSpell : MonoBehaviour, IRewindable
{
    [Header("Spell Settings")]
    public float speed = 5f;
    public float lifetime = 4f;
    public float detonationDuration = 0.5f; // match SpellDetonation clip length

    private int damage;
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private bool isActive;
    private bool isRewinding;
    private float elapsedLifetime;
    private RigidbodyType2D originalBodyType;
    [SerializeField] private AudioClip spellClip;
    [SerializeField] private float spellVolume = 1f;
    [SerializeField] private AudioClip spellExplosionClip;
    [SerializeField] private float spellExplosionVolume = 0.5f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        originalBodyType = rb.bodyType;

        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
    }

    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Unregister(this);
    }

    public void Launch(Vector2 direction, int spellDamage)
    {
        if(spellClip != null) AudioSource.PlayClipAtPoint(spellClip, transform.position, spellVolume);
        damage = spellDamage;
        isActive = true;
        elapsedLifetime = 0f;
        rb.linearVelocity = direction.normalized * speed;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        StartCoroutine(EnableColliderNextFrame());
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
        if (!isActive) return;
        isActive = false;
        rb.linearVelocity = Vector2.zero;
        col.enabled = false;
        StopAllCoroutines();
        StartCoroutine(DetonationRoutine());
    }

    IEnumerator DetonationRoutine()
    {
        if (animator != null)
            animator.SetTrigger("Detonate");
        if(spellExplosionClip != null) AudioSource.PlayClipAtPoint(spellExplosionClip, transform.position, spellExplosionVolume);
        yield return new WaitForSeconds(detonationDuration);
        gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive || isRewinding) return;
        if (other.isTrigger) return;

        // Pass through enemies
        if (other.GetComponent<EnemyBase>() != null) return;

        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.ModifyHealth(-damage);
            Deactivate();
            return;
        }

        // Hit ground or solid wall
        Deactivate();
    }

    // ================= REWIND =================

    public void OnStartRewind()
    {
        isRewinding = true;
        StopAllCoroutines();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (animator != null) animator.speed = 0f;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        rb.bodyType = originalBodyType;
        if (animator != null) animator.speed = 1f;
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
        // Save the visual active state separately from the logic flag so that
        // the detonation window (isActive=false but object still visible) rewinds correctly.
        state.SetCustomData("visible", gameObject.activeSelf);
        state.SetCustomData("isActive", isActive);
        state.SetCustomData("elapsedLifetime", elapsedLifetime);
        state.SetCustomData("flipX", spriteRenderer != null && spriteRenderer.flipX);
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

        // Re-enable collider when rewinding back to a visible state
        if (shouldBeVisible && wasInactive)
            col.enabled = true;

        if (spriteRenderer != null)
            spriteRenderer.flipX = state.GetCustomData<bool>("flipX");
    }
}
