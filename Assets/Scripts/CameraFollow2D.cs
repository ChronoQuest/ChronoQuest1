using UnityEngine;
using System.Collections.Generic; 

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
    private Vector3 shakeOffset;
    private Vector2 panOffset; 
    private Vector3 defaultOffset;
    private float defaultZoomSmoothTime; 
    private float defaultSmoothTime; 
    private Vector3 targetOffset;
    private Vector3 offsetVelocity; 
    private Vector2 previousOffset; 
    private Stack<float> zoomStack = new Stack<float>(); 

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        defaultOffset = offset; 
        defaultZoomSmoothTime = zoomSmoothTime; 
        defaultSmoothTime = smoothTime;
        targetOffset = offset;  

        if (cameraComponent != null)
        {
            defaultZoom = cameraComponent.orthographicSize;
            targetZoom = defaultZoom;
        }

        if (target == null)
            TryAssignPlayerTarget();
    }

    public void ApplyShakeOffset(Vector3 offset)
    {
        shakeOffset = offset;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            TryAssignPlayerTarget();
            return;
        }

        offset = Vector3.SmoothDamp(offset, targetOffset, ref offsetVelocity, smoothTime);
        Vector3 desired = target.position + offset;
        //transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime) + shakeOffset;

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
        zoomStack.Push(targetZoom); 
        targetZoom = size;
    }

    public void ResetZoom()
    {
        // targetZoom = defaultZoom;

        if (zoomStack.Count > 0)
            targetZoom = zoomStack.Pop();
        else
            targetZoom = defaultZoom;
    }

    private void TryAssignPlayerTarget()
    {
        PlayerPlatformer player = FindObjectOfType<PlayerPlatformer>();
        if (player != null)
            target = player.transform;
    }
    
    // methods added for tutorial camera movement
    public void SetTemporaryTarget(Transform newTarget)
    {
        target = newTarget; 
        velocity = Vector3.zero;
    }

    public void RestoreTarget(Transform originalTarget)
    {
        target = originalTarget; 
        velocity = Vector3.zero;
    }

    public void SetSmoothTime(float value)
    {
        smoothTime = Mathf.Max(0.01f, value);
    }

    public void SetOffset(Vector2 newOffset)
    {
        targetOffset.x = newOffset.x;
        targetOffset.y = newOffset.y;
    }

    public void ResetOffset()
    {
        targetOffset = defaultOffset; 
    }

    public void SetZoomSmoothTime(float value)
    {
        zoomSmoothTime = Mathf.Max(0.01f, value);
    }

    public void ResetZoomSmoothTime()
    {
        zoomSmoothTime = defaultZoomSmoothTime;
    }

    public void ResetSmoothTime()
    {
        smoothTime = defaultSmoothTime; 
    }
}  
