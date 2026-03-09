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