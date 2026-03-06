using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.InputSystem; 

public class PauseMenu : MonoBehaviour
{
    public GameObject container; 
    public static bool isPaused = false;
    public int escapePressed = 0;
    
    void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.startButton.wasPressedThisFrame == true)
        {
            if (isPaused) 
                Resume(); 
            else
                Pause(); 
                DataCollectionService.Instance?.RecordPause();
        }
    } 

    public void Pause()
    {
        Debug.Log("Pausing game");
        isPaused = true; 
        container.SetActive(true); 
        Time.timeScale = 0f; 
    }

    public void Resume()
    {
        Debug.Log("Resuming game");
        isPaused = false; 
        container.SetActive(false); 
        Time.timeScale = 1f; 
    }

    public void PauseButton()
    {
        Pause(); 
        DataCollectionService.Instance?.RecordPause();
    }

    public void ResumeButton()
    {
        Resume(); 
    }

    public void MainMenuButton()
    {
        Resume(); 
        SceneManager.LoadScene("TitleScreen"); 
    }

    public void RestartButton()
    {
        Resume(); 
        SceneManager.LoadScene("GameScene"); 
    }
}
