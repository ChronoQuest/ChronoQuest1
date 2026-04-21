using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Drives the two-phase boss fight flow:
//   1. Player walks into the trigger on this GameObject.
//   2. The game locks (Time.timeScale = 0), the phase-1 dialogue types out on an
//      otherwise-hidden panel. Once it finishes, we unlock and call Boss.BeginFight
//      so the music and health bar kick in and the fight actually starts.
//   3. Each frame we watch the boss's health. The first time it drops to or below
//      phase2HealthFraction of its max, we latch once (phase2Triggered), lock the
//      game again, play the phase-2 dialogue, then call Boss.AdvanceToPhase2 so
//      the GMM takes over.
//
// The latch is one-way on purpose: if the player rewinds past the 80% threshold
// we don't want the dialogue to replay. Boss.fightStage is also deliberately not
// part of the rewind snapshot so Phase2 persists through rewinds.
[RequireComponent(typeof(Collider2D))]
public class BossFightController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Boss this controller drives. BeginFight/AdvanceToPhase2 are called on it.")]
    [SerializeField] private Boss boss;

    [Tooltip("Player's PlayerInput — disabled while the dialogue panel is up so " +
             "input actions don't fire during the lock.")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Player root GameObject. Every MonoBehaviour on it gets disabled " +
             "while the dialogue is up (except this controller and PlayerInput). " +
             "Colliders are left untouched.")]
    [SerializeField] private GameObject playerScriptsRoot;

    [Tooltip("Player's Rigidbody2D — zeroed out and set to kinematic while the " +
             "dialogue is up so any leftover velocity (e.g. spell recoil) can't " +
             "push the player off a ledge. Restored to dynamic afterwards.")]
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Tooltip("Player's Animator — forced to its idle state when dialogue starts " +
             "so the player doesn't stay stuck on the fall/jump/cast frame.")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("Name of the idle state on the player Animator. Case-sensitive; " +
             "use the full path (e.g. 'Base.Idle') if it lives inside a sub-state " +
             "machine. For this project it's typically 'Player_Idle'.")]
    [SerializeField] private string playerIdleStateName = "Player_Idle";

    [Tooltip("Animator bool parameters forced to TRUE on lock (e.g. 'isGrounded' " +
             "so an Any State -> Jump/Fall transition doesn't fire the moment we " +
             "play the Idle state).")]
    [SerializeField] private string[] playerAnimatorBoolsToForceTrue = new string[] { "isGrounded" };

    [Tooltip("Animator bool parameters forced to FALSE on lock (e.g. 'isWallSliding').")]
    [SerializeField] private string[] playerAnimatorBoolsToForceFalse = new string[] { "isWallSliding", "IsFrozen" };

    [Tooltip("Player's PlayerMana — refilled to max when phase 2 starts.")]
    [SerializeField] private PlayerMana playerMana;

    [Header("Dialogue UI")]
    [Tooltip("Root GameObject of the dialogue panel. Hidden at start, activated " +
             "during each dialogue, deactivated when the last line finishes.")]
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

    [Header("Phase 2 Dialogue (80% health)")]
    [TextArea(2, 5)]
    [SerializeField] private string[] phase2Lines = new string[]
    {
        "Placeholder phase 2 line 1.",
        "Placeholder phase 2 line 2.",
        "Placeholder phase 2 line 3."
    };

    [Header("Dialogue Pacing")]
    [SerializeField] private float charactersPerSecond = 30f;
    [SerializeField] private float pauseBetweenLines = 1.2f;
    [SerializeField] private float pauseAfterLastLine = 0.8f;

    [Header("Phase 2 Trigger")]
    [Tooltip("Fraction of max health that triggers the phase-2 dialogue + GMM. " +
             "0.8 = the moment boss health drops to 80% of startHealth.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase2HealthFraction = 0.8f;

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
    [SerializeField] private string[] hintLinesCautious = new string[]
    {
        "You just... stand there. Waiting. Watching.",
        "Hesitation is death. Come at me before I come at you."
    };
    [TextArea(2, 5)]
    [SerializeField] private string[] hintLinesFallback = new string[]
    {
        "You're struggling.",
        "Perhaps a change of approach is in order..."
    };

    private bool fightStarted;
    private bool phase2Triggered;
    private bool hintTriggered;
    private int phase2DamageCount;
    private float phase2StartTime;
    private PlayerHealth cachedPlayerHealth;

    // Scripts we turned off during the current dialogue. Tracked so we only re-enable
    // what we actually disabled (anything already disabled stays that way).
    private readonly List<MonoBehaviour> disabledDuringDialogue = new List<MonoBehaviour>();

    private void Awake()
    {
        if (dialogueContainer != null) dialogueContainer.SetActive(false);

        // Make sure our own collider is a trigger — otherwise OnTriggerEnter2D never fires.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnDestroy()
    {
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged -= OnPlayerDamagedInPhase2;
    }

    private void Update()
    {
        if (!fightStarted || boss == null) return;

        // Phase 1 → Phase 2 transition
        if (!phase2Triggered && boss.fightStage == Boss.FightStage.Phase1 && boss.startHealth > 0)
        {
            if (boss.health <= boss.startHealth * phase2HealthFraction)
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
        // Phase 1 dialogue: boss is still in Waiting stage so its Update already
        // early-returns; we just lock the player. Time.timeScale stays at 1 so the
        // player's idle animation keeps playing.
        LockPlayer();

        yield return StartCoroutine(PlayDialogue(phase1Lines));

        UnlockPlayer();
        if (boss != null) boss.BeginFight(Boss.FightStage.Phase1);
    }

    private IEnumerator RunPhase2Transition()
    {
        // Phase 2 dialogue: freeze the boss via its own flag (not Time.timeScale) so
        // the player's idle animation keeps ticking.
        if (boss != null) boss.dialoguePaused = true;
        LockPlayer();
        ClearBossAttacks();
        RefillPlayerMana();

        yield return StartCoroutine(PlayDialogue(phase2Lines));

        UnlockPlayer();
        if (boss != null)
        {
            boss.dialoguePaused = false;
            boss.AdvanceToPhase2();
        }

        // Start tracking damage for the playstyle hint
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
        // OnHealthChanged fires for both damage and healing; only count damage
        phase2DamageCount++;
    }

    private IEnumerator RunPlaystyleHint()
    {
        // Unsubscribe — hint only fires once
        if (cachedPlayerHealth != null)
            cachedPlayerHealth.OnHealthChanged -= OnPlayerDamagedInPhase2;

        // Non-freezing: gameplay continues while the hint types out
        string[] lines = GetPlaystyleHintLines();
        yield return StartCoroutine(PlayHintDialogue(lines));
    }

    /// <summary>
    /// Lightweight typewriter that does NOT pause the boss or lock the player.
    /// Shows text over the fight, then hides itself.
    /// </summary>
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
            || boss.playerStrategyModel.playerTacticalModel == null
            || boss.playerStrategyModel.playerTacticalModel.tacticBeliefs == null)
            return hintLinesFallback;

        var tactics = boss.playerStrategyModel.playerTacticalModel.tacticBeliefs;
        float aggressive = 0f, evasive = 0f, cautious = 0f;
        tactics.TryGetValue(PlayerTacticalModel.TacticType.Aggressive, out aggressive);
        tactics.TryGetValue(PlayerTacticalModel.TacticType.Evasive, out evasive);
        tactics.TryGetValue(PlayerTacticalModel.TacticType.Cautious, out cautious);

        if (aggressive >= evasive && aggressive >= cautious)
            return hintLinesAggressive;
        else if (evasive >= aggressive && evasive >= cautious)
            return hintLinesEvasive;
        else
            return hintLinesCautious;
    }

    // Mirrors IntroCutscene.DisablePlayerControl's script-disable pass. Stops
    // movement, attacks, spellcasting, etc. by shutting down every MonoBehaviour on
    // the player root except the two we need alive (this controller and PlayerInput,
    // which we toggle separately). Also zeroes out the rigidbody and forces the
    // animator to idle so lingering velocity / mid-air frames don't bleed through.
    private bool rigidbodyWasDynamic;

    private void LockPlayer()
    {
        if (playerInput != null) playerInput.enabled = false;

        if (playerRigidbody != null)
        {
            rigidbodyWasDynamic = playerRigidbody.bodyType == RigidbodyType2D.Dynamic;
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        ForcePlayerIdleAnimation();

        if (playerScriptsRoot == null) return;

        disabledDuringDialogue.Clear();
        MonoBehaviour[] scripts = playerScriptsRoot.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour mb in scripts)
        {
            if (mb == null) continue;
            if (!mb.enabled) continue;
            if (mb == this) continue;
            if (mb is PlayerInput) continue;

            // Disabling a MonoBehaviour alone doesn't stop its active coroutines.
            // RainAttack.SpawnRain would keep spawning drops after `enabled = false`,
            // so stop it specifically. Calling StopAllCoroutines on every script
            // instead would also kill PlayerHealth.InvincibilityRoutine mid-flash
            // and leave the sprite hidden — so we stay surgical.
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

        if (playerRigidbody != null && rigidbodyWasDynamic)
        {
            playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    private void ForcePlayerIdleAnimation()
    {
        if (playerAnimator == null) return;

        if (playerAnimatorBoolsToForceTrue != null)
        {
            foreach (string param in playerAnimatorBoolsToForceTrue)
            {
                if (string.IsNullOrEmpty(param)) continue;
                if (HasAnimatorParam(playerAnimator, param, AnimatorControllerParameterType.Bool))
                    playerAnimator.SetBool(param, true);
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

        if (!string.IsNullOrEmpty(playerIdleStateName))
        {
            playerAnimator.Play(playerIdleStateName, 0, 0f);
            playerAnimator.Update(0f);
        }
    }

    // Wipes every boss-spawned projectile/hazard currently in the scene so the
    // phase-2 dialogue starts from a clean slate. Each of these is a self-contained
    // prefab spawned by BossAttackManager — destroying the GameObject is enough.
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
        // Player spells: SpellProjectile covers the basic blast + each rain drop.
        // RainAttack is the spawner coroutine — destroying it stops mid-spawn rain
        // from dropping more projectiles after the dialogue starts.
        DestroyAllOfType<SpellProjectile>();
        // RainAttack lives on the player itself, so we can't Destroy its GameObject
        // without destroying the player. LockPlayer already disables it and stops
        // its coroutine, so any in-flight rain drops are caught by the SpellProjectile
        // sweep above and no more will spawn.
        // Belt-and-suspenders: the SpellPrefab (and rain drops spawned from it) is
        // tagged "Spell", so wipe by tag too. Catches any spell prefab that happens
        // not to carry a SpellProjectile component.
        DestroyAllWithTag("Spell");

        // Wipe any boss-spawned (or otherwise present) regular enemies, but leave
        // the boss itself alone.
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
            // Tag not defined in the project — silently skip rather than throwing.
        }
    }

    private void RefillPlayerMana()
    {
        if (playerMana != null) playerMana.SetMana(playerMana.MaxMana);
    }

    private static bool HasAnimatorParam(Animator animator, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == name && p.type == type) return true;
        }
        return false;
    }

    // Mirrors the typewriter flow in IntroCutscene.PlayIntroDialogue, but uses
    // unscaled time so it keeps running while Time.timeScale is 0.
    private IEnumerator PlayDialogue(string[] lines)
    {
        if (dialogueText == null || lines == null || lines.Length == 0) yield break;

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
            if (string.IsNullOrEmpty(line))
            {
                dialogueText.text = "";
                yield return new WaitForSecondsRealtime(pauseBetweenLines);
                continue;
            }

            // Lock the auto-sized font so the text doesn't rescale mid-typewriter.
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
                yield return new WaitForSecondsRealtime(timePerChar);
            }

            yield return new WaitForSecondsRealtime(pauseBetweenLines);
        }

        yield return new WaitForSecondsRealtime(pauseAfterLastLine);

        if (dialogueContainer != null) dialogueContainer.SetActive(false);
        dialogueText.text = "";
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueText.enableAutoSizing = true;
    }
}
