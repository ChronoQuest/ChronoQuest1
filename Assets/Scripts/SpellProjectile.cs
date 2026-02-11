using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpellProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 15f;
    public float lifetime = 3f;
    public int damage = 1;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Fly in the direction the ball is rotated
        rb.linearVelocity = transform.right * speed;

        // Auto-destruct after a few seconds
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. IGNORE THE PLAYER LAYER
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player")) 
        {
            return; 
        }

        // 2. Try to hit a Slime (Check for the script component)
        SlimeEnemy slime = collision.GetComponent<SlimeEnemy>();
        if (slime != null)
        {
            slime.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 3. HIT SOLID ENVIRONMENT (Walls/Ground)
        // Checks if the object is on the "Ground" layer OR if it's a non-trigger solid
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground") || !collision.isTrigger)
        {
            Debug.Log("Spell hit a solid object on layer: " + LayerMask.LayerToName(collision.gameObject.layer));
            Destroy(gameObject);
        }
    }
}