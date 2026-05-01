using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TimeRewind;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public GameObject content; 
    [Header("Rewind After Death Hint")]
    [SerializeField] private TextMeshProUGUI rewindAfterDeathText;
    [SerializeField] private string rewindAvailableMessage = "Hold Rewind to rewind out of death";
    public float fadeDuration = 1.5f; 
    private float fadeTimer = 0f; 
    private bool isFading = false;
    private bool hasFinished = false;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (content != null)
            content.SetActive(false);
    }

    // ---- fading effect ---- 
    void Update()
    {
        if (isFading && !hasFinished)
        {
            fadeTimer += Time.unscaledDeltaTime;
            
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Clamp01(fadeTimer / fadeDuration); 

            if (fadeTimer >= fadeDuration)
            {
                FinishFade(); 
            }
        }

        UpdateRewindAfterDeathHint();
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
        hasFinished = false;
        isFading = true; 
        fadeTimer = 0f; 

        if (content != null)
            content.SetActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
        }

        UpdateRewindAfterDeathHint(forceShow: true);
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
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (content != null)
            content.SetActive(false);
        Time.timeScale = 1f;
    }

    void FinishFade()
    {
        hasFinished = true; 
        isFading = false;

        if (content != null)
            content.SetActive(true); 

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true; 
            canvasGroup.blocksRaycasts = true; 
        }
        
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

    private void UpdateRewindAfterDeathHint(bool forceShow = false)
    {
        if (rewindAfterDeathText == null)
            return;

        var playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth == null || !playerHealth.IsDead)
        {
            if (!forceShow)
                rewindAfterDeathText.enabled = false;
            return;
        }

        var rewindController = FindFirstObjectByType<TimeRewind.PlayerRewindController>();
        var mana = playerHealth.GetComponent<PlayerMana>();

        bool canRewind = false;

        if (rewindController != null)
            canRewind = rewindController.CanStartRewindWhenDeadNow();
        else if (mana != null)
        {
            // Fallback if controller can't be found: allow if there's any mana.
            canRewind = mana.CurrentMana > 0f;
        }

        if (canRewind)
        {
            rewindAfterDeathText.enabled = true;
            rewindAfterDeathText.text = rewindAvailableMessage;
        }
        else
        {
            rewindAfterDeathText.enabled = false;
        }
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
