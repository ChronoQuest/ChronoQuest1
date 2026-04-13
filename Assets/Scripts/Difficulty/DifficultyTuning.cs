using UnityEngine;

public enum DifficultyTier
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

[CreateAssetMenu(menuName = "ChronoQuest/Difficulty Tuning", fileName = "DifficultyTuning")]
public class DifficultyTuning : ScriptableObject
{
    [Header("Tutorial safety mode")]
    [Tooltip("Scenes where tutorial safety rules can apply. Only GameScene is the tutorial; other levels stay normal.")]
    public string[] tutorialSceneNames = new[] { "GameScene" };

    [Tooltip("If true, tutorial safety triggers when deathCount >= 1 OR timeInSceneSeconds >= threshold.")]
    public bool tutorialSafetyEnabled = true;

    [Tooltip("Tutorial safety triggers if time in the current tutorial scene exceeds this threshold.")]
    public float tutorialTimeThresholdSeconds = 360f;

    [Tooltip("Enemy HP multiplier in tutorial scenes while tutorial safety is active.")]
    [Range(0.1f, 1f)]
    public float tutorialEnemyHpMultiplier = 0.75f;

    [Header("Tier selection timing")]
    [Tooltip("How often to re-evaluate difficulty (seconds).")]
    public float evaluateEverySeconds = 10f;

    [Tooltip("Minimum seconds between tier changes.")]
    public float tierChangeCooldownSeconds = 60f;

    [Tooltip("Recent window used for per-minute rate estimates.")]
    public float rollingWindowSeconds = 120f;

    [Header("Tier thresholds (score)")]
    [Tooltip("Score <= easyThreshold pushes toward Easy.")]
    public float easyThreshold = -0.35f;

    [Tooltip("Score >= hardThreshold pushes toward Hard.")]
    public float hardThreshold = 0.35f;

    [Tooltip("Extra margin required to switch tiers (prevents flip-flopping).")]
    public float hysteresisMargin = 0.10f;

    [Header("Multipliers by tier")]
    public TierMultipliers easy = TierMultipliers.EasyDefaults();
    public TierMultipliers normal = TierMultipliers.NormalDefaults();
    public TierMultipliers hard = TierMultipliers.HardDefaults();

    [System.Serializable]
    public struct TierMultipliers
    {
        [Header("Player resources")]
        public float manaRegenMultiplier;
        public float manaOnHitMultiplier;
        public float healingMultiplier;

        [Header("Enemy survivability")]
        public float enemyHpMultiplier;

        public static TierMultipliers EasyDefaults() => new TierMultipliers
        {
            manaRegenMultiplier = 1.25f,
            manaOnHitMultiplier = 1.10f,
            healingMultiplier = 1.25f,
            enemyHpMultiplier = 0.80f
        };

        public static TierMultipliers NormalDefaults() => new TierMultipliers
        {
            manaRegenMultiplier = 1.00f,
            manaOnHitMultiplier = 1.00f,
            healingMultiplier = 1.00f,
            enemyHpMultiplier = 1.00f
        };

        public static TierMultipliers HardDefaults() => new TierMultipliers
        {
            manaRegenMultiplier = 0.90f,
            manaOnHitMultiplier = 0.95f,
            healingMultiplier = 0.90f,
            enemyHpMultiplier = 1.20f
        };
    }

    public TierMultipliers GetMultipliers(DifficultyTier tier)
    {
        return tier switch
        {
            DifficultyTier.Easy => easy,
            DifficultyTier.Hard => hard,
            _ => normal
        };
    }
}

