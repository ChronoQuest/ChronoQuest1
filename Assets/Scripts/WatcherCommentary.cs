using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using TimeRewind;

// reads ML beliefs + difficulty tier to fire watcher commentary. event-driven.
// rewind/skill/playstyle each fire from their own triggers. max 3 per scene.
// GameScene_2 favours skill over playstyle, GameScene_3 favours playstyle over skill
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

    [Header("Playstyle Timing")]
    [Tooltip("Seconds into the scene before playstyle events can trigger a comment.")]
    [SerializeField] private float playstyleMinSceneTime = 30f;
    [Tooltip("Minimum tactic belief to trigger a playstyle comment.")]
    [SerializeField] private float beliefThreshold = 0.55f;
    [Tooltip("Delay after the triggering event before the playstyle comment plays.")]
    [SerializeField] private float playstyleCommentDelay = 1.5f;

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

    private int commentsThisScene;
    private float sceneStartTime;
    private float lastCommentTime;
    private bool isPlaying;

    private bool playstyleCommentFired;
    private bool rewindCommentFired;
    private bool skillCommentFired;

    private int rewindsThisScene;

    // Event subscriptions
    private PlayerRewindController cachedRewindController;
    private PlayerHealth cachedPlayerHealth;
    private PlayerPlatformer cachedPlatformer;
    private PlayerCombat cachedCombat;
    private PlayerSpellSystem cachedSpellSystem;
    private List<EnemyBase> subscribedEnemies = new List<EnemyBase>();

    // Rewind awareness persists across scenes via static
    private static int scenesWithHighRewind;

    // other dialogue scripts set this while showing text
    public static bool DialogueLocked { get; set; }

    // call on restart / main menu to reset cross-scene watcher state
    public static void ResetAll()
    {
        scenesWithHighRewind = 0;
        DialogueLocked = false;
    }

    private void Start()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        // Reset cross-scene state when entering title screen or the first level (restart)
        if (sceneName == "TitleScreen" || sceneName == "GameScene")
            ResetAll();

        sceneStartTime = Time.time;
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

        // Player damage events (skill trigger for struggling players)
        cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>();
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged += OnPlayerHealthChanged;

        // Enemy death events (skill trigger for good players)
        foreach (var enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            enemy.OnDeath += () => OnEnemyKilled(enemy);
            subscribedEnemies.Add(enemy);
        }

        // Dash and jump events (skill trigger + defensive playstyle trigger)
        cachedPlatformer = FindFirstObjectByType<PlayerPlatformer>();
        if (cachedPlatformer != null)
        {
            cachedPlatformer.OnDashed += OnPlayerDashed;
            cachedPlatformer.OnJumped += OnPlayerJumped;
        }

        // Melee hit events (aggressive playstyle trigger)
        cachedCombat = FindFirstObjectByType<PlayerCombat>();
        if (cachedCombat != null)
        {
            cachedCombat.OnMeleeHit += OnPlayerMeleeHit;
            cachedCombat.OnRainSpellCast += OnPlayerRainSpell;
        }

        // Spell cast events (ability-focused playstyle trigger)
        cachedSpellSystem = FindFirstObjectByType<PlayerSpellSystem>();
        if (cachedSpellSystem != null)
            cachedSpellSystem.OnSpellCast += OnPlayerSpellCast;
    }

    private void UnsubscribeFromEvents()
    {
        if (cachedRewindController != null)
            cachedRewindController.OnRewindStopped -= OnRewindStopped;

        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged -= OnPlayerHealthChanged;

        if (cachedPlatformer != null)
        {
            cachedPlatformer.OnDashed -= OnPlayerDashed;
            cachedPlatformer.OnJumped -= OnPlayerJumped;
        }

        if (cachedCombat != null)
        {
            cachedCombat.OnMeleeHit -= OnPlayerMeleeHit;
            cachedCombat.OnRainSpellCast -= OnPlayerRainSpell;
        }

        if (cachedSpellSystem != null)
            cachedSpellSystem.OnSpellCast -= OnPlayerSpellCast;
    }

    // GameScene_3 favours playstyle, so block skill if playstyle hasnt fired yet
    private bool SkillAllowedByClash()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        // GameScene_3 favours playstyle: block skill if playstyle hasn't had its chance yet
        if (sceneName == "GameScene_3" && enablePlaystyleComments && !playstyleCommentFired)
            return false;
        return true;
    }

    // GameScene_2 favours skill, so block playstyle if skill hasnt fired yet
    private bool PlaystyleAllowedByClash()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        // GameScene_2 favours skill: block playstyle if skill hasn't had its chance yet
        if (sceneName == "GameScene_2" && enableSkillComments && !skillCommentFired)
            return false;
        return true;
    }

    private void OnRewindStopped()
    {
        rewindsThisScene++;

        if (!enableRewindComments) return;
        if (rewindCommentFired) return;
        if (rewindsThisScene < rewindCountThreshold) return;
        if (!CanComment()) return;

        StartCoroutine(DelayedRewindComment());
    }

    private IEnumerator DelayedRewindComment()
    {
        yield return new WaitForSeconds(rewindCommentDelay);

        if (!CanComment()) yield break;
        if (rewindCommentFired) yield break;

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
        if (!SkillAllowedByClash()) return;

        var ddm = DynamicDifficultyManager.Instance;
        if (ddm == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "GameScene_2" && sceneName != "GameScene_3") return;

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
        if (!SkillAllowedByClash()) return;

        var ddm = DynamicDifficultyManager.Instance;
        if (ddm == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "GameScene_2" && sceneName != "GameScene_3") return;

        if (ddm.CurrentTier == DifficultyTier.Hard || ddm.CurrentTier == DifficultyTier.Normal)
        {
            StartCoroutine(DelayedSkillComment(ddm.CurrentTier));
        }
    }

    private void TrySkillFromDashOrKill()
    {
        if (!enableSkillComments) return;
        if (skillCommentFired) return;
        if (Time.time - sceneStartTime < skillMinSceneTime) return;
        if (!CanComment()) return;
        if (!SkillAllowedByClash()) return;

        var ddm = DynamicDifficultyManager.Instance;
        if (ddm == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "GameScene_2" && sceneName != "GameScene_3") return;

        if (ddm.CurrentTier == DifficultyTier.Hard || ddm.CurrentTier == DifficultyTier.Normal)
        {
            StartCoroutine(DelayedSkillComment(ddm.CurrentTier));
        }
    }

    // called by PlatformSectionGoal etc. to trigger a skill comment for Normal/Hard
    // players without the scene-time gate
    public void TryFireSkillComment()
    {
        if (!enableSkillComments) return;
        if (skillCommentFired) return;
        if (!CanComment()) return;
        if (!SkillAllowedByClash()) return;

        var ddm = DynamicDifficultyManager.Instance;
        if (ddm == null) return;

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

    // aggressive: melee hit
    private void OnPlayerMeleeHit()
    {
        TryPlaystyleFromEvent("aggressive");
    }

    // Ability-focused: normal spell or rain spell
    private void OnPlayerSpellCast()
    {
        TryPlaystyleFromEvent("ability");
    }

    private void OnPlayerRainSpell()
    {
        TryPlaystyleFromEvent("ability");
    }

    // Defensive: dash or jump
    private void OnPlayerDashed()
    {
        // Dash triggers both skill (for good players) and defensive playstyle
        TrySkillFromDashOrKill();
        TryPlaystyleFromEvent("defensive");
    }

    private void OnPlayerJumped()
    {
        TryPlaystyleFromEvent("defensive");
    }

    private void TryPlaystyleFromEvent(string triggerCategory)
    {
        if (!enablePlaystyleComments) return;
        if (playstyleCommentFired) return;
        if (Time.time - sceneStartTime < playstyleMinSceneTime) return;
        if (!CanComment()) return;
        if (DialogueLocked) return;
        if (!PlaystyleAllowedByClash()) return;

        var strategyModel = FindFirstObjectByType<PlayerStrategyModel>();
        if (strategyModel == null || strategyModel.playerTacticalModel == null
            || strategyModel.playerTacticalModel.gmmModel == null
            || strategyModel.playerTacticalModel.gmmModel.means == null)
        {
            StartCoroutine(DelayedPlaystyleComment(new[] { "Hmm... I can't read you.", "What are you?" }));
            return;
        }

        float aggressive = 0f, defensive = 0f, abilityFocused = 0f;
        strategyModel.strategyBeliefs.TryGetValue(PlayerStrategyModel.StrategyType.AggressivePlayer, out aggressive);
        strategyModel.strategyBeliefs.TryGetValue(PlayerStrategyModel.StrategyType.DefensivePlayer, out defensive);
        strategyModel.strategyBeliefs.TryGetValue(PlayerStrategyModel.StrategyType.AbilityFocusedPlayer, out abilityFocused);

        if (aggressive + defensive + abilityFocused < 0.01f) return;

        string sceneName = SceneManager.GetActiveScene().name;
        string[] lines = null;

        // Check for balanced playstyle first
        bool isBalanced = aggressive > 0.2f && defensive > 0.2f && abilityFocused > 0.2f
            && Mathf.Abs(aggressive - defensive) < 0.15f
            && Mathf.Abs(aggressive - abilityFocused) < 0.15f
            && Mathf.Abs(defensive - abilityFocused) < 0.15f;
        if (isBalanced)
        {
            lines = GetBalancedLines(sceneName);
        }
        else
        {
            // Only fire if the trigger matches the dominant belief, or
            // the dominant belief matches the trigger category
            switch (triggerCategory)
            {
                case "aggressive":
                    if (aggressive >= defensive && aggressive >= abilityFocused && aggressive >= beliefThreshold)
                        lines = GetAggressiveLines(sceneName);
                    break;
                case "ability":
                    if (abilityFocused >= aggressive && abilityFocused >= defensive && abilityFocused >= beliefThreshold)
                        lines = GetCautiousLines(sceneName);
                    break;
                case "defensive":
                    if (defensive >= aggressive && defensive >= abilityFocused && defensive >= beliefThreshold)
                        lines = GetEvasiveLines(sceneName);
                    break;
            }
        }

        if (lines == null) return;

        StartCoroutine(DelayedPlaystyleComment(lines));
    }

    private IEnumerator DelayedPlaystyleComment(string[] lines)
    {
        yield return new WaitForSeconds(playstyleCommentDelay);

        if (!CanComment()) yield break;
        if (playstyleCommentFired) yield break;

        PlayComment(lines);
        playstyleCommentFired = true;
    }

    private bool CanComment()
    {
        if (commentsThisScene >= maxCommentsPerScene) return false;
        if (isPlaying) return false;
        if (DialogueLocked) return false;
        if (Time.time - lastCommentTime < commentCooldown) return false;
        return true;
    }

    private string[] GetBalancedLines(string sceneName)
    {
        switch (sceneName)
        {
            case "GameScene":
                return new[] { "You adapt... blade, spell, instinct.", "I haven't seen that in a long time." };
            case "GameScene_2":
                return new[] { "No weakness to exploit. You fight with everything you have.", "That makes you... dangerous." };
            case "GameScene_3":
                return new[] { "Balanced in every way.", "I almost respect it." };
            default:
                return null;
        }
    }

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

                yield return PauseAwareWait.Seconds(timePerChar);
            }

            yield return new WaitForSeconds(pauseBetweenLines);
        }

        yield return new WaitForSeconds(pauseAfterLastLine);

        if (audioSource != null) audioSource.pitch = 1f;

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
