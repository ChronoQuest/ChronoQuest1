using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// short cutscene at the start of GameScene_3:
//   1. watcher dialogue
//   2. necromancer revives 2 skeletons
//   3. necromancer turns and runs offscreen
//   4. player regains control
// skeletons should start active with health 0 so they appear as corpses,
// the cutscene calls Revive() on each.
public class Level3IntroCutscene : MonoBehaviour
{
    // ── Player ──────────────────────────────────────────────────────────────
    [Header("Player")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string playerIdleStateName = "Player_Idle";
    [Tooltip("Every script on the player root gets disabled during the cutscene (except PlayerInput).")]
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
    [Tooltip("2 skeletons near the necromancer. Set their health to 0 so they start dead.")]
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
    [Tooltip("Secondary CinemachineCamera. Priority is raised during the revive, dropped back at the end.")]
    [SerializeField] private CinemachineCamera cutsceneCamera;
    [SerializeField] private int cutsceneCameraPriority = 30;
    [SerializeField] private float cutsceneZoomSize = 5f;

    // ── UI ───────────────────────────────────────────────────────────────────
    [Header("UI")]
    [Tooltip("HUD elements to hide during the cutscene.")]
    [SerializeField] private GameObject[] uiToHide;
    [SerializeField] private CanvasGroup hudGroup;
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

    // freeze necromancer AI and kill skeletons so they appear as corpses on
    // the ground from scene load

    private void Awake()
    {
        // cache trigger colliders for the OverlapPoint fallback in Update.
        // dashing flips Physics2D.IgnoreLayerCollision for the dash layers and
        // suppresses OnTriggerEnter2D, so the player can dash straight through
        // and skip the cutscene without it
        _triggerColliders = GetComponents<Collider2D>();

        // disable AI only, leave physics and colliders alone so the necromancer
        // stays grounded
        if (necromancer != null)
            necromancer.enabled = false;

        // grounded idle pose
        if (necromancerAnimator != null)
        {
            necromancerAnimator.SetBool("isGrounded", true);
            necromancerAnimator.SetBool("isWalking", false);
            necromancerAnimator.SetFloat("VerticalNormal", 0f);
        }
    }

    private void Start()
    {
        // kill skeletons one frame after Start so their own Start() has run
        StartCoroutine(KillSkeletonsDeferred());
    }

    private IEnumerator KillSkeletonsDeferred()
    {
        yield return null;

        foreach (var skeleton in skeletons)
        {
            if (skeleton == null) continue;
            skeleton.Die();             // death anim, corpse stays visible
        }

        // wait for death anims, then disable AI
        yield return new WaitForSeconds(1f);

        foreach (var skeleton in skeletons)
        {
            if (skeleton == null) continue;
            skeleton.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cutsceneStarted) return;
        if (other.GetComponent<PlayerPlatformer>() == null) return;

        BeginCutscene();
    }

    // fallback for when the player dashes through the trigger.
    // dash sets Physics2D.IgnoreLayerCollision so OnTriggerEnter2D doesnt fire.
    // OverlapPoint is a direct geometric check that ignores the layer matrix
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

        // hide HUD
        if (hudGroup != null)
        {
            hudGroup.alpha = 0;
        }

        DisablePlayerControl();

        StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        yield return new WaitForSeconds(initialDelay);

        // 1. watcher dialogue
        yield return StartCoroutine(PlayDialogue());

        // 2. pause before revive
        yield return new WaitForSeconds(pauseBeforeRevive);

        // 3. pan camera to necromancer
        if (cutsceneCamera != null)
        {
            cutsceneCamera.Follow = necromancer.transform;
            cutsceneCamera.LookAt = necromancer.transform;
            cutsceneCamera.Lens.OrthographicSize = cutsceneZoomSize;
            cutsceneCamera.Priority = cutsceneCameraPriority;

            // wait for the brain blend before starting revive
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            float blendTime = brain != null ? brain.DefaultBlend.Time : 0.5f;
            yield return new WaitForSeconds(blendTime);
        }

        // 4. revive
        if (necromancerAnimator != null)
            necromancerAnimator.SetTrigger("Revive");

        yield return new WaitForSeconds(necromancerReviveAnimDuration * skeletonReviveMoment);

        foreach (var skeleton in skeletons)
        {
            if (skeleton == null) continue;
            skeleton.Revive();
            skeleton.enabled = true;
        }

        float remaining = necromancerReviveAnimDuration * (1f - skeletonReviveMoment);
        yield return new WaitForSeconds(remaining);
        yield return new WaitForSeconds(pauseAfterRevive);

        // 5. necromancer runs offscreen
        yield return new WaitForSeconds(pauseBeforeRun);

        // always run away from the player
        float dir = Mathf.Sign(necromancer.transform.position.x - playerRigidbody.transform.position.x);
        if (dir == 0f) dir = 1f;
        FaceNecromancer(dir);

        if (necromancerAnimator != null)
            necromancerAnimator.SetBool("isWalking", true);

        float runTimer = 0f;
        bool cameraPanned = false;
        Camera cam = Camera.main;

        while (true)
        {
            Vector3 pos = necromancer.transform.position;
            pos.x += dir * necromancerRunSpeed * Time.deltaTime;
            necromancer.transform.position = pos;

            // offscreen check with a small margin
            if (cam != null)
            {
                Vector3 viewPos = cam.WorldToViewportPoint(pos);
                if (viewPos.x < -0.1f || viewPos.x > 1.1f)
                    break;
            }

            runTimer += Time.deltaTime;

            // pan back + give player control once enough on-camera time has passed
            if (!cameraPanned && runTimer >= necromancerRunOnCameraDuration)
            {
                cameraPanned = true;

                if (cutsceneCamera != null)
                    cutsceneCamera.Priority = 0;

                // keep necromancer AI off so it doesnt override the run loop
                EndCutscene(enableNecromancer: false);
            }

            yield return null;
        }

        // in case the exit was close and the loop ended before the pan
        if (!cameraPanned)
        {
            if (cutsceneCamera != null)
                cutsceneCamera.Priority = 0;

            EndCutscene(enableNecromancer: false);
        }

        // run loop done, hand the necromancer back to its AI
        if (necromancerAnimator != null)
            necromancerAnimator.SetBool("isWalking", false);

        EnableNecromancerControl();
    }

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

