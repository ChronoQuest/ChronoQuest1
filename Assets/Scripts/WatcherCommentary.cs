using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Periodically reads the ML model beliefs and difficulty tier to deliver
/// Watcher commentary about the player's playstyle. Does not freeze the player.
///
/// Three comment categories:
///   1. Playstyle — based on PlayerTacticalModel tactic beliefs (Aggressive/Evasive/Cautious)
///   2. Rewind awareness — based on rewind frequency, evolves from confused to aware across scenes
///   3. Skill assessment — based on DynamicDifficultyManager tier, delivered near scene end
///
/// Max 2 comments per scene. Will not fire during tutorial hints or Level3 cutscene dialogue.
/// </summary>
public class WatcherCommentary : MonoBehaviour
{
    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialogueContainer;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private float charactersPerSecond = 35f;
    [SerializeField] private float pauseBetweenLines = 1.0f;
    [SerializeField] private float pauseAfterLastLine = 1.5f;

    [Header("Timing")]
    [Tooltip("Seconds into the scene before the first comment can trigger.")]
    [SerializeField] private float initialCooldown = 30f;
    [Tooltip("How often to poll the ML model for a potential comment.")]
    [SerializeField] private float pollInterval = 20f;
    [Tooltip("Minimum tactic belief to trigger a playstyle comment.")]
    [SerializeField] private float beliefThreshold = 0.55f;
    [Tooltip("Number of rewinds in this scene before a rewind comment can trigger.")]
    [SerializeField] private int rewindCountThreshold = 3;

    [Header("Limits")]
    [SerializeField] private int maxCommentsPerScene = 2;

    // ── State ───────────────────────────────────────────────────────────────
    private int commentsThisScene;
    private float sceneStartTime;
    private float nextPollTime;
    private bool isPlaying;
    private int rewindCountAtSceneStart;

    // Track which categories have fired this scene
    private bool playstyleCommentFired;
    private bool rewindCommentFired;
    private bool skillCommentFired;

    // Track rewind awareness across scenes (persists via DontDestroyOnLoad)
    private static int scenesWithHighRewind;
    private static bool instanceExists;

    private void Awake()
    {
        if (instanceExists)
        {
            Destroy(gameObject);
            return;
        }
        instanceExists = true;
        DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDestroy()
    {
        if (instanceExists && this != null)
        {
            instanceExists = false;
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }
    }

    private void Start()
    {
        ResetSceneState();
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        ResetSceneState();
    }

    private void ResetSceneState()
    {
        commentsThisScene = 0;
        playstyleCommentFired = false;
        rewindCommentFired = false;
        skillCommentFired = false;
        sceneStartTime = Time.time;
        nextPollTime = Time.time + initialCooldown;

        var data = DataCollectionService.Instance;
        rewindCountAtSceneStart = data != null ? data.RewindActivationCount : 0;

        // Hide dialogue on scene change
        HideDialogue();
    }

    private void Update()
    {
        if (isPlaying) return;
        if (commentsThisScene >= maxCommentsPerScene) return;
        if (Time.time < nextPollTime) return;

        nextPollTime = Time.time + pollInterval;

        if (IsOtherDialogueActive()) return;

        TryComment();
    }

    // ── Comment selection ────────────────────────────────────────────────────

    private void TryComment()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        // Priority order: rewind (rarer, more impactful), playstyle, skill
        if (!rewindCommentFired && TryRewindComment(sceneName))
            return;

        if (!playstyleCommentFired && TryPlaystyleComment(sceneName))
            return;

        // Skill comment fires after enough time in the scene (near end of level)
        if (!skillCommentFired && Time.time - sceneStartTime > 60f && TrySkillComment(sceneName))
            return;
    }

