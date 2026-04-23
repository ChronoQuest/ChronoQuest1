using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Listens for an EnemyBase.OnDeath event and plays a Watcher dialogue.
/// Attach to any GameObject in the scene, wire the enemy and dialogue refs.
/// Does not freeze the player — text appears and fades while gameplay continues.
/// </summary>
public class WatcherDialogueTrigger : MonoBehaviour
{
    [Header("Enemy to watch")]
    [SerializeField] private EnemyBase enemy;

    [Header("Dialogue")]
    [SerializeField] private GameObject dialogueContainer;
    [SerializeField] private TMP_Text dialogueText;
    [TextArea(2, 5)]
    [SerializeField] private string[] dialogueLines = new string[]
    {
        "...Well.",
        "I'll admit, I didn't think you had it in you.",
        "Enjoy it while it lasts..."
    };
    [SerializeField] private float charactersPerSecond = 35f;
    [SerializeField] private float pauseBetweenLines = 1.0f;
    [SerializeField] private float pauseAfterLastLine = 1.5f;
    [Tooltip("Delay after the enemy dies before the dialogue starts.")]
    [SerializeField] private float delayBeforeDialogue = 1.5f;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dialogueBlip;
    [SerializeField] private float dialogueBlipVolume = 1f;
    [SerializeField] private float minPitch = 0.6f;
    [SerializeField] private float maxPitch = 0.7f;
    [SerializeField] private int charsPerSound = 2;

    private bool hasPlayed;

    private void OnEnable()
    {
        if (enemy != null)
            enemy.OnDeath += OnEnemyDeath;
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.OnDeath -= OnEnemyDeath;
    }

    private void OnEnemyDeath()
    {
        if (hasPlayed) return;
        hasPlayed = true;
        StartCoroutine(PlayDialogue());
    }

    private IEnumerator PlayDialogue()
    {
        yield return new WaitForSeconds(delayBeforeDialogue);

        if (dialogueText == null || dialogueLines == null || dialogueLines.Length == 0)
            yield break;

        WatcherCommentary.DialogueLocked = true;

        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        yield return null;

        float timePerChar = 1f / Mathf.Max(1f, charactersPerSecond);

        foreach (string line in dialogueLines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            dialogueText.enableAutoSizing = true;
            dialogueText.text = line;
            dialogueText.ForceMeshUpdate();
            float fittedSize = dialogueText.fontSize;

            dialogueText.enableAutoSizing = false;
            dialogueText.fontSize = fittedSize;
            dialogueText.maxVisibleCharacters = 0;
            yield return null;

            for (int i = 1; i <= line.Length; i++)
            {
                dialogueText.maxVisibleCharacters = i;
                if (audioSource != null && dialogueBlip != null && i % charsPerSound == 0)
                {
                    audioSource.pitch = Random.Range(minPitch, maxPitch);
                    audioSource.PlayOneShot(dialogueBlip, dialogueBlipVolume);
                }

                yield return new WaitForSecondsRealtime(timePerChar);
            }

            yield return new WaitForSeconds(pauseBetweenLines);
        }

        yield return new WaitForSeconds(pauseAfterLastLine);

        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(false);
            dialogueContainer.SetActive(false);
        }

        dialogueText.text = "";
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueText.enableAutoSizing = true;

        WatcherCommentary.DialogueLocked = false;
    }
}
