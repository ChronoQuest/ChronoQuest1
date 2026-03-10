using UnityEngine;
using TimeRewind;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour, IRewindable
{
    [Header("Movement Settings")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float speed = 3f;
    [SerializeField] private float waitTimeAtPoint = 1f;

    private Rigidbody2D _rb;
    private int _targetIndex = 0;
    private float _waitTimer;
    private bool _isRewinding;
    
    // We calculate this so the player can read it
    public Vector2 CurrentVelocity { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
            _targetIndex = 1;
        }
    }

    private void OnEnable() => TimeRewindManager.Instance?.Register(this);
    private void OnDisable() => TimeRewindManager.Instance?.Unregister(this);

    private void FixedUpdate()
    {
        if (_isRewinding || waypoints.Length == 0) 
        {
            CurrentVelocity = Vector2.zero;
            return;
        }

        Vector2 target = waypoints[_targetIndex].position;
        Vector2 current = _rb.position;

        // 1. Calculate the move
        Vector2 newPos = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);
        
        // 2. Calculate the velocity (Distance / Time)
        // We set this property so the Player script can read "platform.CurrentVelocity"
        CurrentVelocity = (newPos - current) / Time.fixedDeltaTime;

        // 3. Move the physics body
        _rb.MovePosition(newPos);

        // 4. Waypoint Logic
        if (Vector2.Distance(current, target) < 0.05f)
        {
            CurrentVelocity = Vector2.zero; // Stop reporting velocity while waiting
            _waitTimer += Time.fixedDeltaTime;
            if (_waitTimer >= waitTimeAtPoint)
            {
                NextWaypoint();
            }
        }
    }

    private void NextWaypoint()
    {
        _waitTimer = 0;
        _targetIndex = (_targetIndex + 1) % waypoints.Length;
    }

    public void OnStartRewind()
    {
        _isRewinding = true;
        CurrentVelocity = Vector2.zero;
    }

    public void OnStopRewind() 
    {
        _isRewinding = false;
        // Logic to find nearest waypoint...
        if (waypoints.Length > 0)
        {
            float closestDist = float.MaxValue;
            for (int i = 0; i < waypoints.Length; i++)
            {
                float d = Vector2.Distance(_rb.position, waypoints[i].position);
                if (d < closestDist)
                {
                    closestDist = d;
                    _targetIndex = i;
                }
            }
        }
    }

    public RewindState CaptureState() => RewindState.Create(transform.position, transform.rotation, Time.time);
    
    public void ApplyState(RewindState state)
    {
        _rb.MovePosition(state.Position);
        _rb.MoveRotation(state.Rotation);
    }
}