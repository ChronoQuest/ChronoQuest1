using UnityEngine;
using System.Collections;

public class PlayerSafetyNet : MonoBehaviour
{
    [Header("Safety Settings")]
    [Tooltip("Select your Ground Layer here.")]
    [SerializeField] private LayerMask groundLayer;
    
    [Tooltip("The tag you put on your Trap objects.")]
    [SerializeField] private string unsafeTag = "Trap"; 

    [Tooltip("How long you must be on safe ground before it saves (Lower is more responsive)")]
    [SerializeField] private float recordInterval = 0.05f; 

    [Header("Detection Box")]
    [Tooltip("Width of the foot check. Should match your player collider width.")]
    [SerializeField] private float boxWidth = 0.5f;
    [Tooltip("Height of the foot check.")]
    [SerializeField] private float boxHeight = 0.2f;
    [Tooltip("Offset from the player center to the feet.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, -0.6f);

    private Vector3 _lastSafePosition;
    private float _safeTimer;
    
    // References
    private Rigidbody2D _rb;
    private PlayerHealth _health;
    private bool _isRespawning;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _health = GetComponent<PlayerHealth>();
        _lastSafePosition = transform.position;
    }

    private void Update()
    {
        if (_health.IsDead || _isRespawning) return;

        // Visual debug to help you adjust the box in the editor
        // (Only works if Gizmos are enabled)
    }

    private void FixedUpdate()
    {
        // We use FixedUpdate for physics checks to be more consistent
        if (_health.IsDead || _isRespawning) return;

        if (IsCurrentlySafe())
        {
            _safeTimer += Time.fixedDeltaTime;
            
            if (_safeTimer >= recordInterval)
            {
                // Save the position slightly above the ground to prevent getting stuck in floor
                _lastSafePosition = transform.position + Vector3.up * 0.1f;
                // Don't reset timer to 0, just cap it, so we stay "safe" continuously
                _safeTimer = recordInterval; 
            }
        }
        else
        {
            // We are in the air or on a trap -> Reset the confidence timer
            _safeTimer = 0f;
        }
    }

    private bool IsCurrentlySafe()
    {
        Vector2 center = (Vector2)transform.position + offset;
        Vector2 size = new Vector2(boxWidth, boxHeight);

        // 1. Get ALL colliders under the feet, not just the first one
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);

        bool foundSolidGround = false;

        foreach (Collider2D hit in hits)
        {
            // Ignore our own collider (if the player is on the Ground layer)
            if (hit.gameObject == gameObject) continue;

            // 2. IMMEDIATE FAIL: If ANY object under us is a trap, we are unsafe.
            if (hit.CompareTag(unsafeTag))
            {
                return false; 
            }

            // 3. If it's not a trigger (it's solid), we found potential ground
            if (!hit.isTrigger)
            {
                foundSolidGround = true;
            }
        }

        // We are safe ONLY if we found solid ground AND didn't find any traps
        return foundSolidGround;
    }

    public void RespawnAtSafety()
    {
        if (_isRespawning) return;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        _isRespawning = true;

        // Optional: Stop velocity
        if (_rb != null) 
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false; // Prevent physics fighting the teleport
        }

        // Teleport
        transform.position = _lastSafePosition;

        yield return new WaitForSeconds(0.1f); // Brief pause

        if (_rb != null) _rb.simulated = true;
        _isRespawning = false;
    }

    // DRAW THE BOX IN THE EDITOR SO YOU CAN SEE IT
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector2 center = (Vector2)transform.position + offset;
        Vector3 size = new Vector3(boxWidth, boxHeight, 1f);
        Gizmos.DrawWireCube(center, size);
    }
}