using UnityEngine;

/// <summary>
/// Reusable component: add to any hazard (blade, spikes, saw, etc.) to deal damage and optional knockback on contact.
/// Works with both trigger and solid colliders.
/// </summary>
public class TrapDamage : MonoBehaviour
{
    [Header("Settings")]
    public int damage = 1;
    public float knockbackForce = 5f;

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
            playerHealth.ModifyHealth(-damage);

        if (knockbackForce > 0f)
        {
            Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                Vector2 direction = (otherPosition - transform.position).normalized;
                playerRb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }
}