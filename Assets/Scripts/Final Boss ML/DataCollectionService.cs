using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// Central, lightweight data collector for player-behaviour features.
/// Stores in-memory counters during play and flushes a single JSONL row per session
/// to Application.persistentDataPath for offline analysis (e.g., Python/Scikit-Learn).
/// </summary>
public class DataCollectionService : MonoBehaviour
{
    public static DataCollectionService Instance { get; private set; }

    /// <summary>Fired after in-memory gameplay counters are zeroed (e.g. door transition session split). Dynamic difficulty rebaselines against this so deltas stay valid.</summary>
    public static event System.Action SessionGameplayCountersReset;

    [Header("Export Settings")]
    [Tooltip("File name (within Application.persistentDataPath) for JSONL export.")]
    [SerializeField] private string fileName = "session_data.jsonl";

    [Tooltip("Log basic info when writing data to disk (including the file path).")]
    [SerializeField] private bool enableDebugLogs = false;

    /// <summary>Folder where session data is saved (Application.persistentDataPath).</summary>
    public static string ExportFolderPath => Application.persistentDataPath;

    /// <summary>Full path of the export file (uses configured fileName). Valid when Instance exists.</summary>
    public string GetExportFilePath() => Path.Combine(Application.persistentDataPath, fileName);

    /// <summary>Opens the folder where session data is saved in the system file manager (Explorer, Finder, etc.).</summary>
    [ContextMenu("Open data folder in Explorer")]
    public static void OpenExportFolder()
    {
        string path = Application.persistentDataPath;
        if (!Directory.Exists(path))
        {
            UnityEngine.Debug.LogWarning($"[DataCollection] Folder does not exist yet: {path}. Play the game and quit once to create it.");
            return;
        }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        Process.Start("explorer.exe", $"\"{path}\"");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        Process.Start("open", $"\"{path}\"");
#else
        Process.Start(path);
#endif
        UnityEngine.Debug.Log($"[DataCollection] Opened folder: {path}");
    }

    // Session identity and timing
    private string _sessionId;
    private float _sessionStartTime;
    private bool _sessionActive;

    // Rewind timing
    private bool _rewindInProgress;
    private float _rewindStartTime;

    // Movement
    private int _dashCount;
    private int _jumpCount;
    private int _wallJumpCount;
    private int _doubleJumpCount;
    private int _rewindActivationCount;
    private float _rewindDurationSeconds;

    // Combat
    private int _meleeAttacks;
    private int _meleeHits;
    private int _spellCasts;
    private int _spellHits;
    private int _rainAttackUses;
    private int _enemyKills;

    // Health / deaths
    private int _damageTakenTotal;
    private int _deathCount;

    // Interaction
    private int _doorsEntered;
    private int _trapHits;
    private int _tutorialStepsCompleted;
    private int _pauseCount;

    private bool _isPrimaryInstance;

    [Serializable]
    private class SessionDataRecord
    {
        public string session_id;
        public float session_duration_seconds;

        public int dash_count;
        public int jump_count;
        public int wall_jump_count;
        public int double_jump_count;
        public int rewind_activation_count;
        public float rewind_duration_seconds;

        public int melee_attacks;
        public int melee_hits;
        public int spell_casts;
        public int spell_hits;
        public int rain_attack_uses;
        public int enemy_kills;

        public int damage_taken_total;
        public int death_count;

        public int doors_entered;
        public int trap_hits;
        public int tutorial_steps_completed;
        public int pause_count;

        // Filled after clustering (left empty in-game)
        public string persona_label;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Secondary instance: destroy without flushing.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _isPrimaryInstance = true;
        DontDestroyOnLoad(gameObject);

        _sessionId = Guid.NewGuid().ToString();
        _sessionStartTime = Time.unscaledTime;
        _sessionActive = true;

        if (enableDebugLogs)
            UnityEngine.Debug.Log($"[DataCollection] Session started. Data will be written to: {GetExportFilePath()}");
    }

    private void OnApplicationQuit()
    {
        FlushSessionIfNeeded();
    }

    private void OnDisable()
    {
        FlushSessionIfNeeded();
    }

    #region Public recording API

    // Movement
    public void RecordDash()
    {
        if (!_sessionActive) return;
        _dashCount++;
    }

    /// <summary>
    /// Records a jump. Flags can be used to distinguish wall and double jumps.
    /// </summary>
    public void RecordJump(bool isWallJump, bool usedDoubleJump)
    {
        if (!_sessionActive) return;
        _jumpCount++;
        if (isWallJump) _wallJumpCount++;
        if (usedDoubleJump) _doubleJumpCount++;
    }

    // Rewind
    public void RecordRewindStarted()
    {
        if (!_sessionActive) return;
        _rewindActivationCount++;
        if (!_rewindInProgress)
        {
            _rewindInProgress = true;
            _rewindStartTime = Time.unscaledTime;
        }
    }

    public void RecordRewindStopped()
    {
        if (!_sessionActive) return;
        if (_rewindInProgress)
        {
            float delta = Time.unscaledTime - _rewindStartTime;
            if (delta > 0f)
            {
                _rewindDurationSeconds += delta;
            }
            _rewindInProgress = false;
        }
    }

    // Combat
    public void RecordMeleeAttempt()
    {
        if (!_sessionActive) return;
        _meleeAttacks++;
    }

    public void RecordMeleeHit()
    {
        if (!_sessionActive) return;
        _meleeHits++;
    }

