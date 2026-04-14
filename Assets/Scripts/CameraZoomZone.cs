using UnityEngine;

public class CameraZoomZone : MonoBehaviour
{
    [SerializeField] private CameraFollow2D cameraFollow;
    [Min(0.1f)]
    [SerializeField] private float zoomSize = 8f;
    [SerializeField] private Vector2 cameraOffset; 
    [SerializeField] private float zoomSmoothOverride = -1f; 
    [SerializeField] private float movementSmoothOverride = -1f; 
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private TutorialManager.TutorialStep requiredStep; 
    private bool isActive = false;

    private void Awake()
    {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
    }

    private void OnValidate()
    {
        if (zoomSize <= 0f)
            zoomSize = 0.1f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cameraFollow == null || other.GetComponent<PlayerPlatformer>() == null)
            return;

        if (tutorial != null && tutorial.currentStep != requiredStep)
            return;

        if (!isActive)
        {
            isActive = true;

            if (zoomSmoothOverride > 0f)
                cameraFollow.SetZoomSmoothTime(zoomSmoothOverride);

            if (movementSmoothOverride > 0f)
                cameraFollow.SetSmoothTime(movementSmoothOverride); 

            cameraFollow.SetZoom(zoomSize);
            cameraFollow.SetOffset(cameraOffset); 
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (cameraFollow == null || other.GetComponent<PlayerPlatformer>() == null)
            return;

        var rewindController = other.GetComponent<TimeRewind.PlayerRewindController>();
        if (rewindController != null && rewindController.IsRewinding)
        {
            return; 
        }

        isActive = false;
        
        cameraFollow.ResetZoom();
        cameraFollow.ResetOffset(); 
        cameraFollow.ResetZoomSmoothTime();
        cameraFollow.ResetSmoothTime();
    }
}