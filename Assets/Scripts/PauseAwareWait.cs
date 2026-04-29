using System.Collections;
using UnityEngine;

// Drop-in replacement for new WaitForSecondsRealtime(seconds) that freezes while
// PauseMenu.isPaused is true. Use for unscaled-time waits that should still pause
// with the pause menu (e.g. dialogue typewriters that need to keep typing through
// timeScale=0 cutscenes but stop typing when the player opens the pause menu).
//
// Usage: yield return PauseAwareWait.Seconds(timePerChar);
public static class PauseAwareWait
{
    public static IEnumerator Seconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!PauseMenu.isPaused) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
