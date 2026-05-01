using UnityEngine;
using TimeRewind;
using System.Collections;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(HitFlash))]

public class EnemyBase : MonoBehaviour, IDamageable, IKnockbackable, IRewindable
{
    [Header("Health")]
    public int health = 3;
    [HideInInspector] public int startHealth;

    private EnemyHealthBar _healthBar;

    [Header("Stun Settings")]
    public bool stunOnLand = false; // Toggle this ON in the Inspector for land enemies
    protected bool isLaunched;

    [Header("Knockback")]
    public float knockbackResistance = 1f;  // higher = less knockback
    public float knockbackUpMultiplier = 0.8f;
    public System.Action OnDeath;

    [Header("Death Settings")]
    public float deathAnimationDuration = 0.6f;

    [Header("Audio")]
    public AudioClip[] damageClips;
    [Range(0f, 1f)] public float damageVolume = 0.5f;
    public AudioClip deathClip;
    [Range(0f, 1f)] public float deathVolume = 0.5f;
    public AudioClip reviveClip;
    [Range(0f, 1f)] public float reviveVolume = 0.5f;
    public AudioClip[] idleClips;
    [Range(0f, 1f)] public float idleVolume = 0.5f;
    public Vector2 idleIntervalRange = new Vector2(3f, 5f);
    private Coroutine idleRoutine;

    protected AudioSource audioSource;

    protected Rigidbody2D rb;
    protected SpriteRenderer sprite;
    protected HitFlash flash;

    protected RigidbodyType2D originalBodyType;
    protected bool isRewinding;
    protected bool wasDead;
    protected bool justBecameAlive; // true for one ApplyState frame when transitioning dead→alive

    public bool IsDead => health <= 0;
    protected bool isStunned;
    protected float stunTimer;
    public bool GetIsStunned() => isStunned;
    protected GameObject foresightGlow;
    public GameObject ForesightGlow => foresightGlow;

    /// <summary>When true, dynamic difficulty will not scale this enemy's HP.</summary>
    public bool immuneToDifficultyScaling;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        flash = GetComponent<HitFlash>();

        if (!immuneToDifficultyScaling)
        {
            float hpMult = 1f;
            if (DynamicDifficultyManager.Instance != null)
            {
                hpMult = DynamicDifficultyManager.Instance.GetEnemyHpMultiplierForScene(SceneManager.GetActiveScene().name);
            }

            if (hpMult != 1f)
            {
                health = Mathf.Max(1, Mathf.RoundToInt(health * hpMult));
            }
        }

        GameObject hitAudioObj = new GameObject("HitAudio");
        hitAudioObj.transform.SetParent(transform);
        hitAudioObj.transform.localPosition = Vector3.zero;

