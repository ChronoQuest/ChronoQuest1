using UnityEngine; 
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

    public void LevelComplete()
    {
        Debug.Log("Level Complete");

        ScoreManager.Instance.AddPoints(200); 
    }

    public void GameOver()
    {
        Debug.Log("Game Over"); 

        int finalScore = ScoreManager.Instance.GetScore(); 

        StartCoroutine(SendScore("PlayerName", finalScore)); 
    }

    IEnumerator SendScore(string name, int score)
    {  
        ScoreEntry entry = new ScoreEntry(name, score);
        string json = JsonUtility.ToJson(entry);

        var request = new UnityEngine.Networking.UnityWebRequest(
            "http://127.0.0.1:8000/score", "POST"
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
