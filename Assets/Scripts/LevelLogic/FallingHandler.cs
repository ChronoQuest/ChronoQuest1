using UnityEngine;

public class FallingHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int damageAmount = 1;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            var health = collision.GetComponent<PlayerHealth>();
            if (health != null) health.ModifyHealth(-damageAmount);

            var safetyNet = collision.GetComponent<PlayerSafetyNet>();
            if (safetyNet != null) safetyNet.RespawnAtSafety();
        }

        // If an enemy falls off the stage, destroy after 5 seconds
        else if (collision.CompareTag("Enemy")) Destroy(collision.transform.root.gameObject, 5f);
    }
}
