using UnityEngine;
using TMPro;

public class ManaBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Player's PlayerMana component here")]
    public PlayerMana playerMana;

    [Tooltip("TextMeshPro text that displays the mana count")]
    public TextMeshProUGUI manaText;

    private void Start()
    {
        if (playerMana != null)
        {
            playerMana.OnManaChanged += UpdateDisplay;
            // Initialize the display immediately
            UpdateDisplay(playerMana.CurrentMana / playerMana.MaxMana);
        }
    }

    private void OnDestroy()
    {
        if (playerMana != null)
            playerMana.OnManaChanged -= UpdateDisplay;
    }

    private void UpdateDisplay(float normalizedMana)
    {
        if (manaText != null && playerMana != null)
        {
            manaText.text = $"Mana: {Mathf.CeilToInt(playerMana.CurrentMana)}";
        }
    }
}
