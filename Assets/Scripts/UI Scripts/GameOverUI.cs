using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections; 

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

    public void ShowGameOver()
    {
        isFading = true; 
        fadeTimer = 0f; 
    }

    void FinishFade()
    {
        hasFinished = true; 

        content.SetActive(true); 

        canvasGroup.interactable = true; 
        canvasGroup.blocksRaycasts = true; 
        
        // pause gameplay once the fade has completed
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
