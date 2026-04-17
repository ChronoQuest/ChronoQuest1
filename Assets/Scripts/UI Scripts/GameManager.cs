using UnityEngine; 
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); 
    }

    public void GameOver()
    {
        Debug.Log("Game Over"); 

        int finalScore = ScoreManager.Instance.GetScore(); 

        StartCoroutine(SendScore("PlayerName", finalScore)); 
    }

    IEnumerator SendScore(string name, int score)
    {
        string json = JsonUtility.ToJson(new ScoreEntry(name, score));

        var request = new UnityEngine.Networking.UnityWebRequest(
            "http://localhost:80/score", "POST"
        );

        
    }
}