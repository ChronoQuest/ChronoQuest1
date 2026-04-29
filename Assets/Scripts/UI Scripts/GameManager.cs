using UnityEngine; 
using UnityEngine.SceneManagement; 
using System.Collections;

[System.Serializable]
public class ScoreEntry
{
    public string name;
    public int score;
    public string strategy; 

    public ScoreEntry(string name, int score, string strategy)
    {
        this.name = name;
        this.score = score; 
        this.strategy = strategy; 
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private int deathCount = 0;
    public int maxDeaths = 3; 
    private bool scoreSent = false; 

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    } 

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        deathCount = 0; 


        if (scene.name == "TitleScreen")
        {
            scoreSent = false;
            deathCount = 0; 

            ScoreManager.Instance.ResetScore(); 
            PlayerStrategyModel.Instance.Reset();
        }
    }

    public void ReturnToMenu()
    {
        Debug.Log("Returning to menu");
        StartCoroutine(ReturnToMenuCoroutine()); 
    }

    IEnumerator ReturnToMenuCoroutine()
    {
        if (!scoreSent)
        {
            yield return StartCoroutine(SendScoreFlow());
        }

        SceneManager.LoadScene("TitleScreen");
    }

    IEnumerator SendScoreFlow()
    {
        scoreSent = true;

        Debug.Log("Sending score...");

        int baseScore = ScoreManager.Instance.GetScore();

        float multiplier = DynamicDifficultyManager.Instance != null
            ? DynamicDifficultyManager.Instance.GetScoreMultiplier()
            : 1f;

        int finalScore = Mathf.RoundToInt(baseScore * multiplier);

        Debug.Log($"Final Score: {baseScore} x {multiplier} = {finalScore}");

        string playerName = PlayerPrefs.GetString("playerName", "Player"); 
        
        if (PlayerStrategyModel.Instance != null)
        {
            PlayerStrategyModel.Instance.DetermineStrategy();
        }
        
        var strategy = StrategyTracker.GetAverageStrategy();

        string strategyString = strategy switch
        {
            PlayerStrategyModel.StrategyType.AggressivePlayer => "aggressive",
            PlayerStrategyModel.StrategyType.DefensivePlayer => "defensive",
            PlayerStrategyModel.StrategyType.AbilityFocusedPlayer => "ability",
            _ => "unknown"
        };

        yield return StartCoroutine(SendScore(playerName, finalScore, strategyString));

        Debug.Log("Score POST complete");
    }

    public void LevelComplete()
    {
        Debug.Log("Level Complete");

        ScoreManager.Instance.AddPoints(200); 
    }

    public void GameOver()
    {
        if (!scoreSent)
        {
            StartCoroutine(SendScoreFlow()); 
        }
    }

    /* public void GameOver()
    {
        if (scoreSent) return;  
        scoreSent = true;

        Debug.Log("Game Over"); 

        int baseScore = ScoreManager.Instance.GetScore();

        float multiplier = DynamicDifficultyManager.Instance != null
            ? DynamicDifficultyManager.Instance.GetScoreMultiplier()
            : 1f;

        int finalScore = Mathf.RoundToInt(baseScore * multiplier);

        Debug.Log($"Final Score: {baseScore} x {multiplier} = {finalScore}");

        string playerName = PlayerPrefs.GetString("playerName", "Player"); 
        
        if (PlayerStrategyModel.Instance != null)
        {
            PlayerStrategyModel.Instance.DetermineStrategy();
        }
        
        PlayerStrategyModel.StrategyType strategy = StrategyTracker.GetAverageStrategy();

        string strategyString = strategy switch
        {
            PlayerStrategyModel.StrategyType.AggressivePlayer => "aggressive",
            PlayerStrategyModel.StrategyType.DefensivePlayer => "defensive",
            PlayerStrategyModel.StrategyType.AbilityFocusedPlayer => "ability",
            _ => "unknown"
        };

        StartCoroutine(SendScore(playerName, finalScore, strategyString)); 
    } */ 

    IEnumerator SendScore(string name, int score, string strategy)
    {  
        ScoreEntry entry = new ScoreEntry(name, score, strategy); 
        string json = JsonUtility.ToJson(entry);

        var request = new UnityEngine.Networking.UnityWebRequest(
            "https://chronoquest1.onrender.com/score", "POST"
        );

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer(); 
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest(); 

        if(request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to send score: " + request.error);
        }
        else
        {
            Debug.Log("Score sent"); 
        }
    }
}
