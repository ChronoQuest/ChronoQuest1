using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class LowHealthVisualController : MonoBehaviour
{
    public static LowHealthVisualController Instance;

    [Header("Volume Reference")]
    [SerializeField]
    private Volume postProcessVolume;

    [Header("Player Light")]
    [SerializeField]
    private Light2D playerPulseLight;

    [Header("Global Light")]
    [SerializeField]
    private Light2D globalLight;

    [Header("Settings")]

    [SerializeField]
    private float pulseSpeed = 6.2f;

    [SerializeField]
    private float darkIntensity = 0.3f;
    [SerializeField]
    private float maxLightIntensity = 0.6f;
    [SerializeField]

    private float playerLightMax = 1.2f;

    [SerializeField]
    private float vignetteMax = 0.55f;
    [SerializeField]
    private float vignetteMin = 0.45f;
    [SerializeField]
    private float blurMax = 0.8f;
    
    private const float MIN_BLUR = 0.5f;
    private Vignette vignette;
    private ColorAdjustments colorAdjust;
    private DepthOfField depthOfField;

    private float originalGlobalIntensity;

    private Coroutine pulseRoutine;

    private void Awake()
    {
        Instance = this;

        if (postProcessVolume != null)
        {
            var profile = postProcessVolume.profile;

            profile.TryGet(out vignette);
            profile.TryGet(out colorAdjust);
            profile.TryGet(out depthOfField);

            if (depthOfField != null)
            {
                depthOfField.active = false;
            }
        }

        if (globalLight != null)
        {
            originalGlobalIntensity =
                globalLight.intensity;
        }
    }

    public void StartLowHealthEffect()
    {
        if (pulseRoutine != null)
            return;
            
        if (depthOfField != null)
        {
            depthOfField.active = true;
            depthOfField.gaussianMaxRadius.value = MIN_BLUR;
        }

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    public void StopLowHealthEffect()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        StartCoroutine(ResetRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        // --- THE FIX: Custom timer that we can pause ---
        float effectTimer = 0f;

        while (true)
        {
            // If the game is paused, freeze the visuals exactly where they are
            if (PauseMenu.isPaused)
            {
                yield return null;
                continue;
            }

            // Only advance the timer if we are not paused
            effectTimer += Time.unscaledDeltaTime;

            float wave = Mathf.Sin(effectTimer * pulseSpeed);
            float t = (wave + 1f) / 2f;
            float shaped = Mathf.Pow(t, 1f);

            // Darken world
            if (globalLight != null)
            {
                globalLight.intensity = Mathf.Lerp(maxLightIntensity, darkIntensity, shaped);
            }

            // Player pulse light
            if (playerPulseLight != null)
            {
                playerPulseLight.intensity = Mathf.Lerp(0.3f, playerLightMax, shaped);
            }

            // Vignette
            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(vignetteMin, vignetteMax, shaped);
            }

            // Red tint
            if (colorAdjust != null)
            {
                colorAdjust.colorFilter.value = Color.Lerp(new Color(1f, 0.9f, 0.9f), new Color(1f, 0.6f, 0.6f), shaped);
            }

            // Blur
            if (depthOfField != null)
            {
                depthOfField.gaussianMaxRadius.value = Mathf.Lerp(0f, blurMax, shaped);
            }

            yield return null;
        }
    }

    private IEnumerator ResetRoutine()
    {
        float t = 0f;

        // Capture starting points for a clean linear reset
        float startGlobalIntensity = globalLight != null ? globalLight.intensity : originalGlobalIntensity;
        float startPlayerIntensity = playerPulseLight != null ? playerPulseLight.intensity : 0f;
        float startVignette = vignette != null ? vignette.intensity.value : 0f;
        Color startColor = colorAdjust != null ? colorAdjust.colorFilter.value : Color.white;

        while (t < 1f)
        {
            // Pause the fade-out if the player pauses the game while it is resetting
            if (PauseMenu.isPaused)
            {
                yield return null;
                continue;
            }

            // Use unscaled delta time so it resets smoothly even if in slow-motion hitstop
            t += Time.unscaledDeltaTime * 2f;
            float lerpT = Mathf.Clamp01(t);

            if (globalLight != null)
                globalLight.intensity = Mathf.Lerp(startGlobalIntensity, originalGlobalIntensity, lerpT);

            if (playerPulseLight != null)
                playerPulseLight.intensity = Mathf.Lerp(startPlayerIntensity, 0f, lerpT);

            if (vignette != null)
                vignette.intensity.value = Mathf.Lerp(startVignette, 0f, lerpT);

            if (colorAdjust != null)
                colorAdjust.colorFilter.value = Color.Lerp(startColor, Color.white, lerpT);

            yield return null;
        }

        // Final cleanup
        if (depthOfField != null)
        {
            depthOfField.gaussianMaxRadius.value = MIN_BLUR;
            depthOfField.active = false;
        }
    }
}