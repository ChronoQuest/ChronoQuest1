using System;
using UnityEngine;
using TMPro;

public class Typewriter : MonoBehaviour
{
    public float timePerCharacter = 0.03f;
    private float timer = 0f; 
    private int visibleCount = 0;
    private bool typing = false; 

    private Action onCompleteCallback = null; 
    private TextMeshProUGUI activeText;

    public void StartTyping(TextMeshProUGUI textComponent, Action onComplete = null)
    {
        activeText = textComponent;
        onCompleteCallback = onComplete;

        activeText.maxVisibleCharacters = 0;
        visibleCount = 0;
        timer = 0f;
        typing = true;
        
        activeText.ForceMeshUpdate();
    }

    private void Update()
    {
        if(!typing || activeText == null)
            return;
        
        timer -= Time.unscaledDeltaTime;
        if(timer <= 0)
        {
            timer += timePerCharacter;
            visibleCount++; 

            activeText.maxVisibleCharacters = visibleCount;

            if (visibleCount >= activeText.textInfo.characterCount)
            {
                typing = false;
                activeText.maxVisibleCharacters = 99999;
                onCompleteCallback?.Invoke();
            }
        }
    }
}