using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Plays a short cutscene at the start of GameScene_3:
///   1. Watcher dialogue ("if you want to get to me…")
///   2. Necromancer revives 2 skeletons
///   3. Necromancer turns and runs into the level
///   4. Player regains control
///
/// Attach to an empty GameObject in the scene. Wire Inspector refs per the
/// header tooltips. Skeletons should be placed in the scene with their
/// GameObjects set ACTIVE but with health set to 0 (dead) — or simply
/// disable their SpriteRenderers and Colliders manually. The cutscene
/// calls Revive() on each, which handles re-enabling everything and
/// playing the rise animation.
/// </summary>
public class Level3IntroCutscene : MonoBehaviour
{
    // ── Player ──────────────────────────────────────────────────────────────
    [Header("Player")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string playerIdleStateName = "Player_Idle";
    [Tooltip("Every MonoBehaviour on the player root to disable during the " +
             "cutscene (except PlayerInput, which we handle separately).")]
    [SerializeField] private GameObject playerScriptsRoot;

    // ── Necromancer ─────────────────────────────────────────────────────────
    [Header("Necromancer")]
    [SerializeField] private NecromancerEnemy necromancer;
    [SerializeField] private Animator necromancerAnimator;
    [SerializeField] private Rigidbody2D necromancerRigidbody;
    [SerializeField] private Collider2D[] necromancerCollidersToDisable;
    [SerializeField] private float necromancerRunSpeed = 4f;

    // ── Skeletons ───────────────────────────────────────────────────────────
    [Header("Skeletons (start dead)")]
    [Tooltip("Place 2 skeleton prefabs in the scene near the necromancer. " +
             "Set their health to 0 in the Inspector so they begin dead " +
             "(sprite hidden, collider off). The cutscene calls Revive().")]
    [SerializeField] private EnemyBase[] skeletons = new EnemyBase[2];

    // ── Watcher Dialogue ────────────────────────────────────────────────────
    [Header("Watcher Dialogue")]
    [SerializeField] private GameObject dialogueContainer;
    [SerializeField] private TMP_Text dialogueText;
    [TextArea(2, 5)]
    [SerializeField] private string[] dialogueLines = new string[]
    {
        "So... you've made it this far.",
        "If you want to get to me, you'll have to get through him first."
    };
    [SerializeField] private float dialogueCharactersPerSecond = 30f;
    [SerializeField] private float dialoguePauseBetweenLines = 1.2f;
    [SerializeField] private float dialoguePauseAfterLastLine = 0.8f;

    // ── Timing ──────────────────────────────────────────────────────────────
    [Header("Timing")]
    [SerializeField] private float initialDelay = 0.5f;
    [SerializeField] private float pauseBeforeRevive = 0.4f;
    [Tooltip("Must match the Necromancer's Revive animation length.")]
    [SerializeField] private float necromancerReviveAnimDuration = 1.2f;
    [Tooltip("Fraction through the revive anim when the skeletons actually rise.")]
    [Range(0.2f, 0.9f)]
    [SerializeField] private float skeletonReviveMoment = 0.5f;
    [SerializeField] private float pauseAfterRevive = 0.6f;
    [SerializeField] private float pauseBeforeRun = 0.3f;
    [Tooltip("How long the necromancer runs on screen before the camera pans back.")]
    [SerializeField] private float necromancerRunOnCameraDuration = 1.5f;

    // ── Camera ──────────────────────────────────────────────────────────────
    [Header("Camera (optional)")]
    [Tooltip("A secondary CinemachineCamera in the scene. During the revive " +
             "its priority is raised so the brain blends to it, then dropped " +
             "back to 0 when the cutscene ends. Leave empty to skip.")]
    [SerializeField] private CinemachineCamera cutsceneCamera;
    [SerializeField] private int cutsceneCameraPriority = 30;
    [SerializeField] private float cutsceneZoomSize = 5f;

    // ── UI ───────────────────────────────────────────────────────────────────
    [Header("UI")]
    [Tooltip("HUD elements to hide during the cutscene.")]
    [SerializeField] private GameObject[] uiToHide;
    // ── Audio ───────────────────────────────────────────────────────────────────
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dialogueBlip;
    [SerializeField] private float dialogueBlipVolume = 1f;
    [SerializeField] private float minPitch = 0.6f;
    [SerializeField] private float maxPitch = 0.7f;
    [SerializeField] private int charsPerSound = 2;

    private bool cutsceneFinished;
    private bool cutsceneStarted;
    private Collider2D[] _triggerColliders;

    // ─────────────────────────────────────────────────────────────────────────
    //  Awake / Start — freeze necromancer AI and kill skeletons so they appear
    //                  as corpses on the ground from the moment the scene loads.
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Cache the trigger colliders for the OverlapPoint fallback in Update.
        // Needed because the player's dash flips Physics2D.IgnoreLayerCollision
        // for every layer in dashPhaseLayers, which suppresses OnTriggerEnter2D
        // entirely if the trigger sits on one of those layers — letting the
        // player dash straight through and skip the cutscene.
        _triggerColliders = GetComponents<Collider2D>();

        // Disable necromancer AI only — leave physics and colliders alone so
        // it stays grounded naturally. It just stands in idle.
        if (necromancer != null)
            necromancer.enabled = false;

        // Force necromancer into grounded idle pose
        if (necromancerAnimator != null)
        {
            necromancerAnimator.SetBool("isGrounded", true);
            necromancerAnimator.SetBool("isWalking", false);
            necromancerAnimator.SetFloat("VerticalNormal", 0f);
        }
    }

