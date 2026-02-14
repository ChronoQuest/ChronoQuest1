using UnityEngine;
using UnityEngine.InputSystem;
using TimeRewind;

[RequireComponent(typeof(PlayerPlatformer))]
public class PlayerSpellSystem : MonoBehaviour, IRewindable
{
    [Header("Spell Settings")]
    public GameObject spellPrefab;
    public Transform firePoint;
    public float cooldown = 0.4f;

    [Header("Recoil & Movement")]
    public float recoilForce = 8f;
    public float recoilDuration = 0.2f;

    private Vector2 dir;
    private PlayerPlatformer player;
    private Animator anim;
    private SpriteRenderer sprite;
    private Rigidbody2D rb;
    private float nextFireTime;
    private float originalGravity;
    // State locks
    public bool isCasting { get; private set; }
    private float recoilTimer;
    private float castFailsafeTimer;
    private const float MAX_CAST_TIME = 1.0f;

    void Awake()
    {
        player = GetComponent<PlayerPlatformer>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        // --- FIX 1: Grab the Rigidbody! ---
        rb = GetComponent<Rigidbody2D>();
        originalGravity = rb.gravityScale;
        // ----------------------------------
    }
    // --- NEW: Register the Cooldown logic to the Rewind Manager ---
    void OnEnable()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (TimeRewindManager.Instance != null) TimeRewindManager.Instance.Unregister(this);
    }

    void Update()
    {
        if (TimeRewind.TimeRewindManager.Instance?.IsRewinding == true)
            return;
        if (recoilTimer > 0)
        {
            recoilTimer -= Time.deltaTime;
        }
        if (isCasting)
        {
            castFailsafeTimer -= Time.deltaTime;
            if (castFailsafeTimer <= 0f)
            {
                isCasting = false;
                rb.gravityScale = originalGravity;
                Debug.LogWarning("Spell animation interrupted! Failsafe restored gravity.");
            }
        }
        if (player.isDashing) return;
        if (WasCastPressed() && Time.time >= nextFireTime)
        {
            CastSpell();
            nextFireTime = Time.time + cooldown;
        }
    }

    void CastSpell()
    {
        if (!spellPrefab || !firePoint) return;

        dir = GetCastDirection();
        if (anim) anim.SetTrigger(GetCastAnimation(dir));
        isCasting = true;
        castFailsafeTimer = MAX_CAST_TIME;

        // Optional: small recoil
        isCasting = true;
        originalGravity = rb.gravityScale;
        rb.gravityScale = 0f; // Disable gravity so they float mid-air
        rb.linearVelocity = Vector2.zero; // Stop all momentum instantly
    }
    public void SpawnSpell()
    {
        if (TimeRewindManager.Instance?.IsRewinding == true) return;
        isCasting = false;
        rb.gravityScale = originalGravity;
        
        // Apply exact velocity instead of AddForce so it's snappy and consistent
        rb.linearVelocity = -dir * recoilForce;
        
        // Lock player movement for a split second so the recoil can actually push them
        recoilTimer = recoilDuration;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float currentX = Mathf.Abs(firePoint.localPosition.x);
        float newX = sprite.flipX ? -currentX : currentX;
        firePoint.localPosition = new Vector3(newX, firePoint.localPosition.y, firePoint.localPosition.z);
        firePoint.rotation = Quaternion.Euler(0, 0, angle);
        GameObject spell = Instantiate(spellPrefab, firePoint.position, firePoint.rotation);
        spell.GetComponent<SpellProjectile>().Init(dir, sprite.flipX);
    }
    // Helper function for the Platformer script to check if it should ignore inputs
    public bool IsMovementLocked()
    {
        return isCasting || recoilTimer > 0f;
    }

    Vector2 GetCastDirection()
    {
        float y = GetVerticalInput();

        // Up cast always allowed
        if (y > 0.5f) return Vector2.up;

        // Down cast ONLY in air
        if (y < -0.5f && !player.isGrounded) return Vector2.down;

        // Forward cast
        return sprite.flipX ? Vector2.left : Vector2.right;
    }

    string GetCastAnimation(Vector2 dir)
    {
        if (dir == Vector2.up)
            return player.isGrounded ? "CastUpGround" : "CastUpAir";

        if (dir == Vector2.down)
            return "CastDownAir";

        return player.isGrounded ? "CastForwardGround" : "CastForwardAir";
    }

    float GetVerticalInput()
    {
        float kb = (Keyboard.current?.wKey.isPressed == true ? 1f : 0f)
                 + (Keyboard.current?.sKey.isPressed == true ? -1f : 0f);

        float gp = Gamepad.current != null ? Gamepad.current.leftStick.y.ReadValue() : 0f;

        return Mathf.Clamp(kb + gp, -1f, 1f);
    }

    bool WasCastPressed()
    {
        return (Mouse.current?.rightButton.wasPressedThisFrame == true)
            || (Gamepad.current?.buttonEast.wasPressedThisFrame == true);
    }
    // ==========================================
    // --- REWIND INTERFACE IMPLEMENTATION ---
    // ==========================================

    public void OnStartRewind() 
    { 
        // Safety reset: If we rewind mid-cast, release the freeze
        if (isCasting)
        {
            isCasting = false;
            rb.gravityScale = originalGravity;
        }
        recoilTimer = 0f;
    }
    public void OnStopRewind() { }

    public RewindState CaptureState()
    {
        var state = RewindState.Create(transform.position, transform.rotation, Time.time);
        
        // Save your spell cooldown timer!
        state.SetCustomData("nextFireTime", nextFireTime);
        return state;
    }

    public void ApplyState(RewindState state)
    {
        // Restore your spell cooldown timer
        nextFireTime = state.GetCustomData<float>("nextFireTime", nextFireTime);
    }
}
