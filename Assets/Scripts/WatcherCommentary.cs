using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using TimeRewind;

/// <summary>
/// Reads ML model beliefs and difficulty tier to deliver Watcher commentary.
/// Event-driven: rewind comments fire after rewinds, skill comments fire after
/// kills or damage, playstyle comments fire on a poll timer.
/// Weighted random selection between comment categories.
/// Max 2 comments per scene. Does not freeze the player.
/// </summary>
public class WatcherCommentary : MonoBehaviour
{
    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialogueContainer;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private float charactersPerSecond = 35f;
    [SerializeField] private float pauseBetweenLines = 1.0f;
    [SerializeField] private float pauseAfterLastLine = 1.5f;

    [Header("Enable / Disable Comment Types")]
    [SerializeField] private bool enablePlaystyleComments = true;
    [SerializeField] private bool enableRewindComments = true;
    [SerializeField] private bool enableSkillComments = true;

    [Header("Category Weights (higher = more likely to be picked)")]
    [SerializeField] private float playstyleWeight = 1f;
    [SerializeField] private float rewindWeight = 1.5f;
    [SerializeField] private float skillWeight = 1f;

    [Header("Playstyle Timing")]
    [Tooltip("Seconds into the scene before playstyle polling starts.")]
    [SerializeField] private float initialCooldown = 30f;
    [Tooltip("How often to poll the ML model for a playstyle comment.")]
    [SerializeField] private float pollInterval = 20f;
    [Tooltip("Minimum tactic belief to trigger a playstyle comment.")]
    [SerializeField] private float beliefThreshold = 0.55f;

    [Header("Rewind")]
    [Tooltip("Number of rewinds in this scene before a rewind comment can trigger.")]
    [SerializeField] private int rewindCountThreshold = 3;
    [Tooltip("Delay after rewind ends before the comment plays.")]
    [SerializeField] private float rewindCommentDelay = 1.5f;

    [Header("Skill")]
    [Tooltip("Minimum seconds into the scene before skill comments can fire.")]
    [SerializeField] private float skillMinSceneTime = 45f;
    [Tooltip("Delay after the triggering event before the skill comment plays.")]
    [SerializeField] private float skillCommentDelay = 1.5f;

    [Header("Limits")]
    [SerializeField] private int maxCommentsPerScene = 3;
    [Tooltip("Minimum seconds between any two comments.")]
    [SerializeField] private float commentCooldown = 15f;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dialogueBlip;
    [SerializeField] private float dialogueBlipVolume = 1f;
    [SerializeField] private float minPitch = 0.6f;
    [SerializeField] private float maxPitch = 0.7f;
    [SerializeField] private int charsPerSound = 2;

    // ── State ───────────────────────────────────────────────────────────────
    private int commentsThisScene;
    private float sceneStartTime;
    private float nextPollTime;
    private float lastCommentTime;
    private bool isPlaying;

    private bool playstyleCommentFired;
    private bool rewindCommentFired;
    private bool skillCommentFired;

    private int rewindsThisScene;

    // Event subscriptions
    private PlayerRewindController cachedRewindController;
    private PlayerHealth cachedPlayerHealth;
    private List<EnemyBase> subscribedEnemies = new List<EnemyBase>();

    // Rewind awareness persists across scenes via static
    private static int scenesWithHighRewind;

    // ── Clash detection ─────────────────────────────────────────────────────

    /// <summary>
    /// Static flag other dialogue scripts set while they're showing text.
    /// </summary>
    public static bool DialogueLocked { get; set; }

