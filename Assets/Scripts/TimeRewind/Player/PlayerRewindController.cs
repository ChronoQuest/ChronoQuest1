using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeRewind
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerRewindController : MonoBehaviour, IRewindable
    {
        [Header("Input")]
        [SerializeField] private Key rewindKey = Key.R;
        [SerializeField] private float rewindHoldThreshold = 0f;

        [Header("Mana Cost")]
        [SerializeField] private float manaDrainPerSecond = 10f;
        
        private Rigidbody2D _rb;
        private bool _isRewinding;
        private bool _rewindInputHeld;
        private float _rewindHoldTimer;
        private RigidbodyType2D _originalBodyType;
        private RewindState _lastAppliedState;
        private PlayerMana _playerMana;
        
        public bool IsRewinding => _isRewinding;
        public event Action OnRewindStarted;
        public event Action OnRewindStopped;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            _playerMana = GetComponent<PlayerMana>();
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
            _rewindInputHeld = false;
            
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[rewindKey].isPressed)
                _rewindInputHeld = true;
            
            var gamepad = Gamepad.current;
            bool bothTriggers = gamepad != null
                && gamepad.leftTrigger.ReadValue() > 0.5f
                && gamepad.rightTrigger.ReadValue() > 0.5f;
            if (bothTriggers)
            {
                _rewindHoldTimer += Time.deltaTime;
                if (_rewindHoldTimer >= rewindHoldThreshold)
                    _rewindInputHeld = true;
            }
            else
                _rewindHoldTimer = 0f;
            
            bool hasMana = _playerMana != null && _playerMana.CurrentMana > 0f;

            if (_rewindInputHeld && hasMana && !TimeRewindManager.Instance.IsRewinding)
            {
                TimeRewindManager.Instance.StartRewind();
            }
            else if (TimeRewindManager.Instance.IsRewinding)
            {
                // Drain mana every frame while rewinding
                bool canContinue = _playerMana != null 
                    && _playerMana.DrainManaContinuous(manaDrainPerSecond);

                // Stop if player releases input OR runs out of mana
                if (!_rewindInputHeld || !canContinue)
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
            OnRewindStarted?.Invoke();
            _originalBodyType = _rb.bodyType;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            animator.speed = 0;
        }
        
        public void OnStopRewind()
        {
            _isRewinding = false;
            OnRewindStopped?.Invoke();
            _rb.bodyType = _originalBodyType;
            
            if (_originalBodyType == RigidbodyType2D.Dynamic)
            {
                _rb.linearVelocity = _lastAppliedState.Velocity;
                _rb.angularVelocity = _lastAppliedState.AngularVelocity;
            }
            animator.speed = 1;
        }
        
        public RewindState CaptureState()
        {
            var state = RewindState.CreateWithPhysics(
                transform.position,
                transform.rotation,
                _rb.linearVelocity,
                _rb.angularVelocity,
                Time.time
            );

            if (animator != null)
            {
                AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
                state.AnimatorStateHash = animInfo.shortNameHash;
                state.AnimatorNormalizedTime = animInfo.normalizedTime;
            }
            if (spriteRenderer != null)
            {
                state.SetCustomData("IsFlipped", spriteRenderer.flipX);
            }
            return state;
        }
        
        public void ApplyState(RewindState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
            _lastAppliedState = state;

            if (animator != null)
            {
                animator.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = state.GetCustomData<bool>("IsFlipped", false); 
            }
        }
        
        #endregion
    }
}
