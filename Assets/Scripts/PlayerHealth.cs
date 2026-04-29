using UnityEngine;
using System;
using System.Collections;
using TimeRewind;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour, IRewindable
{
    [Header("Health Settings")]
    [Range(1, 20)]
    [SerializeField] private int maxHealth = 5; 

    [Header("Invincibility Settings")]
    [Tooltip("How long the player is immune after taking damage (seconds)")]
    [SerializeField] private float iFrameDuration = 2.0f;
    [Tooltip("How fast the sprite flashes during invincibility")]
    [SerializeField] private float flashSpeed = 0.1f;

    [Header("Debug")]
    [Range(0, 20)]
    [SerializeField] private int currentHealth;

    [Header("Low Health Hint")]

    [Tooltip("Heartbeat starts when health is at or below this value")]
    [SerializeField] private int lowHealthThreshold = 2;

    [Tooltip("Disable low-health heartbeat while rewinding")]
    [SerializeField] private bool stopHeartbeatDuringRewind = true;

    private bool hintHeartbeatActive = false;

    [Header("Controller Vibration")]
    [SerializeField] private float vibrationLowFrequency = 0.5f;
    [SerializeField] private float vibrationHighFrequency = 0.8f;
    [SerializeField] private float vibrationDuration = 0.2f;

    // References
    private SpriteRenderer spriteRenderer;
    private bool isInvincible = false;
    private bool _isRewinding = false;
    private Rigidbody2D _rb;
    private float _defaultGravityScale = 1f;
    private RigidbodyConstraints2D _defaultConstraints = RigidbodyConstraints2D.FreezeRotation;
    private bool _pendingRewindReviveEffect;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }
    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private float deathVolume = 0.5f;
    [SerializeField] private AudioClip reviveClip;
    [SerializeField] private float reviveVolume = 0.5f;

    public void SetInvincible(bool value)
    {
        isInvincible = value;
        if (!value && spriteRenderer != null)
            spriteRenderer.enabled = true;
    }

    // Events
    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;
    private Animator animator; 
    public GameOverUI gameOverUI;
    [Header("Death / Game Over")]
    [Tooltip("Safety timeout so death sequence can't stall forever before showing Game Over.")]
    [SerializeField] private float deathSequenceTimeoutSeconds = 2.5f;

    private void Awake()
    {
        // Find the sprite renderer so we can flash it. 
        // "GetComponentInChildren" works even if the sprite is on a child object.
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
        {
            _defaultGravityScale = _rb.gravityScale;
            _defaultConstraints = _rb.constraints;
        }

        if (gameOverUI == null)
            gameOverUI = FindFirstObjectByType<GameOverUI>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }
    // 4. Register with Rewind Manager
    private void OnEnable()
    {
        TimeRewindManager.Instance?.Register(this);
    }

    // 5. Unregister when disabled
    private void OnDisable()
    {
        if (TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.Unregister(this);
        }
        if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
    }

    private void OnValidate()
    {
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        if (currentHealth < 0) currentHealth = 0;
        if (Application.isPlaying) UpdateUI();
    }
    public void ModifyHealth(int amount, bool applyKnockback = true)
    {
        if (IsDead) return;
        if (_isRewinding) return;

        // 1. DAMAGE LOGIC
        if (amount < 0)
        {
            // If we are currently invincible, IGNORE the damage entirely
            if (isInvincible) return;

            // Otherwise, take the damage and start invincibility
            DataCollectionService.Instance?.RecordDamageTaken(-amount);
            // Pass the knockback choice down to TakeDamage
            TakeDamage(amount, applyKnockback);
        }
        
        // 2. HEALING LOGIC (Always allowed)
        else if (amount > 0)
        {
            Heal(amount);
        }
    }

    private void TakeDamage(int amount, bool applyKnockback = true)
    {
        if (sfxSource != null && hitClip != null)
        {
            sfxSource.PlayOneShot(hitClip);
        }
        currentHealth += amount; // Amount is negative, so this subtracts
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateUI(); 

        if (_rb != null && currentHealth > 0 && applyKnockback)
        {
            Collider2D enemy = Physics2D.OverlapCircle(transform.position, 2f, LayerMask.GetMask("Enemy"));
            float knockbackDir = transform.position.x < (enemy != null ? enemy.transform.position.x : transform.position.x + 1) ? -1f : 1f;

            _rb.linearVelocity = Vector2.zero; 
            _rb.AddForce(new Vector2(knockbackDir * 12f, 7f), ForceMode2D.Impulse);

            var movement = GetComponent<PlayerPlatformer>();
            if (movement != null) movement.TriggerKnockbackLock(0.2f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Start the cooldown routine
            StartCoroutine(InvincibilityRoutine());
        }
        StartCoroutine(GamepadVibration());
        CameraShake shaker = Camera.main.GetComponent<CameraShake>();
        if (shaker != null)
        {
            // duration = 0.15s, magnitude = 0.2f
            shaker.Shake(0.1f, 0.1f);
            shaker.FlashRed();
        }
    }

    private void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateUI();
    }

    private void UpdateUI()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        UpdateLowHealthHeartbeat();
    }

    private IEnumerator FreezeAnimatorAfterDeath()
    {
        // waits until in the death state
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Player_Death"))
            yield return null;

        // wait until the animation finishes
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        animator.enabled = false;       // disables the animator 
    }

    private IEnumerator HandleDeath()
    {
        var playerMovement = GetComponent<PlayerPlatformer>();  
        var rb = GetComponent<Rigidbody2D>();
        var col = GetComponent<Collider2D>();  

        if (col != null) col.enabled = false;

        float startUnscaled = Time.unscaledTime;

        // sets the death animation to trigger
        if (animator != null)
        {
            animator.SetTrigger("Die"); 
        }

        // waits until player is grounded
        if (playerMovement != null)
        {
            while (!playerMovement.isGrounded && (Time.unscaledTime - startUnscaled) < deathSequenceTimeoutSeconds) 
                yield return null; 
        }

        // disables all player movement once grounded and death animation has run
        if (playerMovement != null) 
            playerMovement.enabled = false; 

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; 
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll; 
            rb.gravityScale = 0f;
            rb.simulated = false; 
        }

        if (animator != null)
        {
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Player_Death") &&
                   (Time.unscaledTime - startUnscaled) < deathSequenceTimeoutSeconds)
                yield return null; 

            while (animator.GetCurrentAnimatorStateInfo(0).IsName("Player_Death") &&
                   animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f &&
                   (Time.unscaledTime - startUnscaled) < (deathSequenceTimeoutSeconds + 2.0f))
                yield return null; 
        }

        // shows the game over screen once death sequence has finished
        if (gameOverUI != null) gameOverUI.ShowGameOver(); 
    }

    private void Die()
    {   
        if (IsDead) return; 
        IsDead = true;
        if (sfxSource != null && deathClip != null)
        {
            sfxSource.PlayOneShot(deathClip, deathVolume);
        }
        OnDeath?.Invoke();
        DataCollectionService.Instance?.RecordDeath();
        ScoreManager.Instance.RemovePoints(100);
        // GameManager.Instance.PlayerDied(); 

        // Only set safety if the player dies during the tutorial.
        // Platforms read this PlayerPrefs key globally, so a tutorial death can keep later
        // falling-platform sections forgiving even outside tutorial scenes.
        if (DynamicDifficultyManager.IsTutorialSceneContextActive())
        {
            PlayerPrefs.SetInt(DynamicDifficultyManager.TutorialSafetyPlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        Debug.Log("Player Died");

        // Trigger the Game Over UI immediately so death always leads to game-over,
        // even if animation/grounding waits stall or get cancelled by rewind.
        if (gameOverUI == null)
            gameOverUI = FindFirstObjectByType<GameOverUI>();
        if (gameOverUI != null)
            gameOverUI.ShowGameOver();

        StartCoroutine(HandleDeath()); 

        // Ensure sprite is visible when dead (optional)
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }

    // This Coroutine handles the logic and the visual flashing
    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;

        // Visual Feedback: Flash the sprite
        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            while (elapsed < iFrameDuration)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled; // Toggle on/off
                yield return new WaitForSeconds(flashSpeed);
                elapsed += flashSpeed;
            }
            // Ensure sprite is back to visible when done
            spriteRenderer.enabled = true;
        }
        else
        {
            // Fallback if no sprite renderer: just wait
            yield return new WaitForSeconds(iFrameDuration);
        }

        isInvincible = false;
    }
    private IEnumerator GamepadVibration()
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(vibrationLowFrequency, vibrationHighFrequency);
            yield return new WaitForSeconds(vibrationDuration);
            Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
    private void UpdateLowHealthHeartbeat()
    {
        if (_isRewinding && stopHeartbeatDuringRewind)
        {
            StopHintHeartbeat();
            StopLowHealthLighting();
            return;
        }

        bool shouldTrigger =
            currentHealth >= 0 &&
            currentHealth <= lowHealthThreshold &&
            !IsDead;

        if (shouldTrigger && !hintHeartbeatActive)
        {
            RewindHaptics.Instance.StartHintHeartbeat();
            StartLowHealthLighting();

            hintHeartbeatActive = true;
        }
        else if (!shouldTrigger && hintHeartbeatActive)
        {
            StopHintHeartbeat();
            StopLowHealthLighting();
        }
    }
    private void StopHintHeartbeat()
    {
        RewindHaptics.Instance.StopHintHeartbeat();
        hintHeartbeatActive = false;
    }
    private void StartLowHealthLighting()
    {
        if (LowHealthVisualController.Instance != null)
        {
            LowHealthVisualController.Instance.StartLowHealthEffect();
        }
    }

    private void StopLowHealthLighting()
    {
         if (LowHealthVisualController.Instance != null)
        {
            LowHealthVisualController.Instance.StopLowHealthEffect();
        }
    }
        public void OnStartRewind()
    {
        _isRewinding = true;
        StopHintHeartbeat();
        StopLowHealthLighting();
        if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        StopAllCoroutines();
        isInvincible = false;
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        var reviveEffect = GetComponent<PlayerReviveEffect>();
        if (reviveEffect != null && reviveEffect.IsReviving) reviveEffect.Cancel();
    }

    public void OnStopRewind()
    {
        _isRewinding = false;
        if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);

        // Re-evaluate whether low-health effects should be active now that rewind has ended.
        UpdateLowHealthHeartbeat();

        // If we revived during rewind, play the "getting up" effect on exit so it's visible/consistent.
        if (_pendingRewindReviveEffect && !IsDead)
        {
            _pendingRewindReviveEffect = false;
            var reviveEffect = GetComponent<PlayerReviveEffect>();
            if (reviveEffect != null) reviveEffect.Play();
            if (sfxSource != null && reviveClip != null)
            {
                sfxSource.PlayOneShot(reviveClip, reviveVolume);
            }
        }
    }

    public RewindState CaptureState()
    {
        // Create state with required Transform data
        var state = RewindState.Create(transform.position, transform.rotation, Time.time);
        
        // Save ONLY the things this script cares about (Health)
        // ** REQUIREMENT: You must have 'public int Hearts;' in RewindState.cs **
        state.Health = currentHealth;
        
        return state;
    }

    public void ApplyState(RewindState state)
    {
        // Check if health changed during this rewind frame
        if (currentHealth != state.Health)
        {
            currentHealth = state.Health;

            // REVIVAL LOGIC:
            // If we were dead, but rewound to a point where we had health...
            if (IsDead && currentHealth > 0)
            {
                IsDead = false;
                if (spriteRenderer != null) spriteRenderer.enabled = true;

                var col = GetComponent<Collider2D>();
                if (col != null) col.enabled = true;

                var playerMovement = GetComponent<PlayerPlatformer>();
                if (playerMovement != null) playerMovement.enabled = true;

                if (_rb != null)
                {
                    _rb.simulated = true;
                    _rb.gravityScale = _defaultGravityScale;
                    _rb.constraints = _defaultConstraints;
                }

                if (animator != null) animator.enabled = true;

                if (gameOverUI != null) gameOverUI.HideGameOver();

                // Defer the revive "getting up" effect until rewind stops so the slow-mo reads clearly.
                _pendingRewindReviveEffect = true;
            }

            // This will tell HeartDisplay.cs to animate the hearts filling/emptying
            UpdateUI();
        }
    }
}