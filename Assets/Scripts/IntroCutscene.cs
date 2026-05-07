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
    // inspector refs

    [Header("Timeline (optional)")]
    [Tooltip("PlayableDirector for a Timeline cutscene. If empty, the built-in coroutine runs instead.")]
    [SerializeField] private PlayableDirector director;

    [Header("Player References")]
    [Tooltip("Set to kinematic during the cutscene so forces dont displace the player.")]
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Tooltip("Disabled during the cutscene so input actions dont fire.")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Disabled during the cutscene. Rotating the player while the collider is on " +
             "can cause physics to teleport the player to resolve ground overlap.")]
    [SerializeField] private Collider2D playerCollider;

    [Tooltip("Player's root Transform. Used so the boss knows where to walk to.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Optional child transform holding just the SpriteRenderer. If set, only this child " +
             "is rotated during the fall, keeping the root's collider upright.")]
    [SerializeField] private Transform playerSpriteTransform;

    [Tooltip("Player root. Every MonoBehaviour on it gets disabled during the cutscene, " +
             "except PlayerInput and CutsceneSignalReceiver.")]
    [SerializeField] private GameObject playerScriptsRoot;

    [Tooltip("Forced to idle state at cutscene start so it doesnt freeze mid-animation.")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("Name of the idle state on the player Animator. Case-sensitive.")]
    [SerializeField] private string playerIdleStateName = "Player_Idle";

    [Tooltip("Animator bools forced TRUE on cutscene start (e.g. isGrounded).")]
    [SerializeField] private string[] playerAnimatorBoolsToForceTrue = new string[] { "isGrounded" };

    [Tooltip("Animator bools forced FALSE on cutscene start (e.g. isWallSliding).")]
    [SerializeField] private string[] playerAnimatorBoolsToForceFalse = new string[] { "isWallSliding", "IsFrozen" };

    [Header("Boss References")]
    [Tooltip("Boss root Transform.")]
    [SerializeField] private Transform bossTransform;

    [Tooltip("Boss Animator. Leave empty if your boss has none.")]
    [SerializeField] private Animator bossAnimator;

    [Tooltip("Optional. Forced kinematic during the walk so colliders cant shove the boss.")]
    [SerializeField] private Rigidbody2D bossRigidbody;

    [Tooltip("Boss colliders to disable for the whole cutscene. If the damage hitbox stays " +
             "on, walking into the player would trigger a hit before the scripted attack.")]
    [SerializeField] private Collider2D[] bossCollidersToDisable;

    [Header("Sequence Timing")]
    [Tooltip("How long to wait after the scene loads before the boss starts " +
             "doing anything. Gives the camera/UI a moment to settle.")]
    [SerializeField] private float initialDelay = 0.5f;

    [Tooltip("Pause after the boss turns around, before it starts walking.")]
    [SerializeField] private float pauseAfterTurn = 0.4f;

    [Tooltip("How fast the boss walks toward the player (world units/sec).")]
    [SerializeField] private float bossWalkSpeed = 3f;

    [Tooltip("Boss stops this many units from the player so the attack animation lines up.")]
    [SerializeField] private float bossStopDistance = 2.3f;

    [Tooltip("Pause after the boss reaches the player, before the attack.")]
    [SerializeField] private float pauseBeforeHit = 0.3f;

    [Tooltip("Delay between attack trigger and player reacting. Tune to land on the swing's impact frame.")]
    [SerializeField] private float hitImpactDelay = 0.35f;

    [Tooltip("How long to linger on the dead player before the rewind begins.")]
    [SerializeField] private float postHitDelay = 1.5f;

    [Header("Boss Animator Parameters")]
    [Tooltip("Bool param that toggles boss walk/run anim. Leave empty if none.")]
    [SerializeField] private string bossRunBoolParam = "isRunning";

    [Tooltip("Trigger param that fires the boss attack anim. Leave empty if none.")]
    [SerializeField] private string bossAttackTriggerParam = "Attack";

    [Header("Player Death Reaction")]
    [Tooltip("Trigger that fires the player's death anim. Leave empty to skip.")]
    [SerializeField] private string playerDeathTriggerParam = "Death";

    [Header("Rewind Settings")]
    [Tooltip("Force-registered with TimeRewindManager so the buffer fills up before the killing blow.")]
    [SerializeField] private PlayerRewindController playerRewindController;

    [Tooltip("Optional. If empty, resolved from bossTransform. Registered during warmup.")]
    [SerializeField] private Boss bossRewindable;

    [Tooltip("Optional. Resolved from the player at runtime if not assigned.")]
    [SerializeField] private RewindGhostTrail rewindGhostTrail;

    [Tooltip("Global Volume for the rewind screen effect. Assign the scene's RewindVolume.")]
    [SerializeField] private Volume rewindPostProcessVolume;

    [Tooltip("Seconds of rewind history that must be buffered before the killing blow. " +
             "Typically 1-3s. The player just stands idle during this window.")]
    [SerializeField] private float requiredRewindHistorySeconds = 2.5f;

    [Tooltip("Seconds to rewind before the video plays. Should be <= requiredRewindHistorySeconds.")]
    [SerializeField] private float rewindDuration = 3.5f;

    [Header("Rewind UI Prompt")]
    [Tooltip("Prefab or scene object to activate when the player needs to rewind " +
             "(e.g. a hint icon/animation). It is SetActive(true) after the boss " +
             "kills the player and hidden again once R is pressed.")]
    [SerializeField] private GameObject rewindHintPrefab;

    [Header("Rewind Cutscene Video")]
    [Tooltip("Full-screen overlay shown right before the video plays. Hidden at start.")]
    [SerializeField] private GameObject rewindVideoCanvas;

    [Tooltip("Plays the rewind cutscene. Clip is assigned at runtime so leave its clip slot empty.")]
    [SerializeField] private VideoPlayer rewindVideoPlayer;

    [Tooltip("RewindCutscene.mov from Assets/Animations/.")]
    [SerializeField] private VideoClip rewindVideoClip;

    [Tooltip("Wait after the video before loading the gameplay scene. 0 = instant cut.")]
    [SerializeField] private float postVideoDelay = 0.2f;

    [Header("UI")]
    [Tooltip("HUD elements to hide during the cutscene (health bar, mana, minimap, etc).")]
    [SerializeField] private GameObject[] uiToHide;

    [Header("Intro Dialogue")]
    [Tooltip("Dialogue UI root. Toggled at the start/end of the dialogue. Empty = skip.")]
    [SerializeField] private GameObject dialogueContainer;

    [Tooltip("TMP text where dialogue types out.")]
    [SerializeField] private TMP_Text dialogueText;

    [Tooltip("Boss lines before the fight. One entry per line, typewriter reveal.")]
    [TextArea(2, 5)]
    [SerializeField] private string[] dialogueLines = new string[]
    {
        "So... you dare to enter my domain, little one?",
        "Turn back now, and I may yet spare you.",
        "...No? Very well. Your bones will make a fine addition to my collection."
    };

    [Tooltip("How fast each line types out, in characters per second.")]
    [SerializeField] private float dialogueCharactersPerSecond = 30f;

    [Tooltip("Pause after each line finishes before the next.")]
    [SerializeField] private float dialoguePauseBetweenLines = 1.2f;

    [Tooltip("Pause after the last line before the dialogue hides and the boss moves.")]
    [SerializeField] private float dialoguePauseAfterLastLine = 0.8f;

    [Header("Scene Flow")]
    [Tooltip("Scene to load after the rewind video. Must be in Build Settings.")]
    [SerializeField] private string nextSceneName = "GameScene";
    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private float deathVolume = 0.5f;
    [SerializeField] private AudioClip reviveClip;
    [SerializeField] private float reviveVolume = 0.5f;
    [SerializeField] private AudioClip rewindStartClip;
    [SerializeField] private float rewindVolume = 1f;
    [SerializeField] private AudioSource bossAudioSource;
    [SerializeField] private AudioClip dialogueBlip;
    [SerializeField] private float dialogueBlipVolume = 1.5f;
    [SerializeField] private float minPitch = 0.6f;
    [SerializeField] private float maxPitch = 0.7f;
    [SerializeField] private int charsPerSound = 2;

    // guard so a double-fired signal cant load the scene twice
    private bool cutsceneEnded;
    private TimeRewindManager rewindManager;
    private Camera mainCamera;
    private RewindEffects rewindEffects;
    private RewindableAnimator bossAnimatorRewindable;
    private RewindMusicController musicController;

    private void Start()
    {
        rewindManager = TimeRewindManager.Instance;
        musicController = FindFirstObjectByType<RewindMusicController>();

        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            rewindEffects = mainCamera.GetComponent<RewindEffects>();
            if (rewindEffects == null)
            {
                rewindEffects = mainCamera.gameObject.AddComponent<RewindEffects>();
                Debug.Log("[IntroCutscene] Added RewindEffects component to main camera");
            }

            // use the scene's RewindVolume so the effect applies to the whole camera output
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

        if (rewindGhostTrail == null && playerRewindController != null)
        {
            rewindGhostTrail = playerRewindController.GetComponent<RewindGhostTrail>();
            if (rewindGhostTrail == null)
            {
                rewindGhostTrail = playerRewindController.gameObject.AddComponent<RewindGhostTrail>();
                Debug.Log("[IntroCutscene] Added RewindGhostTrail component to player for visual feedback");
            }
        }

        EnsureBossAnimatorRewindable();

        // hide HUD
        if (uiToHide != null)
        {
            foreach (GameObject go in uiToHide)
            {
                if (go != null) go.SetActive(false);
            }
        }

        if (rewindVideoCanvas != null)
            rewindVideoCanvas.SetActive(false);

        DisablePlayerControl();
        // player shouldnt be able to rewind before prompted
        playerRewindController.DisableManualRewind = true;

        if (bossCollidersToDisable != null)
        {
            foreach (Collider2D c in bossCollidersToDisable)
            {
                if (c != null) c.enabled = false;
            }
        }

        // boss rigidbody kinematic for the whole cutscene
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

    private IEnumerator RunScriptedSequence()
    {
        yield return new WaitForSeconds(initialDelay);

        // 0. fill the rewind buffer first, fresh scene starts empty
        yield return StartCoroutine(WarmUpRewindHistory());

        // 1. intro dialogue
        yield return StartCoroutine(PlayIntroDialogue());

        // 2. boss turns to face the player
        if (bossTransform != null && playerTransform != null)
            FaceBossTowardPlayer();

        yield return new WaitForSeconds(pauseAfterTurn);

        // 3. boss walks toward the player
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

        // 4. boss attacks
        yield return new WaitForSeconds(pauseBeforeHit);
        TriggerBossAttack();

        yield return new WaitForSeconds(hitImpactDelay);

        // 5. player dies
        TriggerPlayerDeath();

        // 6. hold on death anim
        yield return new WaitForSeconds(postHitDelay);

        // 7. rewind
        yield return StartCoroutine(RunRewindSequence());

        // 8. video plays inside RunRewindSequence

        // 9. load gameplay scene
        OnCutsceneEnd();
    }

    // builds rewind history before the hit lands. fresh scene = empty buffer,
    // so wait until enough is recorded for the rewind to actually have something
    // to play back
    private IEnumerator WarmUpRewindHistory()
    {
        if (rewindManager == null)
        {
            Debug.LogWarning("[IntroCutscene] TimeRewindManager not found, rewind will be skipped.");
            yield break;
        }

        Debug.Log("[IntroCutscene] Starting rewind history warmup...");

        // force-register so the manager starts filling its buffer even though we
        // disabled all the player scripts above
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

        // also register the boss so its transform/animator state are captured
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

        // wait for the buffer to fill
        float timeout = requiredRewindHistorySeconds + 3f; // safety ceiling
        float waited  = 0f;

        while (waited < requiredRewindHistorySeconds)
        {
            waited += Time.deltaTime;

            if (waited > timeout)
            {
                Debug.LogWarning("[IntroCutscene] Timed out waiting for rewind history, proceeding anyway.");
                break;
            }

            yield return null;
        }

        Debug.Log($"[IntroCutscene] Warmup complete. CanRewind={rewindManager.CanRewind}");

        if (!rewindManager.CanRewind)
        {
            Debug.LogWarning("[IntroCutscene] CanRewind is still false after warm-up. " +
                             "Check that PlayerRewindController.CaptureState() is being called " +
                             "and that the component's FixedUpdate/recording path is active.");
        }
    }

    private IEnumerator RunRewindSequence()
    {
        if (rewindManager == null || !rewindManager.CanRewind)
        {
            Debug.LogWarning("[IntroCutscene] Rewind skipped: manager missing or no recorded history.");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        Debug.Log("[IntroCutscene] Showing rewind hint, waiting for player input...");

        // show the rewind hint and make sure its animator runs while frozen
        if (rewindHintPrefab != null)
        {
            rewindHintPrefab.SetActive(true);
            var hintAnimator = rewindHintPrefab.GetComponentInChildren<Animator>();
            if (hintAnimator != null)
                hintAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        // freeze until the player presses R
        Time.timeScale = 0f;

        // wait indefinitely for R or both triggers
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
        if (sfxSource != null && rewindStartClip != null)
        {
                sfxSource.PlayOneShot(rewindStartClip, rewindVolume);
        }

        Debug.Log("[IntroCutscene] R pressed, starting rewind");

        if (rewindHintPrefab != null)
            rewindHintPrefab.SetActive(false);

        Time.timeScale = 1f;
        rewindManager.StartRewind();

        float rewindTimer = 0f;
        while (rewindTimer < rewindDuration)
        {
            rewindTimer += Time.unscaledDeltaTime;

            if (!rewindManager.IsRewinding || rewindManager.RemainingRewindTime <= 0f)
                break;

            yield return null;
        }

        // freeze the FX at full intensity before stopping the rewind so they dont
        // fade out during the transition to the video
        if (rewindEffects != null)
            rewindEffects.FreezeEffects();

        if (rewindManager.IsRewinding)
            rewindManager.StopRewind();

        // if (sfxSource != null && reviveClip != null)
        // {
        //     sfxSource.PlayOneShot(reviveClip);
        // }
        yield return StartCoroutine(PlayRewindVideo());
        if (playerInput != null)
            playerInput.enabled = false;

        // let slow-mo recovery play
        yield return new WaitForSecondsRealtime(0.1f);

        Debug.Log("[IntroCutscene] Rewind complete, ready for video");
    }

    private void TriggerPlayerDeath()
    {
        if (playerAnimator != null && !string.IsNullOrEmpty(playerDeathTriggerParam))
        {
            if (HasAnimatorParam(playerAnimator, playerDeathTriggerParam, AnimatorControllerParameterType.Trigger))
                playerAnimator.SetTrigger(playerDeathTriggerParam);
        }
        if (sfxSource != null && deathClip != null)
        {
            sfxSource.PlayOneShot(deathClip);
        }
    }

    private IEnumerator PlayRewindVideo()
    {
        if (rewindVideoPlayer == null || rewindVideoClip == null)
        {
            Debug.LogWarning("[IntroCutscene] Rewind video player or clip not assigned, skipping video.");
            yield break;
        }

        if (rewindVideoCanvas != null)
            rewindVideoCanvas.SetActive(true);
        PrepareForVideoPlayback();
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

    private IEnumerator PlayIntroDialogue()
    {
        if (dialogueText == null || dialogueLines == null || dialogueLines.Length == 0)
            yield break;

        // show the dialogue container and its parent canvas
        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
                parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        // wait a frame so canvas + TMP fully initialise
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

            // pre-compute auto-size then lock it so the text doesnt jitter
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

                if (bossAudioSource != null && dialogueBlip != null && i % charsPerSound == 0)
                {
                    bossAudioSource.pitch = Random.Range(minPitch, maxPitch);
                    bossAudioSource.PlayOneShot(dialogueBlip, dialogueBlipVolume);
                }

                yield return PauseAwareWait.Seconds(timePerChar);
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

    public void OnCutsceneEnd()
    {
        if (cutsceneEnded) return;
        cutsceneEnded = true;
        ResetTimeStateForNextScene();
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    private void DisablePlayerControl()
    {
        if (playerInput != null)
            playerInput.enabled = false;

        // PlayerRewindController stays enabled so its rewind hooks keep working,
        // but we set external mode so the player cant trigger a rewind themselves
        // before the prompt
        if (playerRewindController != null)
            playerRewindController.SetExternalRewindActive(true);

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
                // dont disable PlayerRewindController, we still need its rewind hooks.
                // input is suppressed via DisableManualRewind / external mode instead
                if (playerRewindController != null && mb == playerRewindController)
                {
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

        // Keep the music playing in reverse during the video.
        if (musicController != null)
            musicController.ForceReversePitch();
    }

    // adds ColorAdjustments / ChromaticAberration / Vignette to the volume if missing
    private void EnsurePostProcessingComponents(Volume volume)
    {
        if (volume == null || volume.profile == null)
            return;

        var profile = volume.profile;

        if (!profile.Has<ColorAdjustments>())
        {
            profile.Add<ColorAdjustments>();
            Debug.Log("[IntroCutscene] Added ColorAdjustments to post-processing profile");
        }

        if (!profile.Has<ChromaticAberration>())
        {
            profile.Add<ChromaticAberration>();
            Debug.Log("[IntroCutscene] Added ChromaticAberration to post-processing profile");
        }

        if (!profile.Has<Vignette>())
        {
            profile.Add<Vignette>();
            Debug.Log("[IntroCutscene] Added Vignette to post-processing profile");
        }
    }
}