        // disable all gameplay scripts
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

    private void FaceNecromancer(float dir)
    {
        Vector3 scale = necromancer.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * dir;
        necromancer.transform.localScale = scale;
    }

    private void EndCutscene(bool enableNecromancer = true)
    {
        if (cutsceneFinished) return;
        cutsceneFinished = true;
        WatcherCommentary.DialogueLocked = false;

        EnablePlayerControl();

        if (enableNecromancer)
            EnableNecromancerControl();

        // restore HUD
        {
            hudGroup.alpha = 1;
        }

        if (cutsceneCamera != null)
            cutsceneCamera.Priority = 0;
    }

    // typewriter, same flow as IntroCutscene
    private IEnumerator PlayDialogue()
    {
        if (dialogueText == null || dialogueLines == null || dialogueLines.Length == 0)
            yield break;

        // show container + parent canvas
        if (dialogueContainer != null)
        {
            Canvas parentCanvas = dialogueContainer.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null)
                parentCanvas.gameObject.SetActive(true);
            dialogueContainer.SetActive(true);
        }

        // wait a frame for TMP layout
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

                if (audioSource != null && dialogueBlip != null && i % charsPerSound == 0)
                {
                    audioSource.pitch = Random.Range(minPitch, maxPitch);
                    audioSource.PlayOneShot(dialogueBlip, dialogueBlipVolume);
                }

                yield return PauseAwareWait.Seconds(timePerChar);
            }

            yield return new WaitForSeconds(dialoguePauseBetweenLines);
        }

        yield return new WaitForSeconds(dialoguePauseAfterLastLine);

        // hide dialogue
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
