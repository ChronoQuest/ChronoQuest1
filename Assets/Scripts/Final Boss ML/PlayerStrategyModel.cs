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

    void Start()
    {
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
     
    void DetermineStrategy()
    {
        var tactics = playerTacticalModel.tacticBeliefs; 

        float aggressive = tactics[PlayerTacticalModel.TacticType.Aggressive] * 0.7f + tactics[PlayerTacticalModel.TacticType.Evasive] * 0.3f;
        float defensive = tactics[PlayerTacticalModel.TacticType.RewindReliance] * 0.6f + tactics[PlayerTacticalModel.TacticType.Cautious] * 0.4f;
        float ability = tactics[PlayerTacticalModel.TacticType.Ability]; 

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
    }

    // method to return the dominant strategy 
    public StrategyType GetDominantStrategy()
    {
        StrategyType best = StrategyType.AggressivePlayer;          // assigning player to aggressive as fallback option, only cause it's the first enum value
        float max = 0f;

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
