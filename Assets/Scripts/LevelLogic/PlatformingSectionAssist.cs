using UnityEngine;

/// <summary>
/// Place on a trigger collider that bounds a platforming section. Once the player has
/// accumulated <see cref="assistAfterSeconds"/> of total time inside (cumulative — brief exits
/// from knockback, falling off a platform, dying and respawning at a checkpoint outside the
/// bounds, etc. don't reset the counter), falls are cancelled, platforms are locked, and
/// spikes are fully disabled.
///
/// The timer keeps accruing across re-entries until <see cref="PlatformSectionGoal"/> calls
/// <see cref="MarkCleared"/> on this section. Once cleared, the assist will never fire.
/// </summary>
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
    // Counts overlapping Player-tagged colliders. The player prefab has two colliders
    // (Box + Capsule) on the same GameObject, both tagged Player, and they cross the
    // trigger boundary independently — so a simple bool flag would double-count exits.
    private int _playerColliderCount;
    private bool _cleared;
    private bool _assistApplied;
    // Sum of completed inside-intervals (closed when the player fully exits). Time spent
    // in the current still-open interval is added on the fly in Update.
    private float _accumulatedTimeInside;
    // Unscaled time at which the most recent uninterrupted inside-interval started.
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
        // Open a new inside-interval only on the first collider; the second one piggybacks.
        if (_playerColliderCount == 1)
            _intervalStartTime = Time.unscaledTime;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_playerColliderCount > 0) _playerColliderCount--;
        // Close the interval only when ALL Player colliders have exited.
        if (_playerColliderCount == 0)
            _accumulatedTimeInside += Time.unscaledTime - _intervalStartTime;
    }

    /// <summary>True if the assist timer has not yet fired (player is still within the time limit).</summary>
    public bool IsWithinTimeLimit => !_assistApplied;

    /// <summary>Called by <see cref="PlatformSectionGoal"/> when the player reaches the goal trigger.
    /// Permanently disables the assist for this section — the timer stops accumulating and
    /// <see cref="ApplyAssist"/> can never fire afterwards.</summary>
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
            // Intentionally NOT locked by section assist.
            // "Runway" style moving/falling platforms should still fall normally even after the assist timer.
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
