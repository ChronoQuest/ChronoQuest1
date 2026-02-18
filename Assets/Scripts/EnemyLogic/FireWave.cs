using TimeRewind;
using UnityEngine;
public class FireWave : MonoBehaviour, IRewindable
{
    public int damage = 1;
    private Rigidbody2D rb;
    private bool _isRewinding;
    private float startTime;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private Vector2 currentVelocity;
    public float moveSpeed = 2f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startTime = Time.time;
        // Destroy after 12 seconds (5 seconds pre rewind, 5 seconds post rewind, 1 sec buffer for each)
        Destroy(gameObject, 12f);
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }     
        rb = GetComponent<Rigidbody2D>();
    }
    // Update is called once per frame
    void Update()
    {

    }

    void FixedUpdate()
    {
        if(_isRewinding) return;
        currentVelocity = new Vector2(-5f, 0f) * moveSpeed;
        rb.MovePosition(rb.position + currentVelocity * Time.fixedDeltaTime);
        if(transform.position.x < -15f) gameObject.SetActive(false);
    }
    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Unregister(this);
    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isRewinding) return;
        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ModifyHealth(-damage);
            //gameObject.SetActive(false);
        }
    }
    public void OnStartRewind()
    {
        _isRewinding = true;
        // Make Rigidbody Kinematic so physics doesn't interfere
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
    public void OnStopRewind()
    {
        _isRewinding = false;
        // Restore physics
        rb.bodyType = _originalBodyType;
        if (_originalBodyType == RigidbodyType2D.Dynamic)
        {
            rb.linearVelocity = _lastAppliedState.Velocity;
            rb.angularVelocity = _lastAppliedState.AngularVelocity;
        }
    }
    public RewindState CaptureState()
    {
        // Create physics state
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            (rb != null) ? rb.linearVelocity : Vector2.zero,
            (rb != null) ? rb.angularVelocity : 0f,
            Time.time
        );
        // Custom state for being active
        state.SetCustomData("IsActive", gameObject.activeSelf);
        return state;
    }
    public void ApplyState(RewindState state)
    {
        if (state.Timestamp <= startTime + 0.1f)
        {
            Destroy(gameObject);
            return; 
        }
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;
        // Custom state, true is default
        bool wasActive = state.GetCustomData<bool>("IsActive", true);
        // Only change the state if it's different to avoid overhead
        if (gameObject.activeSelf != wasActive)
        {
            gameObject.SetActive(wasActive);
        }
    }
}

