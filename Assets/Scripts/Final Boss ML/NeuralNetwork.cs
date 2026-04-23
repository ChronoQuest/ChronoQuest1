using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json; 

[Serializable]
public class NeuralJSON
{
    public float[][] W1;
    public float[] b1;

    public float[][] W2;
    public float[] b2;

    public float[][] W3;
    public float[] b3;

    public float[] scaler_mean;
    public float[] scaler_scale;
}

public class NeuralNetwork : MonoBehaviour
{
    private float[][] W1, W2, W3;
    private float[] b1, b2, b3; 
    private float[] scalerMean;
    private float[] scalerScale; 

    void Awake()
    {
        LoadModel();
    }

    void LoadModel()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "neural_network_model.json");

        if (!File.Exists(path))
        {
            Debug.LogError("Neural network model file not found: " + path);
            return; 
        }

        string json = File.ReadAllText(path);
        NeuralJSON model = JsonConvert.DeserializeObject<NeuralJSON>(json);

        W1 = model.W1;
        b1 = model.b1;

        W2 = model.W2;
        b2 = model.b2;

        W3 = model.W3;
        b3 = model.b3;

        scalerMean = model.scaler_mean; 
        scalerScale = model.scaler_scale; 

        Debug.Log("Neural network model loaded."); 
    }

    // --- PREDICTIONS --- 
    public float[] Predict(float[] input)
    {
        float[] x = Scale(input);

        float[] h1 = Dense(x, W1, b1);
        h1 = ReLU(h1);

        float[] h2 = Dense(h1, W2, b2);
        h2 = ReLU(h2);

        float[] output = Dense(h2, W3, b3);
        output = Softmax(output);

        return output;
    }

    // --- HELPERS ---
    float[] Dense(float[] input, float[][] W, float[] b)
    {
        int outputSize = W[0].Length;
        float[] result = new float[outputSize]; 

        for (int j=0; j < outputSize; j++)
        {
            float sum = 0f; 
            
            if (b != null && j < b.Length)
            {
                sum = b[j];
            }

            for (int i = 0; i < input.Length; i++)
            {
                sum += input[i] * W[i][j]; 
            }

            result[j] = sum; 
        }

        return result; 
    }

    float[] ReLU(float[] x)
    {
        float[] result = new float[x.Length];

        for (int i = 0; i < x.Length; i++)
        {
            result[i] = Mathf.Max(0, x[i]);
        }

        return result;
    }

    float[] Softmax(float[] x)
    {
        float max = float.MinValue;

        foreach (float v in x)
            if (v > max) max = v;
        
        float sum = 0f; 
        float[] exp = new float[x.Length];

        for (int i = 0; i < x.Length; i++)
        {
            exp[i] = Mathf.Exp(x[i] - max);
            sum += exp[i];
        }

        for (int i = 0; i < x.Length; i++)
        {
            exp[i] /= sum;
        }

        return exp;
    }

    float[] Scale(float[] input)
    {
        float[] scaled = new float[input.Length];

        for (int i = 0; i < input.Length; i++)
        {
            scaled[i] = (input[i] - scalerMean[i]) / scalerScale[i];
        }

        return scaled;
    }
}
