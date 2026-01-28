using UnityEngine;

namespace TimeRewind
{
    [RequireComponent(typeof(Animator))]
    public class RewindableAnimator : MonoBehaviour, IRewindable
    {
        [Header("Settings")]
        [Tooltip("The animator layer to record (usually 0)")]
        [SerializeField] private int animatorLayer = 0;
        
        [Tooltip("Also record and restore transform position/rotation")]
        [SerializeField] private bool includeTransform = false;
        
        private Animator _animator;
        private bool _isRewinding;
        private float _originalSpeed;
        
        public bool IsRewinding => _isRewinding;
        public Animator Animator => _animator;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }
        
        private void OnEnable()
        {
            TimeRewindManager.Instance.Register(this);
        }
        
        private void OnDisable()
        {
            if (TimeRewindManager.Instance != null)
            {
                TimeRewindManager.Instance.Unregister(this);
            }
        }
        
        #endregion

        #region IRewindable Implementation
        
        public void OnStartRewind()
        {
            _isRewinding = true;
            _originalSpeed = _animator.speed;
            _animator.speed = 0f;
        }
        
        public void OnStopRewind()
        {
            _isRewinding = false;
            _animator.speed = _originalSpeed;
        }
        
        public RewindState CaptureState()
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(animatorLayer);
            
            var state = new RewindState
            {
                Timestamp = Time.time,
                AnimatorStateHash = stateInfo.fullPathHash,
                AnimatorNormalizedTime = stateInfo.normalizedTime
            };
            
            if (includeTransform)
            {
                state.Position = transform.position;
                state.Rotation = transform.rotation;
            }
            
            return state;
        }
        
        public void ApplyState(RewindState state)
        {
            _animator.Play(state.AnimatorStateHash, animatorLayer, state.AnimatorNormalizedTime);
            _animator.Update(0f);
            
            if (includeTransform)
            {
                transform.position = state.Position;
                transform.rotation = state.Rotation;
            }
        }
        
        #endregion
    }
}
