using UnityEngine;

/// <summary>
/// Reusable component: add to any hazard (blade, spikes, saw, etc.) to deal damage and optional knockback on contact.
/// Works with both trigger and solid colliders.
/// </summary>
public class TrapDamage : MonoBehaviour
{
    [Header("Settings")]
    public int damage = 1;
    public float knockbackForce = 8f;   // Horizontal push
    public float upwardForce = 6f;      // Vertical arc force

    private int _defaultDamage;
    private float _defaultKnockbackForce;
    private float _defaultUpwardForce;
    private Collider2D _hazardCollider;

    private void Awake()
    {
        _defaultDamage = damage;
        _defaultKnockbackForce = knockbackForce;
        _defaultUpwardForce = upwardForce;
        _hazardCollider = GetComponent<Collider2D>();
    }

    /// <summary>No damage, no knockback, collider off — used by platforming assists.</summary>
    public void SetHazardDisabled(bool disabled)
    {
        damage = disabled ? 0 : _defaultDamage;
        knockbackForce = disabled ? 0f : _defaultKnockbackForce;
        upwardForce = disabled ? 0f : _defaultUpwardForce;
        if (_hazardCollider != null)
            _hazardCollider.enabled = !disabled;
    }

    public void RestoreDefaultDamage()
    {
        damage = _defaultDamage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other.gameObject, other.transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamage(collision.gameObject, collision.transform.position);
    }

    private void TryDamage(GameObject other, Vector3 otherPosition)
    {
        if (!other.CompareTag("Player")) return;
        
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ModifyHealth(-damage);
            DataCollectionService.Instance?.RecordTrapHit();
        }

        if (knockbackForce > 0f)
        {
            PlayerPlatformer player = other.GetComponent<PlayerPlatformer>();
            if (player != null)
            {
                float horizontalDir = Mathf.Sign(otherPosition.x - transform.position.x);

                Vector2 knockback = new Vector2(
                    horizontalDir * knockbackForce,
                    upwardForce
                );

                player.ApplyKnockback(knockback);
            }

        }
    }
}