 using TimeRewind;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Animations;
public class Firecolumns : MonoBehaviour, IRewindable
{
    public int damage = 1;
    public float moveSpeed = 3f;
    public float timeToChange = 0.5f;
    private Rigidbody2D rb;
    private bool _isRewinding;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private float nextChangeTime;
    private Vector2 currentVelocity;
    private float startTime;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Destroy after 12 seconds (5 seconds pre rewind, 5 seconds post rewind, 1 sec buffer for each)
        Destroy(gameObject, 12f);
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }     
        rb = GetComponent<Rigidbody2D>();
        startTime = Time.time;
    }
    // Update is called once per frame
    void Update()
    {
    }

    void FixedUpdate()
    {
        if(_isRewinding) return;
        if (Time.time >= nextChangeTime)
        {
            if (Random.value > 0.5f) currentVelocity = new Vector2(-1f, 0f) * moveSpeed;
            else currentVelocity = new Vector2(1f, 0f) * moveSpeed;
            nextChangeTime = Time.time + timeToChange;
        }
        if (transform.position.x < -14f) currentVelocity = new Vector2(1f, 0f) * moveSpeed;
        if (transform.position.x > -5.3f) currentVelocity = new Vector2(-1f, 0f) * moveSpeed;
        if(Time.time > startTime + 5f) gameObject.SetActive(false);
        rb.linearVelocity = currentVelocity;
    }
    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Unregister(this);
    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isRewinding) return;
        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ModifyHealth(-damage);
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
        nextChangeTime = Time.time + timeToChange;
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
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;
        if (state.Timestamp <= startTime + 0.1f)
        {
            Destroy(gameObject);
            return; 
        }
        // Custom state, true is default
        bool wasActive = state.GetCustomData<bool>("IsActive", true);
        // Only change the state if it's different to avoid overhead
        if (gameObject.activeSelf != wasActive)
        {
            gameObject.SetActive(wasActive);
        }
    }
}

