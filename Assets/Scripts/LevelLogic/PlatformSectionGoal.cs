using UnityEngine;

/// <summary>
/// Trigger at the end of a <see cref="PlatformingSectionAssist"/> route. Marks the section cleared so timed assists do not fire.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlatformSectionGoal : MonoBehaviour
{
    [SerializeField] private PlatformingSectionAssist section;

    private void Awake()
    {
        var c = GetComponent<Collider2D>();
        if (c != null)
            c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Check before marking cleared — was the player still within the time limit?
        bool withinTime = section != null && section.IsWithinTimeLimit;

        section?.MarkCleared();

        // Good players who cleared the platforming section in time get a skill comment
        if (withinTime)
        {
            var watcher = FindFirstObjectByType<WatcherCommentary>();
            watcher?.TryFireSkillComment();
        }
    }
}
