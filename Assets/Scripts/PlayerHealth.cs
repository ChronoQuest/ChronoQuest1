using UnityEngine;
using System;
using System.Collections;

public class PlayerHealth : MonoBehaviour
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

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }

    // Events
    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        // Find the sprite renderer so we can flash it. 
        // "GetComponentInChildren" works even if the sprite is on a child object.
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();
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

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
        Debug.Log("Player Died");
        
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
}