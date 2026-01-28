using UnityEngine;

namespace TimeRewind
{
    public class RewindableTransform : MonoBehaviour, IRewindable
    {
        [Header("Settings")]
        [Tooltip("Use local position/rotation instead of world")]
        [SerializeField] private bool useLocalSpace = false;
        
        private bool _isRewinding;
        
        public bool IsRewinding => _isRewinding;
        
        #region Unity Lifecycle
        
        protected virtual void OnEnable()
        {
            TimeRewindManager.Instance.Register(this);
        }
        
        protected virtual void OnDisable()
        {
            if (TimeRewindManager.Instance != null)
            {
                TimeRewindManager.Instance.Unregister(this);
            }
        }
        
        #endregion

        #region IRewindable Implementation
        
        public virtual void OnStartRewind()
        {
            _isRewinding = true;
        }
        
        public virtual void OnStopRewind()
        {
            _isRewinding = false;
        }
        
        public virtual RewindState CaptureState()
        {
            Vector3 position = useLocalSpace ? transform.localPosition : transform.position;
            Quaternion rotation = useLocalSpace ? transform.localRotation : transform.rotation;
            
            return RewindState.Create(position, rotation, Time.time);
        }
        
        public virtual void ApplyState(RewindState state)
        {
            if (useLocalSpace)
            {
                transform.localPosition = state.Position;
                transform.localRotation = state.Rotation;
            }
            else
            {
                transform.position = state.Position;
                transform.rotation = state.Rotation;
            }
        }
        
        #endregion
    }
}
