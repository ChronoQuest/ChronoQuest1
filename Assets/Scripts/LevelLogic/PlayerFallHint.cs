using UnityEngine;

public class PlayerFallHint : MonoBehaviour
{
    [Header("Fall Detection")]
    [Tooltip("How fast the player must be falling to trigger the hint")]
    public float fallVelocityThreshold = -12f; 
    
    [Tooltip("How long they must be falling at that speed before the hint starts")]
    public float fallTimeThreshold = 0.4f;

    private Rigidbody2D rb;
    private PlayerPlatformer platformer;
    
    private float fallingTimer = 0f;
    private bool isHinting = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        platformer = GetComponent<PlayerPlatformer>();
    }

    private void Update()
    {
        // Check if we are plummeting downwards
        if (rb.linearVelocity.y < fallVelocityThreshold && !platformer.isGrounded)
        {
            fallingTimer += Time.deltaTime;

            // If we've been falling long enough, trigger the panic heartbeat!
            if (fallingTimer >= fallTimeThreshold && !isHinting)
            {
                isHinting = true;
                RewindHaptics.Instance?.StartHintHeartbeat(5f);
            }
        }
        // else
        // {
        //     // The moment we land, jump, or stop falling fast, reset the timer
        //     fallingTimer = 0f;

        //     if (isHinting)
        //     {
        //         isHinting = false;
        //         RewindHaptics.Instance?.StopHintHeartbeat();
        //     }
        // }
    }
}