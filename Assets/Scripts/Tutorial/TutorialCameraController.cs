using UnityEngine;
using System.Collections; 

public class TutorialCameraController : MonoBehaviour
{
    // references 
    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private Transform enemyFocus;
    [SerializeField] private TutorialManager tutorialManager;

    // timing
    [SerializeField] private float panToEnemyDelay = 0.3f;
    [SerializeField] private float focusDuration = 1.2f;

    // camera smoothness
    [SerializeField] private float panSmoothTime = 0.45f;
    [SerializeField] private float normalSmoothTime = 0.15f; 
    [SerializeField] private float returnSmoothTime = 0.3f;

    private Transform player; 
    private bool isTriggered; 

    private PlayerAction cachedActions; 

    private void Awake()
    {
        cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        PlayerPlatformer p = other.GetComponent<PlayerPlatformer>();
        if (p == null) return;

        isTriggered = true;
        player = p.transform;

        // locking player movement 
        cachedActions = p.allowedActions;
        p.allowedActions = PlayerAction.None;
        p.FreezeMovement();

        Animator animator = p.GetComponent<Animator>(); 
        if (animator != null) {
            animator.SetBool("IsFrozen", true);
        }

        StartCoroutine(CameraPanSequence(p));
    }

    private IEnumerator CameraPanSequence(PlayerPlatformer p)
    {
        yield return new WaitForSeconds(panToEnemyDelay);

        cameraFollow.SetSmoothTime(panSmoothTime);
        cameraFollow.SetTemporaryTarget(enemyFocus);

        yield return new WaitForSeconds(focusDuration);

        cameraFollow.SetSmoothTime(returnSmoothTime); 
        cameraFollow.RestoreTarget(player);

        yield return new WaitForSeconds(0.4f);

        cameraFollow.SetSmoothTime(normalSmoothTime);

        // restore player movement after camera sequence
        p.allowedActions = cachedActions;

        Animator animator = p.GetComponent<Animator>(); 
        if (animator != null)
        {
            animator.SetBool("IsFrozen", false); 
        }
    }
}
