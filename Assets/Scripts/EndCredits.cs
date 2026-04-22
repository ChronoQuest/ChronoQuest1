using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Watches the boss for death and, after a short pause, rolls end credits:
// fades the screen to black then scrolls placeholder credit text downward.
// Builds its own overlay canvas at runtime so only one reference (the boss)
// needs to be wired in the scene.
public class EndCredits : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The boss whose death triggers the credits. If left empty we look one up at Start.")]
    [SerializeField] private Boss boss;

    [Tooltip("Optional TMP font asset. If null we fall back to TMP_Settings.defaultFontAsset.")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Timing")]
    [Tooltip("Delay between the boss dying and the credits starting.")]
    [SerializeField] private float delayAfterDeath = 2.5f;
    [Tooltip("Seconds for the black background to fade in.")]
    [SerializeField] private float fadeDuration = 1.5f;
    [Tooltip("Total seconds the credits take to scroll across the screen.")]
    [SerializeField] private float scrollDuration = 20f;

    [Header("Content")]
    [TextArea(6, 30)]
    [SerializeField] private string creditsText =
        "CHRONOQUEST\n\n" +
        "— Credits —\n\n\n" +
        "Placeholder Name\n" +
        "Placeholder Name\n" +
        "Placeholder Name\n" +
        "Placeholder Name\n\n\n" +
        "Thanks for playing!";

    private bool triggered;

    private void Start()
    {
        if (boss == null) boss = FindFirstObjectByType<Boss>();
    }

    private void Update()
    {
        if (triggered || boss == null) return;
        if (!boss.isDead) return;
        triggered = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        float waited = 0f;
        while (waited < delayAfterDeath)
        {
            // Bail if the player rewound past the death during the pre-roll pause.
            if (boss == null || !boss.isDead) { triggered = false; yield break; }
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Image bg;
        RectTransform textRT;
        float textHeight;
        BuildOverlay(out bg, out textRT, out textHeight);

        // Fade to black.
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            bg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        bg.color = Color.black;

        // Scroll the text upward: start below the bottom of the screen, finish above the top.
        float screenH = Screen.height;
        float startY = -screenH * 0.5f - textHeight * 0.5f;
        float endY = screenH * 0.5f + textHeight * 0.5f;

        float scrolled = 0f;
        float duration = Mathf.Max(0.01f, scrollDuration);
        while (scrolled < duration)
        {
            scrolled += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(scrolled / duration);
            textRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, k));
            yield return null;
        }
    }

    private void BuildOverlay(out Image background, out RectTransform textRT, out float textHeight)
    {
        GameObject canvasGO = new GameObject("EndCreditsCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        background = bgGO.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = false;

        GameObject textGO = new GameObject("CreditsText");
        textGO.transform.SetParent(canvasGO.transform, false);
        textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.5f);
        textRT.anchorMax = new Vector2(0.5f, 0.5f);
        textRT.pivot = new Vector2(0.5f, 0.5f);
        textHeight = 1600f;
        textRT.sizeDelta = new Vector2(1200f, textHeight);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        tmp.text = creditsText;
        tmp.fontSize = 54f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        // Place the text off-screen below to start; the scroll phase drives it upward.
        textRT.anchoredPosition = new Vector2(0f, -Screen.height * 0.5f - textHeight * 0.5f);
    }
}
