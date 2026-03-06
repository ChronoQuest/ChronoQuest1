using TimeRewind;
using UnityEngine;
public class Firecolumns : MonoBehaviour, IRewindable
{
    public int damage = 1;
    public float moveSpeed = 3f;
    public float timeToChange = 1f;
    private Rigidbody2D rb;
    private bool _isRewinding;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private float age; 
    private float changeTimer; 
    private Vector2 currentVelocity;
    public int bossFacingDirection;
    void Start()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }     
        rb = GetComponent<Rigidbody2D>();
        
        // Initialize our safe timers
        age = 0f;
        changeTimer = 0.2f; 
    }
    void Update()
    {
    }

    void FixedUpdate()
    {
        if(_isRewinding) return;
        age += Time.fixedDeltaTime;
        changeTimer -= Time.fixedDeltaTime;
        if (changeTimer <= 0f)
        {
            if (Random.value > 0.5f) currentVelocity = new Vector2(-1f, 0f) * moveSpeed;
            else currentVelocity = new Vector2(1f, 0f) * moveSpeed;
            
            changeTimer = timeToChange; // Reset timer
        }
        float minX = Mathf.Min(-9f * bossFacingDirection, -0.2f * bossFacingDirection);
        float maxX = Mathf.Max(-9f * bossFacingDirection, -0.2f * bossFacingDirection);

        if (transform.position.x < minX) currentVelocity = Vector2.right * moveSpeed;
        if (transform.position.x > maxX) currentVelocity = Vector2.left * moveSpeed;

        // Gameplay Safe Deactivation (5 seconds)
        if(age > 5f && gameObject.activeSelf) 
            gameObject.SetActive(false);
        
        // Gameplay Safe Destruction (12 seconds)
        if(age > 12f)
            Destroy(gameObject);

        rb.MovePosition(rb.position + currentVelocity * Time.fixedDeltaTime);
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
        }
    }
    public void OnStartRewind()
    {
        _isRewinding = true;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
    public void OnStopRewind()
    {
        _isRewinding = false;
        rb.bodyType = _originalBodyType;
        if (_originalBodyType == RigidbodyType2D.Dynamic)
        {
            rb.linearVelocity = _lastAppliedState.Velocity;
            rb.angularVelocity = _lastAppliedState.AngularVelocity;
        }
    }
    public RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            (rb != null) ? rb.linearVelocity : Vector2.zero,
            (rb != null) ? rb.angularVelocity : 0f,
            Time.time
        );
        
        state.SetCustomData("IsActive", gameObject.activeSelf);
        state.SetCustomData("Age", age);
        state.SetCustomData("ChangeTimer", changeTimer);
        return state;
    }
    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;
        age = state.GetCustomData<float>("Age", 0f);
        changeTimer = state.GetCustomData<float>("ChangeTimer", 0f);

        // If we rewind before the object was born, destroy it
        if (age <= 0.1f)
        {
            Destroy(gameObject);
            return; 
        }
        bool wasActive = state.GetCustomData<bool>("IsActive", true);
        if (gameObject.activeSelf != wasActive)
        {
            gameObject.SetActive(wasActive);
        }
    }
}