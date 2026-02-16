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

    private Animator anim;

    void Start(){
        anim = GetComponent<Animator>();
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

        // Right Click = Spell
        if (mouse.rightButton.wasPressedThisFrame)
        {
            PerformSpell();
        }

        if (Input.GetKeyDown(KeyCode.N)) 
        {
            GetComponent<Animator>().SetTrigger("RainAttack");
        }
    }

    private void PerformMelee()
    {
        bool isAttackingUp = Input.GetKey(KeyCode.W);
        if (isAttackingUp)
        {
            anim.SetTrigger("TopSlash");
        }
        else
        {
            anim.SetTrigger("Slash");
        }
    }

    public void HitEnemy() 
    {
        Vector2 attackPosition = (Vector2)transform.position + ((Vector2)transform.right * attackOffset);
        if (anim.GetCurrentAnimatorStateInfo(0).IsName("Player_TopSlash"))
        {
            // Calculate position ABOVE the player
            attackPosition = (Vector2)transform.position + ((Vector2)transform.up * topAttackOffset);
        }
        else
        {
            // Calculate position in FRONT of the player
            attackPosition = (Vector2)transform.position + ((Vector2)transform.right * attackOffset);
        }

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPosition, meleeRange);

        foreach (Collider2D enemy in hitEnemies)
        {
            SlimeEnemy slime = enemy.GetComponent<SlimeEnemy>();
            if (slime != null)
            {
                slime.TakeDamage(meleeDamage);
            
                Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
                slime.ApplyKnockback(knockbackDir * knockbackStrength);
            }
        }
    }

    private void PerformSpell()
    {
        if (spellPrefab == null || firePoint == null) return;

        // Calculate direction to mouse
        Vector3 mousePos = Mouse.current.position.ReadValue();
        mousePos.z = 10f; 
        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(mousePos);
        Vector2 direction = (Vector2)worldMousePos - (Vector2)firePoint.position;
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Spawn spell rotated toward mouse
        Instantiate(spellPrefab, firePoint.position, Quaternion.Euler(0, 0, angle));
    }

    // Draws a red circle in the Scene View so you can see your melee range
    private void OnDrawGizmosSelected()
    {
        Vector2 attackPosition = (Vector2)transform.position + ((Vector2)transform.right * attackOffset);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPosition, meleeRange);
    }
}