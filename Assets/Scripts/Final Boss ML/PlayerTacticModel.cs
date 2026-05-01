using System.Collections.Generic;
using System.Linq; 
using UnityEngine; 

public class PlayerTacticalModel : MonoBehaviour
{
    // TODO: modify to make four clusters and add the balanced tactic
    // -- EXPERIMENT EDITS -- 
    public enum TacticType
    {
        Reckless, 
        Idle, 
        Evasive,
        Cautious
    }
    
    // dictionary storing tactic type and it's score
    public Dictionary<TacticType, float> tacticBeliefs = new Dictionary<TacticType, float>();

    // counts for events during gameplay to determine tactic 
    private float dashCount; 
    private float spellCount;
    private float rainSpellCount; 
    private float meleeHits;
    private float jumpCount;
    private float rewindCount; 
    private float damageTaken; 
    private float wallJumpCount; 
    private float meleeAttacks; 

    // tactic is determined and tracked in a 5 seconds window
    private float windowDuration = 5f; 
    private float timer = 0f; 

    public GMMModel gmmModel; 
    public NeuralNetwork neuralModel;
    public bool useNeural = false; 

    void Start()
    {
        Debug.Log("TACTICAL MODEL START");

        foreach (TacticType tactic in System.Enum.GetValues(typeof(TacticType)))
        {
            tacticBeliefs[tactic] = 0f; 
        }
    }

    void OnEnable()
    {
        Debug.Log("TACTICAL MODEL ENABLED");
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
        Debug.Log($"[COUNTS] Dash:{dashCount}, Melee:{meleeHits}, Damage:{damageTaken}");
        
        float[] features = BuildFeatureVector();
        float[] probs;

        if (useNeural)
            probs = neuralModel.Predict(features);
        else
            probs = gmmModel.PredictProba(features);

        int predictedCluster = GetPredictedCluster(probs); 

        if (ExperimentManager.Instance != null)
        {
            ExperimentManager.Instance.LogResult(predictedCluster, "TODO_BOSS_BEHAVIOUR"); 
        }

        float duration = Mathf.Max(timer, 0.001f);
        float meleeRate = (meleeAttacks + meleeHits) / duration;
        float damageRate = damageTaken / duration;

        float recklessBase = probs[0];

        float bias = 0f;

        if (meleeRate > 0.6f)
        {
            bias += 0.05f;
        }

        if (damageRate > 0.2f)
        {
            bias += 0.05f;
        }

        float boostedReckless = Mathf.Clamp01(recklessBase + bias); 

        List<TacticType> keys = new List<TacticType>(tacticBeliefs.Keys);

        foreach (var key in keys)
        {
            tacticBeliefs[key] = 0f;
        }  

        // saves score to the corresponding potential tactic
        tacticBeliefs[TacticType.Idle] = probs[1]; 
        tacticBeliefs[TacticType.Reckless] = Mathf.Clamp01(boostedReckless);
        tacticBeliefs[TacticType.Evasive] = probs[2];
        tacticBeliefs[TacticType.Cautious] = probs[3]; 

        NormaliseBeliefs(); 
    }

    int GetPredictedCluster(float[] probs)
    {
        int maxIndex = 0;
        float maxVal = probs[0]; 

        for (int i = 1; i < probs.Length; i++)
        {
            if (probs[i] > maxVal)
            {
                maxVal = probs[i];
                maxIndex = i; 
            }
        }
        
        return maxIndex; 
    }

    float[] BuildFeatureVector()
    {
       // use the current window duration (e.g. up to 5 seconds)
        float duration = Mathf.Max(timer, 0.001f);

        float dashRate   = dashCount / duration;
        float jumpRate   = (jumpCount + wallJumpCount) / duration;
        float meleeRate  = (meleeAttacks + meleeHits) / duration;   
        float spellRate  = (spellCount + rainSpellCount) / duration;
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

    public void RecordMeleeAttack()
    {
        Debug.Log("Recorded melee attack for tactic model"); 
        meleeAttacks++; 
    }
}
