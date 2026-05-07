using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class BossHealthBar : MonoBehaviour
{
    [Header("References")]
    public RectTransform panel;           // The bar panel that slides in/out
    public RectTransform fillRect;        // Fill image RectTransform
    public Image fillImage;               // Fill image for color changes
    public Image delayedFillImage;        // Optional ghost/lag bar (can be null)
    public TMP_Text bossNameText;
    public TMP_Text phaseText;

    [Header("Slide Animation")]
    public float slideDistance = 120f;    // How many pixels it slides up from off-screen
    public float slideInDuration  = 0.5f;
    public float slideOutDuration = 0.4f;

    [Header("Bar Settings")]
    public float lerpSpeed = 6f;
    public float delayedLerpSpeed = 2f;   // Ghost bar trails behind

    [Header("Colors")]
    public Color highHealthColor = new Color(0.18f, 0.85f, 0.35f);
    public Color midHealthColor  = new Color(1.00f, 0.80f, 0.15f);
    public Color lowHealthColor  = new Color(0.90f, 0.18f, 0.18f);

    private float displayedFill  = 1f;
    private float delayedFill    = 1f;
    private float targetFill     = 1f;

    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private bool    isVisible = false;
    private Coroutine slideCoroutine;

    private float barFullWidth;

    void Awake()
    {
        if (panel != null)
        {
            shownPosition  = panel.anchoredPosition;
            hiddenPosition = shownPosition - new Vector2(0, slideDistance);
            panel.anchoredPosition = hiddenPosition;
        }

        // if (fillRect != null && fillRect.parent != null)
        //     barFullWidth = fillRect.parent.GetComponent<RectTransform>().sizeDelta.x;

        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isVisible) return;

        // Lerp fill toward target
        displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * lerpSpeed);
        ApplyFill(displayedFill);

        // Ghost / delayed bar
        if (delayedFillImage != null)
        {
            delayedFill = Mathf.Lerp(delayedFill, displayedFill, Time.deltaTime * delayedLerpSpeed);
            ApplyDelayedFill(delayedFill);
        }
    }

    public void Show(string bossName, int phase = 1)
    {
        gameObject.SetActive(true);
        isVisible = true;

        if (bossNameText != null) bossNameText.text = bossName.ToUpper();
        SetPhase(phase);

        // Snap fill to full before sliding in
        displayedFill = 1f;
        delayedFill   = 1f;
        targetFill    = 1f;
        ApplyFill(1f);

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideTo(shownPosition, slideInDuration));
    }

    //Call when the boss dies
    public void Hide()
    {
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideOutAndDisable());
    }

    //Update the fill fraction (0-1). Called by BossHealthBarDriver
    public void SetHealth(float fraction)
    {
        targetFill = Mathf.Clamp01(fraction);
    }

    // snap fill instantly, used when rewind restores health
    public void SyncImmediate(float fraction)
    {
        targetFill    = Mathf.Clamp01(fraction);
        displayedFill = targetFill;
        delayedFill   = targetFill;
        ApplyFill(displayedFill);
    }

    /// Call when the boss enters a new phase (refills the bar)
    public void StartNewPhase(int phase)
    {
        SetPhase(phase);
        // Snap delayed fill back to full so it doesn't lag across the refill
        displayedFill = 1f;
        delayedFill   = 1f;
        targetFill    = 1f;
        ApplyFill(1f);
    }


    void SetPhase(int phase)
    {
        if (phaseText != null)
            phaseText.text = phase > 1 ? $"Phase {phase}" : "";
    }

    void ApplyFill(float t)
    {
        if (fillRect == null) return;

        // Drive fill via anchorMax so it always grows/shrinks from the left
        Vector2 max = fillRect.anchorMax;
        max.x = t;
        fillRect.anchorMax = max;

        // Clear any sizeDelta offset so anchors do all the work
        fillRect.sizeDelta = Vector2.zero;

        if (fillImage != null)
            fillImage.color = HealthColor(t);
    }

    void ApplyDelayedFill(float t)
    {
        if (delayedFillImage == null) return;
        RectTransform rt = delayedFillImage.rectTransform;
        Vector2 max = rt.anchorMax;
        max.x = t;
        rt.anchorMax = max;
        rt.sizeDelta = Vector2.zero;
    }

    Color HealthColor(float t)
    {
        if (t > 0.6f) return Color.Lerp(midHealthColor,  highHealthColor, (t - 0.6f) / 0.4f);
        if (t > 0.3f) return Color.Lerp(lowHealthColor,  midHealthColor,  (t - 0.3f) / 0.3f);
        return lowHealthColor;
    }

    IEnumerator SlideTo(Vector2 target, float duration)
    {
        Vector2 start = panel.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            panel.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }
        panel.anchoredPosition = target;
    }

    IEnumerator SlideOutAndDisable()
    {
        isVisible = false;
        yield return SlideTo(hiddenPosition, slideOutDuration);
        gameObject.SetActive(false);
    }
}