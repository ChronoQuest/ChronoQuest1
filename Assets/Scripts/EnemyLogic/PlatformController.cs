using UnityEngine;
using TimeRewind;

public class PlatformController : MonoBehaviour, IRewindable
{
    private Rigidbody2D rb;
    private bool isRewinding;
    private RigidbodyType2D originalBodyType;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);
    }

    public void OnStartRewind()
    {
        isRewinding = true;
        originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        rb.bodyType = originalBodyType;
    }

    public RewindState CaptureState()
    {
        return RewindState.CreateWithPhysics(
            transform.position,
            transform.rotation,
            rb.linearVelocity,
            rb.angularVelocity,
            Time.time
        );
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
    }
}
