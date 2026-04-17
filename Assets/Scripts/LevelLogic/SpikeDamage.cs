using UnityEngine;

public class SpikeDamage : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int damageAmount = 1;
    [Tooltip("If true, sends player back to last safe spot. If false, just damages.")]
    [SerializeField] private bool respawnPlayer = true;

    public int GetDamageAmount() => damageAmount;
    public void SetDamageAmount(int value) => damageAmount = Mathf.Max(0, value);

    public bool GetRespawnPlayer() => respawnPlayer;
    public void SetRespawnPlayer(bool value) => respawnPlayer = value;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 1. Deal Damage
            var health = other.GetComponent<PlayerHealth>();
            if (health != null)
            {
                // Send negative number for damage
                health.ModifyHealth(-damageAmount);
            }

            // 2. Respawn Logic (Only if player isn't dead from the damage)
            if (respawnPlayer && health != null && !health.IsDead)
            {
                var safetyNet = other.GetComponent<PlayerSafetyNet>();
                if (safetyNet != null)
                {
                    safetyNet.RespawnAtSafety();
                }
            }
        }
    }
}