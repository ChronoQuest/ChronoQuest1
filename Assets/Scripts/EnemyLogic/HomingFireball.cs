using TimeRewind;
using UnityEngine;

public class HomingFireball : MonoBehaviour, IRewindable
{
    public int damage = 1;
    public float speed = 8f;
    public float hoverTime = 0.5f;

    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D _collider;

    private bool _isRewinding;
    private RewindState _lastAppliedState;

    private float age;
    private bool directionLocked = false;
    private Vector2 lockedDirection;

    private bool _isExploding = false;
    private float explosionTimer = 0.5f;
    private GameObject player;
    private SpriteRenderer spriteRenderer;
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip fireExplosionClip;
    [Range(0f, 1f)] public float fireExplosionVolume = 0.5f;
    public AudioClip fallingClip;
    [Range(0f, 1f)] public float fallingVolume = 0.5f;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        Destroy(gameObject, 12f);

        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        _collider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        age = 0f;
    }

    void Update()
    {
        if (_isRewinding) return;

        age += Time.deltaTime;

        // Hover phase
        if (age < hoverTime)
        {
            rb.linearVelocity = Vector2.zero;
        }
        else if (!directionLocked)
        {
            LockDirection();
        }

        // Explosion countdown
        if (_isExploding)
        {
            explosionTimer -= Time.deltaTime;
            if (explosionTimer <= 0f)
            {
                gameObject.SetActive(false);
            }
        }
    }

    void RotateToDirection()
    {
        float tilt = lockedDirection.x * 25f;
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);

        if (lockedDirection.x > 0)
            spriteRenderer.flipX = false;
        else if (lockedDirection.x < 0)
            spriteRenderer.flipX = true;
        UpdateColliderFlip();
    }
    void UpdateColliderFlip()
    {
        if (spriteRenderer.flipX)
            _collider.offset = new Vector2(-Mathf.Abs(_collider.offset.x), _collider.offset.y);
        else
            _collider.offset = new Vector2(Mathf.Abs(_collider.offset.x), _collider.offset.y);
    }

    void LockDirection()
    {

        if (player != null)
        {
            lockedDirection = (player.transform.position - transform.position).normalized;

            rb.linearVelocity = lockedDirection * speed;
            directionLocked = true;
            if (audioSource != null && fallingClip != null)
            {
                audioSource.clip = fallingClip;
                audioSource.loop = true;
                audioSource.volume = fallingVolume;
                audioSource.Play();
            }

            RotateToDirection();
        }
    }

    void OnDestroy()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Unregister(this);
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

            float floorTopY = collision.bounds.max.y;

            transform.position = new Vector3(transform.position.x, floorTopY + 1.35f, transform.position.z);
            _collider.offset = new Vector2(0f, -1f);
            transform.rotation = Quaternion.identity;

            if (audioSource != null) audioSource.Stop();
            if(audioSource != null && fireExplosionClip != null) audioSource.PlayOneShot(fireExplosionClip, fireExplosionVolume);
            _isExploding = true;
            explosionTimer = 0.5f;
        }
    }

    public void OnStartRewind()
    {
        _isRewinding = true;

        if (rb == null) rb = GetComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        
        if (audioSource != null)
        {
            audioSource.Pause();
        }

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
        if (audioSource != null && !_isExploding)
        {
            audioSource.UnPause(); // resume falling if still in air
        }

        if (anim != null) anim.speed = 1f;
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

        if (anim != null && anim.GetCurrentAnimatorClipInfo(0).Length > 0)
        {
            var animState = anim.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animState.fullPathHash;
            state.AnimatorNormalizedTime = animState.normalizedTime;
        }

        state.SetCustomData("IsActive", gameObject.activeSelf);
        state.SetCustomData("Age", age);
        state.SetCustomData("LockedDir", lockedDirection);
        state.SetCustomData("DirLocked", directionLocked);
        state.SetCustomData("IsEnding", _isExploding);
        state.SetCustomData("lifetime", explosionTimer);
        state.SetCustomData("ColOffset", _collider != null ? _collider.offset : Vector2.zero);
        state.SetCustomData("IsKinematic", rb != null && rb.bodyType == RigidbodyType2D.Kinematic);

        return state;
    }

    public void ApplyState(RewindState state)
    {
        if (transform.position.y > 5.7f)
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

        age = state.GetCustomData<float>("Age", 0f);
        lockedDirection = state.GetCustomData<Vector2>("LockedDir", Vector2.zero);
        directionLocked = state.GetCustomData<bool>("DirLocked", false);

        _isExploding = state.GetCustomData<bool>("IsEnding", false);
        explosionTimer = state.GetCustomData<float>("lifetime", 0.5f);

        if (_collider != null)
            _collider.offset = state.GetCustomData<Vector2>("ColOffset", Vector2.zero);

        bool wasActive = state.GetCustomData<bool>("IsActive", true);
        if (gameObject.activeSelf != wasActive)
            gameObject.SetActive(wasActive);
    }
}