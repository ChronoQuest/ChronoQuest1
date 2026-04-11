using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the intro cutscene.
///
/// Two modes of operation:
///  1. If a PlayableDirector is assigned, it plays a Timeline and waits for
///     a Timeline Signal to call OnCutsceneEnd() (via CutsceneSignalReceiver).
///  2. Otherwise, it runs a scripted coroutine sequence:
///        boss turns around -> walks to the player -> hits the player ->
///        player falls -> main gameplay scene loads.
///
/// Either way, player input/physics are locked while the cutscene is running
/// and OnCutsceneEnd() is the single "we're done, load the next scene" entry
/// point.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // Inspector references — assign these in the IntroCutscene scene.
    // ---------------------------------------------------------------------

    [Header("Timeline (optional)")]
    [Tooltip("PlayableDirector that holds a Timeline cutscene. If left empty " +
             "the script runs a built-in coroutine sequence instead.")]
    [SerializeField] private PlayableDirector director;

    [Header("Player References")]
    [Tooltip("Player's Rigidbody2D — set to kinematic during the cutscene so " +
             "gravity and forces can't displace the player while the " +
             "sequence is running.")]
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Tooltip("Player's PlayerInput — disabled during the cutscene so input " +
             "actions don't fire.")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Player's Collider2D — disabled during the cutscene so that " +
             "rotating the player's transform during the fall can't cause " +
             "the physics engine to teleport the player to resolve ground " +
             "overlap. Drag the Player GameObject here; Unity will pick its " +
             "Collider2D automatically.")]
    [SerializeField] private Collider2D playerCollider;

    [Tooltip("Player's root Transform. Used by the scripted sequence so the " +
             "boss knows where to walk to. Also rotated during the fall " +
             "unless a separate Player Sprite Transform is assigned.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Optional — a child Transform holding just the SpriteRenderer. " +
             "If assigned, only this child is rotated during the fall " +
             "(leaving the root's collider upright). If empty, the root is " +
             "rotated instead — which is safe as long as Player Collider is " +
             "assigned above.")]
    [SerializeField] private Transform playerSpriteTransform;

    [Tooltip("Drag the Player GameObject here. Every MonoBehaviour script " +
             "on it gets disabled during the cutscene (except PlayerInput " +
             "and CutsceneSignalReceiver, which we need alive). This is " +
             "how we stop PlayerPlatformer, PlayerHealth, PlayerSafetyNet, " +
             "etc. from moving or teleporting the player mid-cutscene.")]
    [SerializeField] private GameObject playerScriptsRoot;

    [Tooltip("Player's Animator. At cutscene start it gets forced to its " +
             "Idle state so it doesn't freeze mid-animation when the " +
             "movement scripts stop feeding it parameters.")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("Name of the Idle state on the player's Animator Controller. " +
             "Must match exactly (case-sensitive). For this project's " +
             "player that's 'Player_Idle'. If your Idle state is in a " +
             "sub-state machine, use the full path like 'Base.Idle'.")]
    [SerializeField] private string playerIdleStateName = "Player_Idle";

    [Tooltip("Bool parameter names on the player's Animator that should be " +
             "force-set to TRUE at cutscene start (e.g. 'isGrounded' — " +
             "without this, an Any State -> Jump/Fall transition can fire " +
             "right after we Play the Idle state and the player pops back " +
             "into the jump pose).")]
    [SerializeField] private string[] playerAnimatorBoolsToForceTrue = new string[] { "isGrounded" };

    [Tooltip("Bool parameter names on the player's Animator that should be " +
             "force-set to FALSE at cutscene start (e.g. 'isWallSliding', " +
             "'IsFrozen').")]
    [SerializeField] private string[] playerAnimatorBoolsToForceFalse = new string[] { "isWallSliding", "IsFrozen" };

    [Header("Boss References")]
    [Tooltip("Boss root Transform — the thing that will actually move across " +
             "the scene during the intro.")]
    [SerializeField] private Transform bossTransform;

    [Tooltip("Boss Animator — used to set run/attack parameters during the " +
             "scripted sequence. Leave empty if your boss doesn't have an " +
             "Animator and you just want the transform movement.")]
    [SerializeField] private Animator bossAnimator;

    [Tooltip("Optional — boss Rigidbody2D. If set, it will be forced " +
             "kinematic during the walk so colliders can't shove the boss " +
             "back while the script moves its transform.")]
    [SerializeField] private Rigidbody2D bossRigidbody;

    [Tooltip("Boss colliders / hitboxes to disable for the whole cutscene. " +
             "Important: if the boss has a damage hitbox, leaving it " +
             "enabled will trigger the player's hit reaction (respawn, " +
             "knockback, etc.) the moment the boss walks into the player — " +
             "before the scripted attack even fires. Drag every Collider2D " +
             "the boss uses for attacks or body contact here.")]
    [SerializeField] private Collider2D[] bossCollidersToDisable;

    [Header("Sequence Timing")]
    [Tooltip("How long to wait after the scene loads before the boss starts " +
             "doing anything. Gives the camera/UI a moment to settle.")]
    [SerializeField] private float initialDelay = 0.5f;

    [Tooltip("Pause after the boss turns around, before it starts walking.")]
    [SerializeField] private float pauseAfterTurn = 0.4f;

    [Tooltip("How fast the boss walks toward the player (world units/sec).")]
    [SerializeField] private float bossWalkSpeed = 3f;

    [Tooltip("The boss stops this many units away from the player — a little " +
             "gap so the attack animation lines up.")]
    [SerializeField] private float bossStopDistance = 2.3f;

    [Tooltip("Pause after the boss reaches the player, before the attack.")]
    [SerializeField] private float pauseBeforeHit = 0.3f;

    [Tooltip("Delay between the attack trigger firing and the player " +
             "actually reacting (tune this to land on the swing's impact " +
             "frame).")]
    [SerializeField] private float hitImpactDelay = 0.35f;

    [Tooltip("How long to linger on the fallen player before loading the " +
             "gameplay scene.")]
    [SerializeField] private float postHitDelay = 1.5f;

    [Header("Boss Animator Parameters")]
    [Tooltip("Bool parameter name on the boss Animator that toggles the " +
             "walk/run animation. Leave empty if you don't have one.")]
    [SerializeField] private string bossRunBoolParam = "isRunning";

    [Tooltip("Trigger parameter name on the boss Animator used to fire the " +
             "attack animation. Leave empty if you don't have one.")]
    [SerializeField] private string bossAttackTriggerParam = "Attack";

    [Header("Player Fall Reaction")]
    [Tooltip("Impulse applied to the player when the boss hits them. " +
             "Negative X = knocked backwards from the hit direction (the " +
             "script flips the sign automatically based on which side the " +
             "boss is on), positive Y = launched up so gravity can bring " +
             "them back down. Higher Y + lower X = more of an upward arc " +
             "and less of a slide when they land.")]
    [SerializeField] private Vector2 playerKnockbackImpulse = new Vector2(4f, 14f);

    [Tooltip("Degrees to rotate the player's transform when they fall " +
             "(visual only). Applied over Player Fall Duration seconds.")]
    [SerializeField] private float playerFallRotation = 90f;

    [Tooltip("How long (seconds) the player takes to rotate from upright to " +
             "the full fall rotation. 0 = instant snap.")]
    [SerializeField] private float playerFallDuration = 0.4f;

    [Header("UI")]
    [Tooltip("GameObjects to hide for the duration of the cutscene — drag " +
             "the health bar, mana bar, any HUD Canvas, minimap, etc. in " +
             "here. They get SetActive(false) at cutscene start. Scene " +
             "unloads at the end so no need to re-enable them.")]
    [SerializeField] private GameObject[] uiToHide;

    [Header("Intro Dialogue")]
    [Tooltip("Root GameObject of the dialogue UI (e.g. the panel that " +
             "holds the text, background, portrait). It gets SetActive " +
             "true at the start of the dialogue and false once the last " +
             "line finishes. Leave empty to skip the dialogue entirely.")]
    [SerializeField] private GameObject dialogueContainer;

    [Tooltip("TextMeshProUGUI where the dialogue lines are typed out. " +
             "Usually a child of the dialogue container above.")]
    [SerializeField] private TMP_Text dialogueText;

    [Tooltip("Lines the boss says before the fight begins. Each entry is " +
             "one line, displayed sequentially with a typewriter effect. " +
             "Add more or edit in the Inspector at any time — no code " +
             "changes needed.")]
    [TextArea(2, 5)]
    [SerializeField] private string[] dialogueLines = new string[]
    {
        "So... you dare to enter my domain, little one?",
        "Turn back now, and I may yet spare you.",
        "...No? Very well. Your bones will make a fine addition to my collection."
    };

    [Tooltip("How fast each line types out, in characters per second.")]
    [SerializeField] private float dialogueCharactersPerSecond = 30f;

    [Tooltip("Pause (seconds) after a line fully types out before the next " +
             "line starts.")]
    [SerializeField] private float dialoguePauseBetweenLines = 1.2f;

    [Tooltip("Pause (seconds) after the last line finishes before the " +
             "dialogue panel hides and the boss starts moving.")]
    [SerializeField] private float dialoguePauseAfterLastLine = 0.8f;

    [Header("Scene Flow")]
    [Tooltip("Name of the scene to load once the cutscene finishes. Must be " +
             "added to Build Settings.")]
    [SerializeField] private string nextSceneName = "GameScene";

    // Guard so a double-fired signal / end-call can't load the scene twice.
    private bool cutsceneEnded;

    // ---------------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------------

    private void Start()
    {
        // Hide any HUD/UI the designer dragged in — health bar, mana bar,
        // minimap, etc. We do this first so nothing flashes on screen for
        // even a single frame before the cutscene takes over.
        if (uiToHide != null)
        {
            foreach (GameObject go in uiToHide)
            {
                if (go != null) go.SetActive(false);
            }
        }

        // Lock the player down before anything else so the first frame of
        // the cutscene is always clean (no stray input, no gravity drift).
        DisablePlayerControl();

        // Disable any boss colliders/hitboxes so the boss can't damage the
        // player by merely touching them during the walk phase.
        if (bossCollidersToDisable != null)
        {
            foreach (Collider2D c in bossCollidersToDisable)
            {
                if (c != null) c.enabled = false;
            }
        }

        // Force the boss rigidbody to Kinematic for the ENTIRE cutscene
        // (not just during the walk). Without this, if the user disabled
        // the boss's ground collider above, gravity would pull the boss
        // through the floor before anything else runs. Kinematic bodies
        // ignore gravity, so the boss stays exactly where it was placed.
        if (bossRigidbody != null)
        {
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
            bossRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (director != null)
        {
            // Timeline path: play it and wait for the Signal to call
            // OnCutsceneEnd() via CutsceneSignalReceiver.
            director.Play();
        }
        else
        {
            // Scripted path: run the hand-authored boss-attacks-player
            // sequence as a coroutine.
            StartCoroutine(RunScriptedSequence());
        }
    }

    // ---------------------------------------------------------------------
    // Scripted cutscene sequence
    // ---------------------------------------------------------------------

    /// <summary>
    /// Boss turns around, walks up to the player, hits them, player falls,
    /// then the main gameplay scene loads.
    /// </summary>
    private IEnumerator RunScriptedSequence()
    {
        // Short beat before anything moves so the fade-in / camera settles.
        yield return new WaitForSeconds(initialDelay);

        // --- 0. Intro dialogue -------------------------------------------
        // Boss monologues before doing anything. Skipped entirely if the
        // designer hasn't assigned a dialogue container + text.
        yield return StartCoroutine(PlayIntroDialogue());

        // --- 1. Boss turns around to face the player ---------------------
        // Assumes the boss starts facing away from the player. We mirror
        // localScale.x based on which side of the boss the player is on.
        if (bossTransform != null && playerTransform != null)
        {
            FaceBossTowardPlayer();
        }

        yield return new WaitForSeconds(pauseAfterTurn);

        // --- 2. Boss walks toward the player ------------------------------
        // We compute an explicit target X that sits `bossStopDistance` units
        // short of the player (on the side the boss is approaching from) and
        // slide the boss toward it. To stop physics colliders from shoving
        // the boss back while we move its transform, the boss Rigidbody2D is
        // temporarily forced kinematic for the duration of the walk.
        SetBossRunning(true);

        // Remember the original body type so we can restore it afterwards.
        RigidbodyType2D bossOriginalBodyType = RigidbodyType2D.Dynamic;
        bool bossWasKinematic = false;
        if (bossRigidbody != null)
        {
            bossOriginalBodyType = bossRigidbody.bodyType;
            bossWasKinematic = true;
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
            bossRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (bossTransform != null && playerTransform != null)
        {
            // Which side is the boss approaching from? Positive = boss is to
            // the right of the player and walks left; negative = the other way.
            float approachSign = Mathf.Sign(bossTransform.position.x - playerTransform.position.x);
            if (approachSign == 0f) approachSign = 1f; // edge case: exactly aligned

            // Target X: stop `bossStopDistance` units short of the player on
            // the approach side.
            float targetX = playerTransform.position.x + approachSign * bossStopDistance;

            // Walk toward targetX until we're within a small epsilon.
            const float arriveEpsilon = 0.05f;
            while (Mathf.Abs(bossTransform.position.x - targetX) > arriveEpsilon)
            {
                Vector3 pos = bossTransform.position;
                pos.x = Mathf.MoveTowards(pos.x, targetX, bossWalkSpeed * Time.deltaTime);
                bossTransform.position = pos;
                yield return null;
            }

            // Snap exactly so the attack frame lines up consistently.
            Vector3 finalPos = bossTransform.position;
            finalPos.x = targetX;
            bossTransform.position = finalPos;
        }

        SetBossRunning(false);

        // Restore the boss rigidbody — although for the intro scene this
        // barely matters since we're about to load the next scene.
        if (bossWasKinematic && bossRigidbody != null)
        {
            bossRigidbody.bodyType = bossOriginalBodyType;
        }

        // --- 3. Boss attacks ----------------------------------------------
        yield return new WaitForSeconds(pauseBeforeHit);
        TriggerBossAttack();

        // Wait until the attack animation is at its impact frame before
        // actually "hitting" the player.
        yield return new WaitForSeconds(hitImpactDelay);

        // --- 4. Player gets knocked down ----------------------------------
        // No force, no unfreezing the rigidbody — we just rotate the sprite
        // child so the player visibly tips over in place. Rotating the root
        // would rotate the collider, which makes the physics engine
        // teleport the player to resolve ground overlap.
        yield return StartCoroutine(PlayPlayerFall());

        // --- 5. Hold on the fallen player, then load gameplay scene -------
        yield return new WaitForSeconds(postHitDelay);
        OnCutsceneEnd();
    }

    private IEnumerator PlayIntroDialogue()
    {
        // Nothing to do if the designer hasn't wired up a text field, or
        // there are no lines to say.
        if (dialogueText == null || dialogueLines == null || dialogueLines.Length == 0)
        {
            yield break;
        }

        // Show the dialogue panel (container is optional — the text
        // component alone is enough to display something).
        if (dialogueContainer != null)
        {
            dialogueContainer.SetActive(true);
        }

        // Clamp so we can't divide by zero if the designer sets it to 0.
        float cps = Mathf.Max(1f, dialogueCharactersPerSecond);
        float timePerChar = 1f / cps;

        foreach (string line in dialogueLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                // Empty line = a beat of silence. Still honor the pause.
                dialogueText.text = "";
                yield return new WaitForSeconds(dialoguePauseBetweenLines);
                continue;
            }

            // Typewriter effect: reveal one character at a time.
            dialogueText.text = "";
            for (int i = 0; i < line.Length; i++)
            {
                dialogueText.text = line.Substring(0, i + 1);
                yield return new WaitForSeconds(timePerChar);
            }

            // Line fully revealed — hold on it before the next one.
            yield return new WaitForSeconds(dialoguePauseBetweenLines);
        }

        // Final beat after the last line, then hide the panel so the
        // boss-walks-and-attacks beat isn't obscured by dialogue UI.
        yield return new WaitForSeconds(dialoguePauseAfterLastLine);

        if (dialogueContainer != null)
        {
            dialogueContainer.SetActive(false);
        }
        dialogueText.text = "";
    }

    private void FaceBossTowardPlayer()
    {
        // Flip the boss along X so it points at the player. This matches the
        // pattern Boss.cs uses elsewhere in the project (mirrors localScale.x).
        Vector3 scale = bossTransform.localScale;
        float desiredSign = (playerTransform.position.x >= bossTransform.position.x) ? 1f : -1f;
        scale.x = Mathf.Abs(scale.x) * desiredSign;
        bossTransform.localScale = scale;
    }

    private void SetBossRunning(bool running)
    {
        // Guarded — only poke the animator if both the animator and the
        // parameter name are set. Keeps the script usable on a placeholder
        // boss that has no animator yet.
        if (bossAnimator != null && !string.IsNullOrEmpty(bossRunBoolParam))
        {
            bossAnimator.SetBool(bossRunBoolParam, running);
        }
    }

    private void TriggerBossAttack()
    {
        if (bossAnimator != null && !string.IsNullOrEmpty(bossAttackTriggerParam))
        {
            bossAnimator.SetTrigger(bossAttackTriggerParam);
        }
    }

    private IEnumerator PlayPlayerFall()
    {
        // Figure out which way the player should get knocked. If the boss
        // is to the LEFT of the player, the hit comes from the left so the
        // player flies to the RIGHT (+X). If the boss is to the right,
        // player flies left (-X). The Inspector value's X is treated as
        // magnitude — we apply the sign here.
        float knockSign = 1f;
        if (bossTransform != null && playerTransform != null)
        {
            knockSign = (bossTransform.position.x < playerTransform.position.x) ? 1f : -1f;
        }

        // Launch the player: switch the rigidbody to Dynamic so gravity and
        // the impulse actually move it, then apply the knockback force.
        // All player scripts are disabled (PlayerPlatformer, PlayerHealth,
        // PlayerSafetyNet, etc.) so nothing will interfere — no respawns,
        // no hit reactions, no input interception.
        if (playerRigidbody != null)
        {
            playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;

            Vector2 impulse = new Vector2(
                Mathf.Abs(playerKnockbackImpulse.x) * knockSign,
                playerKnockbackImpulse.y);
            playerRigidbody.AddForce(impulse, ForceMode2D.Impulse);
        }

        // Rotation target: prefer a dedicated sprite child; otherwise the
        // root. Rotating the root is safe here because the player's
        // scripts are all disabled — no respawn logic can react to a
        // rotated collider hitting the ground.
        Transform rotateTarget = playerSpriteTransform != null
            ? playerSpriteTransform
            : playerTransform;

        // Fall rotation: tip AWAY from the boss so the player falls
        // "backwards" from the hit.
        float rotationSign = -knockSign; // opposite sign so they tip away
        float targetAngle = playerFallRotation * rotationSign;

        if (rotateTarget != null)
        {
            Quaternion startRot = rotateTarget.localRotation;
            Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, targetAngle);

            if (playerFallDuration <= 0f)
            {
                rotateTarget.localRotation = endRot;
            }
            else
            {
                // Smooth tween over playerFallDuration seconds, running in
                // parallel with the physics-driven flight/fall.
                float elapsed = 0f;
                while (elapsed < playerFallDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / playerFallDuration);
                    // Ease-in curve (t^2) so the tip accelerates.
                    rotateTarget.localRotation = Quaternion.Slerp(startRot, endRot, t * t);
                    yield return null;
                }
                rotateTarget.localRotation = endRot;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Public API — called either by the Timeline Signal or by the scripted
    // coroutine once the cutscene has finished.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Invoked when the intro cutscene has finished. Loads the gameplay scene.
    /// </summary>
    public void OnCutsceneEnd()
    {
        if (cutsceneEnded) return;
        cutsceneEnded = true;

        // Load the main gameplay scene. Using single mode so the intro
        // scene (and any Timeline state) is fully unloaded. The gameplay
        // scene will spawn its own player, so there's no need to re-enable
        // this scene's player components.
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void DisablePlayerControl()
    {
        // PlayerInput off → no Input System callbacks fire on the player.
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }

        // Rigidbody2D → kinematic + zero velocity so gravity and any
        // lingering momentum can't displace the player.
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        // NOTE: we used to disable the player collider here, but that broke
        // the Animator — with no ground contact, `isGrounded` was false and
        // the player popped into the Jump/Fall state. Since the rigidbody
        // stays Kinematic for the whole cutscene (and Kinematic bodies are
        // never pushed by physics, even when their collider rotates), we
        // can safely leave the collider enabled.

        // Shut down every MonoBehaviour on the player so nothing can fight
        // us — PlayerPlatformer, PlayerHealth, PlayerSafetyNet, and so on.
        // We explicitly skip PlayerInput (already handled above) and
        // CutsceneSignalReceiver (needed if we swap to Timeline later),
        // and we skip this component itself in case IntroCutscene ends up
        // on the player for some reason.
        if (playerScriptsRoot != null)
        {
            MonoBehaviour[] scripts = playerScriptsRoot.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour mb in scripts)
            {
                if (mb == null) continue;
                if (mb == this) continue;
                if (mb is PlayerInput) continue;
                if (mb is CutsceneSignalReceiver) continue;
                mb.enabled = false;
            }
        }

        // Force the player's Animator to its Idle state. Without this, the
        // moment the movement scripts stop feeding it parameters the
        // Animator can get stuck in a transitional pose (arms mid-swing,
        // T-pose, etc.) which is what "the player looks weird" means.
        if (playerAnimator != null)
        {
            // First set the bools so any Any State transition conditions
            // (like isGrounded == false -> Jump) won't immediately pull
            // the player back out of Idle the next frame.
            if (playerAnimatorBoolsToForceTrue != null)
            {
                foreach (string param in playerAnimatorBoolsToForceTrue)
                {
                    if (string.IsNullOrEmpty(param)) continue;
                    if (HasAnimatorParam(playerAnimator, param, AnimatorControllerParameterType.Bool))
                    {
                        playerAnimator.SetBool(param, true);
                    }
                }
            }
            if (playerAnimatorBoolsToForceFalse != null)
            {
                foreach (string param in playerAnimatorBoolsToForceFalse)
                {
                    if (string.IsNullOrEmpty(param)) continue;
                    if (HasAnimatorParam(playerAnimator, param, AnimatorControllerParameterType.Bool))
                    {
                        playerAnimator.SetBool(param, false);
                    }
                }
            }

            // Now jump to the Idle state and restart it from frame 0.
            if (!string.IsNullOrEmpty(playerIdleStateName))
            {
                playerAnimator.Play(playerIdleStateName, 0, 0f);
                // Force the Animator to immediately re-evaluate so the
                // state change takes effect this frame rather than next.
                playerAnimator.Update(0f);
            }
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
}
