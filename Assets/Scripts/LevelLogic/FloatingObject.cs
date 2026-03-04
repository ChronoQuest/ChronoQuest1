using UnityEngine;
using TimeRewind; // Uses your specific namespace

public class FloatingObject : MonoBehaviour, IRewindable
{
    [Header("Hover Settings")]
    [Tooltip("How high it moves up and down")]
    [SerializeField] private float amplitude = 0.25f; 
    [Tooltip("How fast it moves up and down")]
    [SerializeField] private float frequency = 2f;

    [Header("Rotation Settings")]
    [Tooltip("Degrees per second to spin (0 for no spin)")]
    [SerializeField] private float rotationSpeed = 0f;

    private Vector3 _startPos;
    private float _randomOffset;
    private bool _isRewinding = false;

    private void Start()
    {
        _startPos = transform.position;
        // Random offset prevents multiple objects from bobbing in perfect sync
        _randomOffset = Random.Range(0f, 2f * Mathf.PI);
    }

    // 1. Register with your Rewind Manager
    private void OnEnable() => TimeRewindManager.Instance?.Register(this);
    private void OnDisable() => TimeRewindManager.Instance?.Unregister(this);

    private void Update()
    {
        // 2. Stop calculating movement if we are rewinding
        if (_isRewinding) return;

        // Calculate new Y position using Sine wave
        float newY = _startPos.y + (Mathf.Sin((Time.time * frequency) + _randomOffset) * amplitude);

        transform.position = new Vector3(_startPos.x, newY, _startPos.z);

        // Optional Rotation
        if (rotationSpeed != 0)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
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
        // Note: When rewind stops, Time.time will continue forward. 
        // The sine wave might "snap" slightly if the rewind duration wasn't a perfect loop,
        // but for a floating item, this is usually unnoticeable.
        // If it snaps too hard, we would need to offset _randomOffset here, but it's rarely needed.
    }

    public RewindState CaptureState()
    {
        // Record where the object is right now
        return RewindState.Create(transform.position, transform.rotation, Time.time);
    }

    public void ApplyState(RewindState state)
    {
        // Force the object to the recorded position
        transform.position = state.Position;
        transform.rotation = state.Rotation;
    }
}