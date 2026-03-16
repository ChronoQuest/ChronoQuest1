using UnityEngine;

public class FallingHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int damageAmount = 1;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleCollision(collision);
    }
    private void HandleCollision(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            var health = collision.GetComponent<PlayerHealth>();
            if (health != null) health.ModifyHealth(-damageAmount, false);

            var safetyNet = collision.GetComponent<PlayerSafetyNet>();
            if (safetyNet != null) 
            {
                Debug.Log("Safety net called!");
                safetyNet.respawnDelay = 0f;
                safetyNet.RespawnAtSafety();
                Rigidbody2D playerRb = collision.GetComponentInParent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = Vector2.zero;
                }
            }
        }

        // If an enemy falls off the stage (or is crushed by boss), deactivate then destroy after 5 seconds
        else if (collision.CompareTag("Enemy")){
            collision.transform.root.gameObject.SetActive(false);
            Destroy(collision.transform.root.gameObject, 5f);
        }
        
    }
}
