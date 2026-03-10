using UnityEngine;
using System.Collections;
using TimeRewind; // Uses your namespace

[RequireComponent(typeof(Rigidbody2D))]
public class MovingFallingPlatform : MonoBehaviour, IRewindable
{
    [Header("Movement Settings")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitTimeAtPoint = 1f;

    [Header("Fall Settings")]
    [SerializeField] private float fallDelay = 0.5f;
    [SerializeField] private float shakeAmount = 0.05f;
    [SerializeField] private float gravityScale = 2.5f;
    [SerializeField] private float respawnTime = 3.0f;

    [Header("References")]
    [SerializeField] private Collider2D platformCollider;

    // State Variables
    private Rigidbody2D _rb;
    private int _targetIndex = 0;
    private float _waitTimer;
    private bool _isFalling = false;
    private bool _isRewinding = false;
    private Vector3 _initialPosition;
    
    // Parenting
    private Transform _playerTransform;
    private Coroutine _fallRoutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        
        // Ensure Physics settings are correct for moving platforms
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate; // CRITICAL for smooth riding
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        
        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
            _targetIndex = 1;
        }
        
        _initialPosition = transform.position;

        if (platformCollider == null) 
            platformCollider = GetComponent<Collider2D>();
    }

    private void OnEnable() => TimeRewindManager.Instance?.Register(this);
    private void OnDisable() => TimeRewindManager.Instance?.Unregister(this);

    private void FixedUpdate()
    {
        // 1. REWIND CHECK: Do nothing if rewinding
        if (_isRewinding) return;

        // 2. FALL CHECK: If falling, let Physics handle it
        if (_isFalling) return;

        // 3. MOVEMENT LOGIC (Only runs if NOT falling)
        MovePlatform();
    }

    private void MovePlatform()
    {
        if (waypoints.Length == 0) return;

        Vector2 target = waypoints[_targetIndex].position;
        Vector2 current = _rb.position;

        // Calculate the next position using MoveTowards
        Vector2 nextPos = Vector2.MoveTowards(current, target, moveSpeed * Time.fixedDeltaTime);

        // USE PHYSICS MOVEMENT (This keeps the player attached)
        _rb.MovePosition(nextPos);

        // Check if we reached the target (small threshold)
        if (Vector2.Distance(current, target) < 0.05f)
        {
            _waitTimer += Time.fixedDeltaTime;
            if (_waitTimer >= waitTimeAtPoint)
            {
                _waitTimer = 0;
                _targetIndex = (_targetIndex + 1) % waypoints.Length;
            }
        }
    }

    // ====================================================
    // TRIGGER LOGIC (FALLING)
    // ====================================================

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Check if player is on top (using contact normal)
            if (collision.GetContact(0).normal.y < -0.5f)
            {
                // 1. Parent the player so they move with us (Extra safety)
                _playerTransform = collision.transform;
                _playerTransform.SetParent(transform);

                // 2. Start the fall countdown
                if (!_isFalling && !_isRewinding)
                {
                    _fallRoutine = StartCoroutine(FallSequence());
                }
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (_playerTransform == collision.transform)
            {
                _playerTransform.SetParent(null);
                _playerTransform = null;
            }
        }
    }

    private IEnumerator FallSequence()
    {
        // PHASE 1: SHAKE
        float timer = 0f;
        
        // We need to store where we are relative to the path so shake doesn't drift us
        Vector3 currentPathPos = transform.position;

        while (timer < fallDelay)
        {
            if (_isRewinding) yield break;

            // Visual Shake
            float x = Random.Range(-1f, 1f) * shakeAmount;
            
            // Note: Since FixedUpdate is running MovePosition, we just modify the visual transform slightly
            // Ideally, shake the child "Visuals" object, but this works for simple shakes
            transform.position = currentPathPos + new Vector3(x, 0, 0);
            
            // Keep updating base position in case platform is still moving along path during shake
            currentPathPos = transform.position; 
            
            timer += Time.deltaTime;
            yield return null;
        }

        // PHASE 2: FALL
        _isFalling = true; // Stops MovePlatform()
        
        // Unparent player immediately
        if (_playerTransform != null)
        {
            _playerTransform.SetParent(null);
            _playerTransform = null;
        }

        // Enable Gravity
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = gravityScale;
        
        // Disable Collider (Ghost Mode) so it falls through ground
        if (platformCollider != null) platformCollider.enabled = false;

        // PHASE 3: RESPAWN
        yield return new WaitForSeconds(respawnTime);
        if (!_isRewinding) ResetPlatform();
    }

    private void ResetPlatform()
    {
        _isFalling = false;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        
        if (platformCollider != null) platformCollider.enabled = true;

        // Snap to the current target waypoint to resume path
        if (waypoints.Length > 0)
        {
            transform.position = waypoints[_targetIndex].position;
        }
    }

    // ====================================================
    // REWIND LOGIC
    // ====================================================

    public void OnStartRewind()
    {
        _isRewinding = true;
        if (_fallRoutine != null) StopCoroutine(_fallRoutine);

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;

        if (_playerTransform != null)
        {
            _playerTransform.SetParent(null);
            _playerTransform = null;
        }
    }

    public void OnStopRewind()
    {
        _isRewinding = false;
    }

    public RewindState CaptureState()
    {
        var state = RewindState.Create(transform.position, transform.rotation, Time.time);
        state.SetCustomData("IsFalling", _isFalling);
        state.SetCustomData("WaypointIndex", _targetIndex);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        // Use MovePosition here too for consistency, though direct set is usually fine for rewind
        _rb.MovePosition(state.Position);
        _rb.MoveRotation(state.Rotation);

        bool wasFalling = state.GetCustomData<bool>("IsFalling", false);
        _targetIndex = state.GetCustomData<int>("WaypointIndex", 0);

        if (wasFalling)
        {
            _isFalling = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            if (platformCollider != null) platformCollider.enabled = false;
        }
        else
        {
            _isFalling = false;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            if (platformCollider != null) platformCollider.enabled = true;
        }
    }

    // Debug lines
    private void OnDrawGizmos()
    {
        if (waypoints != null && waypoints.Length > 0)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                    Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);
            }
        }
    }
}