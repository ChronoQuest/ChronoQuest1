using UnityEngine;

/// <summary>
/// Attach to a waypoint GameObject alongside a Collider2D (set as trigger).
/// Resize and reshape the collider in the Inspector to define the zone the
/// Necromancer must enter for the waypoint to count as reached — no fixed radius,
/// each waypoint can have a unique shape and size.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WaypointZone : MonoBehaviour
{
    private Collider2D zone;

    void Awake()
    {
        zone = GetComponent<Collider2D>();
        zone.isTrigger = true;
    }

    /// <summary>Returns true when <paramref name="point"/> is inside this zone's collider.</summary>
    public bool IsInside(Vector2 point) => zone != null && zone.OverlapPoint(point);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
        Gizmos.color = new Color(0f, 1f, 0f, 0.7f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
#endif
}
