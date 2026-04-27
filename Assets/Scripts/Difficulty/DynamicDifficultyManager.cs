using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DynamicDifficultyManager : MonoBehaviour
{
    /// <summary>Set to 1 when the player dies in a tutorial scene (see <see cref="DifficultyTuning.tutorialSceneNames"/>).</summary>
    public const string TutorialSafetyPlayerPrefsKey = "ChronoQuest.TutorialSafetyActive";

    /// <summary>Build name of the only tutorial level (must match the scene asset name in Build Settings).</summary>
    public const string PrimaryTutorialSceneName = "GameScene";

    /// <summary>
    /// True for the main tutorial scene regardless of <see cref="DifficultyTuning"/> inspector mistakes
    /// (case-insensitive, trimmed).
    /// </summary>
    public static bool IsPrimaryTutorialScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return string.Equals(sceneName.Trim(), PrimaryTutorialSceneName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True if <see cref="PrimaryTutorialSceneName"/> is loaded (any slot). Use for PlayerPrefs / death when the
    /// level uses additive loading and <see cref="SceneManager.GetActiveScene"/> may not be the tutorial scene.
    /// </summary>
    public static bool IsPrimaryTutorialSceneLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && IsPrimaryTutorialScene(s.name))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when any loaded scene counts as tutorial (including primary). Prefer over
    /// <c>IsSceneNameTutorial(GetActiveScene().name)</c> when additive scenes are used.
    /// </summary>
    public static bool IsTutorialSceneContextActive()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            if (IsSceneNameTutorial(s.name))
                return true;
        }
        return false;
    }

    public static DynamicDifficultyManager Instance { get; private set; }

    public event Action OnDifficultyChanged;

    [Header("Tuning")]
    [Tooltip("Optional. If null, defaults will be used.")]
    [SerializeField] private DifficultyTuning tuning;

    [Header("Debug")]
    [SerializeField] private bool logTierChanges = false;
    [Tooltip("Logs score + tier every evaluate cycle (Console).")]
    [SerializeField] private bool logEveryEvaluation = false;
    [Tooltip("On-screen panel (top-left). Enable on the DontDestroyOnLoad object while playing: Hierarchy → DynamicDifficultyManager.")]
    [SerializeField] private bool showDebugOverlay = false;

    public bool ShowDebugOverlay { get => showDebugOverlay; set => showDebugOverlay = value; }

    public DifficultyTier CurrentTier { get; private set; } = DifficultyTier.Normal;
    public bool TutorialSafetyActive { get; private set; }

    /// <summary>Last value from <see cref="ComputePerformanceScore"/> at the most recent tier evaluation.</summary>
    public float LastPerformanceScore { get; private set; }

    public float EnemyHpMultiplier => GetCurrentMultipliers().enemyHpMultiplier;
    public float ManaRegenMultiplier => GetCurrentMultipliers().manaRegenMultiplier;
    public float ManaOnHitMultiplier => GetCurrentMultipliers().manaOnHitMultiplier;
    public float HealingMultiplier => GetCurrentMultipliers().healingMultiplier;

    /// <summary>
    /// VeryEasy only: falling platforms stay solid (same idea as tutorial safety lock).
    /// </summary>
    public bool LockFallingPlatformsForCurrentTier =>
        CurrentTier == DifficultyTier.VeryEasy;

    private float _nextSampleTime;
    private float _nextEvalTime;
    private float _lastTierChangeTime;

    private string _activeSceneName;
    private float _sceneEnterUnscaledTime;

    private readonly Queue<Sample> _samples = new Queue<Sample>();
    private Snapshot _lastSnapshot;

    private readonly HashSet<int> _tutorialEnemyHpAdjusted = new HashSet<int>();

    private struct Snapshot
    {
        public float t;
        public int deaths;
        public int damageTaken;
        public int trapHits;
        public int meleeAttacks;
        public int meleeHits;
        public int spellCasts;
        public int spellHits;
        public int enemyKills;
    }

    private struct Sample
    {
        public float dt;
        public int dDeaths;
        public int dDamage;
        public int dTrapHits;
        public int dMeleeAttacks;
        public int dMeleeHits;
        public int dSpellCasts;
        public int dSpellHits;
        public int dEnemyKills;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance != null) return;
        var go = new GameObject("DynamicDifficultyManager");
        go.AddComponent<DynamicDifficultyManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        DataCollectionService.SessionGameplayCountersReset += OnSessionGameplayCountersReset;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        _activeSceneName = SceneManager.GetActiveScene().name;
        _sceneEnterUnscaledTime = Time.unscaledTime;
        _nextSampleTime = Time.unscaledTime + GetSampleIntervalSeconds();
        _nextEvalTime = Time.unscaledTime + Mathf.Max(0.5f, GetTuning().evaluateEverySeconds);

        ResetSampling();
        ApplySceneRules();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            DataCollectionService.SessionGameplayCountersReset -= OnSessionGameplayCountersReset;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Instance = null;
        }
    }

    private void Update()
    {
        if (IsTutorialSceneContextActive() && !TutorialSafetyActive)
        {
            if (ComputeTutorialSafetyActive())
                ApplySceneRules();
        }

        if (Time.unscaledTime >= _nextSampleTime)
        {
            _nextSampleTime = Time.unscaledTime + GetSampleIntervalSeconds();
            PushPerformanceSample();
        }

        if (Time.unscaledTime >= _nextEvalTime)
        {
            _nextEvalTime = Time.unscaledTime + Mathf.Max(0.5f, GetTuning().evaluateEverySeconds);
            TutorialSafetyActive = ComputeTutorialSafetyActive();
            float accDt = ComputeAccumulatedSampleDt();
            float score = ComputePerformanceScore(accDt);
            LastPerformanceScore = score;
            if (logEveryEvaluation)
                Debug.Log($"[DynamicDifficulty] eval tier={CurrentTier} score={score:0.00} rollWindowDt={accDt:0.0}s " +
                          $"platLock={LockFallingPlatformsForCurrentTier} tutorialSafety={TutorialSafetyActive}");
            TryUpdateTier(Time.unscaledTime, score);
            ApplySceneRules();
        }
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;

        var t = GetTuning();
        float accDt = ComputeAccumulatedSampleDt();
        float cooldownLeft = Mathf.Max(0f, t.tierChangeCooldownSeconds - (Time.unscaledTime - _lastTierChangeTime));

        GUILayout.BeginArea(new Rect(10, 10, 440, 260), GUI.skin.box);
        GUILayout.Label("Dynamic difficulty (debug)");
        GUILayout.Label($"Tier: {CurrentTier}  (VeryEasy=0, Easy=1, Normal=2, Hard=3)");
        GUILayout.Label($"Performance score: {LastPerformanceScore:F2}  (rough -1 ... strong +1)");
        GUILayout.Label($"Rolling window fill: {accDt:F0}s / {t.rollingWindowSeconds:F0}s max");
        GUILayout.Label($"Thresholds: VeryEasy≤{t.veryEasyThreshold:F2}  Easy≤{t.easyThreshold:F2}  Hard≥{t.hardThreshold:F2}  Hard↓≤{t.hardDemotionScore:F2}");
        GUILayout.Label($"Falling platforms locked (VeryEasy): {LockFallingPlatformsForCurrentTier}");
        GUILayout.Label($"Neutral score hold (|score|≤{t.neutralScoreHoldRadius:F2}): no tier change");
        GUILayout.Label($"Tutorial safety: {TutorialSafetyActive}");
        GUILayout.Label($"Enemy HP mult: {EnemyHpMultiplier:F2}");
        GUILayout.Label($"Next tier change allowed in: {cooldownLeft:F0}s (cooldown after a change)");
        GUILayout.Label("Tip: enable Log Every Evaluation + Log Tier Changes for Console proof.");
        GUILayout.EndArea();
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        _activeSceneName = newScene.name;
        _sceneEnterUnscaledTime = Time.unscaledTime;
        _tutorialEnemyHpAdjusted.Clear();

        _nextSampleTime = Time.unscaledTime + GetSampleIntervalSeconds();
        _nextEvalTime = Time.unscaledTime + Mathf.Max(0.5f, GetTuning().evaluateEverySeconds);

        if (ShouldResetPerformanceWindowOnSceneChange(oldScene, newScene))
            ResetSampling();

        ApplySceneRules();
    }

    /// <summary>
    /// <see cref="DataCollectionService.SaveSessionAndStartNew"/> zeros counters; rebaseline so the next sample is not a bogus negative delta.
    /// </summary>
    private void OnSessionGameplayCountersReset()
    {
        _lastSnapshot = CaptureSnapshot();
    }

    /// <summary>
    /// Clears the rolling difficulty window only when starting (or returning to) the first level from elsewhere —
    /// not on progression between level scenes, and not when reloading the same tutorial scene.
    /// </summary>
    private static bool ShouldResetPerformanceWindowOnSceneChange(Scene oldScene, Scene newScene)
    {
        if (!IsPrimaryTutorialScene(newScene.name)) return false;
        if (!oldScene.IsValid()) return true;
        if (string.Equals(oldScene.name, newScene.name, StringComparison.OrdinalIgnoreCase))
            return false;
        return !IsPrimaryTutorialScene(oldScene.name);
    }

    private float GetSampleIntervalSeconds()
    {
        var t = GetTuning();
        if (t.sampleEverySeconds > 0f)
            return Mathf.Max(0.25f, t.sampleEverySeconds);
        return Mathf.Max(0.5f, t.evaluateEverySeconds);
    }

    private float ComputeAccumulatedSampleDt()
    {
        float accDt = 0f;
        foreach (var sm in _samples)
            accDt += sm.dt;
        return accDt;
    }

    private DifficultyTuning GetTuning()
    {
        if (tuning != null) return tuning;

        var loaded = Resources.Load<DifficultyTuning>("DifficultyTuning");
        if (loaded != null) tuning = loaded;

        if (tuning == null)
            tuning = ScriptableObject.CreateInstance<DifficultyTuning>();

        return tuning;
    }

    private DifficultyTuning.TierMultipliers GetCurrentMultipliers()
    {
        var t = GetTuning();
        if (IsTutorialSafetySceneActive(out _))
        {
            var baseMult = t.GetMultipliers(CurrentTier);
            baseMult.enemyHpMultiplier *= Mathf.Clamp(t.tutorialEnemyHpMultiplier, 0.1f, 1f);
            return baseMult;
        }
        return t.GetMultipliers(CurrentTier);
    }

    public float GetEnemyHpMultiplierForScene(string sceneName)
    {
        var t = GetTuning();
        if (TutorialSafetyActive && IsPrimaryTutorialScene(sceneName))
            return t.GetMultipliers(CurrentTier).enemyHpMultiplier * Mathf.Clamp(t.tutorialEnemyHpMultiplier, 0.1f, 1f);
        return t.GetMultipliers(CurrentTier).enemyHpMultiplier;
    }

    public int ScaleHealingAmount(int baseAmount)
    {
        if (baseAmount <= 0) return baseAmount;
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * HealingMultiplier));
    }

    private void ResetSampling()
    {
        _samples.Clear();
        _lastSnapshot = CaptureSnapshot();
    }

    private Snapshot CaptureSnapshot()
    {
        var s = new Snapshot { t = Time.unscaledTime };
        var d = DataCollectionService.Instance;
        if (d == null) return s;

        s.deaths = d.DeathCount;
        s.damageTaken = d.DamageTakenTotal;
        s.trapHits = d.TrapHits;
        s.meleeAttacks = d.MeleeAttacks;
        s.meleeHits = d.MeleeHits;
        s.spellCasts = d.SpellCasts;
        s.spellHits = d.SpellHits;
        s.enemyKills = d.EnemyKillCount;
        return s;
    }

    private void PushPerformanceSample()
    {
        var current = CaptureSnapshot();

        var dt = Mathf.Max(0.001f, current.t - _lastSnapshot.t);
        var sample = new Sample
        {
            dt = dt,
            dDeaths = Mathf.Max(0, current.deaths - _lastSnapshot.deaths),
            dDamage = Mathf.Max(0, current.damageTaken - _lastSnapshot.damageTaken),
            dTrapHits = Mathf.Max(0, current.trapHits - _lastSnapshot.trapHits),
            dMeleeAttacks = Mathf.Max(0, current.meleeAttacks - _lastSnapshot.meleeAttacks),
            dMeleeHits = Mathf.Max(0, current.meleeHits - _lastSnapshot.meleeHits),
            dSpellCasts = Mathf.Max(0, current.spellCasts - _lastSnapshot.spellCasts),
            dSpellHits = Mathf.Max(0, current.spellHits - _lastSnapshot.spellHits),
            dEnemyKills = Mathf.Max(0, current.enemyKills - _lastSnapshot.enemyKills)
        };

        _lastSnapshot = current;
        _samples.Enqueue(sample);

        var window = Mathf.Max(10f, GetTuning().rollingWindowSeconds);
        float accDt = 0f;
        foreach (var sm in _samples) accDt += sm.dt;
        while (_samples.Count > 1 && accDt > window)
        {
            var removed = _samples.Dequeue();
            accDt -= removed.dt;
        }

        TutorialSafetyActive = ComputeTutorialSafetyActive();
    }

    private float ComputePerformanceScore(float accDt)
    {
        if (accDt <= 0.01f) return 0f;

        int dDeaths = 0, dDamage = 0, dTrap = 0, dMeleeH = 0, dSpellH = 0, dKills = 0;
        foreach (var s in _samples)
        {
            dDeaths += s.dDeaths;
            dDamage += s.dDamage;
            dTrap += s.dTrapHits;
            dMeleeH += s.dMeleeHits;
            dSpellH += s.dSpellHits;
            dKills += s.dEnemyKills;
        }

        float minutes = accDt / 60f;
        float deathsPerMin = dDeaths / Mathf.Max(0.001f, minutes);
        float damagePerMin = dDamage / Mathf.Max(0.001f, minutes);
        float trapsPerMin = dTrap / Mathf.Max(0.001f, minutes);
        float meleeHitsPerMin = dMeleeH / Mathf.Max(0.001f, minutes);
        float spellHitsPerMin = dSpellH / Mathf.Max(0.001f, minutes);
        float killsPerMin = dKills / Mathf.Max(0.001f, minutes);

        var tun = GetTuning();
        float score = 0f;
        score += Mathf.Clamp(killsPerMin * tun.scoreEnemyKillPerMinuteScale, 0f, tun.scoreEnemyKillRewardMax);
        score += Mathf.Clamp(meleeHitsPerMin * tun.scoreMeleeHitPerMinuteScale, 0f, tun.scoreMeleeHitRewardMax);
        score += Mathf.Clamp(spellHitsPerMin * tun.scoreSpellHitPerMinuteScale, 0f, tun.scoreSpellHitRewardMax);
        score -= Mathf.Clamp(deathsPerMin * tun.scoreDeathsPerMinuteScale, 0f, tun.scoreDeathPenaltyMax);
        score -= Mathf.Clamp(damagePerMin / Mathf.Max(1f, tun.scoreDamagePerMinuteDivisor), 0f, tun.scoreDamagePenaltyMax);
        score -= Mathf.Clamp(trapsPerMin * tun.scoreTrapsPerMinuteScale, 0f, tun.scoreTrapPenaltyMax);

        return Mathf.Clamp(score, -1f, 1f);
    }

    private void TryUpdateTier(float now, float score)
    {
        var t = GetTuning();
        if (now - _lastTierChangeTime < Mathf.Max(0f, t.tierChangeCooldownSeconds))
            return;

        float neutral = Mathf.Max(0f, t.neutralScoreHoldRadius);
        if (Mathf.Abs(score) <= neutral)
            return;

        var oldTier = CurrentTier;
        var margin = Mathf.Max(0f, t.hysteresisMargin);

        switch (CurrentTier)
        {
            case DifficultyTier.VeryEasy:
                if (score > t.veryEasyThreshold + margin)
                    CurrentTier = DifficultyTier.Easy;
                break;
            case DifficultyTier.Easy:
                if (score <= t.veryEasyThreshold - margin)
                    CurrentTier = DifficultyTier.VeryEasy;
                else if (score > t.easyThreshold + margin)
                    CurrentTier = DifficultyTier.Normal;
                break;
            case DifficultyTier.Normal:
                if (score <= t.veryEasyThreshold - margin)
                    CurrentTier = DifficultyTier.VeryEasy;
                else if (score <= t.easyThreshold - margin)
                    CurrentTier = DifficultyTier.Easy;
                else if (score >= t.hardThreshold + margin)
                    CurrentTier = DifficultyTier.Hard;
                break;
            case DifficultyTier.Hard:
                if (score <= t.hardDemotionScore - margin)
                    CurrentTier = DifficultyTier.Normal;
                break;
        }

        if (CurrentTier != oldTier)
        {
            _lastTierChangeTime = now;
            if (logTierChanges)
                Debug.Log($"[DynamicDifficulty] Tier changed {oldTier} -> {CurrentTier} (score={score:0.00})");
            OnDifficultyChanged?.Invoke();
        }
    }

    private bool ComputeTutorialSafetyActive()
    {
        if (!IsTutorialSafetySceneActive(out var elapsed)) return false;
        var t = GetTuning();
        if (!t.tutorialSafetyEnabled) return false;

        // Safety is activated explicitly by the player dying in a tutorial scene (see PlayerHealth).
        // Do not auto-enable by time-in-scene; if the player never dies, hazards/platforms should behave normally.
        return PlayerPrefs.GetInt(TutorialSafetyPlayerPrefsKey, 0) == 1;
    }

    private bool IsTutorialSafetySceneActive(out float elapsedInScene)
    {
        elapsedInScene = Mathf.Max(0f, Time.unscaledTime - _sceneEnterUnscaledTime);
        return IsTutorialSceneContextActive();
    }

    private void ApplySceneRules()
    {
        if (!IsTutorialSceneContextActive())
            return;

        bool wasSafety = TutorialSafetyActive;
        TutorialSafetyActive = ComputeTutorialSafetyActive();
        if (TutorialSafetyActive != wasSafety)
            OnDifficultyChanged?.Invoke();
        if (!TutorialSafetyActive)
            return;

        // Do not disable FallingPlatform / MovingFallingPlatform here — disabling the component
        // breaks rewind registration and does not reliably block fall logic. Platforms lock via
        // PlayerPrefs + RefreshTutorialSafetyLock in their own scripts.

        foreach (var spike in FindObjectsOfType<SpikeDamage>(true))
        {
            spike.SetDamageAmount(0);
            spike.SetRespawnPlayer(false);
        }

        // Traps should still deal damage (eg. swinging blades). Restore any traps that may have been
        // zeroed by earlier builds / play sessions during this run.
        foreach (var trap in FindObjectsOfType<TrapDamage>(true))
            trap.RestoreDefaultDamage();

        var t = GetTuning();
        float extraMult = Mathf.Clamp(t.tutorialEnemyHpMultiplier, 0.1f, 1f);
        foreach (var enemy in FindObjectsOfType<EnemyBase>(true))
        {
            if (enemy.immuneToDifficultyScaling) continue;
            int id = enemy.GetInstanceID();
            if (_tutorialEnemyHpAdjusted.Contains(id)) continue;
            _tutorialEnemyHpAdjusted.Add(id);

            enemy.health = Mathf.Max(1, Mathf.RoundToInt(enemy.health * extraMult));
            enemy.startHealth = Mathf.Max(1, Mathf.RoundToInt(enemy.startHealth * extraMult));
        }
    }

    private bool IsInTutorialScene(string sceneName) => IsSceneNameTutorial(sceneName);

    /// <summary>
    /// Used by falling platforms (Awake may run before Instance exists). Same rules as <see cref="DifficultyTuning.tutorialSceneNames"/>.
    /// </summary>
    public static bool IsSceneNameTutorial(string sceneName)
    {
        if (IsPrimaryTutorialScene(sceneName))
            return true;

        if (Instance != null)
        {
            var t = Instance.GetTuning();
            if (t.tutorialSceneNames == null || t.tutorialSceneNames.Length == 0)
                return false;
            for (int i = 0; i < t.tutorialSceneNames.Length; i++)
            {
                if (string.Equals(sceneName, t.tutorialSceneNames[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        var tuning = Resources.Load<DifficultyTuning>("DifficultyTuning");
        if (tuning == null)
            tuning = ScriptableObject.CreateInstance<DifficultyTuning>();
        if (tuning.tutorialSceneNames == null || tuning.tutorialSceneNames.Length == 0)
            return false;
        for (int i = 0; i < tuning.tutorialSceneNames.Length; i++)
        {
            if (string.Equals(sceneName, tuning.tutorialSceneNames[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
