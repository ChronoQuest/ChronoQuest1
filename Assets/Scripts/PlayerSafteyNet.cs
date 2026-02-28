using UnityEngine;
using System.Collections;

public class PlayerSafetyNet : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Which layers count as safe ground? (Don't include Spikes/Traps)")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("How often to update the safe position (seconds)")]
    [SerializeField] private float recordInterval = 0.1f;

    private Vector3 _lastSafePosition;
    private Rigidbody2D _rb;
    private Collider2D _col;
    private float _timer;
    private bool _isRespawning;

    // References to your other scripts
    private PlayerHealth _health;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _health = GetComponent<PlayerHealth>();
        _lastSafePosition = transform.position;
    }

    private void Update()
    {
        // Don't record position if we are currently dead, respawning, or jumping
        if (_health.IsDead || _isRespawning) return;

        // Simple check: Are we on the ground?
        // You can replace IsTouchingLayers with your own character controller's "isGrounded" bool if you prefer
        if (_col.IsTouchingLayers(groundLayer))
        {
            _timer += Time.deltaTime;
            
            // Only update if we've been on the ground for a split second 
            // (prevents saving the position right as you slip off an edge)
            if (_timer >= recordInterval)
            {
                _lastSafePosition = transform.position;
                _timer = 0f;
            }
        }
        else
        {
            _timer = 0f;
        }
    }

    public void RespawnAtSafety()
    {
        if (_isRespawning || _health.IsDead) return;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        _isRespawning = true;

        // 1. Optional: Freeze player momentarily so they don't fight the teleport
        Vector2 savedVelocity = _rb.linearVelocity;
        _rb.linearVelocity = Vector2.zero;
        _rb.simulated = false; // Disable physics briefly

        // 2. Visual Polish: You could add a screen fade black here
        yield return new WaitForSeconds(0.1f); // Tiny pause for impact

        // 3. Teleport
        transform.position = _lastSafePosition;

        // 4. Reset Physics
        _rb.simulated = true;
        _rb.linearVelocity = Vector2.zero; // Kill momentum so they don't slide off again
        
        _isRespawning = false;
    }
}