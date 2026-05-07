using UnityEngine;

// attach to a waypoint with a Collider2D (trigger). resize the collider in the
// inspector to define the zone the necromancer must enter. no fixed radius, each
// waypoint can be its own shape/size
[RequireComponent(typeof(Collider2D))]
public class WaypointZone : MonoBehaviour
{
    private Collider2D zone;

    void Awake()
    {
        zone = GetComponent<Collider2D>();
        zone.isTrigger = true;
    }

    // true if point is inside this zone's collider
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
