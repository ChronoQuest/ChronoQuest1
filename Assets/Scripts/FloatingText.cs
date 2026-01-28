using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public float floatAmplitude = 10f;
    public float floatFrequency = 2f;

    private RectTransform rectTransform;
    private Vector2 startPosition;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;     // keeping text in the same position      
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        rectTransform.anchoredPosition = startPosition + new Vector2(0f, yOffset);
    }
}
