using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using TimeRewind;

// Drives the two-phase boss fight flow:
//   1. Player walks into the trigger on this GameObject. Phase-1 intro dialogue
//      types out, then Boss.BeginFight kicks off the fight proper.
//   2. After phase2TimerSeconds of fight time, the boss itself "rewinds" — we
//      force the shared TimeRewindManager on and drive it until the rewind lands
//      back at the start of the fight, painted red instead of blue to sell the
//      idea that the boss is doing the rewinding, not the player. The player's
//      mana isn't spent during this.
//   3. Once the rewind lands, we play the phase-2 dialogue and call
//      Boss.AdvanceToPhase2 so the GMM takes over.
//
// The latch is one-way on purpose: if the player rewinds afterwards we don't
// want the phase-2 transition to replay. Boss.fightStage is also deliberately
// not part of the rewind snapshot so Phase2 persists through rewinds.
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

    [Tooltip("Player's Rigidbody2D — X position is frozen via constraints while " +
             "the dialogue is up so horizontal velocity (e.g. spell recoil) can't " +
             "push the player sideways. Y stays unconstrained so gravity still " +
             "pulls the player down if they were airborne when the dialogue opened.")]
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Tooltip("Player's PlayerPlatformer — used to poll grounded state while the " +
             "player's scripts are disabled during dialogue, so the animator's " +
             "isGrounded bool stays truthful and a mid-air lock doesn't look " +
             "like walking on air.")]
    [SerializeField] private PlayerPlatformer playerPlatformer;

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
    [Tooltip("Characters-per-second used only for the last line of phase 2. Lower = " +
             "slower delivery for extra weight on the killing-blow beat.")]
    [SerializeField] private float lastLineCharactersPerSecond = 12f;

    [Tooltip("Per-character shake radius (in text local units) applied to every " +
             "visible glyph on the final phase 2 line.")]
    [SerializeField] private float lastLineShakeAmount = 1.5f;

    [Tooltip("Shake radius applied to characters wrapped in <link=heavy> on the " +
             "final phase 2 line. Use this to emphasise the scariest word.")]
    [SerializeField] private float lastLineHeavyShakeAmount = 4f;

    [Header("Phase 2 Trigger")]
    [Tooltip("Seconds of fight time after BeginFight before the boss-driven " +
             "rewind triggers phase 2.")]
    [SerializeField] private float phase2TimerSeconds = 25f;

    [Header("Boss Rewind")]
    [Tooltip("PlayerRewindController on the player. Put into external mode " +
             "during the boss rewind so mana isn't spent and it doesn't stop " +
             "because R isn't held.")]
    [SerializeField] private PlayerRewindController playerRewindController;

    [Tooltip("RewindEffects component. Temporarily swapped to red during the " +
             "boss rewind, restored to its original blue afterwards.")]
    [SerializeField] private RewindEffects rewindEffects;

    [Tooltip("Rewind tint color used while the boss is rewinding.")]
    [SerializeField] private Color bossRewindTint = new Color(1f, 0.55f, 0.55f, 0.2f);

    [Tooltip("Burst tint color used at the start of the boss rewind.")]
    [SerializeField] private Color bossRewindBurstTint = new Color(1f, 0.45f, 0.45f, 0.35f);

    [Tooltip("RewindGhostTrail on the player. Optional — if left empty we resolve " +
             "it from playerRewindController at runtime. (PlayerRewindController " +
             "adds this component in Awake if it isn't already there, which is " +
             "why you may not be able to drag it in via the Inspector.)")]
    [SerializeField] private RewindGhostTrail rewindGhostTrail;

    [Tooltip("Boss's SpriteRenderer — used as the ghost trail source during " +
             "the boss rewind.")]
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
    [Tooltip("PlayerRewindController on the player. Put into external mode " +
             "during the boss rewind so mana isn't spent and it doesn't stop " +
             "because R isn't held.")]
    
    [Min(0.1f)]
    [SerializeField] private float bossRewindSpeedMultiplier = 3f;

    [Tooltip("Read-only preview: approximate real seconds the boss rewind will " +
             "take given the current multiplier. Actual duration can shift by " +
             "~30% because idle segments fast-forward and the final 0.75s eases " +
             "out. Updates in the editor when you tweak the fields above.")]
    [SerializeField] private float estimatedBossRewindDurationSeconds;

    private bool fightStarted;
    private bool phase2Triggered;
    private float fightStartTime;
    private bool hintTriggered;
    private int phase2DamageCount;
    private float phase2StartTime;
    private PlayerHealth cachedPlayerHealth;

    // Scripts we turned off during the current dialogue. Tracked so we only re-enable
    // what we actually disabled (anything already disabled stays that way).
    private readonly List<MonoBehaviour> disabledDuringDialogue = new List<MonoBehaviour>();

    // Recomputes the boss-rewind duration preview whenever the Inspector changes
    // a relevant field. Pulls rewindSpeed from the scene's TimeRewindManager if
    // present so the estimate stays in sync with the manager's setting; falls
    // back to the default (1.3) if the manager isn't in the scene yet.
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

        // Force middle-center alignment so multi-line lines stay centered inside
        // the dialogue box instead of pushing the block up from a top-aligned anchor.
        if (dialogueText != null) dialogueText.alignment = TextAlignmentOptions.Center;

        // Make sure our own collider is a trigger — otherwise OnTriggerEnter2D never fires.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        if (!fightStarted || phase2Triggered || boss == null) return;
        if (boss.fightStage != Boss.FightStage.Phase1) return;

        // If the player is mid-rewind (their own), wait — starting ours on top
        // would collide with the manager's already-active rewind state.
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding) return;

        if (Time.time - fightStartTime >= phase2TimerSeconds)
        {
            phase2Triggered = true;
            StartCoroutine(RunPhase2Transition());
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

        yield return StartCoroutine(PlayDialogue(phase1Lines, false));

        UnlockPlayer();
        if (boss != null) boss.BeginFight(Boss.FightStage.Phase1);
        fightStartTime = Time.time;
    }

    private IEnumerator RunPhase2Transition()
    {
        // Phase 2 begins with the *boss* rewinding the fight back to the start.
        // We reuse the existing TimeRewindManager (so player + boss + everything
        // rewindable winds back together), force it on without touching the
        // player's mana, and tint it red to signal the boss is driving this.
        if (boss != null) boss.dialoguePaused = true;

        // Kill player input during the rewind. We don't call LockPlayer yet
        // because it disables every MonoBehaviour on the player — including the
        // PlayerRewindController we need registered with the rewind manager.
        if (playerInput != null) playerInput.enabled = false;

        if (playerRewindController != null) playerRewindController.SetExternalRewindActive(true);
        if (rewindEffects != null) rewindEffects.PushTintOverride(bossRewindTint, bossRewindBurstTint);

        // Resolve the ghost trail lazily: PlayerRewindController.Awake adds it at
        // runtime, so it's typically not there at edit time and can't be dragged in.
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
            // Scope the boss-only speed boost around this single rewind. StopRewind
            // also clears the multiplier as a safety net, but we pair explicitly
            // so an aborted rewind path doesn't leak into the next player rewind.
            manager.PushSpeedMultiplier(bossRewindSpeedMultiplier);
            manager.StartRewind();

            // Drive the rewind until it lands back at the start of the fight,
            // or the manager aborts for any reason.
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

        // Wait for the red tint to fully fade out before popping the override —
        // otherwise the original blue tint would flash back on screen during the
        // tail of the post-process fade.
        if (rewindEffects != null)
        {
            while (rewindEffects.IsTintVisuallyActive) yield return null;
            rewindEffects.PopTintOverride();
        }

        // Give the post-rewind slow-motion a frame to settle before we lock.
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
    }

    // Mirrors IntroCutscene.DisablePlayerControl's script-disable pass. Stops
    // movement, attacks, spellcasting, etc. by shutting down every MonoBehaviour on
    // the player root except the two we need alive (this controller and PlayerInput,
    // which we toggle separately). Freezes the rigidbody's X so the player can't
    // slide sideways while still letting gravity pull an airborne player down.
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
            // Zero X velocity so lingering horizontal motion doesn't bleed through,
            // but preserve Y so an airborne player can still fall. FreezePositionX
            // stops any further horizontal drift from collisions/knockback.
            playerRigidbody.linearVelocity = new Vector2(0f, playerRigidbody.linearVelocity.y);
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.constraints = originalRigidbodyConstraints | RigidbodyConstraints2D.FreezePositionX;
        }

        ForcePlayerIdleAnimation();

        // While the player's scripts are disabled, keep the animator's isGrounded
        // bool in sync with actual ground contact so a mid-air lock transitions to
        // idle on landing instead of staying stuck in one frame.
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
                // isGrounded has to reflect reality — forcing it true while the
                // player is airborne would play a walking-on-air idle clip.
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

        // Only snap to the grounded idle state when actually grounded; airborne,
        // leave the animator in whatever fall state it already reached so it
        // naturally transitions via isGrounded once the player lands.
        if (grounded && !string.IsNullOrEmpty(playerIdleStateName))
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
    // emphasizeLastLine: slows the typewriter + starts a per-glyph shake on the
    // final line — used for phase 2's "Prepare to DIE" beat.
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

            // Rich-text tags (colour, link) inflate line.Length but don't count
            // toward visible glyphs — drive the typewriter off textInfo so tags
            // don't cause phantom pauses while the boss "types" invisible markup.
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

                yield return new WaitForSecondsRealtime(timePerChar);
            }

            yield return new WaitForSecondsRealtime(pauseBetweenLines);
        }

        yield return new WaitForSecondsRealtime(pauseAfterLastLine);

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

    // Per-glyph jitter driven off the TMP character vertex buffer. Characters inside
    // a <link=heavy> tag get a larger radius so a single word (e.g. "DIE") shakes
    // more violently than the rest of the line. Runs every frame until stopped;
    // PlayDialogue cancels it after the final pause.
    private IEnumerator ShakeDialogueText()
    {
        if (dialogueText == null) yield break;

        Vector3[][] baseline = null;

        while (true)
        {
            // Rebuild each frame so the freshly-layed-out verts (post typewriter
            // increment) are our shake origin — otherwise we'd accumulate offsets.
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

            // Find the "heavy" link range if present — those glyphs get the larger
            // shake radius. Missing link just means the whole line shakes uniformly.
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
