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

    private bool fightStarted;
    private bool phase2Triggered;

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

    private void Update()
    {
        if (!fightStarted || phase2Triggered || boss == null) return;
        if (boss.fightStage != Boss.FightStage.Phase1) return;
        if (boss.startHealth <= 0) return;

        if (boss.health <= boss.startHealth * phase2HealthFraction)
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

        yield return StartCoroutine(PlayDialogue(phase2Lines));

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
