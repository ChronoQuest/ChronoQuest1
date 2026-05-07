using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using TimeRewind;

// drives the two-phase boss fight. player walks into the trigger -> phase 1 dialogue
// -> Boss.BeginFight. after phase2TimerSeconds, the boss "rewinds" the fight back to
// the start (red tint instead of blue), then phase 2 dialogue + AdvanceToPhase2.
// phase2 latch is one-way so a later rewind doesnt replay the dialogue
[RequireComponent(typeof(Collider2D))]
public class BossFightController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Boss this controller drives. BeginFight/AdvanceToPhase2 are called on it.")]
    [SerializeField] private Boss boss;

    [Tooltip("Disabled during dialogue so input actions dont fire.")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Player root. Every MonoBehaviour on it gets disabled during dialogue, " +
             "except this controller and PlayerInput. Colliders untouched.")]
    [SerializeField] private GameObject playerScriptsRoot;

    [Tooltip("X is frozen during dialogue so spell recoil cant push the player " +
             "sideways. Y stays free so gravity still pulls down if airborne.")]
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Tooltip("Used to poll grounded state during dialogue so the animator's " +
             "isGrounded bool stays correct and a mid-air lock doesnt look like " +
             "walking on air.")]
    [SerializeField] private PlayerPlatformer playerPlatformer;

    [Tooltip("Forced to idle state on dialogue so the player doesnt stay stuck " +
             "on the fall/jump/cast frame.")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("Name of the idle state on the player Animator. Case-sensitive.")]
    [SerializeField] private string playerIdleStateName = "Player_Idle";

    [Tooltip("Animator bools forced TRUE on lock (e.g. isGrounded).")]
    [SerializeField] private string[] playerAnimatorBoolsToForceTrue = new string[] { "isGrounded" };

    [Tooltip("Animator bools forced FALSE on lock (e.g. isWallSliding).")]
    [SerializeField] private string[] playerAnimatorBoolsToForceFalse = new string[] { "isWallSliding", "IsFrozen" };

    [Tooltip("Refilled to max when phase 2 starts.")]
    [SerializeField] private PlayerMana playerMana;

    [Header("Dialogue UI")]
    [Tooltip("Dialogue panel root. Hidden at start, shown during dialogue, hidden again at end.")]
    [SerializeField] private GameObject dialogueContainer;

    [Tooltip("TextMeshProUGUI inside the dialogue panel where lines type out.")]
    [SerializeField] private TMP_Text dialogueText;

    [Header("Phase 1 Dialogue (fight start)")]
    [TextArea(2, 5)]
    [SerializeField] private string[] phase1Lines = new string[]
    {
        "Placeholder phase 1 line 1.",
        "Placeholder phase 1 line 2.",
        "Placeholder phase 1 line 3."
    };

    [Header("Phase 2 Dialogue (after boss rewind)")]
    [TextArea(2, 5)]
    [SerializeField] private string[] phase2Lines = new string[]
    {
        "You think only you could do that?",
        "Placeholder line 2.",
        "Placeholder line 3."
    };

    [Header("Dialogue Pacing")]
    [SerializeField] private float charactersPerSecond = 30f;
    [SerializeField] private float pauseBetweenLines = 1.2f;
    [SerializeField] private float pauseAfterLastLine = 0.8f;

    [Header("Phase 2 Final Line Emphasis")]
    [Tooltip("Slower CPS for the last line of phase 2. Adds weight to the final beat.")]
    [SerializeField] private float lastLineCharactersPerSecond = 12f;

    [Tooltip("Per-glyph shake radius for every visible character on the last phase 2 line.")]
    [SerializeField] private float lastLineShakeAmount = 1.5f;

    [Tooltip("Shake radius for characters wrapped in <link=heavy> on the last phase 2 line.")]
    [SerializeField] private float lastLineHeavyShakeAmount = 4f;

    [Header("Phase 2 Trigger")]
    [Tooltip("Seconds of fight time after BeginFight before phase 2 fires.")]
    [SerializeField] private float phase2TimerSeconds = 25f;

    [Tooltip("Health fraction (0-1) at or below which phase 2 fires early. " +
             "Whichever fires first wins. Set to 0 to disable.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase2HealthThreshold = 0.2f;

    [Header("Phase 2 Playstyle Hint")]
    [Tooltip("Enable the mid-fight hint that tells the player to change playstyle.")]
    [SerializeField] private bool enablePlaystyleHint = true;
    [Tooltip("How many times the player must take damage in Phase 2 before the hint fires.")]
    [SerializeField] private int hintDamageThreshold = 5;
    [Tooltip("Minimum seconds into Phase 2 before the hint can trigger.")]
    [SerializeField] private float hintMinPhase2Time = 15f;

    [TextArea(2, 5)]
    [SerializeField] private string[] hintLinesAggressive = new string[]
    {
        "Not a thought in that head... just mindlessly attacking.",
        "Maybe if you were a little more cautious, you'd stand a chance."
    };
    [TextArea(2, 5)]
    [SerializeField] private string[] hintLinesEvasive = new string[]
    {
        "All that running and nothing to show for it.",
        "Stop fleeing and fight back. Hit me if you can."
    };
    [TextArea(2, 5)]
    [SerializeField] private string[] hintLinesAbilityFocused = new string[]
    {
        "Spells won't save you forever, little mage.",
        "Your magic is predictable. Try something I haven't already seen."
    };
    [TextArea(2, 5)]
    [SerializeField] private string[] hintLinesFallback = new string[]
    {
        "You're struggling.",
        "Perhaps a change of approach is in order..."
    };

    [Header("Boss Death Dialogue")]
    [SerializeField] private float deathDialogueDelay = 1.5f;

    [Header("Boss Rewind")]
    [Tooltip("Put into external mode during the boss rewind so mana isnt spent " +
             "and it doesnt stop when R is released.")]
    [SerializeField] private PlayerRewindController playerRewindController;

    [Tooltip("Swapped to red during the boss rewind, restored to blue after.")]
    [SerializeField] private RewindEffects rewindEffects;

    [Tooltip("Rewind tint while the boss is rewinding.")]
    [SerializeField] private Color bossRewindTint = new Color(1f, 0.55f, 0.55f, 0.2f);

    [Tooltip("Burst tint at the start of the boss rewind.")]
    [SerializeField] private Color bossRewindBurstTint = new Color(1f, 0.45f, 0.45f, 0.35f);

    [Tooltip("Optional. If left empty its resolved from playerRewindController at runtime " +
             "(it gets added in Awake so its often not draggable in the Inspector).")]
    [SerializeField] private RewindGhostTrail rewindGhostTrail;

    [Tooltip("Used as the ghost trail source during the boss rewind.")]
    [SerializeField] private SpriteRenderer bossSprite;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bossRewindStartClip;
    [Range(0f, 1f)]
    [SerializeField] private float bossRewindVolume = 1f;
    [SerializeField] private AudioClip dialogueBlip;
    [SerializeField] private float dialogueBlipVolume = 1.5f;
    [SerializeField] private float minPitch = 0.6f;
    [SerializeField] private float maxPitch = 0.7f;
    [SerializeField] private int charsPerSound = 2;

    [Header("Boss Rewind")]
    [Tooltip("Speed multiplier applied to the boss rewind on top of TimeRewindManager.rewindSpeed.")]
    [Min(0.1f)]
    [SerializeField] private float bossRewindSpeedMultiplier = 3f;

    [Tooltip("Read-only estimate of how long the boss rewind will take. Updates in editor.")]
    [SerializeField] private float estimatedBossRewindDurationSeconds;

    private bool fightStarted;
    private bool phase2Triggered;
    private float fightStartTime;
    private bool hintTriggered;
    private int phase2DamageCount;
    private float phase2StartTime;
    private PlayerHealth cachedPlayerHealth;

    // scripts we turned off during dialogue. tracked so we only re-enable what we
    // actually disabled (anything already disabled stays disabled)
    private readonly List<MonoBehaviour> disabledDuringDialogue = new List<MonoBehaviour>();

    // recomputes the rewind duration preview when the inspector changes. pulls speed
    // from the scene's TimeRewindManager if its there, defaults to 1.3 otherwise
    private void OnValidate()
    {
        float managerSpeed = 1.3f;
#if UNITY_EDITOR
        TimeRewindManager mgr = FindFirstObjectByType<TimeRewindManager>();
        if (mgr != null) managerSpeed = mgr.RewindSpeed;
#endif
        float effective = Mathf.Max(0.01f, managerSpeed * bossRewindSpeedMultiplier);
        estimatedBossRewindDurationSeconds = phase2TimerSeconds / effective;
    }

    private void Awake()
    {
        if (dialogueContainer != null) dialogueContainer.SetActive(false);

        // middle-center alignment so multi-line text stays centered in the box
        if (dialogueText != null) dialogueText.alignment = TextAlignmentOptions.Center;

        // make sure our collider is a trigger, otherwise OnTriggerEnter2D never fires
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (boss != null)
            boss.OnDeath += OnBossDied;
    }

    private void OnDestroy()
    {
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged -= OnPlayerDamagedInPhase2;
        if (boss != null)
            boss.OnDeath -= OnBossDied;
    }

    private void Update()
    {
        if (!fightStarted || boss == null) return;

        // phase 1 -> phase 2 fires on the timer, or early if boss health drops below
        // phase2HealthThreshold. latch is one-way so only the first path triggers
        if (!phase2Triggered && boss.fightStage == Boss.FightStage.Phase1)
        {
            // if the player is mid-rewind, wait. starting ours would collide with the
            // manager's already-active rewind
            if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding) return;

            bool timerElapsed = Time.time - fightStartTime >= phase2TimerSeconds;
            // health > 0 guard so a killing blow doesnt race OnDeath into starting
            // phase 2 on a dead boss
            bool lowHealth = phase2HealthThreshold > 0f
                             && boss.health > 0
                             && boss.startHealth > 0
                             && (float)boss.health / boss.startHealth <= phase2HealthThreshold;

            if (timerElapsed || lowHealth)
            {
                phase2Triggered = true;
                StartCoroutine(RunPhase2Transition());
            }
        }

        // Phase 2 playstyle hint
        if (enablePlaystyleHint && !hintTriggered && boss.fightStage == Boss.FightStage.Phase2
            && phase2DamageCount >= hintDamageThreshold
            && Time.time - phase2StartTime >= hintMinPhase2Time
            && !boss.dialoguePaused)
        {
            hintTriggered = true;
            StartCoroutine(RunPlaystyleHint());
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fightStarted) return;
        if (!other.CompareTag("Player")) return;
        fightStarted = true;
        StartCoroutine(RunPhase1Intro());
    }

    private IEnumerator RunPhase1Intro()
    {
        // boss is still Waiting so its Update already early-returns. just lock the player.
        // timeScale stays at 1 so the idle anim keeps playing
        LockPlayer();

        yield return StartCoroutine(PlayDialogue(phase1Lines, false));

        UnlockPlayer();
        if (boss != null) boss.BeginFight(Boss.FightStage.Phase1);
        fightStartTime = Time.time;
    }

    private IEnumerator RunPhase2Transition()
    {
        // phase 2 starts with the boss rewinding the fight back to the start. reuse
        // TimeRewindManager so everything rewindable winds back together, tint it red
        // so it looks like the boss is driving it
        if (boss != null) boss.dialoguePaused = true;

        // kill player input but dont call LockPlayer yet, that would also disable
        // PlayerRewindController which needs to stay registered with the manager
        if (playerInput != null) playerInput.enabled = false;

        if (playerRewindController != null) playerRewindController.SetExternalRewindActive(true);
        if (rewindEffects != null) rewindEffects.PushTintOverride(bossRewindTint, bossRewindBurstTint);

        // grab the ghost trail at runtime. PlayerRewindController.Awake adds it so
        // its usually not there at edit time and cant be dragged in
        if (rewindGhostTrail == null && playerRewindController != null)
            rewindGhostTrail = playerRewindController.GetComponent<RewindGhostTrail>();
        if (rewindGhostTrail != null && bossSprite != null) rewindGhostTrail.SetSourceOverride(bossSprite);

        TimeRewindManager manager = TimeRewindManager.Instance;
        if (manager != null)
        {
            if (audioSource != null && bossRewindStartClip != null)
            {
                audioSource.PlayOneShot(bossRewindStartClip, bossRewindVolume);
            }
            // scope the speed boost around this single rewind. StopRewind also clears
            // it but pairing explicitly stops it leaking into the next player rewind
            manager.PushSpeedMultiplier(bossRewindSpeedMultiplier);
            manager.StartRewind();

            // drive the rewind until it lands at the start of the fight, or the
            // manager aborts
            while (manager.IsRewinding)
            {
                if (manager.CurrentRewindTime <= fightStartTime)
                {
                    manager.StopRewind();
                    break;
                }
                yield return null;
            }

            manager.ClearSpeedMultiplier();
        }

        if (playerRewindController != null) playerRewindController.SetExternalRewindActive(false);
        if (rewindGhostTrail != null) rewindGhostTrail.ClearSourceOverride();

        // wait for the red tint to fully fade out before popping the override,
        // otherwise the blue tint flashes back during the tail of the fade
        if (rewindEffects != null)
        {
            while (rewindEffects.IsTintVisuallyActive) yield return null;
            rewindEffects.PopTintOverride();
        }

        // give the post-rewind slow-mo a frame to settle before locking
        yield return null;

        LockPlayer();
        ClearBossAttacks();
        RefillPlayerMana();

        yield return StartCoroutine(PlayDialogue(phase2Lines, true));

        UnlockPlayer();
        if (boss != null)
        {
            boss.dialoguePaused = false;
            boss.AdvanceToPhase2();
        }

        // start tracking damage for the playstyle hint
        phase2StartTime = Time.time;
        phase2DamageCount = 0;

        if (cachedPlayerHealth == null && playerScriptsRoot != null)
            cachedPlayerHealth = playerScriptsRoot.GetComponent<PlayerHealth>();
        if (cachedPlayerHealth == null)
            cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>();

        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged += OnPlayerDamagedInPhase2;
    }

    private void OnPlayerDamagedInPhase2(int current, int max)
    {
        phase2DamageCount++;
    }

    private void OnBossDied()
    {
        StartCoroutine(RunBossDeathDialogue());
    }

    private IEnumerator RunBossDeathDialogue()
    {
        yield return new WaitForSeconds(deathDialogueDelay);
        yield return StartCoroutine(PlayHintDialogue(new[] { "Impossible..." }));
    }

    private IEnumerator RunPlaystyleHint()
    {
        // unsub, hint only fires once
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged -= OnPlayerDamagedInPhase2;

        // non-freezing, gameplay continues while the hint types out
        string[] lines = GetPlaystyleHintLines();
        yield return StartCoroutine(PlayHintDialogue(lines));
    }

    // lightweight typewriter that doesnt pause the boss or lock the player
    private IEnumerator PlayHintDialogue(string[] lines)
    {
        if (dialogueText == null || lines == null || lines.Length == 0) yield break;

        WatcherCommentary.DialogueLocked = true;

        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        yield return null;

        float cps = Mathf.Max(1f, charactersPerSecond);
        float timePerChar = 1f / cps;

        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            dialogueText.enableAutoSizing = true;
            dialogueText.text = line;
            dialogueText.ForceMeshUpdate();
            float fitted = dialogueText.fontSize;

            dialogueText.enableAutoSizing = false;
            dialogueText.fontSize = fitted;
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

                yield return new WaitForSeconds(timePerChar);
            }

            yield return new WaitForSeconds(pauseBetweenLines);
        }

        yield return new WaitForSeconds(pauseAfterLastLine);

        if (dialogueContainer != null) dialogueContainer.SetActive(false);
        dialogueText.text = "";
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueText.enableAutoSizing = true;

        WatcherCommentary.DialogueLocked = false;
    }

    private string[] GetPlaystyleHintLines()
    {
        if (boss == null || boss.playerStrategyModel == null
            || boss.playerStrategyModel.strategyBeliefs == null
            || boss.playerStrategyModel.strategyBeliefs.Count == 0)
            return hintLinesFallback;

        var beliefs = boss.playerStrategyModel.strategyBeliefs;
        float aggressive = 0f, defensive = 0f, abilityFocused = 0f;
        beliefs.TryGetValue(PlayerStrategyModel.StrategyType.AggressivePlayer, out aggressive);
        beliefs.TryGetValue(PlayerStrategyModel.StrategyType.DefensivePlayer, out defensive);
        beliefs.TryGetValue(PlayerStrategyModel.StrategyType.AbilityFocusedPlayer, out abilityFocused);

        if (aggressive >= defensive && aggressive >= abilityFocused)
            return hintLinesAggressive;
        else if (defensive >= aggressive && defensive >= abilityFocused)
            return hintLinesEvasive;
        else
            return hintLinesAbilityFocused;
    }

    // mirrors IntroCutscene.DisablePlayerControl. shuts down every MonoBehaviour on
    // the player root except this controller and PlayerInput. freezes rigidbody X so
    // they cant slide sideways but still fall if airborne
    private RigidbodyConstraints2D originalRigidbodyConstraints;
    private bool rigidbodyConstraintsCaptured;
    private Coroutine lockedAnimatorRoutine;

    private void LockPlayer()
    {
        if (playerInput != null) playerInput.enabled = false;

        if (playerRigidbody != null)
        {
            originalRigidbodyConstraints = playerRigidbody.constraints;
            rigidbodyConstraintsCaptured = true;
            // zero X velocity but keep Y so an airborne player can still fall.
            // FreezePositionX stops further horizontal drift from collisions
            playerRigidbody.linearVelocity = new Vector2(0f, playerRigidbody.linearVelocity.y);
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.constraints = originalRigidbodyConstraints | RigidbodyConstraints2D.FreezePositionX;
        }

        // clear in-flight cast/attack state before forcing the animator to idle.
        // those flags are normally cleared by anim events (EndAttack etc.), but
        // playing Idle skips them so they get stuck true and lock the player.
        // reuse OnStartRewind since it already does the exact reset we need
        if (playerScriptsRoot != null)
        {
            var spell = playerScriptsRoot.GetComponent<PlayerSpellSystem>();
            if (spell != null) spell.OnStartRewind();
            var combat = playerScriptsRoot.GetComponent<PlayerCombat>();
            if (combat != null) combat.OnStartRewind();
        }

        ForcePlayerIdleAnimation();

        // while scripts are disabled, keep isGrounded in sync with real contact so
        // a mid-air lock transitions to idle on landing
        if (lockedAnimatorRoutine != null) StopCoroutine(lockedAnimatorRoutine);
        lockedAnimatorRoutine = StartCoroutine(MaintainLockedAnimator());

        if (playerScriptsRoot == null) return;

        disabledDuringDialogue.Clear();
        MonoBehaviour[] scripts = playerScriptsRoot.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour mb in scripts)
        {
            if (mb == null) continue;
            if (!mb.enabled) continue;
            if (mb == this) continue;
            if (mb is PlayerInput) continue;

            // disabling a script doesnt stop its coroutines, and RainAttack.SpawnRain
            // keeps spawning drops. stop it specifically. cant just call
            // StopAllCoroutines on everything, that kills PlayerHealth's invincibility
            // flash and leaves the sprite hidden
            if (mb is RainAttack) mb.StopAllCoroutines();
            mb.enabled = false;
            disabledDuringDialogue.Add(mb);
        }
    }

    private void UnlockPlayer()
    {
        if (playerInput != null) playerInput.enabled = true;

        foreach (MonoBehaviour mb in disabledDuringDialogue)
        {
            if (mb != null) mb.enabled = true;
        }
        disabledDuringDialogue.Clear();

        if (lockedAnimatorRoutine != null)
        {
            StopCoroutine(lockedAnimatorRoutine);
            lockedAnimatorRoutine = null;
        }

        if (playerRigidbody != null && rigidbodyConstraintsCaptured)
        {
            playerRigidbody.constraints = originalRigidbodyConstraints;
            rigidbodyConstraintsCaptured = false;
        }
    }

    private bool PlayerIsGrounded()
    {
        return playerPlatformer != null && playerPlatformer.CheckGrounded();
    }

    private IEnumerator MaintainLockedAnimator()
    {
        while (true)
        {
            if (playerAnimator != null &&
                HasAnimatorParam(playerAnimator, "isGrounded", AnimatorControllerParameterType.Bool))
            {
                playerAnimator.SetBool("isGrounded", PlayerIsGrounded());
            }
            yield return null;
        }
    }
    private void ForcePlayerIdleAnimation()
    {
        if (playerAnimator == null) return;

        bool grounded = PlayerIsGrounded();

        if (playerAnimatorBoolsToForceTrue != null)
        {
            foreach (string param in playerAnimatorBoolsToForceTrue)
            {
                if (string.IsNullOrEmpty(param)) continue;
                if (!HasAnimatorParam(playerAnimator, param, AnimatorControllerParameterType.Bool)) continue;
                // isGrounded has to reflect reality, forcing it true while airborne
                // would play a walking-on-air idle
                bool value = param == "isGrounded" ? grounded : true;
                playerAnimator.SetBool(param, value);
            }
        }
        if (playerAnimatorBoolsToForceFalse != null)
        {
            foreach (string param in playerAnimatorBoolsToForceFalse)
            {
                if (string.IsNullOrEmpty(param)) continue;
                if (HasAnimatorParam(playerAnimator, param, AnimatorControllerParameterType.Bool))
                    playerAnimator.SetBool(param, false);
            }
        }

        // only snap to idle when grounded. if airborne, leave the animator in its
        // fall state so it transitions via isGrounded once the player lands
        if (grounded && !string.IsNullOrEmpty(playerIdleStateName))
        {
            playerAnimator.Play(playerIdleStateName, 0, 0f);
            playerAnimator.Update(0f);
        }
    }

    // wipes every boss-spawned projectile/hazard so phase 2 dialogue starts clean
    private void ClearBossAttacks()
    {
        DestroyAllOfType<Fireball>();
        DestroyAllOfType<HomingFireball>();
        DestroyAllOfType<Firecolumns>();
        DestroyAllOfType<FireRow>();
        DestroyAllOfType<FireWave>();
        DestroyAllOfType<FireExplosion>();
        DestroyAllOfType<FloorFireRow>();
        DestroyAllOfType<PlatformController>();
        // SpellProjectile covers the basic blast + each rain drop. RainAttack lives
        // on the player so we cant destroy it, but LockPlayer already stopped its
        // coroutine
        DestroyAllOfType<SpellProjectile>();
        // also wipe by tag, catches any spell prefab without a SpellProjectile component
        DestroyAllWithTag("Spell");

        // wipe regular enemies but leave the boss alone
        foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (e == null) continue;
            if (e is Boss) continue;
            Destroy(e.gameObject);
        }
    }

    private static void DestroyAllOfType<T>() where T : Component
    {
        foreach (T obj in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj != null) Destroy(obj.gameObject);
        }
    }

    private static void DestroyAllWithTag(string tag)
    {
        try
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag(tag))
            {
                if (go != null) Destroy(go);
            }
        }
        catch (UnityException)
        {
            // tag not defined in the project, just skip
        }
    }

    private void RefillPlayerMana()
    {
        if (playerMana != null) playerMana.SetMana(playerMana.MaxMana);
    }

    // like WaitForSecondsRealtime but freezes while the pause menu is up. dialogue
    // uses unscaled time so it plays through cutscene timeScale=0 locks, but pause
    // ALSO drives timeScale=0 and should stop the typewriter
    private static IEnumerator WaitRealtimeRespectingPause(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!PauseMenu.isPaused) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static bool HasAnimatorParam(Animator animator, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == name && p.type == type) return true;
        }
        return false;
    }

    // mirrors IntroCutscene.PlayIntroDialogue but uses unscaled time so it keeps
    // running while timeScale=0. emphasizeLastLine slows the typewriter + starts
    // a per-glyph shake on the final line
    private IEnumerator PlayDialogue(string[] lines, bool emphasizeLastLine)
    {
        if (dialogueText == null || lines == null || lines.Length == 0) yield break;

        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        yield return null;

        float baseCps = Mathf.Max(1f, charactersPerSecond);
        float slowCps = Mathf.Max(1f, lastLineCharactersPerSecond);
        Coroutine shakeRoutine = null;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            bool emphasise = emphasizeLastLine && lineIndex == lines.Length - 1;
            float timePerChar = 1f / (emphasise ? slowCps : baseCps);

            if (string.IsNullOrEmpty(line))
            {
                dialogueText.text = "";
                yield return WaitRealtimeRespectingPause(pauseBetweenLines);
                continue;
            }

            // lock the auto-sized font so text doesnt rescale mid-typewriter
            dialogueText.enableAutoSizing = true;
            dialogueText.text = line;
            dialogueText.ForceMeshUpdate();
            float fitted = dialogueText.fontSize;

            dialogueText.enableAutoSizing = false;
            dialogueText.fontSize = fitted;
            dialogueText.maxVisibleCharacters = 0;
            yield return null;

            // rich-text tags inflate line.Length but arent visible glyphs. drive the
            // typewriter off textInfo so tags dont cause phantom pauses
            dialogueText.ForceMeshUpdate();
            int glyphCount = dialogueText.textInfo.characterCount;

            if (emphasise) shakeRoutine = StartCoroutine(ShakeDialogueText());

            for (int i = 1; i <= glyphCount; i++)
            {
                dialogueText.maxVisibleCharacters = i;

                if (audioSource != null && dialogueBlip != null && i % charsPerSound == 0)
                {
                    audioSource.pitch = Random.Range(minPitch, maxPitch);
                    audioSource.PlayOneShot(dialogueBlip, dialogueBlipVolume);
                }

                yield return WaitRealtimeRespectingPause(timePerChar);
            }

            yield return WaitRealtimeRespectingPause(pauseBetweenLines);
        }

        yield return WaitRealtimeRespectingPause(pauseAfterLastLine);

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (dialogueContainer != null) dialogueContainer.SetActive(false);
        dialogueText.text = "";
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueText.enableAutoSizing = true;
    }

    // per-glyph jitter via the TMP vertex buffer. chars inside <link=heavy> get a
    // larger radius so a single word can shake harder than the rest of the line
    private IEnumerator ShakeDialogueText()
    {
        if (dialogueText == null) yield break;

        Vector3[][] baseline = null;

        while (true)
        {
            // hold current glyph positions while paused, no rebuild, no jitter
            if (PauseMenu.isPaused)
            {
                yield return null;
                continue;
            }

            // rebuild each frame so the fresh verts are our shake origin, otherwise
            // we accumulate offsets
            dialogueText.ForceMeshUpdate();
            TMPro.TMP_TextInfo info = dialogueText.textInfo;
            int meshCount = info.meshInfo.Length;

            if (baseline == null || baseline.Length != meshCount)
                baseline = new Vector3[meshCount][];

            for (int m = 0; m < meshCount; m++)
            {
                Vector3[] src = info.meshInfo[m].vertices;
                if (baseline[m] == null || baseline[m].Length != src.Length)
                    baseline[m] = new Vector3[src.Length];
                System.Array.Copy(src, baseline[m], src.Length);
            }

            // find the "heavy" link range. those glyphs get the larger radius.
            // no link = whole line shakes uniformly
            int heavyStart = -1;
            int heavyEnd = -1;
            for (int l = 0; l < info.linkCount; l++)
            {
                TMPro.TMP_LinkInfo link = info.linkInfo[l];
                if (link.GetLinkID() == "heavy")
                {
                    heavyStart = link.linkTextfirstCharacterIndex;
                    heavyEnd = heavyStart + link.linkTextLength;
                    break;
                }
            }

            int visible = Mathf.Min(dialogueText.maxVisibleCharacters, info.characterCount);
            for (int c = 0; c < visible; c++)
            {
                TMPro.TMP_CharacterInfo ci = info.characterInfo[c];
                if (!ci.isVisible) continue;

                int m = ci.materialReferenceIndex;
                int v = ci.vertexIndex;

                float amp = c >= heavyStart && c < heavyEnd
                    ? lastLineHeavyShakeAmount
                    : lastLineShakeAmount;

                Vector3 offset = new Vector3(
                    Random.Range(-amp, amp),
                    Random.Range(-amp, amp),
                    0f);

                Vector3[] verts = info.meshInfo[m].vertices;
                verts[v + 0] = baseline[m][v + 0] + offset;
                verts[v + 1] = baseline[m][v + 1] + offset;
                verts[v + 2] = baseline[m][v + 2] + offset;
                verts[v + 3] = baseline[m][v + 3] + offset;
            }

            for (int m = 0; m < meshCount; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                dialogueText.UpdateGeometry(info.meshInfo[m].mesh, m);
            }

            yield return null;
        }
    }
}
