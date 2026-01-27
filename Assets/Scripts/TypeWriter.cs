using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class Typewriter : MonoBehaviour
{
    public float timePerCharacter = 0.03f;

    private string displayMessage = "";
    private float timer = 0f; 
    private int charIndex = 0;
    private bool typing = false; 

    private Action onCompleteCallback = null; 

    private TextMeshProUGUI activeText;         // reference to the text component
    private string currentMessage; 

    public void StartTyping(TextMeshProUGUI textComponent, Action onComplete = null)
    {
        activeText = textComponent;
        onCompleteCallback = onComplete;

        currentMessage = activeText.text;
        
        displayMessage = "";
        charIndex = 0;
        timer = 0f;
        typing = true;

        activeText.text = "";
    }

    private void Update()
    {
        if(!typing)
            return;
        
        timer -= Time.deltaTime;
        if(timer <= 0)
        {
            timer += timePerCharacter;
            charIndex++; 

            if (charIndex >= currentMessage.Length)
            {
                charIndex = currentMessage.Length;
                typing = false;
                displayMessage = currentMessage;
                onCompleteCallback?.Invoke();
            }
            else
            {
                displayMessage = currentMessage.Substring(0, charIndex) + "_"; // add cursor
            }

            activeText.text = displayMessage;
        }
    }
}
