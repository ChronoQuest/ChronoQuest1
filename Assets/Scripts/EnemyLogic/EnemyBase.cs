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
    [HideInInspector] public int startHealth;

    [Header("Stun Settings")]
    public bool stunOnLand = false; // Toggle this ON in the Inspector for land enemies
    protected bool isLaunched;

    [Header("Knockback")]
    public float knockbackResistance = 1f;  // higher = less knockback
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
    protected bool justBecameAlive; // true for one ApplyState frame when transitioning dead→alive

    public bool IsDead => health <= 0;
    protected bool isStunned;

    public bool GetIsStunned() => isStunned;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        flash = GetComponent<HitFlash>();
        startHealth = health;
        originalBodyType = rb.bodyType; // captured once — represents alive body type
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

        if (!isStunned)
        {
            StartCoroutine(HitStunRoutine(0.2f));
        }
        
        if (health <= 0)
        {
            Die();
        }
    }

    // ================= KNOCKBACK =================
    public virtual void ApplyKnockback(Vector2 force)
    {
        if (isRewinding || wasDead) return;

        //force /= knockbackResistance;
        rb.linearVelocity = Vector2.zero;

        //Vector2 finalForce = new Vector2(force.x, Mathf.Max(Mathf.Abs(force.x), Mathf.Abs(force.y)) * knockbackUpMultiplier);
        //rb.AddForce(finalForce, ForceMode2D.Impulse);
        //StartCoroutine(HitStunRoutine(0.25f));

        float horizontalForce = (force.x / knockbackResistance) * 0.6f; 
        float verticalForce = Mathf.Max(Mathf.Abs(force.x), Mathf.Abs(force.y)) * knockbackUpMultiplier * 0.7f;

        rb.AddForce(new Vector2(horizontalForce, verticalForce), ForceMode2D.Impulse);

        if (stunOnLand){
            isLaunched = true;
        }
        else{
            StartCoroutine(HitStunRoutine(0.25f));
        }   
    }

    protected System.Collections.IEnumerator HitStunRoutine(float duration)
    {
        isStunned = true;
        isLaunched = false;

        if (flash != null)
        {
            while (flash.IsFlashing)
            {
                yield return null; 
            }
        }

        if (sprite != null && !wasDead) 
            sprite.color = new Color(0.7f, 0.7f, 0.7f);

        yield return new WaitForSeconds(duration);

        if (sprite != null && !wasDead)
        {
            if (flash != null && !flash.IsFlashing)
                sprite.color = Color.white;
        }

        isStunned = false;
    }

    // ================= DEATH =================
    public virtual void Die()
    {
        wasDead = true;
        rb.bodyType = RigidbodyType2D.Kinematic; // freeze in place — prevents falling through floor
        rb.linearVelocity = Vector2.zero;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
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
    // ================= REVIVE =================
    public virtual void Revive()
    {
        StopAllCoroutines(); // stop any pending DeathRoutine that would re-hide the sprite
        wasDead = false;
        health = startHealth;
        rb.bodyType = originalBodyType;
        if (sprite != null) sprite.enabled = true;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
    }

    // ================= REWIND =================
    public virtual void OnStartRewind()
    {
        isRewinding = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public virtual void OnStopRewind()
    {
        isRewinding = false;
        // Restore alive body type if living, keep frozen if still dead
        rb.bodyType = wasDead ? RigidbodyType2D.Kinematic : originalBodyType;

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

        justBecameAlive = false;

        if (state.Health > 0 && wasDead)
        {
            // dead → alive transition
            justBecameAlive = true;
            wasDead = false;
            rb.bodyType = originalBodyType;
            if (sprite != null) sprite.enabled = true;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }
        else if (state.Health <= 0 && !wasDead)
        {
            // alive → dead transition (rewinding past the death event)
            wasDead = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }
}
