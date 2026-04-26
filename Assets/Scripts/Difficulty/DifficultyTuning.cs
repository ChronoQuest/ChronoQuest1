using UnityEngine;

public enum DifficultyTier
{
    /// <summary>Strongest assists (lowest enemy HP, platforms may be locked).</summary>
    VeryEasy = 0,
    Easy = 1,
    Normal = 2,
    Hard = 3
}

[CreateAssetMenu(menuName = "ChronoQuest/Difficulty Tuning", fileName = "DifficultyTuning")]
public class DifficultyTuning : ScriptableObject
{
    [Header("Tutorial safety mode")]
    [Tooltip("Scenes where tutorial safety rules can apply. Only GameScene is the tutorial; other levels stay normal.")]
    public string[] tutorialSceneNames = new[] { "GameScene" };

    [Tooltip("If true, tutorial safety can be activated in tutorial scenes (set when the player dies in tutorial).")]
    public bool tutorialSafetyEnabled = true;

    [Tooltip("Deprecated (not used). Tutorial safety no longer auto-triggers by time in scene.")]
    public float tutorialTimeThresholdSeconds = 360f;

    [Tooltip("Enemy HP multiplier in tutorial scenes while tutorial safety is active.")]
    [Range(0.1f, 1f)]
    public float tutorialEnemyHpMultiplier = 0.75f;

    [Header("Tier selection timing")]
    [Tooltip("How often to capture performance samples for the rolling window (seconds). Uses unscaled time.")]
    public float sampleEverySeconds = 1f;

    [Tooltip("How often to re-evaluate tier from the rolling window (seconds).")]
    public float evaluateEverySeconds = 10f;

    [Tooltip("Minimum seconds between tier changes.")]
    public float tierChangeCooldownSeconds = 60f;

    [Tooltip("Recent window used for per-minute rate estimates.")]
    public float rollingWindowSeconds = 120f;

    [Header("Tier thresholds (score)")]
    [Tooltip("Score <= this (more negative = worse play) pushes toward VeryEasy.")]
    public float veryEasyThreshold = -0.35f;

    [Tooltip("Score <= easyThreshold (but above veryEasy band) pushes toward Easy.")]
    public float easyThreshold = -0.15f;

    [Tooltip("Score >= hardThreshold pushes toward Hard.")]
    public float hardThreshold = 0.15f;

    [Tooltip("Extra margin required to switch tiers (prevents flip-flopping).")]
    public float hysteresisMargin = 0.05f;

    [Header("Multipliers by tier")]
    public TierMultipliers veryEasy = TierMultipliers.VeryEasyDefaults();
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

        public static TierMultipliers VeryEasyDefaults() => new TierMultipliers
        {
            manaRegenMultiplier = 1.35f,
            manaOnHitMultiplier = 1.15f,
            healingMultiplier = 1.35f,
            enemyHpMultiplier = 0.65f
        };

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
            manaOnHitMultiplier = 0.85f,
            healingMultiplier = 0.90f,
            enemyHpMultiplier = 1.40f
        };
    }

    public TierMultipliers GetMultipliers(DifficultyTier tier)
    {
        return tier switch
        {
            DifficultyTier.VeryEasy => veryEasy,
            DifficultyTier.Easy => easy,
            DifficultyTier.Hard => hard,
            _ => normal
        };
    }
}

