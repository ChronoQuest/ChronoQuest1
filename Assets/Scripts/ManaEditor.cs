using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Debug / settings UI that lets you adjust max mana at runtime with a slider.
/// Attach to a UI panel that contains a Slider and an optional label.
/// </summary>
public class ManaEditor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Player's PlayerMana component here")]
    public PlayerMana playerMana;

    [Tooltip("A UI Slider to adjust the max mana value")]
    public Slider manaSlider;

    [Tooltip("Optional label that displays the current max mana value")]
    public TextMeshProUGUI label;

    [Header("Slider Range")]
    [SerializeField] private float minMana = 1f;
    [SerializeField] private float maxMana = 200f;

    private void Awake()
    {
        // UI elements need an EventSystem to receive input.
        // If none exists in the scene, create one automatically.
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    private void Start()
    {
        if (manaSlider != null && playerMana != null)
        {
            // Disable keyboard/gamepad navigation so A/D movement keys
            // don't accidentally change the slider after it's been clicked.
            manaSlider.navigation = new Navigation { mode = Navigation.Mode.None };

            manaSlider.minValue = minMana;
            manaSlider.maxValue = maxMana;
            manaSlider.wholeNumbers = true;
            manaSlider.value = playerMana.MaxMana;
            manaSlider.onValueChanged.AddListener(OnSliderChanged);
            UpdateLabel();
        }
    }

    private void OnDestroy()
    {
        if (manaSlider != null)
            manaSlider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        if (playerMana != null)
        {
            playerMana.SetMaxMana(value);
            UpdateLabel();
        }

        // Clear UI focus so movement keys go back to the player immediately
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void UpdateLabel()
    {
        if (label != null && manaSlider != null)
            label.text = $"Mana: {Mathf.CeilToInt(playerMana.CurrentMana)}";
    }
}