        audioSource = hitAudioObj.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 3f;
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        startHealth = health;
        originalBodyType = rb.bodyType; // captured once — represents alive body type
        foresightGlow = transform.Find("Lit")?.gameObject;
        _healthBar = GetComponent<EnemyHealthBar>();
    }
    public virtual void Update()
    {
        if (isRewinding || wasDead) return;

        if (stunTimer > 0f)
        {
            isStunned = true;
            stunTimer -= Time.deltaTime;
            if (sprite != null && (flash == null || !flash.IsFlashing))
            {
                sprite.color = new Color(0.7f, 0.7f, 0.7f);
            }

            if (stunTimer <= 0f)
            {
                isStunned = false;
                stunTimer = 0f;
                if (sprite != null) sprite.color = Color.white;
            }
        }
    }
    protected virtual void OnEnable()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Register(this);
        if (idleClips != null && idleClips.Length > 0)
        {
            idleRoutine = StartCoroutine(IdleSoundLoop());
        }
    }

    protected virtual void OnDisable()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Unregister(this);
        if (idleRoutine != null)
    {
        StopCoroutine(idleRoutine);
        idleRoutine = null;
    }
        
    }
    
    // ================= DAMAGE =================
    public virtual void TakeDamage(int amount)
    {
        if (wasDead) return;
        health -= amount;
        flash?.Flash();

        if (damageClips != null && damageClips.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, damageClips.Length);
            audioSource.PlayOneShot(damageClips[randomIndex], damageVolume);
        }

        if (!isStunned)
        {
            stunTimer = 0.2f;
        }
        
        if (health <= 0)
        {
            Die();
        }
    }

    // ================= KNOCKBACK =================
    public virtual void ApplyKnockback(Vector2 force)
    {
        if (isRewinding || wasDead) return;

        //force /= knockbackResistance;
        rb.linearVelocity = Vector2.zero;

        //Vector2 finalForce = new Vector2(force.x, Mathf.Max(Mathf.Abs(force.x), Mathf.Abs(force.y)) * knockbackUpMultiplier);
        //rb.AddForce(finalForce, ForceMode2D.Impulse);
        //StartCoroutine(HitStunRoutine(0.25f));

        float horizontalForce = (force.x / knockbackResistance) * 0.6f; 
        float verticalForce = Mathf.Max(Mathf.Abs(force.x), Mathf.Abs(force.y)) * knockbackUpMultiplier * 0.7f;

        rb.AddForce(new Vector2(horizontalForce, verticalForce), ForceMode2D.Impulse);

        if (stunOnLand){
            isLaunched = true;
        }
        else{
            stunTimer = 0.25f;
        }   
    }

    // ================= DEATH =================
    public virtual void Die()
    {
        wasDead = true;
        DataCollectionService.Instance?.RecordEnemyKill();
        DeathSound();
        rb.bodyType = RigidbodyType2D.Kinematic; // freeze in place — prevents falling through floor
        rb.linearVelocity = Vector2.zero;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (foresightGlow != null) foresightGlow.SetActive(false);
        ScoreManager.Instance.AddPoints(50); 
        OnDeath?.Invoke();
        StartCoroutine(DeathRoutine());
        // Do not Destroy - stay registered so rewind can restore us
    }
    public void DeathSound()
    {
        if (deathClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathClip, deathVolume);
        }
        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }
    }
    public virtual IEnumerator DeathRoutine()
    {
        // Wait for the specific enemy's animation to finish
        yield return new WaitForSeconds(deathAnimationDuration);
        
        // Hide the sprite instead of Destroying (so it can be rewound)
        if (sprite != null) sprite.enabled = false;
    }
    // ================= REVIVE =================
    public virtual void Revive()
    {
        StopAllCoroutines(); // stop any pending DeathRoutine that would re-hide the sprite
        wasDead = false;
        if (reviveClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(reviveClip, reviveVolume);
        }   
        health = startHealth;
        rb.bodyType = originalBodyType;
        if (sprite != null) sprite.enabled = true;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
        if (idleRoutine == null && idleClips != null && idleClips.Length > 0)
        {
            idleRoutine = StartCoroutine(IdleSoundLoop());
        }

        _healthBar?.SyncImmediate();
    }

    // ================= REWIND =================
    public virtual void OnStartRewind()
    {
        isRewinding = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public virtual void OnStopRewind()
    {
        isRewinding = false;
        // Restore alive body type if living, keep frozen if still dead
        rb.bodyType = wasDead ? RigidbodyType2D.Kinematic : originalBodyType;

        // If rewind stopped during a death animation, restart the cleanup coroutine
        // so the sprite gets hidden (StopAllCoroutines in OnStartRewind killed it)
        if (wasDead)
            StartCoroutine(DeathRoutine());
    }


    public virtual RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            rb.linearVelocity,
            rb.angularVelocity,
            Time.time
        );

        state.Health = health;
        state.SetCustomData("flipX", sprite.flipX);
        state.SetCustomData("lifetime", stunTimer);
        return state;
    }

    public virtual void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        rb.linearVelocity = state.Velocity;

        health = state.Health;
        sprite.flipX = state.GetCustomData<bool>("flipX");
        stunTimer = state.GetCustomData<float>("lifetime");
        isStunned = stunTimer > 0f;
        if (isStunned && sprite != null) 
        {
            sprite.color = new Color(0.7f, 0.7f, 0.7f);
        }
        else if (!isStunned && sprite != null)
        {
            sprite.color = Color.white;
        }

        justBecameAlive = false;

        if (state.Health > 0 && wasDead)
        {
            // dead → alive transition
            justBecameAlive = true;
            wasDead = false;
            rb.bodyType = originalBodyType;
            if (sprite != null) sprite.enabled = true;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }
        else if (state.Health <= 0 && !wasDead)
        {
            // alive → dead transition (rewinding past the death event)
            wasDead = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }
    private IEnumerator IdleSoundLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(idleIntervalRange.x, idleIntervalRange.y);
            yield return new WaitForSeconds(waitTime);

            if (wasDead || isRewinding) continue;

            if (idleClips != null && idleClips.Length > 0 && audioSource != null)
            {
                int index = Random.Range(0, idleClips.Length);
                audioSource.PlayOneShot(idleClips[index], idleVolume);
            }
        }
    }
}
