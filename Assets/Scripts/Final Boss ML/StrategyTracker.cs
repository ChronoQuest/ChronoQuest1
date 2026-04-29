using System.Collections.Generic;
using UnityEngine; 

public static class StrategyTracker
{
    public static float aggressiveSum = 0f;
    public static float defensiveSum = 0f;
    public static float abilitySum = 0f;
    public static int count = 0;

    public static void AddSample(Dictionary<PlayerStrategyModel.StrategyType, float> beliefs)
    {
        aggressiveSum += beliefs[PlayerStrategyModel.StrategyType.AggressivePlayer];
        defensiveSum  += beliefs[PlayerStrategyModel.StrategyType.DefensivePlayer];
        abilitySum    += beliefs[PlayerStrategyModel.StrategyType.AbilityFocusedPlayer];
        count++;

        Debug.Log($"[StrategyTracker] Sample {count} | Agg: {beliefs[PlayerStrategyModel.StrategyType.AggressivePlayer]:F2} | Def: {beliefs[PlayerStrategyModel.StrategyType.DefensivePlayer]:F2} | Abil: {beliefs[PlayerStrategyModel.StrategyType.AbilityFocusedPlayer]:F2}");
    }

    public static PlayerStrategyModel.StrategyType GetAverageStrategy()
    {
        if (count == 0) return PlayerStrategyModel.StrategyType.AggressivePlayer;

        float aggressive = aggressiveSum / count;
        float defensive  = defensiveSum / count;
        float ability    = abilitySum / count;

        if (defensive > aggressive && defensive > ability)
            return PlayerStrategyModel.StrategyType.DefensivePlayer;

        if (ability > aggressive && ability > defensive)
            return PlayerStrategyModel.StrategyType.AbilityFocusedPlayer;

        return PlayerStrategyModel.StrategyType.AggressivePlayer;
    }

    public static void Reset()
    {
        aggressiveSum = 0f;
        defensiveSum = 0f;
        abilitySum = 0f;
        count = 0;
    }
}
