using UnityEngine;
using TimeRewind; // Uses your namespace

public class HealthPickup : MonoBehaviour, IRewindable
{
    [Header("Settings")]
    [SerializeField] private int healAmount = 1;
    [SerializeField] private bool consumeOnPickup = true;

    [Header("Effects")]
    [SerializeField] private GameObject pickupEffectPrefab;
    [SerializeField] private AudioClip pickupSound;

    // State
    private bool _isCollected = false;
    private bool _isRewinding = false;

    // References
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
    }

    private void OnEnable() => TimeRewindManager.Instance?.Register(this);
    private void OnDisable() => TimeRewindManager.Instance?.Unregister(this);

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Don't trigger if already collected or rewinding
        if (_isCollected || _isRewinding) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

            // Only pick up if player is actually hurt
            if (playerHealth != null && playerHealth.CurrentHealth < playerHealth.MaxHealth)
            {
                Collect(playerHealth);
            }
        }
    }

    private void Collect(PlayerHealth player)
    {
        // 1. Apply Health
        player.ModifyHealth(healAmount);

        // 2. Play Effects
        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
        
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        // 3. "Disable" the object (without destroying it, so we can rewind)
        SetCollectedState(true);
    }

    private void SetCollectedState(bool collected)
    {
        _isCollected = collected;

        // We hide the visuals and disable the collider, but KEEP the GameObject active
        // so the Rewind Manager can still talk to it.
        if (_spriteRenderer != null) _spriteRenderer.enabled = !collected;
        if (_collider != null) _collider.enabled = !collected;
    }

    // ====================================================
    // REWIND IMPLEMENTATION
    // ====================================================

    public void OnStartRewind()
    {
        _isRewinding = true;
    }

    public void OnStopRewind()
    {
        _isRewinding = false;
    }

    public RewindState CaptureState()
    {
        // We only need to track if it was collected or not
        var state = RewindState.Create(transform.position, transform.rotation, Time.time);
        state.SetCustomData("IsCollected", _isCollected);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        bool wasCollected = state.GetCustomData<bool>("IsCollected", false);

        // If the state differs from our current state, update it
        if (_isCollected != wasCollected)
        {
            SetCollectedState(wasCollected);
            
            // Optional Polish: If we just "un-collected" it, maybe play a small sound?
            // if (!wasCollected) Debug.Log("Heart Respawned!");
        }
    }
}