    private void Start()
    {
        // Kill skeletons one frame after Start so their own Start() has run
        // and initialised animator/collider references. Die() must be called
        // while the MonoBehaviour is still enabled so its coroutine can run.
        StartCoroutine(KillSkeletonsDeferred());
    }

    private IEnumerator KillSkeletonsDeferred()
    {
        // Wait one frame so every skeleton's Start() has executed
        yield return null;

        foreach (var skeleton in skeletons)
        {
            if (skeleton == null) continue;
            skeleton.Die();             // triggers death anim, leaves corpse visible
        }

        // Wait for death animations to finish, then disable AI
        yield return new WaitForSeconds(1f);

        foreach (var skeleton in skeletons)
        {
            if (skeleton == null) continue;
            skeleton.enabled = false;   // AI off so they can't act while dead
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Trigger — player walks into this object's Collider2D (set to IsTrigger)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cutsceneStarted) return;
        if (other.GetComponent<PlayerPlatformer>() == null) return;

        BeginCutscene();
    }

    // Fallback for when the player dashes through the trigger.
    // PlayerMovement.Dash() calls Physics2D.IgnoreLayerCollision for every layer
    // in dashPhaseLayers, which suppresses OnTriggerEnter2D for the duration of
    // the dash. OverlapPoint is a direct geometric check on this specific
    // collider and ignores the layer-collision matrix, so it still detects the
    // player while phasing.
    private void Update()
    {
        if (cutsceneStarted) return;
        if (playerRigidbody == null || _triggerColliders == null) return;

        Vector2 playerPos = playerRigidbody.position;
        for (int i = 0; i < _triggerColliders.Length; i++)
        {
            var col = _triggerColliders[i];
            if (col == null || !col.enabled || !col.isTrigger) continue;
            if (col.OverlapPoint(playerPos))
            {
                BeginCutscene();
                return;
            }
        }
    }

    private void BeginCutscene()
    {
        cutsceneStarted = true;
        WatcherCommentary.DialogueLocked = true;

        // Hide HUD
        if (uiToHide != null)
            foreach (var go in uiToHide)
                if (go != null) go.SetActive(false);

        DisablePlayerControl();

        StartCoroutine(RunCutscene());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cutscene sequence
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunCutscene()
    {
        yield return new WaitForSeconds(initialDelay);

        // ── 1. Watcher dialogue ──────────────────────────────────────────────
        yield return StartCoroutine(PlayDialogue());

        // ── 2. Necromancer faces the skeletons / camera ──────────────────────
        yield return new WaitForSeconds(pauseBeforeRevive);

        // ── 3. Pan camera to the necromancer ─────────────────────────────────
        if (cutsceneCamera != null)
        {
            cutsceneCamera.Follow = necromancer.transform;
            cutsceneCamera.LookAt = necromancer.transform;
            cutsceneCamera.Lens.OrthographicSize = cutsceneZoomSize;
            cutsceneCamera.Priority = cutsceneCameraPriority;

            // Wait for the CinemachineBrain blend to finish before starting the revive
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            float blendTime = brain != null ? brain.DefaultBlend.Time : 0.5f;
            yield return new WaitForSeconds(blendTime);
        }

        // ── 4. Necromancer casts revive ──────────────────────────────────────
        if (necromancerAnimator != null)
            necromancerAnimator.SetTrigger("Revive");

        // Wait until the right moment in the animation to pop the skeletons up
        yield return new WaitForSeconds(necromancerReviveAnimDuration * skeletonReviveMoment);

        foreach (var skeleton in skeletons)
        {
            if (skeleton == null) continue;
            skeleton.Revive();       // restores health, collider, sprite, plays Revive anim
            skeleton.enabled = true; // re-enable AI so they fight after the cutscene
        }

        // Wait for the rest of the revive animation
        float remaining = necromancerReviveAnimDuration * (1f - skeletonReviveMoment);
        yield return new WaitForSeconds(remaining);
        yield return new WaitForSeconds(pauseAfterRevive);

        // ── 5. Necromancer turns and runs out of frame ────────────────────────
        yield return new WaitForSeconds(pauseBeforeRun);

        // Always run away from the player
        float dir = Mathf.Sign(necromancer.transform.position.x - playerRigidbody.transform.position.x);
        if (dir == 0f) dir = 1f;
        FaceNecromancer(dir);

        if (necromancerAnimator != null)
            necromancerAnimator.SetBool("isWalking", true);

        // Run until the necromancer is off-screen
        float runTimer = 0f;
        bool cameraPanned = false;
        Camera cam = Camera.main;

        while (true)
        {
            Vector3 pos = necromancer.transform.position;
            pos.x += dir * necromancerRunSpeed * Time.deltaTime;
            necromancer.transform.position = pos;

            // Check if the necromancer is off-screen (with a small margin)
            if (cam != null)
            {
                Vector3 viewPos = cam.WorldToViewportPoint(pos);
                if (viewPos.x < -0.1f || viewPos.x > 1.1f)
                    break;
            }

            runTimer += Time.deltaTime;

            // After enough time on camera, pan back and give the player control
            if (!cameraPanned && runTimer >= necromancerRunOnCameraDuration)
            {
                cameraPanned = true;

                if (cutsceneCamera != null)
                    cutsceneCamera.Priority = 0;

                // Give the player control back, but keep necromancer AI off
                // so it doesn't override our movement loop
                EndCutscene(enableNecromancer: false);
            }

            yield return null;
        }

        // In case the exit was very close and the loop ended before the pan
        if (!cameraPanned)
        {
            if (cutsceneCamera != null)
                cutsceneCamera.Priority = 0;

            EndCutscene(enableNecromancer: false);
        }

        // Run loop finished — now hand the necromancer to its AI
        if (necromancerAnimator != null)
            necromancerAnimator.SetBool("isWalking", false);

        EnableNecromancerControl();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Player lock / unlock
    // ─────────────────────────────────────────────────────────────────────────

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

        // Disable all gameplay scripts on the player
        if (playerScriptsRoot != null)
        {
            foreach (MonoBehaviour mb in playerScriptsRoot.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb is PlayerInput) continue;
                mb.enabled = false;
            }
        }

        if (playerAnimator != null)
        {
            playerAnimator.SetFloat("Speed", 0f);
            playerAnimator.SetBool("isGrounded", true);
            playerAnimator.SetBool("isWallSliding", false);
            playerAnimator.Play(playerIdleStateName, 0, 0f);
            playerAnimator.Update(0f);
        }
    }

    private void EnablePlayerControl()
    {
        // Re-enable all gameplay scripts
        if (playerScriptsRoot != null)
        {
            foreach (MonoBehaviour mb in playerScriptsRoot.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                mb.enabled = true;
            }
        }

        if (playerRigidbody != null)
            playerRigidbody.bodyType = RigidbodyType2D.Dynamic;

        if (playerInput != null)
            playerInput.enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Necromancer lock / unlock
    // ─────────────────────────────────────────────────────────────────────────

    private void DisableNecromancerControl()
    {
        if (necromancer != null)
            necromancer.enabled = false;

        if (necromancerRigidbody != null)
        {
            necromancerRigidbody.linearVelocity = Vector2.zero;
            necromancerRigidbody.angularVelocity = 0f;
            necromancerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (necromancerCollidersToDisable != null)
            foreach (var c in necromancerCollidersToDisable)
                if (c != null) c.enabled = false;

        if (necromancerAnimator != null)
        {
            necromancerAnimator.SetBool("isGrounded", true);
            necromancerAnimator.SetBool("isWalking", false);
            necromancerAnimator.SetFloat("VerticalNormal", 0f);
            necromancerAnimator.Update(0f);
        }
    }

    private void EnableNecromancerControl()
    {
        if (necromancerRigidbody != null)
            necromancerRigidbody.bodyType = RigidbodyType2D.Dynamic;

        if (necromancerCollidersToDisable != null)
            foreach (var c in necromancerCollidersToDisable)
                if (c != null) c.enabled = true;

        if (necromancer != null)
            necromancer.enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Necromancer helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void FaceNecromancer(float dir)
    {
        Vector3 scale = necromancer.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * dir;
        necromancer.transform.localScale = scale;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  End
    // ─────────────────────────────────────────────────────────────────────────

    private void EndCutscene(bool enableNecromancer = true)
    {
        if (cutsceneFinished) return;
        cutsceneFinished = true;
        WatcherCommentary.DialogueLocked = false;

        EnablePlayerControl();

        if (enableNecromancer)
            EnableNecromancerControl();

        // Restore HUD
        if (uiToHide != null)
            foreach (var go in uiToHide)
                if (go != null) go.SetActive(true);

        // Ensure cutscene camera is deactivated
        if (cutsceneCamera != null)
            cutsceneCamera.Priority = 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Dialogue — same typewriter system as IntroCutscene
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator PlayDialogue()
    {
        if (dialogueText == null || dialogueLines == null || dialogueLines.Length == 0)
            yield break;

        // Show container + parent canvas
        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
                parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        // Wait a frame for TMP layout
        yield return null;

        float cps = Mathf.Max(1f, dialogueCharactersPerSecond);
        float timePerChar = 1f / cps;

        foreach (string line in dialogueLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                dialogueText.text = "";
                yield return new WaitForSeconds(dialoguePauseBetweenLines);
                continue;
            }

            // Pre-compute auto-size then lock it so the text doesn't jitter
            dialogueText.enableAutoSizing = true;
            dialogueText.text = line;
            dialogueText.ForceMeshUpdate();
            float fittedSize = dialogueText.fontSize;

            dialogueText.enableAutoSizing = false;
            dialogueText.fontSize = fittedSize;
            dialogueText.maxVisibleCharacters = 0;
            yield return null;

            // Typewriter reveal
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

            yield return new WaitForSeconds(dialoguePauseBetweenLines);
        }

        yield return new WaitForSeconds(dialoguePauseAfterLastLine);

        // Hide dialogue
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
}
