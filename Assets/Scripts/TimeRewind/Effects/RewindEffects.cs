using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TimeRewind
{
    public class RewindEffects : MonoBehaviour
    {
        [Header("Visual Effects")]
        [Tooltip("Post-processing volume to modify during rewind (optional)")]
        [SerializeField] private Volume postProcessVolume;
        
        [Tooltip("Target saturation during rewind (-100 to 100, default is desaturated)")]
        [SerializeField] private float rewindSaturation = -50f;
        
        [Tooltip("Target chromatic aberration intensity during rewind (0-1)")]
        [SerializeField] private float rewindChromaticAberration = 0.5f;
        
        [Tooltip("Target vignette intensity during rewind (0-1)")]
        [SerializeField] private float rewindVignetteIntensity = 0.4f;
        
        [Tooltip("How fast effects transition in/out")]
        [SerializeField] private float effectTransitionSpeed = 5f;
        
        [Header("Screen Tint")]
        [SerializeField] private Color rewindTintColor = new Color(0.5f, 0.7f, 1f, 0.2f);
        
        [Header("Audio Effects")]
        [Tooltip("Audio source for rewind sound effects")]
        [SerializeField] private AudioSource audioSource;
        
        [Tooltip("Looping sound to play during rewind")]
        [SerializeField] private AudioClip rewindLoopSound;
        
        [Tooltip("Sound to play when rewind starts")]
        [SerializeField] private AudioClip rewindStartSound;
        
        [Tooltip("Sound to play when rewind ends")]
        [SerializeField] private AudioClip rewindEndSound;
        
        [Header("UI")]
        [Tooltip("UI element to show rewind progress (optional)")]
        [SerializeField] private UnityEngine.UI.Image progressBar;
        
        [Tooltip("UI element to show during rewind (optional)")]
        [SerializeField] private GameObject rewindIndicator;
        
        // Post-processing components
        private ColorAdjustments _colorAdjustments;
        private ChromaticAberration _chromaticAberration;
        private Vignette _vignette;
        
        // Original values
        private float _originalSaturation;
        private float _originalChromaticAberration;
        private float _originalVignetteIntensity;
        
        // Current effect values
        private float _currentEffectWeight;
        private bool _isRewinding;
        
        #region Unity Lifecycle
        
        private void Start()
        {
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                InitializePostProcessing();
            }
            
            if (rewindIndicator != null)
                rewindIndicator.SetActive(false);
            
            if (progressBar != null)
                progressBar.fillAmount = 0f;
        }
        
        private void OnEnable()
        {
            if (TimeRewindManager.Instance != null)
            {
                TimeRewindManager.Instance.OnRewindStart += HandleRewindStart;
                TimeRewindManager.Instance.OnRewindStop += HandleRewindStop;
                TimeRewindManager.Instance.OnRewindProgress += HandleRewindProgress;
            }
        }
        
        private void OnDisable()
        {
            if (TimeRewindManager.Instance != null)
            {
                TimeRewindManager.Instance.OnRewindStart -= HandleRewindStart;
                TimeRewindManager.Instance.OnRewindStop -= HandleRewindStop;
                TimeRewindManager.Instance.OnRewindProgress -= HandleRewindProgress;
            }
        }
        
        private void Update()
        {
            UpdateEffects();
        }
        
        #endregion

        #region Initialization
        
        private void InitializePostProcessing()
        {
            var profile = postProcessVolume.profile;
            
            if (profile.TryGet(out _colorAdjustments))
            {
                _originalSaturation = _colorAdjustments.saturation.value;
            }
            
            if (profile.TryGet(out _chromaticAberration))
            {
                _originalChromaticAberration = _chromaticAberration.intensity.value;
            }
            
            if (profile.TryGet(out _vignette))
            {
                _originalVignetteIntensity = _vignette.intensity.value;
            }
        }
        
        #endregion

        #region Event Handlers
        
        private void HandleRewindStart()
        {
            _isRewinding = true;
            
            if (audioSource != null && rewindStartSound != null)
            {
                audioSource.PlayOneShot(rewindStartSound);
            }
            
            if (audioSource != null && rewindLoopSound != null)
            {
                audioSource.clip = rewindLoopSound;
                audioSource.loop = true;
                audioSource.Play();
            }
            
            if (rewindIndicator != null)
                rewindIndicator.SetActive(true);
        }
        
        private void HandleRewindStop()
        {
            _isRewinding = false;
            
            if (audioSource != null && audioSource.isPlaying && audioSource.clip == rewindLoopSound)
            {
                audioSource.Stop();
            }
            
            if (audioSource != null && rewindEndSound != null)
            {
                audioSource.PlayOneShot(rewindEndSound);
            }
            
            if (rewindIndicator != null)
                rewindIndicator.SetActive(false);
        }
        
        private void HandleRewindProgress(float progress)
        {
            if (progressBar != null)
            {
                progressBar.fillAmount = progress;
            }
        }
        
        #endregion

        #region Effect Updates
        
        private void UpdateEffects()
        {
            // Smoothly transition effect weight
            float targetWeight = _isRewinding ? 1f : 0f;
            _currentEffectWeight = Mathf.MoveTowards(
                _currentEffectWeight, 
                targetWeight, 
                effectTransitionSpeed * Time.unscaledDeltaTime
            );
            
            // Apply post-processing effects
            ApplyPostProcessingEffects();
        }
        
        private void ApplyPostProcessingEffects()
        {
            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = Mathf.Lerp(
                    _originalSaturation, 
                    rewindSaturation, 
                    _currentEffectWeight
                );
            }
            
            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = Mathf.Lerp(
                    _originalChromaticAberration, 
                    rewindChromaticAberration, 
                    _currentEffectWeight
                );
            }
            
            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Lerp(
                    _originalVignetteIntensity, 
                    rewindVignetteIntensity, 
                    _currentEffectWeight
                );
            }
        }
        
        #endregion

        #region Public Methods
        
        public void SetPostProcessVolume(Volume volume)
        {
            postProcessVolume = volume;
            if (volume != null && volume.profile != null)
            {
                InitializePostProcessing();
            }
        }
        
        public void SetAudioSource(AudioSource source)
        {
            audioSource = source;
        }
        
        #endregion
    }
}
