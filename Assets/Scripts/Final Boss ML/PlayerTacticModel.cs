using System.Collections.Generic;
using UnityEngine; 

public class PlayerTacticalModel : MonoBehaviour
{
    public enum TacticType
    {
        Aggressive, 
        Evasive,
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

    public GMMModel gmmModel; 

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
            DebugTactics();
            ResetWindow(); 
        }
    }

    // calculates player tactic score based on events recorded in the game
    void DetermineTactics()
    {
        float[] features = BuildFeatureVector();
        float[] clusterProbs = gmmModel.PredictProba(features);

        List<TacticType> keys = new List<TacticType>(tacticBeliefs.Keys);

        foreach (var key in keys)
        {
            tacticBeliefs[key] = 0f;
        }  

        // saves score to the corresponding potential tactic
        tacticBeliefs[TacticType.Aggressive] = clusterProbs[2]; 
        tacticBeliefs[TacticType.Evasive] = clusterProbs[3];
        tacticBeliefs[TacticType.Cautious] = clusterProbs[0];

        tacticBeliefs[TacticType.Aggressive] += 0.5f * clusterProbs[1];
        tacticBeliefs[TacticType.Evasive] += 0.5f * clusterProbs[1];

        NormaliseBeliefs(); 
    }

    float[] BuildFeatureVector()
    {
       // use the current window duration (e.g. up to 5 seconds)
        float duration = Mathf.Max(timer, 0.001f);

        float dashRate   = dashCount / duration;
        float jumpRate   = (jumpCount + wallJumpCount) / duration;
        float meleeRate  = meleeHits / duration;   
        float spellRate  = spellCount / duration;
        float rewindRate = rewindCount / duration;
        float damageRate = damageTaken / duration;

        float meleeAccuracy = meleeHits / (meleeHits + 1f);
        float spellAccuracy = spellCount / (spellCount + 1f);

        return new float[]
        {
            dashRate,
            jumpRate,
            meleeRate,
            spellRate,
            rewindRate,
            damageRate,
            meleeAccuracy, 
            spellAccuracy
        };
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

    void DebugTactics()
    {
        string output = "TACTICS: ";

        foreach (var pair in tacticBeliefs)
        {
            output += pair.Key + ": " + pair.Value.ToString("F2") + " | "; 
        }

        Debug.Log(output);
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
