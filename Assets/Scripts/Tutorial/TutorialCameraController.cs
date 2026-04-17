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

    [SerializeField] private float panToEnemyDelay = 0.3f;
    [SerializeField] private float focusDuration = 1.2f;
    [SerializeField] private int focusPriority = 30;
    [SerializeField] private float focusZoomSize = 4f;

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

        Animator animator = p.GetComponent<Animator>();
        if (animator != null)
            animator.SetBool("IsFrozen", true);

        StartCoroutine(CameraPanSequence(p));
    }

    private IEnumerator CameraPanSequence(PlayerPlatformer p)
    {
        yield return new WaitForSeconds(panToEnemyDelay);

        // Pan to enemy — set target here so each instance controls its own focus point
        if (enemyFocus != null)
            focusCamera.Target.TrackingTarget = enemyFocus;
        focusCamera.Lens.OrthographicSize = focusZoomSize;
        focusCamera.Priority = focusPriority;

        yield return new WaitForSeconds(focusDuration);

        // Return — CinemachineBrain blend settings handle the transition
        focusCamera.Priority = 0;

        // Wait for the blend back to finish before restoring player control
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        float blendTime = brain != null ? brain.DefaultBlend.Time : 0.3f;
        yield return new WaitForSeconds(blendTime + 0.1f);

        p.allowedActions = cachedActions;

        Animator animator = p.GetComponent<Animator>();
        if (animator != null)
            animator.SetBool("IsFrozen", false);
    }
}