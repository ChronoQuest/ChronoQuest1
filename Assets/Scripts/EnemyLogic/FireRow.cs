 using TimeRewind;
using UnityEngine;
public class FireRow : MonoBehaviour, IRewindable
{
    public int damage = 1;
    public float moveSpeed = 3f;
    private Rigidbody2D rb;
    private bool _isRewinding;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private bool fullSizeReached = false;
    public Transform fireVisual;
    public float maxGrowSize = 18f;
    private float currentGrowSize;
    public int bossFacingDirection = 1;
    private Animator animator;
    private bool isEnding = false;
    public float fireHeight = 2f;
    [SerializeField] private float verticalOffset = 1.35f;
    [SerializeField] private float baseHeight;
    private float currentAge = 0f;
    private BoxCollider2D boxCol;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        boxCol = GetComponent<BoxCollider2D>();
        //Destroy after 12 seconds (5 seconds pre rewind, 5 seconds post rewind, 1 sec buffer for each)
        Destroy(gameObject, 12f);
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }     

        rb = GetComponent<Rigidbody2D>();
        animator = fireVisual.GetComponent<Animator>();

        currentGrowSize = 1f;

        // baseY = fireVisual.localPosition.y;
    }

    void Grow(float scale)
    {
        float yPos = baseHeight + verticalOffset;
        float hitboxXPos = ((-scale / 2f) + 0.5f) * bossFacingDirection;
        if (boxCol != null)
        {
            boxCol.size = new Vector2(scale, fireHeight); 
            boxCol.offset = new Vector2(hitboxXPos, yPos);
        }
        float maxVisualWidth = 6f;
        float finalVisualXPos = -10.1f * bossFacingDirection;
        fireVisual.localScale = new Vector3(maxVisualWidth * -bossFacingDirection, fireHeight, 1f);
        fireVisual.localPosition = new Vector3(finalVisualXPos, yPos, 0f);
    }

    void Update()
    {
    }

    void FixedUpdate()
    {
        if (_isRewinding) return;
        currentAge += Time.fixedDeltaTime;
        if(!isEnding && currentGrowSize < maxGrowSize){
            Grow(currentGrowSize);
            currentGrowSize += 0.5f;
        }

        if(!isEnding && currentAge > 6f)
        {
            isEnding = true;
            Debug.Log("Fire row ending triggered"); 

            if (animator != null)
            {
                animator.SetBool("isEnding", true);
            }

            Invoke(nameof(DisableFire), 0.4f);
        }

    }

    void DisableFire()
    {
        gameObject.SetActive(false); 
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
        // Make Rigidbody Kinematic so physics doesn't interfere
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f; 

        // TODO: handle animation during rewind, need animation to go backwards
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
    
        // TODO: handle animation during rewind, need animation to go back forwards
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
        state.SetCustomData("GrowSize", currentGrowSize);
        state.SetCustomData("FullSize", fullSizeReached);
        state.SetCustomData("Age", currentAge);
        state.SetCustomData("IsEnding", isEnding);
        return state;
    }
    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;
        if (state.Timestamp <= currentAge + 0.1f)
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
        currentGrowSize = state.GetCustomData<float>("GrowSize", 1f);
        fullSizeReached = state.GetCustomData<bool>("FullSize", false);
        currentAge = state.GetCustomData<float>("Age", 0f);
        isEnding = state.GetCustomData<bool>("IsEnding", false);
        
        Grow(currentGrowSize);
    }
}

