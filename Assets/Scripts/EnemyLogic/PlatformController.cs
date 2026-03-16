using UnityEngine;
using TimeRewind;

public class PlatformController : MonoBehaviour, IRewindable
{
    private Rigidbody2D rb;
    private bool isRewinding;
    public float moveHeight = 5f;
    public float duration = 2f;
    public float topWait = 5f;
    private Vector3 startPos;
    private Vector3 targetPos;
    private bool movingUp = true;
    private float progress = 0f;
    private float topWaitProgress = 0f;
    private bool waitingAtTop = false;
    public bool cycleComplete;

    void Start()
    {
        cycleComplete = false;
        rb = GetComponent<Rigidbody2D>();
        if (TimeRewindManager.Instance != null)
            TimeRewindManager.Instance.Register(this);

        startPos = transform.position;
        targetPos = startPos + Vector3.up * moveHeight;
    }

    void Update()
    {
        if (isRewinding) return;

        if (waitingAtTop)
        {
            topWaitProgress += Time.deltaTime;
            if (topWaitProgress >= topWait)
            {
                waitingAtTop = false;
                Lower();
            }
        }
        else
        {
            progress += Time.deltaTime / duration;
            progress = Mathf.Clamp01(progress);
            transform.position = Vector3.Lerp(startPos, targetPos, progress);

            if (progress >= 1f && movingUp)
            {
                waitingAtTop = true;
                topWaitProgress = 0f;
            } else if (progress >= 1f && !movingUp)
            {
                cycleComplete = true;
            }
        }
    }

    public void Raise()
    {
        cycleComplete = false;
        startPos = transform.position;
        targetPos = startPos + Vector3.up * moveHeight;
        progress = 0f;
        movingUp = true;
        waitingAtTop = false;
        topWaitProgress = 0f;
    }

    public void Lower()
    {
        startPos = transform.position;
        targetPos = startPos - Vector3.up * moveHeight;
        progress = 0f;
        movingUp = false;
    }
    public void OnStartRewind()
    {
        isRewinding = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
    }

    public RewindState CaptureState()
    {
        var state = RewindState.CreateWithPhysics(transform.position, transform.rotation, rb.linearVelocity, rb.angularVelocity, Time.time);
        state.SetCustomData("MovingUp", movingUp);
        state.SetCustomData("Progress", progress);
        state.SetCustomData("WaitingAtTop", waitingAtTop);
        state.SetCustomData("TopWaitProgress", topWaitProgress);
        state.SetCustomData("StartPos", startPos);
        state.SetCustomData("TargetPos", targetPos);
        state.SetCustomData("CycleComplete", cycleComplete);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;

        movingUp = state.GetCustomData<bool>("MovingUp", true);
        progress = state.GetCustomData<float>("Progress", 0f);
        waitingAtTop = state.GetCustomData<bool>("WaitingAtTop", false);
        topWaitProgress = state.GetCustomData<float>("TopWaitProgress", 0f);
        startPos = state.GetCustomData<Vector3>("StartPos", transform.position);
        targetPos = state.GetCustomData<Vector3>("TargetPos", transform.position);
        
    }
}
