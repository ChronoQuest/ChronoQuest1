using UnityEngine; 
using UnityEngine.SceneManagement; 
using System.Collections;

[System.Serializable]
public class ScoreEntry
{
    public string name;
    public int score;

    public ScoreEntry(string name, int score)
    {
        this.name = name;
        this.score = score; 
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private int deathCount = 0;
    public int maxDeaths = 3; 

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

    void Update()
    {
        // TESTING LEADERBOARD
        if (Input.GetKeyDown(KeyCode.K))
        {
            GameOver(); 
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
    }

    public void PlayerDied()
    {
        deathCount++; 
        Debug.Log("Player died. Count: " + deathCount); 

        if(SceneManager.GetActiveScene().name == "FinalBoss")
        {
            if (deathCount >= maxDeaths)
            {
                GameOver(); 
            }
        }
    }

    public void LevelComplete()
    {
        Debug.Log("Level Complete");

        ScoreManager.Instance.AddPoints(200); 
    }

    public void GameOver()
    {
        Debug.Log("Game Over"); 

        int finalScore = ScoreManager.Instance.GetScore(); 
        string playerName = PlayerPrefs.GetString("playerName", "Player"); 

        StartCoroutine(SendScore(playerName, finalScore)); 
    }

    IEnumerator SendScore(string name, int score)
    {  
        ScoreEntry entry = new ScoreEntry(name, score); 
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
