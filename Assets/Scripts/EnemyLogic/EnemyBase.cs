using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(HitFlash))]
public abstract class EnemyBase : MonoBehaviour, IDamageable, IKnockbackable
{
    protected int currentHealth;

    [Header("Knockback")]
    public float knockbackResistance = 1f; // higher = less knockback

    protected Rigidbody2D rb;
    protected SpriteRenderer sprite;
    protected HitFlash flash;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        flash = GetComponent<HitFlash>();

  
    }

    // ================= DAMAGE =================
    public virtual void TakeDamage(int amount)
    {
        currentHealth -= amount;
        flash?.Flash();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // ================= KNOCKBACK =================
    public virtual void ApplyKnockback(Vector2 force)
    {
        force /= knockbackResistance;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
    }

    // ================= DEATH =================
    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}
