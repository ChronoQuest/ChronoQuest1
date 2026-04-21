using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PauseMenu : MonoBehaviour
{
    public GameObject container;
    public static bool isPaused = false;
    public int escapePressed = 0;

    [Header("UI Navigation")]
    [SerializeField] private GameObject firstSelectedButton;

    private Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        bool pausePressed =
            Keyboard.current.escapeKey.wasPressedThisFrame ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        if (pausePressed)
        {
            TogglePause();
        }

        // Allow controller B / Circle to resume
        if (isPaused && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            ResumeButton();
        }
    }

    void TogglePause()
    {
        if (isPaused)
            ResumeButton();
        else
            PauseGame();
    }

    void PauseGame()
    {
        isPaused = true;
        container.SetActive(true);
        Time.timeScale = 0f;
        if (_canvas != null) _canvas.sortingOrder = 999;

        escapePressed++;

        DataCollectionService.Instance?.RecordPause();

        // Focus first UI button for controller navigation
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    public void PauseButton()
    {
        PauseGame();
    }

    public void ResumeButton()
    {
        isPaused = false;
        container.SetActive(false);
        Time.timeScale = 1f;
        if (_canvas != null) _canvas.sortingOrder = 0;

        EventSystem.current.SetSelectedGameObject(null);
    }

    public void MainMenuButton()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (container != null)
            container.SetActive(false);

        SceneManager.LoadScene("TitleScreen");
    }

    public void RestartButton()
    {
        isPaused = false;
        Time.timeScale = 1f;

        SceneManager.LoadScene("GameScene");
    }
}