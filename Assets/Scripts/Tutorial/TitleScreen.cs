using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; 
using UnityEngine.InputSystem;
using TimeRewind;

public class TitleScreen : MonoBehaviour
{
    public float floatAmplitude = 10f;
    public float floatFrequency = 2f;
    public TMP_InputField nameInput; 

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private RewindMusicController musicController;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;     // keeping text in the same position      

        // Try to find a music controller in the scene for title music.
        musicController = FindFirstObjectByType<RewindMusicController>();
        if (musicController != null)
        {
            musicController.PlayTitleMusic();
        }
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        rectTransform.anchoredPosition = startPosition + new Vector2(0f, yOffset);
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) StartButton();
    }

    public void StartButton()
    {
        string playerName = nameInput.text;

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player"; 
        }

        PlayerPrefs.SetString("playerName", playerName); 
        
        // Clear persisted tutorial safety so falling platforms behave normally on a fresh run.
        PlayerPrefs.DeleteKey(DynamicDifficultyManager.TutorialSafetyPlayerPrefsKey);
        PlayerPrefs.Save();
        SceneManager.LoadScene("introCutscene");
    }
}