    private bool TryPlaystyleComment(string sceneName)
    {
        var tacticalModel = FindFirstObjectByType<PlayerTacticalModel>();
        if (tacticalModel == null) return false;

        // Find dominant tactic
        float aggressive = 0f, evasive = 0f, cautious = 0f;
        tacticalModel.tacticBeliefs.TryGetValue(PlayerTacticalModel.TacticType.Aggressive, out aggressive);
        tacticalModel.tacticBeliefs.TryGetValue(PlayerTacticalModel.TacticType.Evasive, out evasive);
        tacticalModel.tacticBeliefs.TryGetValue(PlayerTacticalModel.TacticType.Cautious, out cautious);

        // Need a clear dominant tactic
        string[] lines = null;

        if (aggressive >= beliefThreshold && aggressive > evasive && aggressive > cautious)
        {
            lines = GetAggressiveLines(sceneName);
        }
        else if (evasive >= beliefThreshold && evasive > aggressive && evasive > cautious)
        {
            lines = GetEvasiveLines(sceneName);
        }
        else if (cautious >= beliefThreshold && cautious > aggressive && cautious > evasive)
        {
            lines = GetCautiousLines(sceneName);
        }

        if (lines == null) return false;

        PlayComment(lines);
        playstyleCommentFired = true;
        return true;
    }

    private bool TryRewindComment(string sceneName)
    {
        var data = DataCollectionService.Instance;
        if (data == null) return false;

        int rewindsThisScene = data.RewindActivationCount - rewindCountAtSceneStart;
        if (rewindsThisScene < rewindCountThreshold) return false;

        string[] lines = GetRewindLines(sceneName);
        if (lines == null) return false;

        PlayComment(lines);
        rewindCommentFired = true;
        scenesWithHighRewind++;
        return true;
    }

    private bool TrySkillComment(string sceneName)
    {
        // Only at end of level 1 or 2
        if (sceneName != "GameScene" && sceneName != "GameScene_2") return false;

        var ddm = DynamicDifficultyManager.Instance;
        if (ddm == null) return false;

        string[] lines = GetSkillLines(ddm.CurrentTier);
        if (lines == null) return false;

        PlayComment(lines);
        skillCommentFired = true;
        return true;
    }

    // ── Dialogue lines ──────────────────────────────────────────────────────

    private string[] GetAggressiveLines(string sceneName)
    {
        switch (sceneName)
        {
            case "GameScene":
                return new[] { "Charging in head-first... bold. Reckless, but bold." };
            case "GameScene_2":
                return new[] { "Still swinging wildly, I see.", "You fight like you've got nothing to lose." };
            case "GameScene_3":
                return new[] { "All that fury... and yet here you are, still breathing.", "Impressive, in a brutish sort of way." };
            default:
                return null;
        }
    }

    private string[] GetEvasiveLines(string sceneName)
    {
        switch (sceneName)
        {
            case "GameScene":
                return new[] { "Running away won't save you forever." };
            case "GameScene_2":
                return new[] { "You dodge well. But you can't outrun what's coming." };
            case "GameScene_3":
                return new[] { "Always slipping away... like smoke.", "It won't matter. You'll run out of room eventually." };
            default:
                return null;
        }
    }

    private string[] GetCautiousLines(string sceneName)
    {
        switch (sceneName)
        {
            case "GameScene":
                return new[] { "Careful, aren't you? Measuring every step.", "It won't help." };
            case "GameScene_2":
                return new[] { "Still tiptoeing around, I see. Patience is a virtue... for the doomed." };
            case "GameScene_3":
                return new[] { "You think caution will keep you alive?", "How endearing." };
            default:
                return null;
        }
    }

