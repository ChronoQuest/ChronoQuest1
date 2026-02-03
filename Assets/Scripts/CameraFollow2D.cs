using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private float zoomSmoothTime = 0.2f;

    private Vector3 velocity;
    private float zoomVelocity;
    private Camera cameraComponent;
    private float defaultZoom;
    private float targetZoom;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            defaultZoom = cameraComponent.orthographicSize;
            targetZoom = defaultZoom;
        }

        if (target == null)
            TryAssignPlayerTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            TryAssignPlayerTarget();
            return;
        }

        Vector3 desired = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        if (cameraComponent != null)
        {
            cameraComponent.orthographicSize = Mathf.SmoothDamp(
                cameraComponent.orthographicSize,
                targetZoom,
                ref zoomVelocity,
                zoomSmoothTime
            );
        }
    }

    public void SetZoom(float size)
    {
        targetZoom = size;
    }

    public void ResetZoom()
    {
        targetZoom = defaultZoom;
    }

    private void TryAssignPlayerTarget()
    {
        PlayerPlatformer player = FindObjectOfType<PlayerPlatformer>();
        if (player != null)
            target = player.transform;
    }
}
