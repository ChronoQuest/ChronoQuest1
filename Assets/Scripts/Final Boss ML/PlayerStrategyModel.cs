using System.Collections.Generic; 
using UnityEngine;

public class PlayerStrategyModel : MonoBehaviour
{
    public PlayerTacticalModel playerTacticalModel; 

    // enum defining the different types of strategies a player could fall into
    public enum StrategyType
    {
        AggressivePlayer,
        DefensivePlayer,
        AbilityFocusedPlayer
    }

    public Dictionary<StrategyType, float> strategyBeliefs = new Dictionary<StrategyType, float>(); 
    private float windowDuration = 30f;
    private float timer = 0f; 

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        playerTacticalModel = FindObjectOfType<PlayerTacticalModel>();

        foreach (StrategyType strategy in System.Enum.GetValues(typeof(StrategyType)))
        {
            strategyBeliefs[strategy] = 0f; 
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= windowDuration)
        {
            DetermineStrategy();
            DebugStrategy(); 
            timer = 0f; 
        }
    }
     
    // -- EXPERIMENT EDITS --
    void DetermineStrategy()
    {
        foreach (StrategyType strategy in System.Enum.GetValues(typeof(StrategyType)))
        {
            strategyBeliefs[strategy] = 0f; 
        }
        
        var tactics = playerTacticalModel.tacticBeliefs; 

        float aggressive = tactics[PlayerTacticalModel.TacticType.Aggressive];
        float defensive = tactics[PlayerTacticalModel.TacticType.Evasive];
        float ability = tactics[PlayerTacticalModel.TacticType.Cautious]; 

        strategyBeliefs[StrategyType.AggressivePlayer] += aggressive;
        strategyBeliefs[StrategyType.DefensivePlayer] += defensive;
        strategyBeliefs[StrategyType.AbilityFocusedPlayer] += ability;

        NormaliseStrategy();
    }

    void NormaliseStrategy()
    {
        float total = 0f; 

        foreach (var value in strategyBeliefs.Values)
        {
            total += value; 
        }

        if (total <= 0f) return; 

        List<StrategyType> keys = new List<StrategyType>(strategyBeliefs.Keys);

        foreach (var key in keys)
        {
            strategyBeliefs[key] /= total; 
        }
    }

    void DebugStrategy()
    {
        string output = "STRATEGY: ";

        foreach (var pair in strategyBeliefs)
        {
            output += pair.Key + ": " + pair.Value.ToString("F2") + " | "; 
        }

        Debug.Log(output);

        // TODO: add functionality to record all strategies in a session
    }

    // method to return the dominant strategy 
    public StrategyType GetDominantStrategy()
    {
        StrategyType best = StrategyType.AggressivePlayer;          // assigning player to aggressive as fallback option, only cause it's the first enum value
        float max = float.MinValue;

        foreach (var pair in strategyBeliefs)
        {
            if (pair.Value > max)
            {
                max = pair.Value;
                best = pair.Key; 
            }
        }

        return best;
    }
}
