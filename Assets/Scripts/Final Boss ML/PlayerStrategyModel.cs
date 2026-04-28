using System.Collections.Generic; 
using System.Linq; 
using UnityEngine;

public class PlayerStrategyModel : MonoBehaviour
{
    public static PlayerStrategyModel Instance; 
    public PlayerTacticalModel playerTacticalModel; 
    public List<Dictionary<StrategyType, float>> strategyHistory = new List<Dictionary<StrategyType, float>>(); 

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
        Instance = this; 
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
    public void DetermineStrategy()
    {
        foreach (StrategyType strategy in System.Enum.GetValues(typeof(StrategyType)))
        {
            strategyBeliefs[strategy] = 0f; 
        }
        
        var tactics = playerTacticalModel.tacticBeliefs; 

        float reckless = tactics[PlayerTacticalModel.TacticType.Reckless];
        float evasive = tactics[PlayerTacticalModel.TacticType.Evasive];
        float cautious = tactics[PlayerTacticalModel.TacticType.Cautious];
        float idle = tactics[PlayerTacticalModel.TacticType.Idle]; 

        // reckless -> mostly aggressive, slightly defensive 
        strategyBeliefs[StrategyType.AggressivePlayer] += reckless * 0.8f;
        strategyBeliefs[StrategyType.DefensivePlayer] += reckless * 0.2f;

        // evasive -> mostly defensive, slightly aggressive 
        strategyBeliefs[StrategyType.DefensivePlayer] += evasive * 0.7f;
        strategyBeliefs[StrategyType.AggressivePlayer] += evasive * 0.3f;

        // cautious -> mostly ability/control, slightly defensive 
        strategyBeliefs[StrategyType.AbilityFocusedPlayer] += cautious * 0.8f;
        strategyBeliefs[StrategyType.DefensivePlayer] += cautious * 0.2f;

        float confidence = 1f - idle; 
        foreach (StrategyType strategy in strategyBeliefs.Keys.ToList())
        {
            strategyBeliefs[strategy] *= confidence; 
        }

        NormaliseStrategy();

        strategyHistory.Add(new Dictionary<StrategyType, float>(strategyBeliefs));
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

    public Dictionary<StrategyType, float> GetAverageStrategy()
    {
        Dictionary<StrategyType, float> avg = new Dictionary<StrategyType, float>(); 

        foreach (StrategyType strategy in System.Enum.GetValues(typeof(StrategyType)))
        {
            avg[strategy] = 0f;
        }

        if (strategyHistory.Count == 0)
            return new Dictionary<StrategyType, float>(strategyBeliefs);

        foreach (var snapshot in strategyHistory)
        {
            foreach (var pair in snapshot)
            {
                avg[pair.Key] += pair.Value;
            }
        }

        int count = strategyHistory.Count;

        foreach (StrategyType strategy in avg.Keys.ToList())
        {
            avg[strategy] /= count;
        }

        return avg;
    }

    public StrategyType GetAverageDominantStrategy()
    {
        var avg = GetAverageStrategy();

        StrategyType best = StrategyType.AggressivePlayer;
        float max = float.MinValue;

        foreach (var pair in avg)
        {
            if (pair.Value > max)
            {
                max = pair.Value;
                best = pair.Key;
            }
        }

        return best;
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

    public void Reset()
    {
        foreach (StrategyType strategy in System.Enum.GetValues(typeof(StrategyType)))
        {
            strategyBeliefs[strategy] = 0f;
        } 

        strategyHistory.Clear(); 
        timer = 0f;

        Debug.Log("PlayerStrategyModel reset");
    }
}
