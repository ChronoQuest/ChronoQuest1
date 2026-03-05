using System.Collections.Generic;
using UnityEngine; 

public class PlayerTacticalModel : MonoBehaviour
{
    public enum TacticType
    {
        Aggressive, 
        Evasive,
        Ability, 
        RewindReliance,
        Cautious
    }
    
    // dictionary storing tactic type and it's score
    public Dictionary<TacticType, float> tacticBeliefs = new Dictionary<TacticType, float>();

    // counts for events during gameplay to determine tactic 
    private float dashCount; 
    private float spellCount;
    private float meleeHits;
    private float jumpCount;
    private float rewindCount; 
    private float damageTaken; 
    private float wallJumpCount; 

    // tactic is determined and tracked in a 5 seconds window
    private float windowDuration = 5f; 
    private float timer = 0f; 

    void Start()
    {
        foreach (TacticType tactic in System.Enum.GetValues(typeof(TacticType)))
        {
            tacticBeliefs[tactic] = 0f; 
        }
    }
    
    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= windowDuration)
        {
            DetermineTactics();
            ResetWindow(); 
        }
    }

    // calculates player tactic score based on events recorded in the game
    void DetermineTactics()
    {
        float aggressive = meleeHits;
        float evasive = dashCount * 0.6f + jumpCount * 0.4f; 
        float ability = spellCount; 
        float rewindReliance = rewindCount; 
        float cautious = damageTaken * 0.6f + rewindCount * 0.4f; 

        // saves score to the corresponding potential tactic
        tacticBeliefs[TacticType.Aggressive] = aggressive; 
        tacticBeliefs[TacticType.Evasive] = evasive;
        tacticBeliefs[TacticType.Ability] = ability;
        tacticBeliefs[TacticType.RewindReliance] = rewindReliance; 
        tacticBeliefs[TacticType.Cautious] = cautious;

        NormaliseBeliefs(); 
    }

    void NormaliseBeliefs()
    {
        float total = 0f;

        foreach (var value in tacticBeliefs.Values)
        {
            total += value;
        }

        if (total <= 0f) return; 

        List<TacticType> keys = new List<TacticType>(tacticBeliefs.Keys); 

        foreach (var key in keys)
        {
            tacticBeliefs[key] /= total;
        }
    }

    void ResetWindow()
    {
        dashCount = 0;
        spellCount = 0;
        meleeHits = 0; 
        jumpCount = 0; 
        rewindCount = 0; 
        damageTaken = 0; 
        timer = 0;
    }

    // recording events and incrementing counters
    public void RecordDash()
    {
        Debug.Log("Recorded dash for tactic model"); 
        dashCount++; 
    }

    public void RecordJump()
    {
        Debug.Log("Recorded jump for tactic model"); 
        jumpCount++; 
    }

    public void RecordSpell()
    {
        Debug.Log("Recorded spell for tactic model"); 
        spellCount++; 
    } 
    
    public void RecordMeleeHit()
    {
        Debug.Log("Recorded melee hit for tactic model"); 
        meleeHits++;
    }

    public void RecordRewind()
    {
        Debug.Log("Recorded rewind for tactic model"); 
        rewindCount++;
    }

    public void RecordDamage(int amount)
    {
        Debug.Log("Recorded damage hit for tactic model"); 
        damageTaken += amount; 
    }
}
