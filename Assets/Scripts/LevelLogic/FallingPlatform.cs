using UnityEngine;
using System.Collections;
using TimeRewind;

[RequireComponent(typeof(Rigidbody2D))]
public class FallingPlatform : MonoBehaviour, IRewindable
{
    [Header("Settings")]
    [SerializeField] private float fallDelay = 0.5f;
    [SerializeField] private float shakeAmount = 0.05f;
    [SerializeField] private float respawnTime = 3.0f; 

    [Header("References")]
    [Tooltip("Assign the Tilemap Collider or Box Collider here")]
    [SerializeField] private Collider2D platformCollider;

    private Rigidbody2D _rb;
    private Vector3 _startPos;
    private bool _isFalling = false;
    private bool _isRewinding = false;
    private Coroutine _fallRoutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        
        // Ensure it floats at start
        _rb.bodyType = RigidbodyType2D.Kinematic; 
        _rb.linearVelocity = Vector2.zero;
        
        // Auto-find collider if you forgot to drag it in
        if (platformCollider == null) 
            platformCollider = GetComponent<Collider2D>();

        _startPos = transform.position;
    }

    private void OnEnable() => TimeRewindManager.Instance?.Register(this);
    private void OnDisable() => TimeRewindManager.Instance?.Unregister(this);

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Only trigger if Player stands on top
        if (!_isFalling && collision.gameObject.CompareTag("Player"))
        {
            // Check if player is actually above (normal pointing down)
            if (collision.GetContact(0).normal.y < -0.5f)
            {
                if (!_isRewinding) 
                    _fallRoutine = StartCoroutine(FallSequence());
            }
        }
    }

    private IEnumerator FallSequence()
    {
        _isFalling = true;
        float timer = 0f;

        // 1. Shake Phase
        while (timer < fallDelay)
        {
            if (_isRewinding) yield break; 

            float x = Random.Range(-1f, 1f) * shakeAmount;
            transform.position = _startPos + new Vector3(x, 0, 0); 
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 2. Fall Phase
        transform.position = _startPos; // Snap back to center
        
        // Physics Fall
        _rb.bodyType = RigidbodyType2D.Dynamic; 
        _rb.gravityScale = 2.5f; // Fall slightly faster than player for dramatic effect
        
        // Wait 0.5 seconds while falling, so the player rides it down briefly
        float fallTimer = 0f;
        while (fallTimer < 0.5f)
        {
             if (_isRewinding) yield break;
             fallTimer += Time.deltaTime;
             yield return null;
        }

        // GHOST MODE: Disable collider so it passes through floor/spikes
        if (platformCollider != null) 
            platformCollider.enabled = false;

        // 3. Respawn Timer
        yield return new WaitForSeconds(respawnTime);
        if (!_isRewinding) ResetPlatform();
    }

    private void ResetPlatform()
    {
        _isFalling = false;
        
        // Reset Physics
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        
        // Reset Transform
        transform.position = _startPos;
        transform.rotation = Quaternion.identity;
        
        // Re-enable Collider so player can stand on it again
        if (platformCollider != null) 
            platformCollider.enabled = true;
    }

    // ====================================================
    // REWIND IMPLEMENTATION
    // ====================================================

    public void OnStartRewind()
    {
        _isRewinding = true;
        if (_fallRoutine != null) StopCoroutine(_fallRoutine);
        
        // Stop physics immediately so we don't fight the rewind position
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
    }

    public void OnStopRewind()
    {
        _isRewinding = false;

        // Now that _isFalling is correctly updated by ApplyState, this check works!
        if (_isFalling)
        {
            // If we are basically at the start position, we were likely Shaking.
            if (Vector3.Distance(transform.position, _startPos) < 0.1f)
            {
                // Restart the full sequence (Shake -> Fall)
                if (_fallRoutine != null) StopCoroutine(_fallRoutine);
                _fallRoutine = StartCoroutine(FallSequence());
            }
            else
            {
                // We are deep in the pit. We were Falling.
                // Resume physics immediately so it doesn't freeze in mid-air.
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.gravityScale = 2.5f; 
                
                // Ensure collider is OFF so we don't get stuck in the floor
                if (platformCollider != null) platformCollider.enabled = false;

                // Start a "Rescue Timer" to ensure it respawns eventually
                if (_fallRoutine != null) StopCoroutine(_fallRoutine);
                _fallRoutine = StartCoroutine(ResumeFall());
            }
        }
        else
        {
            // We rewound to before the player touched it. 
            // Ensure everything is reset to Idle/Safe state.
            ResetPlatform();
        }
    }
    private IEnumerator ResumeFall()
    {
        // Wait for the remainder of the respawn time
        yield return new WaitForSeconds(respawnTime);
        if (!_isRewinding) ResetPlatform();
    }
    public RewindState CaptureState()
    {
        var state = RewindState.Create(transform.position, transform.rotation, Time.time);
        state.SetCustomData("IsFalling", _isFalling);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;

        // 1. UPDATE THE CLASS VARIABLE (Crucial Fix)
        // We must update this every frame, whether true or false.
        _isFalling = state.GetCustomData<bool>("IsFalling", false);

        // 2. Force Kinematic DURING rewind
        // Never set Dynamic here. We don't want gravity fighting the rewind manager.
        // We only turn Dynamic back on in OnStopRewind.
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;

  
    }
}