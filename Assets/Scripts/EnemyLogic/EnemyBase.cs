using UnityEngine;
using TimeRewind;

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


    protected Rigidbody2D rb;
    protected SpriteRenderer sprite;
    protected HitFlash flash;

    protected RigidbodyType2D originalBodyType;
    protected bool isRewinding;
    protected bool wasDead;

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
        if(isRewinding) return;

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
    protected virtual void Die()
    {
        Destroy(gameObject);
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
    }
}
