using UnityEngine;
using System.Collections;
using TimeRewind; // Needed to check if player started rewinding

public class PlayerSafetyNet : MonoBehaviour
{
    [Header("Safety Settings")]
    [Tooltip("Select your Ground Layer here.")]
    [SerializeField] private LayerMask groundLayer;
    
    [Tooltip("The tag you put on your Trap objects.")]
    [SerializeField] private string unsafeTag = "Trap"; 

    [Tooltip("How long you must be on safe ground before it saves")]
    [SerializeField] private float recordInterval = 0.05f; 

    [Header("Respawn Settings")]
    [Tooltip("Time to wait before teleporting (gives player chance to rewind)")]
    [SerializeField] private float respawnDelay = 1.0f; // NEW SETTING

    [Header("Detection Box")]
    [SerializeField] private float boxWidth = 0.5f;
    [SerializeField] private float boxHeight = 0.2f;
    [SerializeField] private Vector2 offset = new Vector2(0f, -0.6f);

    private Vector3 _lastSafePosition;
    private float _safeTimer;
    
    // References
    private Rigidbody2D _rb;
    private PlayerHealth _health;
    private bool _isRespawning;
    private Coroutine _respawnRoutine; // Store reference to cancel it

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _health = GetComponent<PlayerHealth>();
        _lastSafePosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (_health.IsDead || _isRespawning) return;
        
        // If we are currently rewinding, do not update safe position
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding) return;

        if (IsCurrentlySafe())
        {
            _safeTimer += Time.fixedDeltaTime;
            
            if (_safeTimer >= recordInterval)
            {
                _lastSafePosition = transform.position + Vector3.up * 0.1f;
                _safeTimer = recordInterval; 
            }
        }
        else
        {
            _safeTimer = 0f;
        }
    }

    private bool IsCurrentlySafe()
    {
        Vector2 center = (Vector2)transform.position + offset;
        Vector2 size = new Vector2(boxWidth, boxHeight);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);
        bool foundSolidGround = false;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.CompareTag(unsafeTag)) return false; 
            if (!hit.isTrigger) foundSolidGround = true;
        }

        return foundSolidGround;
    }

    public void RespawnAtSafety()
    {
        if (_isRespawning || _health.IsDead) return;
        
        // Start the delayed respawn
        _respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    // Call this if the player presses Rewind manually to cancel the pending respawn
    public void CancelRespawn()
    {
        if (_isRespawning && _respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
            
            // Re-enable physics if we disabled them
            if (_rb != null) 
            {
                _rb.simulated = true;
                _rb.linearVelocity = Vector2.zero;
            }
            
            _isRespawning = false;
        }
    }

    private IEnumerator RespawnRoutine()
    {
        _isRespawning = true;

        // 1. FREEZE PLAYER (Optional: Keep them in the spikes for a moment)
        if (_rb != null) 
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false; // Freezes them in place
        }

        // 2. WAIT FOR DELAY (Grace Period)
        float timer = 0f;
        while (timer < respawnDelay)
        {
            timer += Time.deltaTime;

            // CHECK: Did the player start rewinding during this delay?
            if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            {
                // Player saved themselves! Cancel everything.
                if (_rb != null) _rb.simulated = true;
                _isRespawning = false;
                yield break; // Exit the coroutine immediately
            }

            yield return null;
        }

        // 3. TELEPORT (If they didn't rewind)
        transform.position = _lastSafePosition;

        yield return new WaitForSeconds(0.1f); // Tiny pause to stabilize landing

        if (_rb != null) _rb.simulated = true;
        _isRespawning = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector2 center = (Vector2)transform.position + offset;
        Vector3 size = new Vector3(boxWidth, boxHeight, 1f);
        Gizmos.DrawWireCube(center, size);
    }
}