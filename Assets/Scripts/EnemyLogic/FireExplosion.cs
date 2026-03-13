using TimeRewind;
using UnityEngine;

public class FireExplosion : MonoBehaviour, IRewindable
{
    public int damage = 1;    
    [Header("Explosion Settings")]
    public float maxSize = 3f;
    public float telegraphDuration = 1.0f; 
    public float growDuration = 0.2f;      
    public float shrinkDuration = 0.3f;    
    public float lingerDuration = 0.2f;

    private float _age = 0f;
    private bool _isRewinding;
    private CircleCollider2D _circleCol;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    void Start()
    {
        Destroy(gameObject, telegraphDuration + growDuration + shrinkDuration + lingerDuration + 5f);

        _circleCol = GetComponent<CircleCollider2D>();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }

        EvaluateState(); 
    }

    void FixedUpdate()
    {
        if (_isRewinding) return;
        
        _age += Time.fixedDeltaTime;
        EvaluateState();
    }

    private void EvaluateState()
    {
        if (_age < telegraphDuration)
        {
            _circleCol.enabled = false; 
            if (_animator != null && !_isRewinding) _animator.speed = 0f;
            
            SetVisualScale(0.5f);
            SetAlpha(Mathf.PingPong(_age * 5f, 1f));
        }
        else
        {
            SetAlpha(1f); 
            SetVisualScale(1f);
            
            if (_animator != null)
            {
                if (!_isRewinding) _animator.speed = 1f;

                AnimatorStateInfo animState = _animator.GetCurrentAnimatorStateInfo(0);
                float animPercent = animState.normalizedTime;

                if (animPercent < 1f)
                {
                    _circleCol.enabled = true; 
                }
                else
                {
                    _circleCol.enabled = false;
                    gameObject.SetActive(false);
                }
            }
        }
    }

    private void SetVisualScale(float size)
    {
        transform.localScale = new Vector3(size, size, 1f);
    }

    private void SetAlpha(float a)
    {
        if (_spriteRenderer != null)
        {
            Color c = _spriteRenderer.color;
            c.a = a;
            _spriteRenderer.color = c;
        }
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

    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Unregister(this);
    }

    public void OnStartRewind()
    {
        _isRewinding = true;
        if (_animator != null) _animator.speed = 0f;
    }

    public void OnStopRewind()
    {
        _isRewinding = false;
        if (_animator != null) _animator.speed = 1f;
    }

    public RewindState CaptureState()
    {
        var state = RewindState.Create(transform.position, transform.rotation, Time.time);
        
        state.SetCustomData("IsActive", gameObject.activeSelf);
        state.SetCustomData("Age", _age);

        if (_animator != null && _animator.layerCount > 0)
        {
            AnimatorStateInfo animState = _animator.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animState.shortNameHash;
            state.AnimatorNormalizedTime = animState.normalizedTime;
        }

        return state;
    }

    public void ApplyState(RewindState state)
    {
        float restoredAge = state.GetCustomData<float>("Age", 0f);
        
        if (restoredAge <= 0.05f)
        {
             Destroy(gameObject);
             return; 
        }
        
        bool wasActive = state.GetCustomData<bool>("IsActive", true);
        if (gameObject.activeSelf != wasActive)
        {
            gameObject.SetActive(wasActive);
        }

        _age = restoredAge;
        EvaluateState();

        if (_animator != null && state.AnimatorStateHash != 0)
        {
            _animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
        }
    }
}