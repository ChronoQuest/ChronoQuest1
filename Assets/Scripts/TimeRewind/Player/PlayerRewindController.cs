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
        [Tooltip("Minimum time rewind runs after starting; prevents a quick tap from starting then immediately stopping.")]
        [SerializeField] private float minRewindDuration = 0.25f;
        [Tooltip("Frames input must be released before rewind stops; prevents one-frame glitches from stopping rewind.")]
        [SerializeField] private int releaseFramesRequired = 2;

        [Header("Mana Cost")]
        [SerializeField] private float manaDrainPerSecond = 10f;
        
        private Rigidbody2D _rb;
        private bool _isRewinding;
        private float _rewindStartTime;
        private bool _rewindInputHeld;
        private float _rewindHoldTimer;
        private int _releaseFrameCount;
        private bool blockRewindInput = false;
        private RigidbodyType2D _originalBodyType;
        private RewindState _lastAppliedState;
        private PlayerMana _playerMana;
        
        public bool IsRewinding => _isRewinding;
        public event Action OnRewindStarted;
        public event Action OnRewindStopped;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        public PlayerTacticalModel playerTacticalModel; 
        
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

            if (!blockRewindInput && _rewindInputHeld && hasMana && !TimeRewindManager.Instance.IsRewinding)
            {
                TimeRewindManager.Instance.StartRewind();
            }
            else if (TimeRewindManager.Instance.IsRewinding)
            {
                // Drain mana every frame while rewinding (unscaled so cost is constant per real second)
                bool canContinue = _playerMana != null 
                    && _playerMana.DrainManaContinuousUnscaled(manaDrainPerSecond);

                bool minDurationElapsed = (Time.unscaledTime - _rewindStartTime) >= minRewindDuration;
                if (!canContinue)
                {
                    TimeRewindManager.Instance.StopRewind();
                    _releaseFrameCount = 0;
                }
                else if (!_rewindInputHeld && minDurationElapsed)
                {
                    _releaseFrameCount++;
                    if (_releaseFrameCount >= releaseFramesRequired)
                    {
                        TimeRewindManager.Instance.StopRewind();
                        _releaseFrameCount = 0;
                    }
                }
                else
                {
                    _releaseFrameCount = 0;
                }
            }
            else
            {
                _releaseFrameCount = 0;
            }
            
            if (blockRewindInput && !_rewindInputHeld)
            {
                blockRewindInput = false;
            }
        }
        
        #endregion
        public void SetRewindBlocked(bool blocked)
        {
            blockRewindInput = blocked;
        }

        #region Input Callbacks
        
        public void OnRewind(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                _rewindInputHeld = true;
            }
            else if (context.canceled)
            {
                // Ignore cancel while rewinding so UI/focus doesn't falsely release and stop rewind
                if (!_isRewinding)
                    _rewindInputHeld = false;
            }
        }
        
        #endregion

        #region IRewindable Implementation
        
        public void OnStartRewind()
        {
            _isRewinding = true;
            _rewindStartTime = Time.unscaledTime;
            DataCollectionService.Instance?.RecordRewindStarted();
            playerTacticalModel.RecordRewind(); 

            OnRewindStarted?.Invoke();
            _originalBodyType = _rb.bodyType;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            if (animator != null) animator.speed = 0;
            GetComponent<PlayerSafetyNet>()?.CancelRespawn();
        }
        
        public void OnStopRewind()
        {
            _isRewinding = false;
            DataCollectionService.Instance?.RecordRewindStopped();
            OnRewindStopped?.Invoke();
            _rb.bodyType = _originalBodyType;
            
            if (_originalBodyType == RigidbodyType2D.Dynamic)
            {
                _rb.linearVelocity = _lastAppliedState.Velocity;
                _rb.angularVelocity = _lastAppliedState.AngularVelocity;
            }
            if (animator != null) animator.speed = 1;
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
                state.AnimatorStateHash = animInfo.fullPathHash;
                state.AnimatorNormalizedTime = animInfo.normalizedTime;
                state.SetCustomData("VerticalNormal", animator.GetFloat("VerticalNormal"));
                state.SetCustomData("Speed", animator.GetFloat("Speed"));
                state.SetCustomData("isGrounded", animator.GetBool("isGrounded"));
                state.SetCustomData("isWallSliding", animator.GetBool("isWallSliding"));

                if (animator.layerCount > 1)
                {
                    AnimatorStateInfo layer1Info = animator.GetCurrentAnimatorStateInfo(1);
                    state.SetCustomData("Layer1Hash", layer1Info.fullPathHash);
                    state.SetCustomData("Layer1Time", layer1Info.normalizedTime);
                }
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
                animator.SetFloat("VerticalNormal", state.GetCustomData<float>("VerticalNormal", 0f));
                animator.SetFloat("Speed", state.GetCustomData<float>("Speed", 0f));
                animator.SetBool("isGrounded", state.GetCustomData<bool>("isGrounded", true));
                animator.SetBool("isWallSliding", state.GetCustomData<bool>("isWallSliding", false));

                if (animator.layerCount > 1)
                {
                    int layer1Hash = state.GetCustomData<int>("Layer1Hash", 0);
                    if (layer1Hash != 0)
                    {
                        float layer1Time = state.GetCustomData<float>("Layer1Time", 0f);
                        animator.Play(layer1Hash, 1, layer1Time);
                    }
                }
                animator.Update(0f);
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = state.GetCustomData<bool>("IsFlipped", false); 
            }
        }
        
        #endregion
    }
}