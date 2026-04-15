using UnityEngine;
using Unity.Cinemachine;

public class CameraZoomZone : MonoBehaviour
{
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private float zoomSize = 8f;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private TutorialManager.TutorialStep requiredStep;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerPlatformer>() == null) return;
        if (tutorial != null && tutorial.currentStep != requiredStep) return;

        virtualCamera.Lens.OrthographicSize = zoomSize;
        virtualCamera.Priority = activePriority;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerPlatformer>() == null) return;

        virtualCamera.Priority = 0;
    }

    private void OnDisable()
    {
        if (virtualCamera != null)
            virtualCamera.Priority = 0;
    }
}
