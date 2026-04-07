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
    public float beatGap = 0.1f; // Changed from 0.1f to 0.2f

    [Header("Beat Durations")]

    // Increased so the motor has physical time to spool up
    public float strongBeatDuration = 0.12f; // Changed from 0.05f
    public float weakBeatDuration = 0.08f;   // Changed from 0.03f

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

                currentRoutine =
                    StartCoroutine(ReverseHeartbeatRoutine());

                break;

            case HeartMode.NormalHeartbeat:

                currentRoutine =
                    StartCoroutine(NormalHeartbeatRoutine());

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

            float normalized =
                Mathf.InverseLerp(0.5f, 4f, speed);

            float interval =
                Mathf.Lerp(
                    baseBeatInterval,
                    fastBeatInterval,
                    normalized
                );

            float strength =
                baseStrength *
                globalStrengthScale;

            // dub FIRST (weak)

            yield return Beat(
                strength * 0.7f,
                weakBeatDuration
            );

            yield return new WaitForSecondsRealtime(
                beatGap
            );

            // LUB SECOND (strong)

            yield return Beat(
                strength,
                strongBeatDuration
            );

            // Speed-scaled pause

            yield return new WaitForSecondsRealtime(
                interval
            );
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

            float strength =
                baseStrength *
                globalStrengthScale;

            // LUB FIRST (strong)

            yield return Beat(
                strength,
                strongBeatDuration
            );

            yield return new WaitForSecondsRealtime(
                beatGap
            );

            // dub SECOND (weak)

            yield return Beat(
                strength * 0.7f,
                weakBeatDuration
            );

            // Long biological pause

            yield return new WaitForSecondsRealtime(
                baseBeatInterval
            );
        }
    }

    #endregion

    #region Beat Execution

    private IEnumerator Beat(
        float strength,
        float duration)
    {
        if (Gamepad.current == null)
            yield break;

        float low =
            strength * lowMotorWeight;

        float high =
            strength * highMotorWeight;

        Gamepad.current.SetMotorSpeeds(
            low,
            high
        );

        yield return new WaitForSecondsRealtime(
            duration
        );

        StopMotors();
    }

    private void StopMotors()
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(
                0f,
                0f
            );
        }
    }

    #endregion

    #region Hint Timeout

    private IEnumerator HintTimeout(float duration)
    {
        yield return new WaitForSecondsRealtime(
            duration
        );

        StopHintHeartbeat();
    }

    #endregion

    #region Helpers

    private float GetRewindSpeed()
    {
        var mgr =
            TimeRewind.TimeRewindManager.Instance;

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