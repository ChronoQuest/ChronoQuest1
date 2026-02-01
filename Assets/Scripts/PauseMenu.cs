using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public GameObject container; 
    public static bool isPaused = false;
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Escape key was pressed.");
            isPaused = !isPaused;
            container.SetActive(isPaused);
            Time.timeScale = isPaused ? 0f : 1f;
        }
    }

    public void PauseButton()
    {
        container.SetActive(true);
        Time.timeScale = 0f;    
        isPaused = true; 
    }

    public void ResumeButton()
    {
        container.SetActive(false);
        Time.timeScale = 1f;    
        isPaused = false;
    }

    public void MainMenuButton()
    {
        // TODO: navigate to starting screen
    }
}
