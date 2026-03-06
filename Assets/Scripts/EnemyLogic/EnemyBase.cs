using UnityEngine;
using TimeRewind;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(HitFlash))]
public class EnemyBase : MonoBehaviour, IDamageable, IKnockbackable, IRewindable
{
    [Header("Health")]
    public int health = 3;

    [Header("Knockback")]
    public float knockbackResistance = 1f; // higher = less knockback
    public float knockbackUpMultiplier = 0.8f;
    public System.Action OnDeath;

    [Header("Death Settings")]
    public float deathAnimationDuration = 0.6f;

    protected Rigidbody2D rb;
    protected SpriteRenderer sprite;
    protected HitFlash flash;

    protected RigidbodyType2D originalBodyType;
    protected bool isRewinding;
    protected bool wasDead;

    public bool IsDead => health <= 0;
    protected bool isStunned;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        flash = GetComponent<HitFlash>();

  
    }
    protected virtual void OnEnable()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
    }

    protected virtual void OnDisable()
    {
        if (TimeRewindManager.Instance != null)
        TimeRewindManager.Instance.Unregister(this);
}
    
    // ================= DAMAGE =================
    public virtual void TakeDamage(int amount)
    {
        if (wasDead) return;
        health -= amount;
        flash?.Flash();

        if (health <= 0)
        {
            Die();
        }
    }

    // ================= KNOCKBACK =================
    public virtual void ApplyKnockback(Vector2 force)
    {
        if (isRewinding || wasDead) return;

        force /= knockbackResistance;
        rb.linearVelocity = Vector2.zero;

        Vector2 finalForce = new Vector2(force.x, Mathf.Max(Mathf.Abs(force.x), Mathf.Abs(force.y)) * knockbackUpMultiplier);
        rb.AddForce(finalForce, ForceMode2D.Impulse);
        StartCoroutine(HitStunRoutine(0.25f));
    }

    private System.Collections.IEnumerator HitStunRoutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    // ================= DEATH =================
    public virtual void Die()
    {
        wasDead = true;
        //if (sprite != null) sprite.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        rb.linearVelocity = Vector2.zero;
        OnDeath?.Invoke();
        StartCoroutine(DeathRoutine());
        // Do not Destroy - stay registered so rewind can restore us
    }
    public virtual IEnumerator DeathRoutine()
    {
        // Wait for the specific enemy's animation to finish
        yield return new WaitForSeconds(deathAnimationDuration);
        
        // Hide the sprite instead of Destroying (so it can be rewound)
        if (sprite != null) sprite.enabled = false;
    }
    // ================= REWIND =================
    public virtual void OnStartRewind()
    {
        isRewinding = true;
        originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public virtual void OnStopRewind()
    {
        isRewinding = false;
        rb.bodyType = originalBodyType;

        // If rewind stopped during a death animation, restart the cleanup coroutine
        // so the sprite gets hidden (StopAllCoroutines in OnStartRewind killed it)
        if (wasDead)
            StartCoroutine(DeathRoutine());
    }


    public virtual RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            rb.linearVelocity,
            rb.angularVelocity,
            Time.time
        );

        state.Health = health;
        state.SetCustomData("flipX", sprite.flipX);
        return state;
    }

    public virtual void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        rb.linearVelocity = state.Velocity;

        health = state.Health;
        sprite.flipX = state.GetCustomData<bool>("flipX");

        if (state.Health > 0 && wasDead)
        {
            wasDead = false;
            if (sprite != null) sprite.enabled = true;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }
    }
}
