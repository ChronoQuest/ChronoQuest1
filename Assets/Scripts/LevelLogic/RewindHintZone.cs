using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RewindHintZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // When the player falls into the trap, start the heartbeat
        if (collision.CompareTag("Player"))
        {
            RewindHaptics.Instance?.StartHintHeartbeat(10f);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // If they somehow escape (or rewind out of the zone), stop the heartbeat
        if (collision.CompareTag("Player"))
        {
            RewindHaptics.Instance?.StopHintHeartbeat();
        }
    }
}