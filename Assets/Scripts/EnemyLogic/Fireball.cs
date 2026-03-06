using TimeRewind;
using UnityEngine;
using System.Collections;

public class Fireball : MonoBehaviour, IRewindable
{
    public int damage = 1;
    private Rigidbody2D rb;
    private bool _isRewinding;
    private RigidbodyType2D _originalBodyType;
    private RewindState _lastAppliedState;
    private Animator anim;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Destroy after 12 seconds (5 seconds pre rewind, 5 seconds post rewind, 1 sec buffer for each)
        Destroy(gameObject, 12f);
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }    
        
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
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

        if (collision.CompareTag("Ground"))
        {
            if (anim != null)
            {
                anim.SetTrigger("HitGround"); 
            }

            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic; 
            
            transform.position += Vector3.up * 0.7f; 

            StartCoroutine(DestroyAfterImpact()); 
        }
    }

    private IEnumerator DestroyAfterImpact()
    {
        yield return new WaitForSeconds(0.5f);
        gameObject.SetActive(false);
    }

    public void OnStartRewind()
    {
        _isRewinding = true;
        StopAllCoroutines(); 

        // Make Rigidbody Kinematic so physics doesn't interfere
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        if (anim != null) anim.speed = 0f;
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
        if (gameObject.activeSelf != wasActive)
        {
            gameObject.SetActive(wasActive);
        }
        if (rb != null)
        {
            bool wasKinematic = state.GetCustomData<bool>("IsKinematic", false);
            rb.bodyType = wasKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        }
    }
}