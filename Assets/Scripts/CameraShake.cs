using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    private CameraFollow2D followScript;

    void Awake() => followScript = GetComponent<CameraFollow2D>();

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