using UnityEngine;

public class TrapDamage : MonoBehaviour
{
    [Header("Settings")]
    public int damage = 1;
    public float knockbackForce = 5f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the object we hit is the Player
        if (collision.CompareTag("Player"))
        {
            // 1. Deal Damage
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ModifyHealth(-damage);
            }

            // 2. Optional: Add Knockback
            // This pushes the player away so they don't get hit twice instantly
            Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // Calculate direction from Blade -> Player
                Vector2 direction = (collision.transform.position - transform.position).normalized;
                playerRb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }
}