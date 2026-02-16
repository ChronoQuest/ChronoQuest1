using UnityEngine;
using UnityEngine.InputSystem;

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

    private Animator anim;
    private Rigidbody2D rb;
    private PlayerPlatformer movement;

    void Start(){
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerPlatformer>();
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Left Click = Melee
        if (mouse.leftButton.wasPressedThisFrame)
        {
            PerformMelee();
        }

        if (Input.GetKeyDown(KeyCode.N)) 
        {
            GetComponent<Animator>().SetTrigger("RainAttack");
        }
    }

    private void PerformMelee()
    {
        bool isUp = Input.GetKey(KeyCode.W);
        bool isDown = Input.GetKey(KeyCode.S);
        bool isGrounded = movement != null && movement.isGrounded;
        
        if (isGrounded)
        {
            if (isUp) anim.SetTrigger("TopSlash");
            else anim.SetTrigger("Slash");
        }
        else
        {
            if (isUp) anim.SetTrigger("AirSlashUp");
            else if (isDown) anim.SetTrigger("AirSlashDown");
            else anim.SetTrigger("AirSlashSide");
        }
    }

    public void HitEnemy() 
    {
        Vector2 attackPosition = (Vector2)transform.position;
    
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);

        // 1. DYNAMIC HITBOX PLACEMENT
        // Check if the current animation is an "Upward" attack

        bool isUpAttack = state.IsName("Player_TopSlash") || state.IsName("Player_AirSlash_Up") || anim.GetNextAnimatorStateInfo(0).IsName("Player_AirSlash_Up");
        bool isDownAttack = state.IsName("Player_AirSlashDown") || anim.GetNextAnimatorStateInfo(0).IsName("Player_AirSlashDown");

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
            float direction = GetComponent<SpriteRenderer>().flipX ? -1f : 1f;
            attackPosition += new Vector2(direction * attackOffset, 0);        
        }
        // 2. COLLISION DETECTION
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPosition, meleeRange);

        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyBase target = enemy.GetComponent<EnemyBase>();
            if (target != null)
            {
                target.TakeDamage(meleeDamage);

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