using UnityEngine;

public class SpotlightFollow : MonoBehaviour
{
    public Transform player;

    void Update()
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(player.position);
        transform.position = screenPos; 
    }
}
