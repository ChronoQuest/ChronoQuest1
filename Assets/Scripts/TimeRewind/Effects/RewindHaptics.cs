using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TimeRewind;

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

    [Header("Heartbeat Audio")]

    [Tooltip("AudioClip for the strong beat (LUB). Played in sync with the strong motor pulse.")]
    public AudioClip lubClip;

    [Tooltip("AudioClip for the weak beat (dub). Played in sync with the weak motor pulse.")]
    public AudioClip dubClip;

    [Range(0f, 1f)]
    [Tooltip("Volume of heartbeat sounds during normal (low-health) mode.")]
    public float heartbeatVolume = 0.6f;

    [Range(0f, 1f)]
    [Tooltip("Volume of heartbeat sounds during rewind mode.")]
    public float rewindHeartbeatVolume = 0.5f;

    [Header("Muffled Audio Effect")]

    [Tooltip("Apply a low-pass filter to the music when the heartbeat is active.")]
    public bool muffle = true;

    [Range(200f, 5000f)]
    [Tooltip("Low-pass cutoff frequency while muffled. Lower = more underwater.")]
    public float muffleCutoffFrequency = 800f;

    #endregion

    private Coroutine currentRoutine;
    private Coroutine hintTimeoutRoutine;
    private AudioSource lubSource;
    private AudioSource dubSource;
    private RewindMusicController _musicController;

    private enum HeartMode
    {
        None,
        ReverseHeartbeat,
        NormalHeartbeat
    }

    private HeartMode activeMode = HeartMode.None;
    private bool _useAudio;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            lubSource = gameObject.AddComponent<AudioSource>();
            lubSource.playOnAwake = false;
            lubSource.loop = false;
            lubSource.spatialBlend = 0f;

            dubSource = gameObject.AddComponent<AudioSource>();
            dubSource.playOnAwake = false;
            dubSource.loop = false;
            dubSource.spatialBlend = 0f;
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
        _useAudio = true;
        StartMode(HeartMode.ReverseHeartbeat);
    }

    public void StopRewindPulse()
    {
        StopAllHaptics();
    }

    // Called by PlayerHealth or Hint systems
    public void StartHintHeartbeat(float duration = -1f)
    {
        // Audio + muffle only for low-health (no duration).
        // Timed hints (RewindHintZone, PlayerFallHint, etc.) get vibration only.
        _useAudio = duration < 0f;
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

        if (_useAudio)
            SetMuffledEffect(true);

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
        SetMuffledEffect(false);
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

            // dub FIRST (weak), reversed clip
            yield return Beat(strength * 0.7f, weakBeatDuration, dubClip, rewindHeartbeatVolume, reverse: true);

            // Wait Gap
            yield return PausableWait(beatGap);

            // LUB SECOND (strong), reversed clip
            yield return Beat(strength, strongBeatDuration, lubClip, rewindHeartbeatVolume, reverse: true);

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
            yield return Beat(strength, strongBeatDuration, lubClip, heartbeatVolume, reverse: false);

            // Wait Gap
            yield return PausableWait(beatGap);

            // dub SECOND (weak)
            yield return Beat(strength * 0.7f, weakBeatDuration, dubClip, heartbeatVolume, reverse: false);

            // Long biological pause
            yield return PausableWait(baseBeatInterval);
        }
    }

    #endregion

    #region Beat Execution

    private IEnumerator Beat(float strength, float duration, AudioClip clip = null, float volume = 0.5f, bool reverse = false)
    {
        if (PauseMenu.isPaused)
            yield break;
        if (Gamepad.current != null)
                {
                    float low = strength * lowMotorWeight;
                    float high = strength * highMotorWeight;
                    Gamepad.current.SetMotorSpeeds(low, high);
                }
        // Pick the right source so lub and dub can overlap
        AudioSource source = (clip == lubClip) ? lubSource : dubSource;

        if (_useAudio && source != null && clip != null)
        {
            source.clip = clip;
            source.volume = volume;

            if (reverse)
            {
                source.pitch = -1f;
                source.time = Mathf.Max(0.01f, clip.length - 0.01f);
            }
            else
            {
                source.pitch = 1f;
                source.time = 0f;
            }

            source.Play();
        }

       

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

    #region Muffled Effect

    private void SetMuffledEffect(bool on)
    {
        if (!muffle)
            return;

        if (_musicController == null)
            _musicController = FindFirstObjectByType<RewindMusicController>();

        if (_musicController != null)
            _musicController.SetMuffled(on, muffleCutoffFrequency);
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