    /// <summary>
    /// Call this when the player restarts or returns to the main menu
    /// to reset all cross-scene Watcher state.
    /// </summary>
    public static void ResetAll()
    {
        scenesWithHighRewind = 0;
        DialogueLocked = false;
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Start()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        // Reset cross-scene state when entering title screen or the first level (restart)
        if (sceneName == "TitleScreen" || sceneName == "GameScene")
            ResetAll();

        sceneStartTime = Time.time;
        nextPollTime = Time.time + initialCooldown;
        lastCommentTime = -commentCooldown; // allow immediate first comment
        DialogueLocked = false;
        isPlaying = false;

        Debug.Log($"[WatcherCommentary] Started in {sceneName}");

        HideDialogue();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        // Rewind events
        cachedRewindController = FindFirstObjectByType<PlayerRewindController>();
        if (cachedRewindController != null)
            cachedRewindController.OnRewindStopped += OnRewindStopped;

        // Player damage events
        cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>();
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged += OnPlayerHealthChanged;

        // Enemy death events — subscribe to all enemies in the scene
        foreach (var enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            enemy.OnDeath += () => OnEnemyKilled(enemy);
            subscribedEnemies.Add(enemy);
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (cachedRewindController != null)
            cachedRewindController.OnRewindStopped -= OnRewindStopped;

        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged -= OnPlayerHealthChanged;
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private void OnRewindStopped()
    {
        rewindsThisScene++;

        if (!enableRewindComments) return;
        if (rewindCommentFired) return;
        if (rewindsThisScene < rewindCountThreshold) return;
        if (!CanComment()) return;

        // Try rewind via weighted selection (but it's the triggered category)
        StartCoroutine(DelayedRewindComment());
    }

    private IEnumerator DelayedRewindComment()
    {
        yield return new WaitForSeconds(rewindCommentDelay);

        if (!CanComment()) yield break;
        if (rewindCommentFired) yield break;

        string sceneName = SceneManager.GetActiveScene().name;
        string[] lines = GetRewindLines();
        if (lines == null) yield break;

        PlayComment(lines);
        rewindCommentFired = true;
        scenesWithHighRewind++;
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        if (!enableSkillComments) return;
        if (skillCommentFired) return;
        if (Time.time - sceneStartTime < skillMinSceneTime) return;
        if (!CanComment()) return;

        var ddm = DynamicDifficultyManager.Instance;
        if (ddm == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "GameScene" && sceneName != "GameScene_2") return;

        // Only trigger on damage (health went down) for struggling players
        if (ddm.CurrentTier == DifficultyTier.Easy || ddm.CurrentTier == DifficultyTier.VeryEasy)
        {
            StartCoroutine(DelayedSkillComment(ddm.CurrentTier));
        }
    }

    private void OnEnemyKilled(EnemyBase enemy)
    {
        if (!enableSkillComments) return;
        if (skillCommentFired) return;
        if (Time.time - sceneStartTime < skillMinSceneTime) return;
        if (!CanComment()) return;

        var ddm = DynamicDifficultyManager.Instance;
        if (ddm == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "GameScene" && sceneName != "GameScene_2") return;

        // Trigger on kill for good/normal players
        if (ddm.CurrentTier == DifficultyTier.Hard || ddm.CurrentTier == DifficultyTier.Normal)
        {
            StartCoroutine(DelayedSkillComment(ddm.CurrentTier));
        }
    }

    private IEnumerator DelayedSkillComment(DifficultyTier tier)
    {
        yield return new WaitForSeconds(skillCommentDelay);

        if (!CanComment()) yield break;
        if (skillCommentFired) yield break;

        string[] lines = GetSkillLines(tier);
        if (lines == null) yield break;

        PlayComment(lines);
        skillCommentFired = true;
    }

    // ── Poll-based playstyle check ──────────────────────────────────────────

    private void Update()
    {
        if (!enablePlaystyleComments) return;
        if (isPlaying) return;
        if (playstyleCommentFired) return;
        if (!CanComment()) return;
        if (Time.time < nextPollTime) return;

        nextPollTime = Time.time + pollInterval;

        if (DialogueLocked) return;

        // Use weighted selection: if playstyle is chosen, try it.
        // If not, we just skip this tick (event-driven categories handle themselves).
        if (ShouldPickPlaystyle())
        {
            TryPlaystyleComment();
        }
    }

    /// <summary>
    /// Weighted coin flip: should we attempt a playstyle comment this tick?
    /// Only considers playstyle vs "do nothing" — rewind and skill are event-driven.
    /// Returns true with probability proportional to playstyleWeight.
    /// </summary>
    private bool ShouldPickPlaystyle()
    {
        // If the other event-driven categories haven't fired yet, they might still
        // fire, so we scale down the playstyle chance proportionally.
        float totalWeight = playstyleWeight;
        if (enableRewindComments && !rewindCommentFired) totalWeight += rewindWeight;
        if (enableSkillComments && !skillCommentFired) totalWeight += skillWeight;

        float roll = Random.value * totalWeight;
        return roll < playstyleWeight;
    }

    private void TryPlaystyleComment()
    {
        var strategyModel = FindFirstObjectByType<PlayerStrategyModel>();
        if (strategyModel == null || strategyModel.playerTacticalModel == null
            || strategyModel.playerTacticalModel.gmmModel == null
            || strategyModel.playerTacticalModel.gmmModel.means == null)
        {
            // GMM not loaded — Watcher can't read the player
            PlayComment(new[] { "Hmm... I can't read you.", "What are you?" });
            playstyleCommentFired = true;
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;

        float aggressive = 0f, defensive = 0f, abilityFocused = 0f;
        strategyModel.strategyBeliefs.TryGetValue(PlayerStrategyModel.StrategyType.AggressivePlayer, out aggressive);
        strategyModel.strategyBeliefs.TryGetValue(PlayerStrategyModel.StrategyType.DefensivePlayer, out defensive);
        strategyModel.strategyBeliefs.TryGetValue(PlayerStrategyModel.StrategyType.AbilityFocusedPlayer, out abilityFocused);

        if (aggressive + defensive + abilityFocused < 0.01f) return;

        string[] lines = null;

        if (aggressive >= defensive && aggressive >= abilityFocused && aggressive >= beliefThreshold)
            lines = GetAggressiveLines(sceneName);
        else if (defensive >= aggressive && defensive >= abilityFocused && defensive >= beliefThreshold)
            lines = GetEvasiveLines(sceneName);
        else if (abilityFocused >= aggressive && abilityFocused >= defensive && abilityFocused >= beliefThreshold)
            lines = GetCautiousLines(sceneName);

        // Fallback after enough time
        if (lines == null && Time.time - sceneStartTime > 45f)
        {
            if (aggressive >= defensive && aggressive >= abilityFocused)
                lines = GetAggressiveLines(sceneName);
            else if (defensive >= aggressive && defensive >= abilityFocused)
                lines = GetEvasiveLines(sceneName);
            else
                lines = GetCautiousLines(sceneName);
        }

        if (lines == null) return;

        PlayComment(lines);
        playstyleCommentFired = true;
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    private bool CanComment()
    {
        if (commentsThisScene >= maxCommentsPerScene) return false;
        if (isPlaying) return false;
        if (DialogueLocked) return false;
        if (Time.time - lastCommentTime < commentCooldown) return false;
        return true;
    }

    // ── Dialogue lines ──────────────────────────────────────────────────────

    private string[] GetAggressiveLines(string sceneName)
    {
        switch (sceneName)
        {
            case "GameScene":
                return new[] { "Charging in head-first...","bold..."," ...Reckless, but bold." };
            case "GameScene_2":
                return new[] { "All that fury... and yet here you are, still breathing.", "Impressive, in a brutish sort of way." };
            case "GameScene_3":
                return new[] { "Still swinging wildly, I see.", "You fight like you've got nothing to lose." };
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
                return new[] { "A spellcaster... interesting.", "Let's see how long that mana holds." };
            case "GameScene_2":
                return new[] { "Still leaning on your little magic.", "Every spell you cast... I learn something new about you." };
            case "GameScene_3":
                return new[] { "You wield your magic like a crutch.", "When it fails you... and it will... what then?" };
            default:
                return null;
        }
    }

    private string[] GetRewindLines()
    {
        // Awareness must progress in order — later lines only play
        // if the earlier confused reaction has already happened
        if (scenesWithHighRewind == 0)
        {
            return new[] { "...What was that?", "Something shifted. What trick are you playing?" };
        }
        else if (scenesWithHighRewind == 1)
        {
            return new[] { "There it is again... that ripple.", "You're doing something. I can feel it." };
        }
        else if (scenesWithHighRewind == 2)
        {
            return new[] { "You think I haven't noticed?", "That little trick of yours...","it won't work on me." };
        }
        else
        {
            return new[] { "I know your tricks now. Every last one.", "Go on then. It changes nothing." };
        }
    }

    private string[] GetSkillLines(DifficultyTier tier)
    {
        switch (tier)
        {
            case DifficultyTier.VeryEasy:
                return new[] { "This is painful to watch.", "You're barely holding on, aren't you?" };
            case DifficultyTier.Easy:
                return new[] { "You're struggling, aren't you?", "This is almost too easy to watch." };
            case DifficultyTier.Normal:
                return new[] { "Adequate. Nothing more.", "far from enough to match me..." };
            case DifficultyTier.Hard:
                return new[] { "Not bad... not bad at all.", "But your fate is all the same." };
            default:
                return null;
        }
    }

    // ── Playback ─────────────────────────────────────────────────────────────

    private void PlayComment(string[] lines)
    {
        commentsThisScene++;
        lastCommentTime = Time.time;
        Debug.Log($"[WatcherCommentary] Playing comment ({commentsThisScene}/{maxCommentsPerScene}): \"{lines[0]}\"");
        StartCoroutine(PlayDialogue(lines));
    }

    private IEnumerator PlayDialogue(string[] lines)
    {
        
        isPlaying = true;
        DialogueLocked = true;

        if (dialogueText == null || lines == null || lines.Length == 0)
        {
            isPlaying = false;
            DialogueLocked = false;
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

        HideDialogue();
        isPlaying = false;
        DialogueLocked = false;
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
