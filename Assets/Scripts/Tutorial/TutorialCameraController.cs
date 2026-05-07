using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class TutorialCameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private Transform enemyFocus;
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private bool requireRewindCompleted = false;
    [SerializeField] private TutorialManager.TutorialStep requiredStep;

    [SerializeField] private float panToEnemyDelay = 0.5f;
    [SerializeField] private float focusDuration = 2.0f;
    [SerializeField] private int focusPriority = 30;
    [SerializeField] private float focusZoomSize = 4f;
    [SerializeField] private float blendTime = 1.2f;
    [SerializeField] private CinemachineBlendDefinition.Styles blendStyle = CinemachineBlendDefinition.Styles.EaseInOut;

    // Enemy freezing
    [SerializeField] private bool freezeEnemiesDuringPan = false;
    [SerializeField] private float enemyFreezeRadius = 20f;

    private bool isTriggered;
    private PlayerAction cachedActions;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        if (tutorialManager != null)
        {
            if (requireRewindCompleted && !tutorialManager.rewindCompleted)
                return;

            if (tutorialManager.currentStep != requiredStep)
                return;
        }

        PlayerPlatformer p = other.GetComponent<PlayerPlatformer>();
        if (p == null) return;

        isTriggered = true;

        cachedActions = p.allowedActions;
        p.allowedActions = PlayerAction.None;
        p.FreezeMovement();

        // Slow/freeze enemies when triggered
        if (freezeEnemiesDuringPan && tutorialManager != null)
        {
            tutorialManager.SlowingEnemies(enemyFreezeRadius, 0f);
        }

        Animator animator = p.GetComponent<Animator>();
        if (animator != null)
            animator.SetBool("IsFrozen", true);

        StartCoroutine(CameraPanSequence(p));
    }

    private IEnumerator CameraPanSequence(PlayerPlatformer p)
    {
        // Override the brain's default blend so the pan in/out is slow and eased
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        CinemachineBlendDefinition originalBlend = default;
        bool blendOverridden = false;
        if (brain != null)
        {
            originalBlend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, blendTime);
            blendOverridden = true;
        }

        yield return new WaitForSeconds(panToEnemyDelay);

        // pan to enemy. set target here so each instance controls its own focus point
        if (enemyFocus != null)
            focusCamera.Target.TrackingTarget = enemyFocus;
        focusCamera.Lens.OrthographicSize = focusZoomSize;
        focusCamera.Priority = focusPriority;

        yield return new WaitForSeconds(focusDuration);

        // return: CinemachineBrain blend settings handle the transition
        focusCamera.Priority = 0;

        // Wait for the blend back to finish before restoring player control
        yield return new WaitForSeconds(blendTime + 0.1f);

        if (blendOverridden)
            brain.DefaultBlend = originalBlend;

        p.allowedActions = cachedActions;

        Animator animator = p.GetComponent<Animator>();
        if (animator != null)
            animator.SetBool("IsFrozen", false);

        // Restore enemies after the camera sequence finishes
        if (freezeEnemiesDuringPan && tutorialManager != null)
        {
            tutorialManager.RestoreEnemies();
        }
    }
}