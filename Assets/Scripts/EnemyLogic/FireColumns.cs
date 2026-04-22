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
    public Transform fireVisualLeft;
    public Transform fireVisualRight;
    private Animator animatorLeft;
    private Animator animatorRight;
    private bool isEnding = false;
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip fireLoopClip;
    [Range(0f, 1f)] public float fireVolume = 0.6f;
    void Start()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Register(this);
        }     
        rb = GetComponent<Rigidbody2D>();
        animatorLeft = fireVisualLeft.GetComponent<Animator>();
        animatorRight = fireVisualRight.GetComponent<Animator>();
        if (audioSource != null && fireLoopClip != null)
        {
            audioSource.clip = fireLoopClip;
            audioSource.loop = true;
            audioSource.volume = fireVolume;
            audioSource.Play();
        }
        
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

        if(age > 4.6f && gameObject.activeSelf &&!isEnding){
            isEnding = true;
            if (animatorLeft != null && animatorRight != null)
            {
                animatorLeft.SetBool("isEnding", true);
                animatorRight.SetBool("isEnding", true);
            }
        }
        if (age > 5f)
        {
            if (audioSource != null) audioSource.Stop();
            gameObject.SetActive(false);
        }

        
        // Gameplay Safe Destruction (12 seconds)
        if(age > 12f) Destroy(gameObject);

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
        if (audioSource != null) audioSource.Pause();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
    public void OnStopRewind()
    {
        _isRewinding = false;
        if (audioSource != null) audioSource.UnPause();
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
        state.SetCustomData("IsEnding", isEnding);
        return state;
    }
    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        _lastAppliedState = state;
        age = state.GetCustomData<float>("Age", 0f);
        changeTimer = state.GetCustomData<float>("ChangeTimer", 0f);
        isEnding = state.GetCustomData<bool>("IsEnding", false);

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
