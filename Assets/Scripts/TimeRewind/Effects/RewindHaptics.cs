using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class RewindHaptics : MonoBehaviour
{
    #region Singleton (Auto-Created)

    private static RewindHaptics _instance;

    public static RewindHaptics Instance
    {
        get
        {
            if (!Application.isPlaying) return null;
            if (_instance == null)
            {
                CreateInstance();
            }

            return _instance;
        }
    }

    private static void CreateInstance()
    {
        var go = new GameObject("RewindHaptics");
        _instance = go.AddComponent<RewindHaptics>();
        DontDestroyOnLoad(go);
    }

    #endregion

    #region Settings

    [Header("Heartbeat Strength")]

    [Range(0.01f, 0.1f)]
    public float baseStrength = 0.035f;

    [Range(0f, 1f)]
    public float globalStrengthScale = 0.45f;

    [Header("Timing")]

    [Tooltip("Pause after double beat (normal heartbeat)")]
    public float baseBeatInterval = 0.75f;

    [Tooltip("Fastest pause during rewind")]
    public float fastBeatInterval = 0.28f;

    [Tooltip("Gap between the two beats - INCREASED for motor spin-down")]
    public float beatGap = 0.1f; 

    [Header("Beat Durations")]

    // Increased so the motor has physical time to spool up
    public float strongBeatDuration = 0.12f; 
    public float weakBeatDuration = 0.08f;   

    [Header("Motor Balance")]

    [Range(0f, 1f)]
    public float lowMotorWeight = 0.35f;

    [Range(0f, 1f)]
    public float highMotorWeight = 0.9f;

    #endregion

    private Coroutine currentRoutine;
    private Coroutine hintTimeoutRoutine;

    private enum HeartMode
    {
        None,
        ReverseHeartbeat,
        NormalHeartbeat
    }

    private HeartMode activeMode = HeartMode.None;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region Public API

    // Called by TimeRewindManager
    public void StartRewindPulse()
    {
        StartMode(HeartMode.ReverseHeartbeat);
    }

    public void StopRewindPulse()
    {
        StopAllHaptics();
    }

    // Called by PlayerHealth or Hint systems
    public void StartHintHeartbeat(float duration = -1f)
    {
        StartMode(HeartMode.NormalHeartbeat);

        if (duration > 0f)
        {
            if (hintTimeoutRoutine != null)
                StopCoroutine(hintTimeoutRoutine);

            hintTimeoutRoutine =
                StartCoroutine(HintTimeout(duration));
        }
    }

    public void StopHintHeartbeat()
    {
        if (activeMode == HeartMode.NormalHeartbeat)
        {
            StopAllHaptics();
        }
    }

    #endregion

    #region Mode Control

    private void StartMode(HeartMode mode)
    {
        if (activeMode == mode)
            return;

        StopAllHaptics();

        activeMode = mode;

        switch (mode)
        {
            case HeartMode.ReverseHeartbeat:
                currentRoutine = StartCoroutine(ReverseHeartbeatRoutine());
                break;

            case HeartMode.NormalHeartbeat:
                currentRoutine = StartCoroutine(NormalHeartbeatRoutine());
                break;
        }
    }

    private void StopAllHaptics()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        if (hintTimeoutRoutine != null)
        {
            StopCoroutine(hintTimeoutRoutine);
            hintTimeoutRoutine = null;
        }

        StopMotors();
        activeMode = HeartMode.None;
    }

    #endregion

    #region Reverse Heartbeat (Rewind)

    private IEnumerator ReverseHeartbeatRoutine()
    {
        while (true)
        {
            if (Gamepad.current == null)
            {
                yield return null;
                continue;
            }

            float speed = GetRewindSpeed();
            float normalized = Mathf.InverseLerp(0.5f, 4f, speed);
            float interval = Mathf.Lerp(baseBeatInterval, fastBeatInterval, normalized);
            float strength = baseStrength * globalStrengthScale;

            // dub FIRST (weak)
            yield return Beat(strength * 0.7f, weakBeatDuration);

            // Wait Gap
            yield return PausableWait(beatGap);

            // LUB SECOND (strong)
            yield return Beat(strength, strongBeatDuration);

            // Speed-scaled pause
            yield return PausableWait(interval);
        }
    }

    #endregion

    #region Normal Heartbeat (Hint)

    private IEnumerator NormalHeartbeatRoutine()
    {
        while (true)
        {
            if (Gamepad.current == null)
            {
                yield return null;
                continue;
            }

            float strength = baseStrength * globalStrengthScale;

            // LUB FIRST (strong)
            yield return Beat(strength, strongBeatDuration);

            // Wait Gap
            yield return PausableWait(beatGap);

            // dub SECOND (weak)
            yield return Beat(strength * 0.7f, weakBeatDuration);

            // Long biological pause
            yield return PausableWait(baseBeatInterval);
        }
    }

    #endregion

    #region Beat Execution

    private IEnumerator Beat(float strength, float duration)
    {
        if (Gamepad.current == null || PauseMenu.isPaused)
            yield break;

        float low = strength * lowMotorWeight;
        float high = strength * highMotorWeight;

        Gamepad.current.SetMotorSpeeds(low, high);

        // Custom timer instead of WaitForSeconds so we can abort mid-beat if paused
        float timer = 0f;
        while (timer < duration)
        {
            if (PauseMenu.isPaused)
            {
                StopMotors();
                yield break; // Instantly abort this beat if the player pauses
            }
            
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        StopMotors();
    }

    private void StopMotors()
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }

    #endregion

    #region Hint Timeout

    private IEnumerator HintTimeout(float duration)
    {
        yield return PausableWait(duration);
        StopHintHeartbeat();
    }

    #endregion

    #region Helpers

    // --- THE FIX: Custom wait routine that freezes the timer when paused ---
    private IEnumerator PausableWait(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (PauseMenu.isPaused)
            {
                StopMotors(); // Failsafe to ensure motors die while paused
                yield return null; // Wait a frame but DO NOT advance the timer
                continue;
            }
            
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private float GetRewindSpeed()
    {
        var mgr = TimeRewind.TimeRewindManager.Instance;

        if (mgr == null)
            return 1f;

        return mgr.RewindSpeed;
    }

    #endregion

    #region Safety

    private void OnDisable()
    {
        StopAllHaptics();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            StopAllHaptics();
        }
    }

    #endregion
}