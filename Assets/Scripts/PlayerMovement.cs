using UnityEngine;
using UnityEngine.InputSystem;
using TimeRewind;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerRewindController))]
public class PlayerPlatformer : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    private Rigidbody2D rb;
    public float horizontalInput;
    private bool jumpPressed;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.35f;
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded;
    
    [Header("Jump Buffer")]
    [SerializeField] private float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Skip input processing during rewind
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            return;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        
        if (jumpBufferCounter > 0)
            jumpBufferCounter -= Time.deltaTime;
        
        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
            jumpPressed = false;
        }
        
        if (jumpBufferCounter > 0 && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0;
        }
    }

    private void FixedUpdate()
    {
        // Skip physics updates during rewind
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            return;

        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        horizontalInput = context.ReadValue<Vector2>().x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) {
            Debug.Log("Jump Pressed");
            jumpPressed = true;
        }
    }
}
