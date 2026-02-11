using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Melee Settings")]
    public float meleeRange = 1.2f;
    public int meleeDamage = 1;
    public float attackOffset = 1.0f; // Distance in front of player

    [Header("Spell Settings")]
    public GameObject spellPrefab;
    public Transform firePoint;

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
    }

    private void PerformMelee()
    {
        // Calculate the center of the hit circle
        Vector2 attackPosition = (Vector2)transform.position + ((Vector2)transform.right * attackOffset);
        
        // Find all colliders inside that circle
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPosition, meleeRange);

        foreach (Collider2D enemy in hitEnemies)
        {
            SlimeEnemy slime = enemy.GetComponent<SlimeEnemy>();
            if (slime != null)
            {
                // CALL THE METHOD IN YOUR SLIME SCRIPT
                slime.TakeDamage(meleeDamage);
                Debug.Log("Melee hit the slime!");
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