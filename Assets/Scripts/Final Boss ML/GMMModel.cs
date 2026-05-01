using System; 
using UnityEngine;
using System.IO;

[Serializable]
public class GMMJson
{
    public int n_components;
    public int n_features;
    public float[] means;
    public float[] weights;
    public float[] scaler_mean;
    public float[] scaler_scale;
}

public class GMMModel : MonoBehaviour
{
    public float[][] means;
    public float[] weights;
    public float[] scalerMean;
    public float[] scalerScale;
    public int nComponents;

    void Awake()
    {
        LoadModel();
    }

    void LoadModel()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "gmm_model.json");

        if (!File.Exists(path))
        {
            Debug.LogError("GMM file not found: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        GMMJson model = JsonUtility.FromJson<GMMJson>(json);

        weights = model.weights;
        scalerMean = model.scaler_mean;
        scalerScale = model.scaler_scale;
        nComponents = model.n_components;
        
        means = new float[nComponents][];

        for (int k = 0; k < nComponents; k++)
        {
            means[k] = new float[model.n_features];

            for (int i = 0; i < model.n_features; i++)
            {
                means[k][i] = model.means[k * model.n_features + i];
            }
        }

        Debug.Log("GMM model loaded successfully.");
    }

    public float[] PredictProba(float[] features)
    {
        Debug.Log("Feature length: " + features.Length);
        Debug.Log("Scaler length: " + scalerMean.Length);
        Debug.Log("Means row length: " + means[0].Length);
        
        if (means == null || weights == null || scalerMean == null || scalerScale == null)
        {
            Debug.LogError("GMMModel not initialised properly.");
            return new float[0];
        }

        if (features.Length != scalerMean.Length)
        {
            Debug.LogError("Feature length does not match scaler.");
            return new float[0];
        }

        float[] probs = new float[nComponents];
        float[] scaled = Scale(features);

        for (int k = 0; k < nComponents; k++)
        {
            if (means[k] == null)
            {
                Debug.LogError("Means row " + k + " is null.");
                continue;
            }

            float dist = 0f;

            for (int i = 0; i < scaled.Length; i++)
            {
                float diff = scaled[i] - means[k][i];
                dist += diff * diff;
            }

            probs[k] = weights[k] * Mathf.Exp(-dist);
        }

        float total = 0f;

        for (int i = 0; i < probs.Length; i++)
            total += probs[i];

        if (total <= 0f)
        {
            Debug.LogWarning("GMM probabilities summed to zero. Returning uniform distribution.");

            for (int i = 0; i < probs.Length; i++)
                probs[i] = 1f / probs.Length;

            return probs;
        }

        for (int i = 0; i < probs.Length; i++)
            probs[i] /= total;

        return probs;
    }

    float[] Scale(float[] features)
    {
        float[] scaled = new float[features.Length];

        for (int i = 0; i < features.Length; i++)
        {
            scaled[i] = (features[i] - scalerMean[i]) / scalerScale[i];
        }

        return scaled;
    }
}
