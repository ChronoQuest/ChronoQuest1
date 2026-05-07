using UnityEngine;

// trigger at the end of a platforming section. marks it cleared so timed assists dont fire
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

        // check before marking cleared - was the player still in time?
        bool withinTime = section != null && section.IsWithinTimeLimit;

        section?.MarkCleared();

        // good players who cleared the section in time get a skill comment
        if (withinTime)
        {
            var watcher = FindFirstObjectByType<WatcherCommentary>();
            watcher?.TryFireSkillComment();
        }
    }
}
