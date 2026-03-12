using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeRewind
{
    public class TimeRewindManager : MonoBehaviour
    {
        #region Singleton
        
        private static TimeRewindManager _instance;
        
        public static TimeRewindManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<TimeRewindManager>();
                    
                    if (_instance == null)
                    {
                        var go = new GameObject("TimeRewindManager");
                        _instance = go.AddComponent<TimeRewindManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                
                _instance.EnsureInitialized();
                
                return _instance;
            }
        }
        
        #endregion

        #region Configuration
        
        [Header("Rewind Settings")]
        [Tooltip("Maximum duration of rewind history in seconds")]
        [SerializeField] private float maxRewindDuration = 5f;
        
        [Tooltip("How many states to record per second (higher = smoother but more memory)")]
        [SerializeField] private int recordsPerSecond = 50;
        
        [Tooltip("Speed multiplier for rewinding (1 = real-time, 2 = double speed)")]
        [SerializeField] private float rewindSpeed = 1.3f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("Rewind Time Scale")]
        [Tooltip("Global timeScale while rewinding (1 = normal, 0.3 = strong slow-motion)")]
        [SerializeField] private float rewindSlowTimeScale = 0.5f;

        [Header("Dynamic Playback (Idle Fast-Forward)")]
        [Tooltip("Minimum contiguous stationary time (seconds) in recorded history to treat as idle and speed up rewind")]
        [SerializeField] private float idleFastForwardThresholdSeconds = 1.5f;
        [Tooltip("Rewind speed multiplier when inside an idle segment")]
        [SerializeField] private float idlePlaybackMultiplier = 2.5f;
        [Tooltip("Rate per second at which playback multiplier moves toward target for smooth transition")]
        [SerializeField] private float playbackTransitionSpeed = 3f;
        [Tooltip("Position delta below this = stationary")]
        [SerializeField] private float idlePositionThreshold = 0.01f;
        [Tooltip("Rotation delta in degrees below this = stationary")]
        [SerializeField] private float idleRotationThresholdDegrees = 0.1f;
        
        #endregion

        #region State
        
        private Dictionary<IRewindable, RewindBuffer<RewindState>> _rewindables;
        private bool _isRewinding;
        private float _currentRewindTime;
        private float _recordTimer;
        private float _recordInterval;
        private bool _initialized;
        private bool _recordingEnabled = true; 
        
        // Cached time scale used during rewind so we can restore it afterwards
        private float _cachedTimeScale = 1f;

        private float _currentPlaybackMultiplier = 1f;
        
        #endregion

        #region Properties
        
        public bool IsRewinding => _isRewinding;
        public float CurrentRewindTime => _currentRewindTime;
        
        public bool CanRewind
        {
            get
            {
                if (_rewindables == null || _rewindables.Count == 0)
                    return false;
                    
                foreach (var kvp in _rewindables)
                {
                    if (kvp.Value.HasStates)
                        return true;
                }
                return false;
            }
        }
        
        public float RewindProgress
        {
            get
            {
                if (!_isRewinding || !CanRewind)
                    return 0f;
                
                float oldestTime = GetOldestRecordedTime();
                float newestTime = GetNewestRecordedTime();
                float totalDuration = newestTime - oldestTime;
                
                if (totalDuration <= 0)
                    return 0f;
                
                return 1f - ((_currentRewindTime - oldestTime) / totalDuration);
            }
        }
        
        public float RemainingRewindTime
        {
            get
            {
                if (!CanRewind)
                    return 0f;
                
                float oldestTime = GetOldestRecordedTime();
                return _currentRewindTime - oldestTime;
            }
        }
        
        public float MaxRewindDuration => maxRewindDuration;
        
        #endregion

        #region Events
        
        public event Action OnRewindStart;
        public event Action OnRewindStop;
        public event Action<float> OnRewindProgress;
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            EnsureInitialized();
        }
        
        private void EnsureInitialized()
        {
            if (_initialized)
                return;
                
            _rewindables = new Dictionary<IRewindable, RewindBuffer<RewindState>>();
            _recordInterval = 1f / recordsPerSecond;
            _recordTimer = 0f;
            _initialized = true;
            
            if (enableDebugLogs)
                Debug.Log("[TimeRewind] Manager initialized");
        }
        
        private void Update()
        {
            if (PauseMenu.isPaused)
                return;
            
            if (_isRewinding)
            {
                UpdateRewind();
            }
        }
        
        private void FixedUpdate()
        {
            if (!_isRewinding && _recordingEnabled)
            {
                UpdateRecording();
            }
        }
        
        #endregion

        #region Public Methods
        
        public void Register(IRewindable rewindable)
        {
            if (rewindable == null)
                return;
                
            EnsureInitialized();
            
            if (_rewindables == null)
            {
                _rewindables = new Dictionary<IRewindable, RewindBuffer<RewindState>>();
            }
            
            if (_rewindables.ContainsKey(rewindable))
                return;
            
            int bufferCapacity = Mathf.CeilToInt(maxRewindDuration * recordsPerSecond);
            _rewindables[rewindable] = new RewindBuffer<RewindState>(bufferCapacity);
        }
        
        public void Unregister(IRewindable rewindable)
        {
            if (rewindable == null || _rewindables == null)
                return;
            
            _rewindables.Remove(rewindable);
        }
        
        public void StartRewind()
        {
            if (PauseMenu.isPaused)
                return; 
            
            if (_isRewinding)
            {
                if (enableDebugLogs)
                    Debug.Log("[TimeRewind] StartRewind called but already rewinding");
                return;
            }
            
            if (!CanRewind)
            {
                if (enableDebugLogs)
                    Debug.Log($"[TimeRewind] StartRewind called but CanRewind=false (registered: {_rewindables?.Count ?? 0})");
                return;

            }

                       // Cache current time scale and apply slow-motion during rewind
            _cachedTimeScale = Time.timeScale;
            Time.timeScale = rewindSlowTimeScale;
            
            _isRewinding = true;
            // _currentRewindTime = Time.time;
            _currentRewindTime = GetNewestRecordedTime(); 
            _currentPlaybackMultiplier = 1f;

            if (enableDebugLogs)
                Debug.Log($"[TimeRewind] Rewind STARTED at time {_currentRewindTime:F2}");
            
            foreach (var rewindable in _rewindables.Keys)
            {
                var mb = rewindable as MonoBehaviour;
                if (mb != null)
                    rewindable.OnStartRewind();
            }
            
            OnRewindStart?.Invoke();
        }
        
        public void StopRewind()
        {
            if (!_isRewinding)
                return;

            if (PauseMenu.isPaused)
                return;
            
            _isRewinding = false;

            if (_cachedTimeScale <= 0f)
                _cachedTimeScale = 1f;

            Time.timeScale = _cachedTimeScale;
            
            TrimFutureStates();
            
            foreach (var rewindable in _rewindables.Keys)
            {
                var mb = rewindable as MonoBehaviour;
                if (mb != null)
                    rewindable.OnStopRewind();
            }
            
            OnRewindStop?.Invoke();
        }
        
        public void ClearHistory()
        {
            foreach (var buffer in _rewindables.Values)
            {
                buffer.Clear();
            }
        }

        public void PauseRecording()
        {
            _recordingEnabled = false;

            if (enableDebugLogs)
            {
                Debug.Log("[TimeRewind] Recording paused");
            }
        }

        public void ResumeRecording()
        {
            _recordingEnabled = true; 

            if (enableDebugLogs)
            {
                Debug.Log("[TimeRewind] Recording resumed");
            }

        }
        
        #endregion

        #region Private Methods
        
        private void UpdateRecording()
        {
            if (_rewindables == null || _rewindables.Count == 0)
                return;
                
            _recordTimer += Time.fixedDeltaTime;
            
            if (_recordTimer >= _recordInterval)
            {
                _recordTimer = 0f;
                RecordCurrentStates();
            }
        }
        
        private void RecordCurrentStates()
        {
            List<IRewindable> toRemove = null;
            
            foreach (var kvp in _rewindables)
            {
                var mb = kvp.Key as MonoBehaviour;
                if (mb == null)
                {
                    toRemove ??= new List<IRewindable>();
                    toRemove.Add(kvp.Key);
                    continue;
                }
                
                var state = kvp.Key.CaptureState();
                kvp.Value.Add(state);
            }
            
            if (toRemove != null)
            {
                foreach (var item in toRemove)
                {
                    _rewindables.Remove(item);
                }
            }
        }
        
        private void UpdateRewind()
        {
            if (PauseMenu.isPaused)
                return;

            float targetMultiplier = 1f;
            if (TryGetPlayerBuffer(out var playerBuffer) && playerBuffer.Count >= 2)
            {
                var idleSegments = GetIdleSegments(playerBuffer);
                if (IsTimeInIdleSegment(_currentRewindTime, idleSegments))
                    targetMultiplier = idlePlaybackMultiplier;
            }

            _currentPlaybackMultiplier = Mathf.MoveTowards(
                _currentPlaybackMultiplier,
                targetMultiplier,
                playbackTransitionSpeed * Time.deltaTime);

            _currentRewindTime -= Time.deltaTime * rewindSpeed * _currentPlaybackMultiplier;
            
            float oldestTime = GetOldestRecordedTime();
            
            if (_currentRewindTime < oldestTime)
            {
                _currentRewindTime = oldestTime;
            }
            
            // Snapshot the keys to avoid InvalidOperationException if a
            // rewindable is unregistered (e.g. destroyed) during iteration.
            var keys = new System.Collections.Generic.List<IRewindable>(_rewindables.Keys);
            
            foreach (var key in keys)
            {
                var mb = key as MonoBehaviour;
                if (mb == null)
                    continue;
                
                if (!_rewindables.TryGetValue(key, out var buffer))
                    continue;
                
                if (buffer.GetInterpolationStates(
                    _currentRewindTime,
                    s => s.Timestamp,
                    out var before,
                    out var after,
                    out float t))
                {
                    var interpolatedState = RewindState.Lerp(before, after, t);
                    key.ApplyState(interpolatedState);
                }
            }
            
            OnRewindProgress?.Invoke(RewindProgress);
        }
        
        private void TrimFutureStates()
        {
            foreach (var kvp in _rewindables)
            {
                var buffer = kvp.Value;
                
                if (!buffer.HasStates)
                    continue;
                
                int keepCount = 0;
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (buffer.Get(i).Timestamp <= _currentRewindTime)
                        keepCount = i + 1;
                    else
                        break;
                }
                
                buffer.TrimToCount(keepCount);
            }
        }
        
        private bool TryGetPlayerBuffer(out RewindBuffer<RewindState> buffer)
        {
            buffer = null;
            if (_rewindables == null)
                return false;
            foreach (var kvp in _rewindables)
            {
                if (kvp.Key is PlayerRewindController)
                {
                    buffer = kvp.Value;
                    return true;
                }
            }
            return false;
        }

        private bool AreStatesStationary(RewindState a, RewindState b)
        {
            if (Vector3.Distance(a.Position, b.Position) > idlePositionThreshold)
                return false;
            if (Quaternion.Angle(a.Rotation, b.Rotation) > idleRotationThresholdDegrees)
                return false;
            return true;
        }

        private List<(float start, float end)> GetIdleSegments(RewindBuffer<RewindState> playerBuffer)
        {
            var segments = new List<(float start, float end)>();
            int runStart = -1;
            for (int i = 0; i < playerBuffer.Count - 1; i++)
            {
                var a = playerBuffer.Get(i);
                var b = playerBuffer.Get(i + 1);
                if (AreStatesStationary(a, b))
                {
                    if (runStart < 0)
                        runStart = i;
                }
                else
                {
                    if (runStart >= 0)
                    {
                        float tStart = playerBuffer.Get(runStart).Timestamp;
                        float tEnd = playerBuffer.Get(i).Timestamp;
                        if (tEnd - tStart >= idleFastForwardThresholdSeconds)
                            segments.Add((tStart, tEnd));
                        runStart = -1;
                    }
                }
            }
            if (runStart >= 0)
            {
                float tStart = playerBuffer.Get(runStart).Timestamp;
                float tEnd = playerBuffer.Get(playerBuffer.Count - 1).Timestamp;
                if (tEnd - tStart >= idleFastForwardThresholdSeconds)
                    segments.Add((tStart, tEnd));
            }
            return segments;
        }

        private static bool IsTimeInIdleSegment(float time, List<(float start, float end)> segments)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                var (start, end) = segments[i];
                if (time >= start && time <= end)
                    return true;
            }
            return false;
        }

        private float GetOldestRecordedTime()
        {
            float oldestTime = float.MaxValue;
            
            foreach (var buffer in _rewindables.Values)
            {
                if (buffer.HasStates)
                {
                    float bufferOldest = buffer.GetOldest().Timestamp;
                    if (bufferOldest < oldestTime)
                        oldestTime = bufferOldest;
                }
            }
            
            return oldestTime == float.MaxValue ? Time.time : oldestTime;
        }
        
        private float GetNewestRecordedTime()
        {
            float newestTime = 0f;
            
            foreach (var buffer in _rewindables.Values)
            {
                if (buffer.HasStates)
                {
                    float bufferNewest = buffer.GetNewest().Timestamp;
                    if (bufferNewest > newestTime)
                        newestTime = bufferNewest;
                }
            }
            
            return newestTime;
        }
        
        #endregion
    }
}
