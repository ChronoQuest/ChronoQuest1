using TimeRewind;
using UnityEngine;
using System.Collections;

public class Fireball : MonoBehaviour, IRewindable
{
    public int damage = 1;
    private Rigidbody2D rb;
    private bool _isRewinding;
    private RewindState _lastAppliedState;
    private Animator anim;
    private Collider2D _collider;
    private float _explosionTimer = 0.5f;
    private bool _isExploding = false;
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip fireExplosionClip;
    [Range(0f, 1f)] public float fireExplosionVolume = 0.5f;
    public AudioClip fallingClip;
    [Range(0f, 1f)] public float fallingVolume = 0.5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }    
        
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        _collider = GetComponent<Collider2D>();
        if (audioSource != null && fallingClip != null)
        {
            audioSource.clip = fallingClip;
            audioSource.loop = true;
            audioSource.volume = fallingVolume;
            audioSource.Play();
        }
    }
    void Update()
    {
        if (_isRewinding) return;

        if (_isExploding)
        {
            _explosionTimer -= Time.deltaTime;
            if (_explosionTimer <= 0f)
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

        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ModifyHealth(-damage);
        }

        if (collision.CompareTag("Trigger"))
        {
            if (anim != null)
            {
                anim.SetTrigger("HitGround"); 
            }

            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic; 
            
            // Move pos, collider and rotation slightly to account for change in sprite
            transform.position += Vector3.up * 0.85f;
            _collider.offset = new Vector2(0f, -0.9f);
            transform.rotation = Quaternion.identity;

            if (audioSource != null) audioSource.Stop();
            if(audioSource != null && fireExplosionClip != null) audioSource.PlayOneShot(fireExplosionClip, fireExplosionVolume);

            _isExploding = true;
            _explosionTimer = 0.5f;
        }
    }
    public void OnStartRewind()
    {
        _isRewinding = true;
        StopAllCoroutines(); 

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        
        if (anim != null) anim.speed = 0f;
    }
    public void OnStopRewind()
    {
        _isRewinding = false;
        bool wasKinematic = _lastAppliedState.GetCustomData<bool>("IsKinematic", false);
        rb.bodyType = wasKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        if (rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.linearVelocity = _lastAppliedState.Velocity;
            rb.angularVelocity = _lastAppliedState.AngularVelocity;
        }
        if (anim != null) anim.speed = 1f;
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
        if (anim != null && anim.GetCurrentAnimatorClipInfo(0).Length > 0)
        {
            var animState = anim.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animState.fullPathHash;
            state.AnimatorNormalizedTime = animState.normalizedTime;
        }
        // Custom state for being active
        state.SetCustomData("IsActive", gameObject.activeSelf);
        state.SetCustomData("IsKinematic", rb != null && rb.bodyType == RigidbodyType2D.Kinematic);
        state.SetCustomData("ColOffset", _collider != null ? _collider.offset : Vector2.zero);
        state.SetCustomData("IsEnding", _isExploding);
        state.SetCustomData("lifetime", _explosionTimer);
        return state;
    }
    public void ApplyState(RewindState state)
    {
        // If the fireball reaches the spawn point, destroy it
        if (transform.position.y > 8.5f)
        {
            Destroy(gameObject);
            return;
        }
            
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;
        if (anim != null && state.AnimatorStateHash != 0)
        {
            anim.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
        }

        bool wasActive = state.GetCustomData<bool>("IsActive", true);
        if (gameObject.activeSelf != wasActive) gameObject.SetActive(wasActive);
        if (_collider != null) _collider.offset = state.GetCustomData<Vector2>("ColOffset", Vector2.zero);
        _isExploding = state.GetCustomData<bool>("IsEnding", false);
        _explosionTimer = state.GetCustomData<float>("lifetime", 0.5f);
    }
}