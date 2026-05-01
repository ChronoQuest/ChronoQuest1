using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class ManaBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Player's PlayerMana component here (auto-found if left empty).")]
    public PlayerMana playerMana;

    [Header("Bar")]
    [Tooltip("The fill Image RectTransform. Keep Image Type as Sliced. Pivot must be (0, 0.5).")]
    public RectTransform fillRect;

    [Tooltip("The fill Image component (same object as fillRect).")]
    public Image fillImage;

    [Tooltip("How fast the bar fills/drains visually.")]
    public float lerpSpeed = 8f;

    [Header("Color")]
    public Color manaColor = new Color(0f, 0.75f, 1f);

    [Header("Low Mana Pulse")]
    [Tooltip("Mana fraction below which the bar starts pulsing.")]
    [Range(0f, 1f)]
    public float lowManaThreshold = 0.2f;

    [Tooltip("How fast the bar pulses when mana is low.")]
    public float pulseSpeed = 3f;

    [Tooltip("How dark the bar gets at the bottom of each pulse (0 = black, 1 = no change).")]
    [Range(0.2f, 1f)]
    public float pulseDimFactor = 0.4f;

    [Header("Insufficient Mana Flash")]
    public Color flashColor = new Color(0.9f, 0.18f, 0.18f);
    public float flashDuration = 0.15f;
    public int flashCount = 2;

    [Header("Text (Optional)")]
    [Tooltip("TextMeshPro text that displays the mana count alongside the bar.")]
    public TextMeshProUGUI manaText;

    [Tooltip("Toggle the numeric mana text on or off.")]
    public bool showText = true;

    private float displayedFill;
    private float targetFill;
    private Coroutine flashRoutine;
    private TextMeshProUGUI manaNumberText;

    private void Start()
    {
        if (playerMana == null)
            playerMana = FindFirstObjectByType<PlayerMana>();

        // Find the "mana_number" text in the parent canvas
        Transform canvas = transform.parent;
        if (canvas != null)
        {
            Transform manaNumberObj = canvas.Find("mana_number");
            if (manaNumberObj != null)
                manaNumberText = manaNumberObj.GetComponent<TextMeshProUGUI>();
        }

        if (playerMana != null)
        {
            playerMana.OnManaChanged += OnManaChanged;
            playerMana.OnManaSpendFailed += OnManaSpendFailed;
            targetFill = playerMana.CurrentMana / playerMana.MaxMana;
            displayedFill = targetFill;
            ApplyFill(displayedFill);
            UpdateText();
        }

        ApplyTextVisibility();
    }

    private void OnDestroy()
    {
        if (playerMana != null)
        {
            playerMana.OnManaChanged -= OnManaChanged;
            playerMana.OnManaSpendFailed -= OnManaSpendFailed;
        }
    }

    private void Update()
    {
        displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * lerpSpeed);
        ApplyFill(displayedFill);
    }

    private void OnManaChanged(float normalizedMana)
    {
        targetFill = normalizedMana;
        UpdateText();
    }

    private void ApplyFill(float t)
    {
        if (fillRect == null) return;

        Vector3 s = fillRect.localScale;
        s.x = Mathf.Clamp01(t);
        fillRect.localScale = s;

        if (fillImage == null || flashRoutine != null) return;

        Color color = manaColor;
        if (t <= lowManaThreshold && t > 0f)
        {
            float pulse = Mathf.Lerp(pulseDimFactor, 1f, (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
            color *= pulse;
            color.a = 1f;
        }
        fillImage.color = color;
    }

    private void UpdateText()
    {
        if (manaText != null && showText && playerMana != null)
            manaText.text = $"{Mathf.CeilToInt(playerMana.CurrentMana)} / {Mathf.CeilToInt(playerMana.MaxMana)}";

        if (manaNumberText != null && playerMana != null)
            manaNumberText.text = $"{Mathf.CeilToInt(playerMana.CurrentMana)}/{Mathf.CeilToInt(playerMana.MaxMana)}";
    }

    private void ApplyTextVisibility()
    {
        if (manaText != null)
            manaText.gameObject.SetActive(showText);
    }

    private void OnManaSpendFailed()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < flashCount; i++)
        {
            if (fillImage != null)
                fillImage.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            if (fillImage != null)
                fillImage.color = manaColor;
            yield return new WaitForSeconds(flashDuration);
        }
        flashRoutine = null;
    }

    private void OnValidate()
    {
        ApplyTextVisibility();

        if (fillImage != null)
            fillImage.color = manaColor;
    }
}
