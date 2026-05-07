using UnityEngine;

// trigger collider that bounds a platforming section. once the player has spent
// assistAfterSeconds (cumulative) inside, falls cancel, platforms lock, spikes
// disable. the timer keeps adding up across re-entries until the goal trigger
// calls MarkCleared. once cleared the assist never fires
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlatformingSectionAssist : MonoBehaviour
{
    [Tooltip("Cumulative unscaled time the player must spend inside the bounds before the assist fires.")]
    [SerializeField] private float assistAfterSeconds = 80f;

    [Tooltip("Prints to Console when assist runs (for testing).")]
    [SerializeField] private bool logWhenAssistApplied = false;

    [SerializeField] private FallingPlatform[] fallingPlatforms;
    [SerializeField] private MovingFallingPlatform[] movingFallingPlatforms;
    [SerializeField] private TrapDamage[] spikes;

    private Collider2D _bounds;
    // count of overlapping Player colliders. the player has two colliders (Box +
    // Capsule), both tagged Player, that cross the boundary separately, so a bool
    // would double-count exits
    private int _playerColliderCount;
    private bool _cleared;
    private bool _assistApplied;
    // sum of completed inside-intervals. the still-open interval is added in Update
    private float _accumulatedTimeInside;
    // unscaled time when the current inside-interval started
    private float _intervalStartTime;

    private bool PlayerInside => _playerColliderCount > 0;

    private void Awake()
    {
        _bounds = GetComponent<Collider2D>();
        if (_bounds != null)
            _bounds.isTrigger = true;

        if (fallingPlatforms == null) fallingPlatforms = System.Array.Empty<FallingPlatform>();
        if (movingFallingPlatforms == null) movingFallingPlatforms = System.Array.Empty<MovingFallingPlatform>();
        if (spikes == null) spikes = System.Array.Empty<TrapDamage>();
    }

    private void Update()
    {
        if (_cleared || _assistApplied) return;

        float total = _accumulatedTimeInside;
        if (PlayerInside) total += Time.unscaledTime - _intervalStartTime;

        if (total >= assistAfterSeconds)
            ApplyAssist();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerColliderCount++;
        // open a new interval only on the first collider, the second piggybacks
        if (_playerColliderCount == 1)
            _intervalStartTime = Time.unscaledTime;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_playerColliderCount > 0) _playerColliderCount--;
        // close the interval only when ALL player colliders have exited
        if (_playerColliderCount == 0)
            _accumulatedTimeInside += Time.unscaledTime - _intervalStartTime;
    }

    // true if the assist timer hasnt fired yet
    public bool IsWithinTimeLimit => !_assistApplied;

    // called by the goal trigger when the player clears the section. disables the
    // assist permanently
    public void MarkCleared()
    {
        _cleared = true;
    }

    private void ApplyAssist()
    {
        if (_assistApplied) return;
        _assistApplied = true;

        foreach (var p in fallingPlatforms)
        {
            if (p == null) continue;
            p.CancelFallForAssist();
            p.SetSectionAssistLocked(true);
        }

        foreach (var p in movingFallingPlatforms)
        {
            if (p == null) continue;
            // not locked by the assist. runway-style moving/falling platforms still
            // fall normally even after the timer

        }

        foreach (var s in spikes)
        {
            if (s == null) continue;
            s.SetHazardDisabled(true);
        }

        if (logWhenAssistApplied)
            Debug.Log($"[PlatformingSectionAssist] Assist applied on {name} after {assistAfterSeconds}s cumulative in bounds.", this);
    }
}
