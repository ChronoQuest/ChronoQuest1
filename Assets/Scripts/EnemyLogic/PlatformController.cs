using UnityEngine;
using TimeRewind;

public class PlatformController : MonoBehaviour, IRewindable
{
    private Rigidbody2D rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Register(this);
    }

    // Update is called once per frame
    void Update()
    {
    
    }

    public void OnStartRewind()
    {    

    }
    public void OnStopRewind()
    {

    }
    public RewindState CaptureState()
    {
        return RewindState.Create(
            transform.position,
            transform.rotation,
            Time.time
        );
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
    }
}
