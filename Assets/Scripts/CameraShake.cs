using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    private CameraFollow2D followScript;
    
    [Header("Flash Settings")]
    private float flashAlpha = 0f;
    private Color flashColor = new Color(1, 0, 0, 0.4f); // red, 40% transparency

    void Awake() => followScript = GetComponent<CameraFollow2D>();

    public void FlashRed()
    {
        flashAlpha = 0.4f; // Set the starting brightness
    }
    
    void Update()
    {
        // Gradually fade the flash alpha back to 0
        if (flashAlpha > 0)
        {
            flashAlpha -= Time.unscaledDeltaTime * 2f; // Adjust '2f' to change fade speed
        }
    }

    private void OnGUI()
    {
        if (flashAlpha > 0)
        {
            // Draw a texture over the entire screen
            GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
    }
    
    public void Shake(float duration, float magnitude)
    {
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            // Tell the follow script the offset
            followScript.ApplyShakeOffset(new Vector3(x, y, 0));

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Reset offset to zero when done
        followScript.ApplyShakeOffset(Vector3.zero);
    }
}