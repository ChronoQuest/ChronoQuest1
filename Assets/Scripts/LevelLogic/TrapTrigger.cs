using UnityEngine;

public class TrapTrigger : MonoBehaviour
{
    [SerializeField] private TrapFloor parentTrap;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            parentTrap.TriggerBreak();
        }
    }
}