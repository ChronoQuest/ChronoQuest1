using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeRewind
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerRewindController : MonoBehaviour, IRewindable
    {
        [Header("Input")]
        [Tooltip("The key to hold for rewinding time")]
        [SerializeField] private Key rewindKey = Key.R;
        
        private Rigidbody2D _rb;
        private bool _isRewinding;
        private bool _rewindInputHeld;
        private RigidbodyType2D _originalBodyType;
        private RewindState _lastAppliedState;
        
        public bool IsRewinding => _isRewinding;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }
        
        private void OnEnable()
        {
            var manager = TimeRewindManager.Instance;
            if (manager != null)
            {
                manager.Register(this);
            }
        }
        
        private void OnDisable()
        {
            if (TimeRewindManager.Instance != null)
            {
                TimeRewindManager.Instance.Unregister(this);
            }
        }
        
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                _rewindInputHeld = keyboard[rewindKey].isPressed;
            }
            
            if (_rewindInputHeld && !TimeRewindManager.Instance.IsRewinding)
            {
                TimeRewindManager.Instance.StartRewind();
            }
            else if (!_rewindInputHeld && TimeRewindManager.Instance.IsRewinding)
            {
                TimeRewindManager.Instance.StopRewind();
            }
        }
        
        #endregion

        #region Input Callbacks
        
        public void OnRewind(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                _rewindInputHeld = true;
            }
            else if (context.canceled)
            {
                _rewindInputHeld = false;
            }
        }
        
        #endregion

        #region IRewindable Implementation
        
        public void OnStartRewind()
        {
            _isRewinding = true;
            _originalBodyType = _rb.bodyType;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
        
        public void OnStopRewind()
        {
            _isRewinding = false;
            _rb.bodyType = _originalBodyType;
            
            if (_originalBodyType == RigidbodyType2D.Dynamic)
            {
                _rb.linearVelocity = _lastAppliedState.Velocity;
                _rb.angularVelocity = _lastAppliedState.AngularVelocity;
            }
        }
        
        public RewindState CaptureState()
        {
            return RewindState.CreateWithPhysics(
                transform.position,
                transform.rotation,
                _rb.linearVelocity,
                _rb.angularVelocity,
                Time.time
            );
        }
        
        public void ApplyState(RewindState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
            _lastAppliedState = state;
        }
        
        #endregion
    }
}
