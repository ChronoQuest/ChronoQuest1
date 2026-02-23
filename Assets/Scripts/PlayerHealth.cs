using UnityEngine;
using System;
using System.Collections;
using TimeRewind;

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

    // References
    private SpriteRenderer spriteRenderer;
    private bool isInvincible = false;
    private bool _isRewinding = false;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }

    // Events
    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;
    private Animator animator; 
    public GameOverUI gameOverUI;

    private void Awake()
    {
        // Find the sprite renderer so we can flash it. 
        // "GetComponentInChildren" works even if the sprite is on a child object.
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>(); 
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
    }

    private void OnValidate()
    {
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        if (currentHealth < 0) currentHealth = 0;
        if (Application.isPlaying) UpdateUI();
    }

    public void ModifyHealth(int amount)
    {
        if (IsDead) return;
        if (_isRewinding) return;

        // 1. DAMAGE LOGIC
        if (amount < 0)
        {
            // If we are currently invincible, IGNORE the damage entirely
            if (isInvincible) return;

            // Otherwise, take the damage and start invincibility
            TakeDamage(amount);
        }
        
        // 2. HEALING LOGIC (Always allowed)
        else if (amount > 0)
        {
            Heal(amount);
        }
    }

    private void TakeDamage(int amount)
    {
        currentHealth += amount; // Amount is negative, so this subtracts
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateUI();

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Start the cooldown routine
            StartCoroutine(InvincibilityRoutine());
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

        // sets the death animation to trigger
        if (animator != null)
        {
            animator.SetTrigger("Die"); 
        }

        // waits until player is grounded
        if (playerMovement != null)
        {
            while (!playerMovement.isGrounded) 
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
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Player_Death"))
                yield return null; 

            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f) 
                yield return null; 
        }

        // shows the game over screen once death sequence has finished
        if (gameOverUI != null) gameOverUI.ShowGameOver(); 
    }

    private void Die()
    {   
        if (IsDead) return; 
        IsDead = true;
        Debug.Log("Player Died");

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

    public void OnStartRewind()
    {
        _isRewinding = true;
        
        // Optional: If you want to stop flashing immediately when rewind starts:
        StopAllCoroutines();
        isInvincible = false;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }

    public void OnStopRewind()
    {
        _isRewinding = false;
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
                // Re-enable movement script here if you disabled it in Die()
            }

            // This will tell HeartDisplay.cs to animate the hearts filling/emptying
            UpdateUI();
        }
    }
}