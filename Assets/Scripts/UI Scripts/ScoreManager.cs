using UnityEngine;
using System.Collections;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance; 
    public int score = 1000;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); 
    }

    public void AddPoints(int amount)
    {
        score += amount;
        Debug.Log("Score: " + score);
    }

    public void RemovePoints(int amount)
    {
        score -= amount;
        if (score < 0) score = 0;       // dealing with negative scores
        Debug.Log("Score: " + score);
    }

    public int GetScore()
    {
        return score; 
    }
}