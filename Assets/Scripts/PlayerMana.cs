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
    
    // Events: The UI Manager will listen to this to update the blue bar
    public event Action<float> OnManaChanged;

    private void Start()
    {
        CurrentMana = maxMana;
        OnManaChanged?.Invoke(CurrentMana / maxMana); // Update UI at start
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
    private void ModifyMana(float amount)
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
        return false;
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
        return false; // Stop rewinding if out of mana
    }
    
    // FOR REWIND SYSTEM: Force set mana to a specific value
    public void SetMana(float value)
    {
        CurrentMana = value;
        OnManaChanged?.Invoke(CurrentMana / maxMana);
    }
}
