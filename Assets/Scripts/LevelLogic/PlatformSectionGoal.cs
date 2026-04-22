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
        section?.MarkCleared();
    }
}
