using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal; 

public class CaveLightTrigger : MonoBehaviour
{
    [Header("Lighting Settings")]
    [Tooltip("The brightness of the player's light INSIDE the cave.")]
    public float insideCaveIntensity = 1.0f;
    
    [Tooltip("The brightness of the player's light OUTSIDE the cave.")]
    public float outsideCaveIntensity = 0.0f;
    
    [Tooltip("How long the fade takes in seconds.")]
    public float fadeDuration = 1.5f;

    private UnityEngine.Rendering.Universal.Light2D playerLight;
    private Coroutine fadeCoroutine;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object entering the trigger is the Player
        if (other.CompareTag("Player"))
        {
            // Find the Light2D component on the player (or its children)
            if (playerLight == null)
            {
                playerLight = other.GetComponentInChildren<Light2D>();
            }

            if (playerLight != null)
            {
                // Stop any current fading and start fading UP to cave brightness
                if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
                fadeCoroutine = StartCoroutine(FadeLight(playerLight.intensity, insideCaveIntensity));
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Check if the player is leaving the trigger zone
        if (other.CompareTag("Player") && playerLight != null)
        {
            // Stop any current fading and start fading DOWN to outside brightness
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeLight(playerLight.intensity, outsideCaveIntensity));
        }
    }

    private IEnumerator FadeLight(float startIntensity, float targetIntensity)
    {
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Smoothly transition the intensity using Lerp
            playerLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, elapsedTime / fadeDuration);
            
            yield return null; // Wait for the next frame before continuing
        }

        // Ensure the light hits the exact target value at the end
        playerLight.intensity = targetIntensity;
    }
}