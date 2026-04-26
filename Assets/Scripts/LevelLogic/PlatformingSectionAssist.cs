using UnityEngine;

/// <summary>
/// Place on a trigger collider that bounds a platforming section. If the player remains inside for
/// <see cref="assistAfterSeconds"/> (default 1:20) without reaching a goal that calls <see cref="MarkCleared"/>,
/// falls are cancelled, platforms are locked, and spikes are fully disabled.
/// Timer resets when the player exits the bounds without clearing.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlatformingSectionAssist : MonoBehaviour
{
    [Tooltip("Uses unscaled time. Default 80 seconds = 1:20.")]
    [SerializeField] private float assistAfterSeconds = 80f;

    [Tooltip("Prints to Console when assist runs (for testing).")]
    [SerializeField] private bool logWhenAssistApplied = false;

    [SerializeField] private FallingPlatform[] fallingPlatforms;
    [SerializeField] private MovingFallingPlatform[] movingFallingPlatforms;
    [SerializeField] private TrapDamage[] spikes;

    private Collider2D _bounds;
    private bool _playerInside;
    private bool _cleared;
    private bool _assistApplied;
    private float _sectionStartUnscaledTime;

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
        if (_cleared || _assistApplied || !_playerInside) return;
        if (Time.unscaledTime - _sectionStartUnscaledTime >= assistAfterSeconds)
            ApplyAssist();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = true;
        _sectionStartUnscaledTime = Time.unscaledTime;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;
        if (!_cleared && !_assistApplied)
            _sectionStartUnscaledTime = 0f;
    }

    /// <summary>True if the assist timer has not yet fired (player is still within the time limit).</summary>
    public bool IsWithinTimeLimit => !_assistApplied;

    /// <summary>Call from a goal trigger (see <see cref="PlatformSectionGoal"/>) when the section is completed.</summary>
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
            Debug.Log($"[PlatformingSectionAssist] Assist applied on {name} after {assistAfterSeconds}s in bounds.", this);
    }
}
