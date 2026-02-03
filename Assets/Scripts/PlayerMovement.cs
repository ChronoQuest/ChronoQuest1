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

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    public bool isGrounded;
    
    [Header("Jump Buffer")]
    [SerializeField] private float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;

    [Header("Input Keys")]
    [SerializeField] private Key moveLeftKey = Key.A;
    [SerializeField] private Key moveRightKey = Key.D;
    [SerializeField] private Key jumpKey = Key.Space;

    private bool jumpPressedThisFrame;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            return;

        horizontalInput = 0f;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float left = keyboard[moveLeftKey].isPressed ? -1f : 0f;
            float right = keyboard[moveRightKey].isPressed ? 1f : 0f;
            horizontalInput = left + right;
            
            if (keyboard[jumpKey].wasPressedThisFrame)
                jumpPressedThisFrame = true;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float stickX = gamepad.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.1f)
                horizontalInput = stickX;
            
            if (gamepad.dpad.left.isPressed)
                horizontalInput = -1f;
            if (gamepad.dpad.right.isPressed)
                horizontalInput = 1f;
            
            if (gamepad.buttonSouth.wasPressedThisFrame)
                jumpPressedThisFrame = true;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        
        if (jumpBufferCounter > 0)
            jumpBufferCounter -= Time.deltaTime;
        
        if (jumpPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
            jumpPressedThisFrame = false;
        }
        
        if (jumpBufferCounter > 0 && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0;
        }
    }

    private void FixedUpdate()
    {
        if (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
            return;

        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }
    
    public void Jump()
    {
        jumpPressedThisFrame = true;
    }
}
