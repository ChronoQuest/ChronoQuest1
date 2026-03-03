using UnityEngine;

namespace TimeRewind
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class RewindableRigidbody2D : MonoBehaviour, IRewindable
    {
        private Rigidbody2D _rb;
        private bool _isRewinding;
        private RigidbodyType2D _originalBodyType;
        
        public bool IsRewinding => _isRewinding;
        public Rigidbody2D Rigidbody => _rb;
        
        #region Unity Lifecycle
        
        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }
        
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
            _originalBodyType = _rb.bodyType;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
        
        public virtual void OnStopRewind()
        {
            _isRewinding = false;
            _rb.bodyType = _originalBodyType;
        }
        
        public virtual RewindState CaptureState()
        {
            return RewindState.CreateWithPhysics(
                transform.position,
                transform.rotation,
                _rb.linearVelocity,
                _rb.angularVelocity,
                Time.time
            );
        }
        
        public virtual void ApplyState(RewindState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
        }
        
        protected virtual void RestoreVelocity(RewindState lastState)
        {
            if (_originalBodyType == RigidbodyType2D.Dynamic)
            {
                _rb.linearVelocity = lastState.Velocity;
                _rb.angularVelocity = lastState.AngularVelocity;
            }
        }
        
        #endregion
    }
}
