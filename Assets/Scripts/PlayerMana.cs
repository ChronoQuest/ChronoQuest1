using UnityEngine;
using System;

public class PlayerMana : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float manaGainOnHit = 10f; // Reward for combat
    [SerializeField] private float passiveRegenRate = 0f; // Souls-likes usually actully don't have passive regen, but keeping it optional
    
    // Encapsulation: Other scripts can READ mana, but only this script can CHANGE it.
    public float CurrentMana { get; private set; }
    public float MaxMana => maxMana;
    
    // Events: The UI Manager will listen to this to update the blue bar
    public event Action<float> OnManaChanged;
    public event Action OnManaSpendFailed;

    private float _baseManaGainOnHit;
    private float _basePassiveRegenRate;

    private void Awake()
    {
        CurrentMana = maxMana;
        _baseManaGainOnHit = manaGainOnHit;
        _basePassiveRegenRate = passiveRegenRate;
        //OnManaChanged?.Invoke(CurrentMana / maxMana); // Update UI at start
        Debug.Log($"Mana System Awake: Current Mana set to {CurrentMana} using Max Mana {maxMana}");
    }

    private void OnEnable()
    {
        if (DynamicDifficultyManager.Instance != null)
            DynamicDifficultyManager.Instance.OnDifficultyChanged += ApplyDifficulty;
        ApplyDifficulty();
    }

    private void OnDisable()
    {
        if (DynamicDifficultyManager.Instance != null)
            DynamicDifficultyManager.Instance.OnDifficultyChanged -= ApplyDifficulty;
    }

    private void Start() 
    {
        OnManaChanged?.Invoke(CurrentMana / maxMana);
    }

    private void Update()
    {
        // Optional: Passive Regen
        if (passiveRegenRate > 0 && CurrentMana < maxMana)
        {
            ModifyMana(passiveRegenRate * Time.deltaTime);
        }
    }

    // Call this when attacking an enemy
    public void AddManaOnHit()
    {
        ModifyMana(manaGainOnHit);
    }

    // General method to add/subtract mana safely
    public void ModifyMana(float amount)
    {
        CurrentMana = Mathf.Clamp(CurrentMana + amount, 0, maxMana);
        
        // Notify the UI (sends a percentage 0.0 to 1.0)
        OnManaChanged?.Invoke(CurrentMana / maxMana);
    }

    // Returns true if spell was cast successfully
    public bool TrySpendMana(float cost)
    {
        if (CurrentMana >= cost)
        {
            ModifyMana(-cost);
            return true;
        }
        OnManaSpendFailed?.Invoke();
        return false;
    }

    // called by external systems (e.g. rewind) to trigger the insufficient mana warning
    public void NotifySpendFailed()
    {
        OnManaSpendFailed?.Invoke();
    }

    // Specifically for the Rewind Mechanic (called every frame while rewinding)
    public bool DrainManaContinuous(float amountPerSecond)
    {
        float cost = amountPerSecond * Time.deltaTime;
        if (CurrentMana >= cost)
        {
            ModifyMana(-cost);
            return true;
        }
        // not enough mana to continue, drain whatever is left to zero so the
        // hasMana check cleanly prevents rewind from restarting
        ModifyMana(-CurrentMana);
        return false;
    }

    // Rewind: drain based on real time so mana cost is constant per second regardless of playback speed
    public bool DrainManaContinuousUnscaled(float amountPerSecond)
    {
        float cost = amountPerSecond * Time.unscaledDeltaTime;
        if (CurrentMana >= cost)
        {
            ModifyMana(-cost);
            return true;
        }
        ModifyMana(-CurrentMana);
        return false;
    }
    
    // FOR REWIND SYSTEM: Force set mana to a specific value
    public void SetMana(float value)
    {
        CurrentMana = value;
        OnManaChanged?.Invoke(CurrentMana / maxMana);
    }
    
    // Allow runtime adjustment of max mana (e.g. from a UI slider)
    public void SetMaxMana(float newMax)
    {
        newMax = Mathf.Max(newMax, 1f); // Prevent zero/negative max mana
        maxMana = newMax;
        CurrentMana = maxMana; // Refill mana when max is changed
        OnManaChanged?.Invoke(CurrentMana / maxMana);
    }

    private void ApplyDifficulty()
    {
        float manaOnHitMult = 1f;
        float regenMult = 1f;

        if (DynamicDifficultyManager.Instance != null)
        {
            manaOnHitMult = DynamicDifficultyManager.Instance.ManaOnHitMultiplier;
            regenMult = DynamicDifficultyManager.Instance.ManaRegenMultiplier;
        }

        manaGainOnHit = _baseManaGainOnHit * manaOnHitMult;
        passiveRegenRate = _basePassiveRegenRate * regenMult;
    }
}
