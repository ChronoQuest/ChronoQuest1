using UnityEngine;

public class ExperimentSetup : MonoBehaviour
{
    public string testID;
    public string groundTruth; 
    
    void Start()
    {
        if (ExperimentManager.Instance != null)
        {
            ExperimentManager.Instance.currentTestID = testID;
            ExperimentManager.Instance.groundTruth = groundTruth; 

            Debug.Log($"Experiment Setup: {testID} - {groundTruth}"); 
        }
    }
}