    private string[] GetRewindLines(string sceneName)
    {
        // Early scenes: Watcher is confused about what he's sensing
        // Later scenes: he's figured it out but won't say the word "rewind"
        if (scenesWithHighRewind == 0)
        {
            // First time noticing — confused
            switch (sceneName)
            {
                case "GameScene":
                    return new[] { "...What was that?", "Something shifted. What trick are you playing?" };
                case "GameScene_2":
                    return new[] { "There it is again... that ripple.", "You're doing something. I can feel it." };
                default:
                    return new[] { "That feeling... time bending around you.", "I see what you're doing now." };
            }
        }
        else if (scenesWithHighRewind == 1)
        {
            // Second time — suspicious, starting to understand
            return new[] { "You think I haven't noticed?", "Whatever you're pulling... it won't work on me." };
        }
        else
        {
            // Third+ time — fully aware
            return new[] { "I know your tricks now. Every last one.", "Go on then. It changes nothing." };
        }
    }

    private string[] GetSkillLines(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.Easy:
                return new[] { "You're struggling, aren't you?", "This is almost too easy to watch." };
            case DifficultyTier.Hard:
                return new[] { "Not bad... not bad at all.", "But your fate is all the same." };
            case DifficultyTier.Normal:
                return new[] { "Adequate. Nothing more.", "We'll see how long that lasts." };
            default:
                return null;
        }
    }

    // ── Clash detection ─────────────────────────────────────────────────────

    private bool IsOtherDialogueActive()
    {
        // Check tutorial hints
        var tutorial = FindFirstObjectByType<TutorialManager>();
        if (tutorial != null && tutorial.currentStep != TutorialManager.TutorialStep.None
            && tutorial.currentStep != TutorialManager.TutorialStep.Complete)
            return true;

        // Check Level3 intro cutscene
        var l3Cutscene = FindFirstObjectByType<Level3IntroCutscene>();
        if (l3Cutscene != null)
        {
            // If the cutscene object exists and is active, check if it's still running
            // by seeing if the dialogue container is active
            var field = typeof(Level3IntroCutscene).GetField("cutsceneFinished",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                bool finished = (bool)field.GetValue(l3Cutscene);
                if (!finished) return true;
            }
        }

        // Check WatcherDialogueTrigger (death dialogue)
        var deathDialogue = FindFirstObjectByType<WatcherDialogueTrigger>();
        if (deathDialogue != null)
        {
            // Check if its dialogue container is currently active
            var containerField = typeof(WatcherDialogueTrigger).GetField("dialogueContainer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (containerField != null)
            {
                var container = containerField.GetValue(deathDialogue) as GameObject;
                if (container != null && container.activeInHierarchy)
                    return true;
            }
        }

        // Check locked door dialogue
        var doorLoader = FindFirstObjectByType<DoorSceneLoader>();
        if (doorLoader != null)
        {
            var containerField = typeof(DoorSceneLoader).GetField("lockedDialogueContainer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (containerField != null)
            {
                var container = containerField.GetValue(doorLoader) as GameObject;
                if (container != null && container.activeInHierarchy)
                    return true;
            }
        }

        return false;
    }

    // ── Playback ─────────────────────────────────────────────────────────────

    private void PlayComment(string[] lines)
    {
        commentsThisScene++;
        StartCoroutine(PlayDialogue(lines));
    }

    private IEnumerator PlayDialogue(string[] lines)
    {
        isPlaying = true;

        if (dialogueText == null || lines == null || lines.Length == 0)
        {
            isPlaying = false;
            yield break;
        }

        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        yield return null;

        float timePerChar = 1f / Mathf.Max(1f, charactersPerSecond);

        foreach (string line in lines)
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
                yield return new WaitForSeconds(timePerChar);
            }

            yield return new WaitForSeconds(pauseBetweenLines);
        }

        yield return new WaitForSeconds(pauseAfterLastLine);

        HideDialogue();
        isPlaying = false;
    }

    private void HideDialogue()
    {
        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(false);
            dialogueContainer.SetActive(false);
        }

        if (dialogueText != null)
        {
            dialogueText.text = "";
            dialogueText.maxVisibleCharacters = int.MaxValue;
            dialogueText.enableAutoSizing = true;
        }
    }
}
