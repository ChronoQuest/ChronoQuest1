using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using TimeRewind;

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

    [Tooltip("How long to linger on the fallen/dead player before the " +
             "rewind begins. Gives the death animation a moment to " +
             "settle.")]
    [SerializeField] private float postHitDelay = 1.5f;

    [Header("Boss Animator Parameters")]
    [Tooltip("Bool parameter name on the boss Animator that toggles the " +
             "walk/run animation. Leave empty if you don't have one.")]
    [SerializeField] private string bossRunBoolParam = "isRunning";

    [Tooltip("Trigger parameter name on the boss Animator used to fire the " +
             "attack animation. Leave empty if you don't have one.")]
    [SerializeField] private string bossAttackTriggerParam = "Attack";

    [Header("Player Death Reaction")]
    [Tooltip("Trigger parameter name on the player's Animator used to fire " +
             "the death animation when the boss hits them. Must match " +
             "exactly what it's called in your Animator Controller. " +
             "Leave empty to skip.")]
    [SerializeField] private string playerDeathTriggerParam = "Death";

    [Header("Rewind Settings")]
    [Tooltip("The PlayerRewindController (or whatever IRewindable component " +
             "is on the player). We need a reference so we can force-register " +
             "it with TimeRewindManager and wait until it has recorded enough " +
             "history before the cutscene hit lands.")]
    [SerializeField] private PlayerRewindController playerRewindController;

    [Tooltip("OPTIONAL: The Boss script (which implements IRewindable). If left empty, " +
             "the script will attempt to find it from bossTransform. We register it " +
             "during warmup to ensure the boss's transform and state are captured for rewind.")]
    [SerializeField] private Boss bossRewindable;

    [Tooltip("The RewindGhostTrail component (if present). If not assigned, the script " +
             "will find it on the player automatically to ensure ghost trail visuals " +
             "are visible during rewind.")]
    [SerializeField] private RewindGhostTrail rewindGhostTrail;

    [Tooltip("Global Volume used for the rewind screen effect. Assign the intro scene's " +
             "'RewindVolume' object here so the visual treatment applies to the whole scene.")]
    [SerializeField] private Volume rewindPostProcessVolume;

    [Tooltip("How many seconds of rewind history must be buffered before the " +
             "boss is allowed to deliver the killing blow. This ensures the " +
             "rewind actually has something to play back. Must be > 0. " +
             "Typically 1–3 s. The scene will show the idle player for this " +
             "long before anything happens, so keep it short.")]
    [SerializeField] private float requiredRewindHistorySeconds = 2.5f;

    [Tooltip("How many seconds to rewind the player before the video plays. " +
             "Should be <= requiredRewindHistorySeconds and <= the " +
             "TimeRewindManager's maxRewindDuration.")]
    [SerializeField] private float rewindDuration = 3.5f;

    [Header("Rewind UI Prompt")]
    [Tooltip("Prefab or scene object to activate when the player needs to rewind " +
             "(e.g. a hint icon/animation). It is SetActive(true) after the boss " +
             "kills the player and hidden again once R is pressed.")]
    [SerializeField] private GameObject rewindHintPrefab;

    [Header("Rewind Cutscene Video")]
    [Tooltip("A full-screen RawImage Canvas (or similar overlay GameObject) " +
             "that sits in front of everything. It is hidden at the start " +
             "of the intro and made visible right before the video plays. " +
             "Attach a VideoPlayer component to this same GameObject (or " +
             "to any child) and reference it in Rewind Video Player below.")]
    [SerializeField] private GameObject rewindVideoCanvas;

    [Tooltip("The VideoPlayer that will play the rewind cutscene. Configure " +
             "its Render Mode in the Inspector (e.g. Render Texture or " +
             "Camera Near Plane). The clip is assigned at runtime from the " +
             "Rewind Video Clip field below, so leave the VideoPlayer's " +
             "own clip slot empty.")]
    [SerializeField] private VideoPlayer rewindVideoPlayer;

    [Tooltip("The imported RewindCutscene.mov video clip. Drag the .mov " +
             "asset from Assets/Animations/ here. Unity imports .mov files " +
             "as VideoClip assets automatically.")]
    [SerializeField] private VideoClip rewindVideoClip;

    [Tooltip("How long to wait after the video finishes before loading the " +
             "gameplay scene. A small value (0.1 – 0.5 s) gives a clean " +
             "cut; set to 0 for an immediate transition.")]
    [SerializeField] private float postVideoDelay = 0.2f;

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
    [Tooltip("Name of the scene to load once the rewind video finishes. " +
             "Must be added to Build Settings.")]
    [SerializeField] private string nextSceneName = "GameScene";

    // Guard so a double-fired signal / end-call can't load the scene twice.
    private bool cutsceneEnded;
    private TimeRewindManager rewindManager;
    private Camera mainCamera;
    private RewindEffects rewindEffects;
    private RewindableAnimator bossAnimatorRewindable;
    

    // ---------------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------------

    private void Start()
    {
        rewindManager = TimeRewindManager.Instance;

        // Find the main camera and ensure it has RewindEffects for visual feedback
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            rewindEffects = mainCamera.GetComponent<RewindEffects>();
            if (rewindEffects == null)
            {
                // Add RewindEffects component if it doesn't exist
                rewindEffects = mainCamera.gameObject.AddComponent<RewindEffects>();
                Debug.Log("[IntroCutscene] Added RewindEffects component to main camera");
            }
            
            // Use the intro scene's dedicated rewind volume so the effect applies
            // consistently to the whole camera output, not whichever Volume is found first.
            var volume = ResolveRewindVolume();
            if (volume != null)
            {
                // Ensure volume has the required post-processing components
                EnsurePostProcessingComponents(volume);
                
                // Use the public method to properly initialize the component
                rewindEffects.SetPostProcessVolume(volume);
                Debug.Log("[IntroCutscene] Set post-processing volume for RewindEffects");
            }
            else
            {
                Debug.LogWarning("[IntroCutscene] No post-processing volume found - RewindEffects visual feedback disabled. " +
                    "Create a Volume in the scene and assign it to RewindEffects component.");
            }
        }

        // Find RewindGhostTrail if not assigned (it provides ghost trail visuals during rewind)
        if (rewindGhostTrail == null && playerRewindController != null)
        {
            rewindGhostTrail = playerRewindController.GetComponent<RewindGhostTrail>();
            if (rewindGhostTrail == null)
            {
                // Add RewindGhostTrail if it doesn't exist
                rewindGhostTrail = playerRewindController.gameObject.AddComponent<RewindGhostTrail>();
                Debug.Log("[IntroCutscene] Added RewindGhostTrail component to player for visual feedback");
            }
        }

        EnsureBossAnimatorRewindable();

        // Hide any HUD/UI the designer dragged in.
        if (uiToHide != null)
        {
            foreach (GameObject go in uiToHide)
            {
                if (go != null) go.SetActive(false);
            }
        }

        // Keep the rewind video overlay hidden until we need it.
        if (rewindVideoCanvas != null)
            rewindVideoCanvas.SetActive(false);

        // Lock the player down before anything else.
        DisablePlayerControl();

        // Disable any boss colliders/hitboxes.
        if (bossCollidersToDisable != null)
        {
            foreach (Collider2D c in bossCollidersToDisable)
            {
                if (c != null) c.enabled = false;
            }
        }

        // Force the boss rigidbody to Kinematic for the ENTIRE cutscene.
        if (bossRigidbody != null)
        {
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
            bossRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (director != null)
        {
            director.Play();
        }
        else
        {
            StartCoroutine(RunScriptedSequence());
        }
    }

    // ---------------------------------------------------------------------
    // Scripted cutscene sequence
    // ---------------------------------------------------------------------

    private IEnumerator RunScriptedSequence()
    {
        yield return new WaitForSeconds(initialDelay);

        // ── 0. Register the player with the rewind manager and wait until
        //        enough history has been recorded to make the rewind visible.
        //        This is the key step that was missing before: in a fresh scene
        //        the buffer starts empty, so we must let it fill up first.
        yield return StartCoroutine(WarmUpRewindHistory());

        // ── 1. Intro dialogue ────────────────────────────────────────────
        yield return StartCoroutine(PlayIntroDialogue());

        // ── 2. Boss turns around to face the player ──────────────────────
        if (bossTransform != null && playerTransform != null)
            FaceBossTowardPlayer();

        yield return new WaitForSeconds(pauseAfterTurn);

        // ── 3. Boss walks toward the player ──────────────────────────────
        SetBossRunning(true);

        if (bossRigidbody != null)
        {
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
            bossRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (bossTransform != null && playerTransform != null)
        {
            float approachSign = Mathf.Sign(bossTransform.position.x - playerTransform.position.x);
            if (approachSign == 0f) approachSign = 1f;

            float targetX = playerTransform.position.x + approachSign * bossStopDistance;

            const float arriveEpsilon = 0.05f;
            while (Mathf.Abs(bossTransform.position.x - targetX) > arriveEpsilon)
            {
                Vector3 pos = bossTransform.position;
                pos.x = Mathf.MoveTowards(pos.x, targetX, bossWalkSpeed * Time.deltaTime);
                bossTransform.position = pos;
                yield return null;
            }

            Vector3 finalPos = bossTransform.position;
            finalPos.x = targetX;
            bossTransform.position = finalPos;
        }

        SetBossRunning(false);

        // ── 4. Boss attacks ───────────────────────────────────────────────
        yield return new WaitForSeconds(pauseBeforeHit);
        TriggerBossAttack();

        yield return new WaitForSeconds(hitImpactDelay);

        // ── 5. Player is killed ───────────────────────────────────────────
        TriggerPlayerDeath();

        // ── 6. Hold on the death animation ───────────────────────────────
        yield return new WaitForSeconds(postHitDelay);

        // ── 7. REWIND ─────────────────────────────────────────────────────
        // Stop recording new history so the buffer stays at its current
        // "alive" state while we replay backwards through it.
        yield return StartCoroutine(RunRewindSequence());

        // ── 8. Play the rewind cutscene video ─────────────────────────────
        yield return StartCoroutine(PlayRewindVideo());

        // ── 9. Load the gameplay scene ────────────────────────────────────
        OnCutsceneEnd();
    }

    // ---------------------------------------------------------------------
    // Rewind warm-up — build history BEFORE the hit lands
    // ---------------------------------------------------------------------

    /// <summary>
    /// Registers the player with TimeRewindManager (if not already registered)
    /// and waits until the buffer contains at least <see cref="requiredRewindHistorySeconds"/>
    /// of recorded states. The player just stands idle during this window —
    /// keep the value small (1–3 s) so it feels invisible.
    /// </summary>
    private IEnumerator WarmUpRewindHistory()
    {
        if (rewindManager == null)
        {
            Debug.LogWarning("[IntroCutscene] TimeRewindManager not found — rewind will be skipped.");
            yield break;
        }

        Debug.Log("[IntroCutscene] Starting rewind history warmup...");

        // Force-register the player's IRewindable component so the manager
        // starts filling its buffer immediately, even if the component's own
        // OnEnable hasn't run (because we disabled all player scripts above).
        if (playerRewindController != null && playerRewindController is IRewindable playerRewindable)
        {
            rewindManager.Register(playerRewindable);
            Debug.Log("[IntroCutscene] Registered player for rewind recording");
        }
        else
        {
            Debug.LogWarning("[IntroCutscene] playerRewindController is not assigned or does not implement IRewindable. " +
                             "Assign it in the Inspector. Rewind will be skipped.");
            yield break;
        }

        // Force-register the boss's IRewindable component so its transform,
        // animator state, and other rewindable data are captured during warmup.
        if (bossRewindable == null && bossTransform != null)
        {
            bossRewindable = bossTransform.GetComponent<Boss>();
        }

        if (bossRewindable != null && bossRewindable is IRewindable bossIRewindable)
        {
            rewindManager.Register(bossIRewindable);
            Debug.Log("[IntroCutscene] Registered boss for rewind recording");
        }
        else
        {
            Debug.LogWarning("[IntroCutscene] Boss is not found or does not implement IRewindable. " +
                             "Assign the Boss component in the Inspector or ensure bossTransform is set. " +
                             "Boss rewind may not work properly.");
        }

        if (bossAnimatorRewindable != null)
        {
            rewindManager.Register(bossAnimatorRewindable);
            Debug.Log("[IntroCutscene] Registered boss animator rewind helper");
        }

        // Wait until the buffer has accumulated enough history.
        float timeout = requiredRewindHistorySeconds + 3f; // safety ceiling
        float waited  = 0f;

        while (waited < requiredRewindHistorySeconds)
        {
            waited += Time.deltaTime;

            if (waited > timeout)
            {
                Debug.LogWarning("[IntroCutscene] Timed out waiting for rewind history — proceeding anyway.");
                break;
            }

            yield return null;
        }

        Debug.Log($"[IntroCutscene] Warmup complete. CanRewind={rewindManager.CanRewind}");

        // Double-check we actually have states; warn loudly if not.
        if (!rewindManager.CanRewind)
        {
            Debug.LogWarning("[IntroCutscene] CanRewind is still false after warm-up. " +
                             "Check that PlayerRewindController.CaptureState() is being called " +
                             "and that the component's FixedUpdate/recording path is active.");
        }
    }

    // ---------------------------------------------------------------------
    // Rewind sequence
    // ---------------------------------------------------------------------

    private IEnumerator RunRewindSequence()
    {
        if (rewindManager == null || !rewindManager.CanRewind)
        {
            Debug.LogWarning("[IntroCutscene] Rewind skipped: manager missing or no recorded history.");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        Debug.Log("[IntroCutscene] Showing rewind hint, waiting for player input...");

        // Show the rewind hint and ensure its animator runs while the game is frozen
        if (rewindHintPrefab != null)
        {
            rewindHintPrefab.SetActive(true);
            var hintAnimator = rewindHintPrefab.GetComponentInChildren<Animator>();
            if (hintAnimator != null)
                hintAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        // Freeze the game until the player presses R
        Time.timeScale = 0f;

        // Wait indefinitely for the player to press R or both triggers
        while (true)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                break;

            var gamepad = Gamepad.current;
            if (gamepad != null
                && gamepad.leftTrigger.ReadValue() > 0.5f
                && gamepad.rightTrigger.ReadValue() > 0.5f)
                break;

            yield return null;
        }

        Debug.Log("[IntroCutscene] R pressed — starting rewind!");

        // Hide the rewind hint
        if (rewindHintPrefab != null)
            rewindHintPrefab.SetActive(false);

        // Unfreeze and trigger the rewind
        Time.timeScale = 1f;
        rewindManager.StartRewind();

        // Let the rewind play out (with all visual effects!)
        float rewindTimer = 0f;
        while (rewindTimer < rewindDuration)
        {
            rewindTimer += Time.unscaledDeltaTime;

            if (!rewindManager.IsRewinding || rewindManager.RemainingRewindTime <= 0f)
                break;

            yield return null;
        }

        if (rewindManager.IsRewinding)
            rewindManager.StopRewind();

        // Disable player input again
        if (playerInput != null)
            playerInput.enabled = false;

        // Let slow-motion recovery play
        yield return new WaitForSecondsRealtime(0.1f);

        Debug.Log("[IntroCutscene] Rewind complete, ready for video");
    }

    // ---------------------------------------------------------------------
    // Player death
    // ---------------------------------------------------------------------

    private void TriggerPlayerDeath()
    {
        if (playerAnimator != null && !string.IsNullOrEmpty(playerDeathTriggerParam))
        {
            if (HasAnimatorParam(playerAnimator, playerDeathTriggerParam, AnimatorControllerParameterType.Trigger))
                playerAnimator.SetTrigger(playerDeathTriggerParam);
        }
    }

    // ---------------------------------------------------------------------
    // Rewind video playback
    // ---------------------------------------------------------------------

    private IEnumerator PlayRewindVideo()
    {
        if (rewindVideoPlayer == null || rewindVideoClip == null)
        {
            Debug.LogWarning("[IntroCutscene] Rewind video player or clip not assigned — skipping video.");
            yield break;
        }

        PrepareForVideoPlayback();

        if (rewindVideoCanvas != null)
            rewindVideoCanvas.SetActive(true);

        rewindVideoPlayer.clip        = rewindVideoClip;
        rewindVideoPlayer.isLooping   = false;
        rewindVideoPlayer.playOnAwake = false;

        rewindVideoPlayer.Prepare();
        while (!rewindVideoPlayer.isPrepared)
            yield return null;

        rewindVideoPlayer.Play();

        while (rewindVideoPlayer.isPlaying)
            yield return null;

        yield return new WaitForSeconds(postVideoDelay);
    }

    // ---------------------------------------------------------------------
    // Dialogue
    // ---------------------------------------------------------------------

    private IEnumerator PlayIntroDialogue()
    {
        if (dialogueText == null || dialogueLines == null || dialogueLines.Length == 0)
            yield break;

        // Activate the dialogue container and its parent canvas (if hidden).
        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
                parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        // Wait a frame so the canvas and TMP components fully initialize.
        yield return null;

        float cps        = Mathf.Max(1f, dialogueCharactersPerSecond);
        float timePerChar = 1f / cps;

        foreach (string line in dialogueLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                dialogueText.text = "";
                yield return new WaitForSeconds(dialoguePauseBetweenLines);
                continue;
            }

            // Compute the auto-sized font for the full line, then lock it in
            // so the size stays stable during the typewriter reveal.
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

            yield return new WaitForSeconds(dialoguePauseBetweenLines);
        }

        yield return new WaitForSeconds(dialoguePauseAfterLastLine);

        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
                parentCanvas.gameObject.SetActive(false);
            dialogueContainer.SetActive(false);
        }

        dialogueText.text = "";
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueText.enableAutoSizing = true;
    }

    // ---------------------------------------------------------------------
    // Boss helpers
    // ---------------------------------------------------------------------

    private void FaceBossTowardPlayer()
    {
        Vector3 scale    = bossTransform.localScale;
        float desiredSign = (playerTransform.position.x >= bossTransform.position.x) ? 1f : -1f;
        scale.x          = Mathf.Abs(scale.x) * desiredSign;
        bossTransform.localScale = scale;
    }

    private void SetBossRunning(bool running)
    {
        if (bossAnimator != null && !string.IsNullOrEmpty(bossRunBoolParam))
        {
            bossAnimator.SetBool(bossRunBoolParam, running);
            bossAnimator.Update(0f);
        }
    }

    private void TriggerBossAttack()
    {
        if (bossAnimator != null && !string.IsNullOrEmpty(bossAttackTriggerParam))
        {
            bossAnimator.SetTrigger(bossAttackTriggerParam);
            bossAnimator.Update(0f);
        }
    }

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------

    public void OnCutsceneEnd()
    {
        if (cutsceneEnded) return;
        cutsceneEnded = true;
        ResetTimeStateForNextScene();
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void DisablePlayerControl()
    {
        if (playerInput != null)
            playerInput.enabled = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (playerScriptsRoot != null)
        {
            MonoBehaviour[] scripts = playerScriptsRoot.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour mb in scripts)
            {
                if (mb == null) continue;
                if (mb == this) continue;
                if (mb is PlayerInput) continue;
                if (mb is CutsceneSignalReceiver) continue;
                if (mb is RewindGhostTrail) continue;
                // Disable PlayerRewindController's input handling to prevent interference
                // with the automatic cutscene rewind. We'll re-enable it after the rewind.
                if (playerRewindController != null && mb == playerRewindController)
                {
                    // Don't disable entirely - just set a flag or disable later
                    continue;
                }
                mb.enabled = false;
            }
        }

        if (playerAnimator != null)
        {
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
    }

    private static bool HasAnimatorParam(Animator animator, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == name && p.type == type) return true;
        }
        return false;
    }

    private Volume ResolveRewindVolume()
    {
        if (rewindPostProcessVolume != null)
            return rewindPostProcessVolume;

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        foreach (Volume volume in volumes)
        {
            if (volume != null && volume.isGlobal && volume.gameObject.name == "RewindVolume")
                return volume;
        }

        foreach (Volume volume in volumes)
        {
            if (volume != null && volume.isGlobal)
                return volume;
        }

        return null;
    }

    private void EnsureBossAnimatorRewindable()
    {
        if (bossAnimator == null)
            return;

        bossAnimatorRewindable = bossAnimator.GetComponent<RewindableAnimator>();
        if (bossAnimatorRewindable == null)
        {
            bossAnimatorRewindable = bossAnimator.gameObject.AddComponent<RewindableAnimator>();
            Debug.Log("[IntroCutscene] Added RewindableAnimator to boss for animation rewind support");
        }

        bossAnimatorRewindable.enabled = true;
    }

    private void ResetTimeStateForNextScene()
    {
        if (rewindManager != null)
        {
            if (rewindManager.IsRewinding)
                rewindManager.StopRewind();

            rewindManager.StopAllCoroutines();
            rewindManager.ClearHistory();
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void BlockAllPlayerInputForVideo()
    {
        if (playerInput != null)
            playerInput.enabled = false;

        if (playerRewindController != null)
            playerRewindController.enabled = false;
    }

    private void PrepareForVideoPlayback()
    {
        BlockAllPlayerInputForVideo();
        ResetTimeStateForNextScene();
    }

    /// <summary>
    /// Ensures the post-processing volume has the required components for rewind visual effects.
    /// Creates ColorAdjustments, ChromaticAberration, and Vignette if missing.
    /// </summary>
    private void EnsurePostProcessingComponents(Volume volume)
    {
        if (volume == null || volume.profile == null)
            return;

        var profile = volume.profile;

        // Ensure ColorAdjustments component exists
        if (!profile.Has<ColorAdjustments>())
        {
            profile.Add<ColorAdjustments>();
            Debug.Log("[IntroCutscene] Added ColorAdjustments to post-processing profile");
        }

        // Ensure ChromaticAberration component exists
        if (!profile.Has<ChromaticAberration>())
        {
            profile.Add<ChromaticAberration>();
            Debug.Log("[IntroCutscene] Added ChromaticAberration to post-processing profile");
        }

        // Ensure Vignette component exists
        if (!profile.Has<Vignette>())
        {
            profile.Add<Vignette>();
            Debug.Log("[IntroCutscene] Added Vignette to post-processing profile");
        }
    }
}
