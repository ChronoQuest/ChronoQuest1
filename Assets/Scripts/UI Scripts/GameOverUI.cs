using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TimeRewind;

public class GameOverUI : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public GameObject content; 
    public float fadeDuration = 1.5f; 
    private float fadeTimer = 0f; 
    private bool isFading = false;
    private bool hasFinished = false;

    // ---- fading effect ---- 
    void Update()
    {
        if (isFading && !hasFinished)
        {
            fadeTimer += Time.unscaledDeltaTime;
            
            canvasGroup.alpha = Mathf.Clamp01(fadeTimer / fadeDuration); 

            if (fadeTimer >= fadeDuration)
            {
                FinishFade(); 
            }
        }
    }

    private void OnEnable()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.OnRewindStop += OnRewindStopped;
    }

    private void OnDisable()
    {
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.OnRewindStop -= OnRewindStopped;
    }

    public void ShowGameOver()
    {
        isFading = true; 
        fadeTimer = 0f; 
    }

    /// <summary>
    /// Dismisses the game over screen immediately and resumes gameplay.
    /// Idempotent: no-op if the UI is not currently shown or fading in.
    /// </summary>
    public void HideGameOver()
    {
        if (!hasFinished && !isFading)
            return;
        hasFinished = false;
        isFading = false;
        fadeTimer = 0f;
        canvasGroup.alpha = 0f;
        content.SetActive(false);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        Time.timeScale = 1f;
    }

    void FinishFade()
    {
        hasFinished = true; 
        isFading = false;

        content.SetActive(true); 

        canvasGroup.interactable = true; 
        canvasGroup.blocksRaycasts = true; 
        
        // Pause gameplay once the fade has completed, but not while the player is rewinding
        if (TimeRewindManager.Instance == null || !TimeRewindManager.Instance.IsRewinding)
            Time.timeScale = 0f; 
    }

    private void OnRewindStopped()
    {
        // When rewind ends and we're still in game-over state, pause again
        var playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
            Time.timeScale = 0f;
    }

    // ---- button event methods ---- 
    public void RestartButton()
    {
        Time.timeScale = 1f; 
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); 
    }

    public void QuitButton()
    {
        SceneManager.LoadScene("TitleScreen");
    }
}
