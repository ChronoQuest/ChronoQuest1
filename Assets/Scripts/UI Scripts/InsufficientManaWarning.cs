using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows a center-screen warning when the player tries to spend mana they don't have.
/// Attach to a Canvas with a TMP_Text child. Listens to PlayerMana.OnManaSpendFailed.
/// </summary>
public class InsufficientManaWarning : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text warningText;

    [Header("Settings")]
    [TextArea(2, 3)]
    [SerializeField] private string message = "Not enough mana!\nUse melee attacks or wait.";
    [SerializeField] private float displayDuration = 1.2f;
    [SerializeField] private float fadeDuration = 0.4f;

    private PlayerMana playerMana;
    private Coroutine activeRoutine;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        playerMana = FindFirstObjectByType<PlayerMana>();
        if (playerMana != null)
            playerMana.OnManaSpendFailed += Show;

        canvasGroup = warningText != null ? warningText.GetComponent<CanvasGroup>() : null;
        if (canvasGroup == null && warningText != null)
            canvasGroup = warningText.gameObject.AddComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (playerMana != null)
            playerMana.OnManaSpendFailed -= Show;
    }

    public void Show()
    {
        if (warningText == null || canvasGroup == null) return;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        warningText.text = message;
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - (elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        activeRoutine = null;
    }
}