    public void RecordSpellCast()
    {
        if (!_sessionActive) return;
        _spellCasts++;
    }

    public void RecordSpellHit()
    {
        if (!_sessionActive) return;
        _spellHits++;
    }

    public void RecordRainAttackUse()
    {
        if (!_sessionActive) return;
        _rainAttackUses++;
    }

    public void RecordEnemyKill()
    {
        if (!_sessionActive) return;
        _enemyKills++;
    }

    // Health / deaths
    public void RecordDamageTaken(int amount)
    {
        if (!_sessionActive) return;
        if (amount <= 0) return;
        _damageTakenTotal += amount;
    }

    public void RecordDeath()
    {
        if (!_sessionActive) return;
        _deathCount++;
    }

    // Interaction
    public void RecordDoorEntered(string sceneName)
    {
        if (!_sessionActive) return;
        _doorsEntered++;
    }

    public void RecordTrapHit()
    {
        if (!_sessionActive) return;
        _trapHits++;
    }

    public void RecordTutorialStepCompleted()
    {
        if (!_sessionActive) return;
        _tutorialStepsCompleted++;
    }

    public void RecordPause()
    {
        if (!_sessionActive) return;
        _pauseCount++;
    }

    /// <summary>
    /// Writes the current session to file immediately, then starts a new session (e.g. call when player passes through a door).
    /// Use this so data is saved before a scene load or other transition.
    /// </summary>
    public void SaveSessionAndStartNew()
    {
        if (!_isPrimaryInstance) return;
        if (!_sessionActive) return;
        WriteCurrentSessionToFile();
        StartNewSession();
    }

    #endregion

    #region Internal helpers

    private void WriteCurrentSessionToFile()
    {
        if (!_isPrimaryInstance) return;

        var record = new SessionDataRecord
        {
            session_id = _sessionId,
            session_duration_seconds = Mathf.Max(0f, Time.unscaledTime - _sessionStartTime),

            dash_count = _dashCount,
            jump_count = _jumpCount,
            wall_jump_count = _wallJumpCount,
            double_jump_count = _doubleJumpCount,
            rewind_activation_count = _rewindActivationCount,
            rewind_duration_seconds = _rewindDurationSeconds,

            melee_attacks = _meleeAttacks,
            melee_hits = _meleeHits,
            spell_casts = _spellCasts,
            spell_hits = _spellHits,
            rain_attack_uses = _rainAttackUses,
            enemy_kills = _enemyKills,

            damage_taken_total = _damageTakenTotal,
            death_count = _deathCount,

            doors_entered = _doorsEntered,
            trap_hits = _trapHits,
            tutorial_steps_completed = _tutorialStepsCompleted,
            pause_count = _pauseCount,

            persona_label = string.Empty
        };

        string json = JsonUtility.ToJson(record);

        try
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.AppendAllText(path, json + Environment.NewLine);
            if (enableDebugLogs)
            {
                UnityEngine.Debug.Log($"[DataCollection] Wrote session data to: {path}");
            }
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
            {
                UnityEngine.Debug.LogWarning($"[DataCollection] Failed to write data: {ex.Message}");
            }
        }
    }

    private void StartNewSession()
    {
        _sessionId = Guid.NewGuid().ToString();
        _sessionStartTime = Time.unscaledTime;

        _dashCount = 0;
        _jumpCount = 0;
        _wallJumpCount = 0;
        _doubleJumpCount = 0;
        _rewindActivationCount = 0;
        _rewindDurationSeconds = 0f;
        _rewindInProgress = false;

        _meleeAttacks = 0;
        _meleeHits = 0;
        _spellCasts = 0;
        _spellHits = 0;
        _rainAttackUses = 0;
        _enemyKills = 0;

        _damageTakenTotal = 0;
        _deathCount = 0;

        _doorsEntered = 0;
        _trapHits = 0;
        _tutorialStepsCompleted = 0;
        _pauseCount = 0;

        if (enableDebugLogs)
            UnityEngine.Debug.Log($"[DataCollection] New session started. Data will be written to: {GetExportFilePath()}");

        SessionGameplayCountersReset?.Invoke();
    }

    private void FlushSessionIfNeeded()
    {
        if (!_isPrimaryInstance) return;
        if (!_sessionActive) return;

        _sessionActive = false;
        WriteCurrentSessionToFile();
    }

    #endregion

    public int DashCount => _dashCount;
    public int JumpCount => _jumpCount;
    public int WallJumpCount => _wallJumpCount;
    public int DoubleJumpCount => _doubleJumpCount;
    public int RewindActivationCount => _rewindActivationCount;
    public float RewindDurationSeconds => _rewindDurationSeconds;

    public int MeleeAttacks => _meleeAttacks;
    public int MeleeHits => _meleeHits;
    public int SpellCasts => _spellCasts;
    public int SpellHits => _spellHits;
    public int RainAttackUses => _rainAttackUses;
    public int EnemyKillCount => _enemyKills;

    public int DamageTakenTotal => _damageTakenTotal;
    public int DeathCount => _deathCount;

    public int DoorsEntered => _doorsEntered;
    public int TrapHits => _trapHits;
    public int TutorialStepsCompleted => _tutorialStepsCompleted;
    public int PauseCount => _pauseCount;

    public float SessionDurationSeconds => Time.unscaledTime - _sessionStartTime;
}
