using UnityEngine;

public class CameraZoomZone : MonoBehaviour
{
    [SerializeField] private CameraFollow2D cameraFollow;
    [Min(0.1f)]
    [SerializeField] private float zoomSize = 8f;
    [SerializeField] private Vector2 cameraOffset; 

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
        if (cameraFollow == null)
            return;

        if (other.GetComponent<PlayerPlatformer>() == null)
            return;

        cameraFollow.SetZoom(zoomSize);
        cameraFollow.SetOffset(cameraOffset); 
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (cameraFollow == null)
            return;

        if (other.GetComponent<PlayerPlatformer>() == null)
            return;

        cameraFollow.ResetZoom();
        cameraFollow.ResetOffset(); 
    }
}
