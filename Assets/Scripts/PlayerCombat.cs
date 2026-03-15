using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    [Header("Melee Settings")]
    public float meleeRange = 3.0f;
    public int meleeDamage = 1;
    public float attackOffset = 1.0f; // Distance in front of player
    public float topAttackOffset = 2.0f; // Offset for attacking upward

    [Header("Spell Settings")]
    public GameObject spellPrefab;
    public Transform firePoint;

    [Header("Knockback")]
    public float knockbackStrength = 8f;

    [Header("Air Combat")]
    public float pogoForce = 12f;

    [Header("Combo Settings")]
    [SerializeField] private int comboStep = 0;
    [SerializeField] private bool queuedAttack;

    [Header("Failsafe Settings")]
    [Tooltip("Maximum time in seconds an attack can last before forcefully resetting.")]
    public float attackTimeout = 0.6f; 
    private float attackTimer = 0f;

    [Header("Dependencies")]
    private PlayerMana manaSystem;

    private Animator anim;
    private Rigidbody2D rb;
    private PlayerPlatformer movement;
    private SpriteRenderer spriteRenderer;
    public PlayerTacticalModel playerTacticalModel;
    public bool isAttacking { get; private set; }

    void Start(){
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerPlatformer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        manaSystem = GetComponent<PlayerMana>();
    }

    void Update()
    {
        if (!movement.IsActionAllowed(PlayerAction.Attack))
            return;

        if (PauseMenu.isPaused) return;

        // --- THE FAILSAFE TIMER ---
        if (isAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer > attackTimeout)
            {
                Debug.LogWarning("Attack failsafe triggered! Resetting combat state.");
                CancelAttack();
            }
        }
    

        bool attackPressed = false;

        // 1. Check Mouse Input
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            attackPressed = true;
        }
        // 2. Check Gamepad Input (buttonWest = Square on PS / X on Xbox)
        if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
        {
            attackPressed = true;
        }
        if (attackPressed)
        {
            // If we are NOT attacking, start the combo immediately
            if (!isAttacking)
            {
                comboStep = 0;
                PerformMelee();
            }
            // If we ARE attacking, queue up the next hit
            else if (!queuedAttack)
            {
                queuedAttack = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.N)|| (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)) 
        {
            // Costs 20 mana
            if (manaSystem != null && manaSystem.TrySpendMana(20f))
            {
                anim.SetTrigger("RainAttack");
            }
            else
            {
                Debug.Log("Not enough mana for Rain Attack!");
            }
        }
    }

    private void PerformMelee()
    {

        float moveInput = Keyboard.current.dKey.isPressed ? 1 : (Keyboard.current.aKey.isPressed ? -1 : 0);
        if (Gamepad.current != null) moveInput += Gamepad.current.leftStick.x.ReadValue();

        if (moveInput > 0.1f) spriteRenderer.flipX = false;
        else if (moveInput < -0.1f) spriteRenderer.flipX = true;

        isAttacking = true;
        queuedAttack = false;
        attackTimer = 0f;
        DataCollectionService.Instance?.RecordMeleeAttempt();
        playerTacticalModel.RecordMeleeHit(); 

        
        float dir = spriteRenderer.flipX ? -1f : 1f;
        bool isUp = false;
        bool isDown = false;

        // Check Keyboard Directions
        if (Keyboard.current != null)
        {
            isUp |= Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            isDown |= Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
        }

        // Check Gamepad Directions (Left Stick or D-Pad)
        if (Gamepad.current != null)
        {
            isUp |= Gamepad.current.leftStick.y.ReadValue() > 0.5f || Gamepad.current.dpad.up.isPressed;
            isDown |= Gamepad.current.leftStick.y.ReadValue() < -0.5f || Gamepad.current.dpad.down.isPressed;
        }
        bool isGrounded = movement != null && movement.isGrounded;
        
        if (isGrounded && !isUp)
        {
            // --- THE FIX: Force the animation state instantly ---
            // The "-1, 0f" tells Unity to play it from frame 0, ignoring all transition blending
            if (comboStep == 0)
            {
                anim.Play("Player_Slash", -1, 0f); // <-- CHANGE TO YOUR SLASH 1 STATE NAME
                rb.linearVelocity = new Vector2(dir * 4f, rb.linearVelocity.y);
                comboStep = 1;
            }
            else
            {
                anim.Play("Player_Slash2", -1, 0f); // <-- CHANGE TO YOUR SLASH 2 STATE NAME
                rb.linearVelocity = new Vector2(dir * 6f, rb.linearVelocity.y);
                comboStep = 0;
            }
        }
        else
        {
            comboStep = 0; // Reset combo if we do an air/up attack
            if (isGrounded && isUp) anim.SetTrigger("TopSlash");
            else if (isUp) anim.SetTrigger("AirSlashUp");
            else if (isDown) anim.SetTrigger("AirSlashDown");
            else 
            {
                anim.SetTrigger("AirSlashSide");
                rb.linearVelocity = new Vector2(dir * 3f, rb.linearVelocity.y);
            }
            isAttacking = false;
        }
    }
    public void EndAttack()
    {
        // This should be called via an Animation Event near the end of Slash 1 and Slash 2
        if (queuedAttack)
        {
            // If the player mashed the button, instantly fire the next attack in the chain
            PerformMelee();
        }
        else
        {
            // If the player stopped mashing, reset everything
            isAttacking = false;
            comboStep = 0; 
        }
        attackTimer = 0f;
    }
    public void CancelAttack()
    {
        isAttacking = false;
        queuedAttack = false;
        comboStep = 0;
        attackTimer = 0f; 
    }

    public void HitEnemy() 
    {
        Vector2 attackPosition = (Vector2)transform.position;
    
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);

        // 1. DYNAMIC HITBOX PLACEMENT
        // Check if the current animation is an "Upward" attack

        bool isUpAttack = state.IsName("Player_TopSlash") || state.IsName("Player_AirSlash_Up") || anim.GetNextAnimatorStateInfo(0).IsName("Player_AirSlash_Up");
        bool isDownAttack = state.IsName("Player_AirSlash_Down") || anim.GetNextAnimatorStateInfo(0).IsName("Player_AirSlash_Down");

        if (isUpAttack)
        {
            attackPosition += (Vector2)transform.up * topAttackOffset;
        }
        // Check if the current animation is the "Downward" air attack
        else if (isDownAttack)
        {
            attackPosition += (Vector2)transform.up * -topAttackOffset; // Negative Y moves hitbox down
        }
        // Default to Side attack
        else 
        {
            float direction = spriteRenderer.flipX ? -1f : 1f;
            attackPosition += new Vector2(direction * attackOffset, 0);        
        }
        // 2. COLLISION DETECTION
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPosition, meleeRange);
        bool hitAnything = false;

        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyBase target = enemy.GetComponent<EnemyBase>();
            if (target != null)
            {
                hitAnything = true;
                target.TakeDamage(meleeDamage);
                DataCollectionService.Instance?.RecordMeleeHit();
                if (manaSystem != null) manaSystem.AddManaOnHit();

                // 3. PHYSICS INTERACTION (The Pogo)
                if (state.IsName("Player_AirSlashDown"))
                {
                    // Push player UP (Bounce)
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoForce);
                    // Push enemy DOWN
                    target.ApplyKnockback(Vector2.down * knockbackStrength);
                }
                else
                {
                    // Standard Knockback away from player
                    Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
                    target.ApplyKnockback(knockbackDir * knockbackStrength);
                }
            }
        }
        if (hitAnything)
        {
            TriggerHitstop(0.07f);
        }
    }

    public void TriggerHitstop(float duration = 0.05f)
    {
        StartCoroutine(HitstopRoutine(duration));
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        Time.timeScale = 0f; 
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    // Draws a red circle in the Scene View so you can see your melee range
    private void OnDrawGizmosSelected()
    {
        Vector2 attackPosition = (Vector2)transform.position + ((Vector2)transform.right * attackOffset);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPosition, meleeRange);

        Gizmos.color = Color.blue; // Different color for clarity
        Vector2 topPos = (Vector2)transform.position + ((Vector2)transform.up * topAttackOffset);
        Gizmos.DrawWireSphere(topPos, meleeRange);
    }
}