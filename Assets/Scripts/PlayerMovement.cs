using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
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
    public bool isGrounded;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        jumpPressed = false;

        Debug.Log(Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer));

    }

    private void FixedUpdate()
    {
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
