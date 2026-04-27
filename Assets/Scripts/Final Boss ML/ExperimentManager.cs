using UnityEngine;
using System.Collections.Generic; 
using System.IO;

[System.Serializable]
public class ExperimentResult
{
    public string testID;
    public string groundTruth;
    public int predictedCluster;
    public string bossBehaviour; 
    public float timestamp; 
}

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance; 
    public string currentTestID; 
    public string groundTruth;
    private List<ExperimentResult> results = new List<ExperimentResult>(); 

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);  
        } else
        {
            Destroy(gameObject); 
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            SaveToJSON(); 
        }
    }

    public void LogResult(int predictedCluster, string bossBehaviour)
    {
        ExperimentResult result = new ExperimentResult
        {
            testID = currentTestID,
            groundTruth = groundTruth,
            predictedCluster = predictedCluster,
            bossBehaviour = bossBehaviour,
            timestamp = Time.time
        };

        results.Add(result); 
        Debug.Log("Logged test"); 
    }

    public void SaveToJSON()
    {
        string json = JsonUtility.ToJson(new Wrapper { results = results }, true);
        string path = Path.Combine(Application.persistentDataPath, "experiment_results.json");
        File.WriteAllText(path, json); 
        Debug.Log("Saved JSON to: " + path); 
    }

    [System.Serializable]
    private class Wrapper
    {
        public List<ExperimentResult> results; 
    }

    void OnApplicationQuit()
    {
        SaveToJSON();
    }
}
