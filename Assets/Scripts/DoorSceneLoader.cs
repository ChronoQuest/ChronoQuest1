using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class DoorSceneLoader : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private bool isExitDoor;
    private Animator animator;

    [Header("Locked Door Dialogue (GameScene_3)")]
    [SerializeField] private GameObject lockedDialogueContainer;
    [SerializeField] private TMP_Text lockedDialogueText;
    [TextArea(2, 5)]
    [SerializeField] private string[] lockedDialogueLines = new string[]
    {
        "That door won't open for you.",
        "The necromancer holds the key. Kill him, or rot here."
    };
    [SerializeField] private float lockedDialogueCharsPerSec = 35f;
    [SerializeField] private float lockedDialoguePause = 1.0f;

    private bool lockedDialoguePlayed;
    private Coroutine lockedDialogueCoroutine;

    private void Start()
    {
        animator = GetComponent<Animator>();
        if (!isExitDoor) animator.SetBool("IsOpened", true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (WatcherCommentary.DialogueLocked) return;
        if (other.CompareTag("Player"))
        {
            if (isExitDoor)
            {
                if (SceneManager.GetActiveScene().name == "GameScene_3")
                {
                    var necromancer = FindFirstObjectByType<NecromancerEnemy>();
                    if (necromancer != null && !necromancer.IsDead())
                    {
                        if (!lockedDialoguePlayed && lockedDialogueText != null)
                        {
                            lockedDialoguePlayed = true;
                            lockedDialogueCoroutine = StartCoroutine(PlayLockedDialogue());
                        }
                        return;
                    }
                }

                DataCollectionService.Instance?.RecordDoorEntered(sceneName);
                DataCollectionService.Instance?.SaveSessionAndStartNew();
                animator.SetBool("IsOpened", true);
                StartCoroutine(Transition(other.gameObject));
            }
            else
            {
                animator.SetBool("IsOpened", false);
            }
        }
    }

    private IEnumerator PlayLockedDialogue()
    {
        WatcherCommentary.DialogueLocked = true;

        if (lockedDialogueContainer != null)
        {
            Canvas parentCanvas = lockedDialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);
            lockedDialogueContainer.SetActive(true);
        }

        yield return null;

        float timePerChar = 1f / Mathf.Max(1f, lockedDialogueCharsPerSec);

        foreach (string line in lockedDialogueLines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            lockedDialogueText.enableAutoSizing = true;
            lockedDialogueText.text = line;
            lockedDialogueText.ForceMeshUpdate();
            float fittedSize = lockedDialogueText.fontSize;

            lockedDialogueText.enableAutoSizing = false;
            lockedDialogueText.fontSize = fittedSize;
            lockedDialogueText.maxVisibleCharacters = 0;
            yield return null;

            for (int i = 1; i <= line.Length; i++)
            {
                lockedDialogueText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(timePerChar);
            }

            yield return new WaitForSeconds(lockedDialoguePause);
        }

        yield return new WaitForSeconds(0.5f);

        if (lockedDialogueContainer != null)
        {
            Canvas parentCanvas = lockedDialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(false);
            lockedDialogueContainer.SetActive(false);
        }

        lockedDialogueText.text = "";
        lockedDialogueText.maxVisibleCharacters = int.MaxValue;
        lockedDialogueText.enableAutoSizing = true;

        WatcherCommentary.DialogueLocked = false;
    }

    private IEnumerator Transition(GameObject player)
    {
        player.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        animator.SetBool("IsOpened", false);
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(sceneName);
    }

}