using TimeRewind;
using UnityEngine;

public class FloorFireRow : MonoBehaviour, IRewindable
{
    public int damage = 1;
    public float moveSpeed = 3f;
    private Rigidbody2D rb;
    private bool _isRewinding;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private bool fullSizeReached = false;
    public Transform fireVisual;
    private float _age; 
    public float maxGrowSize = 25.5f;
    private float currentGrowSize;
    public int bossFacingDirection = 1;
    private Animator animator;
    private bool isEnding = false;
    private BoxCollider2D boxCol;

    void Start()
    {
        Destroy(gameObject, 12f);
        rb = GetComponent<Rigidbody2D>();
        animator = fireVisual.GetComponent<Animator>();
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }    
        
        _age = 0f;
        currentGrowSize = 1f;
        boxCol = GetComponent<BoxCollider2D>();
    }

    void Grow(float scale)
    {
        float targetXPos = ((-scale / 2f) + 0.5f) * bossFacingDirection;

        // 1. Scale and move the Hitbox
        if (boxCol != null)
        {
            boxCol.size = new Vector2(scale, 1f); 
            boxCol.offset = new Vector2(targetXPos, 0f); 
        }

        fireVisual.localScale = new Vector3(scale * 0.85f, 20f, 1f);
        fireVisual.localPosition = new Vector3(targetXPos, 3.2f, 0f);
    }

    void FixedUpdate()
    {
        if (_isRewinding) return;
        _age += Time.fixedDeltaTime;

        if (!isEnding && currentGrowSize < maxGrowSize)
        {
            currentGrowSize += 0.25f;
            Grow(currentGrowSize);
        } 
        else if (!isEnding && currentGrowSize >= maxGrowSize)
        {
            fullSizeReached = true;
        }

        if (!isEnding && _age > 6f)
        {
            isEnding = true;
        }

        if (isEnding)
        {
            if (currentGrowSize > 1f)
            {
                currentGrowSize -= 0.5f;
                Grow(currentGrowSize);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
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
        if (animator != null) animator.speed = 0f;
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
        
        if (animator != null) animator.speed = 1f;
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
        if (animator != null && animator.GetCurrentAnimatorClipInfo(0).Length > 0)
        {
            var animState = animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animState.fullPathHash;
            state.AnimatorNormalizedTime = animState.normalizedTime;
        }
        state.SetCustomData("IsActive", gameObject.activeSelf);
        state.SetCustomData("GrowSize", currentGrowSize);
        state.SetCustomData("FullSize", fullSizeReached);
        state.SetCustomData("Age", _age);
        state.SetCustomData("IsEnding", isEnding);        
        return state;
    }
    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;
        float restoredAge = state.GetCustomData<float>("Age", 0f);
        if (restoredAge <= 0.1f)
        {
             Destroy(gameObject);
             return; 
        }
        bool wasActive = state.GetCustomData<bool>("IsActive", true);
        if (gameObject.activeSelf != wasActive)
        {
            gameObject.SetActive(wasActive);
        }
        if (animator != null && state.AnimatorStateHash != 0)
        {
            animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
        }
        currentGrowSize = state.GetCustomData<float>("GrowSize", 1f);
        fullSizeReached = state.GetCustomData<bool>("FullSize", false);
        _age = restoredAge;
        
        isEnding = state.GetCustomData<bool>("IsEnding", false);
        
        if (animator != null)
        {
            animator.SetBool("isEnding", isEnding);
        }
        
        Grow(currentGrowSize);
    }
}