using UnityEngine;

public class SpikeDamage : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int damageAmount = 1;
    [Tooltip("If true, sends player back to last safe spot. If false, just damages.")]
    [SerializeField] private bool respawnPlayer = true;

    private int _defaultDamage;
    private bool _defaultRespawn;
    private Collider2D _hazardCollider;

    private void Awake()
    {
        _defaultDamage = damageAmount;
        _defaultRespawn = respawnPlayer;
        _hazardCollider = GetComponent<Collider2D>();
    }

    public int GetDamageAmount() => damageAmount;
    public void SetDamageAmount(int value) => damageAmount = Mathf.Max(0, value);

    public bool GetRespawnPlayer() => respawnPlayer;
    public void SetRespawnPlayer(bool value) => respawnPlayer = value;

    /// <summary>No damage, no respawn knockback, collider off — used by platforming assists.</summary>
    public void SetHazardDisabled(bool disabled)
    {
        damageAmount = disabled ? 0 : _defaultDamage;
        respawnPlayer = !disabled && _defaultRespawn;
        if (_hazardCollider != null)
            _hazardCollider.enabled = !disabled;
    }

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