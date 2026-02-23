using UnityEngine;
using System.Collections;
using TimeRewind;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class SpellProjectile : MonoBehaviour, IRewindable
{
    public float speed = 15f;
    public float lifetime = 3f;
    public int damage = 1;
    public float knockbackStrength = 6f;


    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private bool hasHit = false;

    // --- Rewind Variables ---
    private bool isRewinding = false;
    private RigidbodyType2D originalBodyType;
    private float currentLifetime = 0f;
    private float startLifetime = 0f;

    void Awake()
    {
        startLifetime = Time.deltaTime;
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }
    void OnEnable()
    {
        // Register to the Rewind Manager when spawned
        if (TimeRewindManager.Instance != null) 
            TimeRewindManager.Instance.Register(this);
    }

    void OnDisable()
    {
        // Unregister when destroyed
        if (TimeRewindManager.Instance != null) 
            TimeRewindManager.Instance.Unregister(this);
    }
        // Called by PlayerSpellSystem
    public void Init(Vector2 direction, bool flipped)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // Set velocity so projectile moves
        rb.linearVelocity = direction.normalized * speed;

        // Rotate projectile to face movement direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        // Auto destroy after lifetime
}

    // This allows us to pause the aging process while time is going backwards
    void Update()
    {
        if (isRewinding) return;

        if (!hasHit)
        {
            currentLifetime += Time.deltaTime;
            if (currentLifetime >= lifetime)
            {
                ExecuteImpact();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;
        
        // 1. Ignore the Player entirely
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player")) return;

        // 2. Check for Enemies / Destructibles
        IDamageable dmg = collision.GetComponent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(damage);

            IKnockbackable kb = collision.GetComponent<IKnockbackable>();
            if (kb != null)
            {
                Vector2 dir = (collision.transform.position - transform.position).normalized;
                kb.ApplyKnockback(dir * knockbackStrength);
            }
            // Check if enemy died
            EnemyBase enemy = collision.GetComponent<EnemyBase>();
            if (enemy != null && enemy.IsDead) return;
            
            ExecuteImpact();
            return; // Stop running code here so we don't hit the ground check below
        }

        // 3. Only impact the environment if it's the Ground layer OR a solid (non-trigger) object
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground") || !collision.isTrigger)
        {
            ExecuteImpact();
        }
    }

    // Helper function to keep things clean - hide but do not destroy so rewind can restore
    private void ExecuteImpact()
    {
        hasHit = true;
        rb.linearVelocity = Vector2.zero; // Stop moving
        anim.SetTrigger("Impact");        // Play explosion
        StartCoroutine(HideAfterImpact(0.3f));
    }

    private IEnumerator HideAfterImpact(float delay)
    {
        float timer = delay;
        while (timer > 0)
        {
            if (!isRewinding) timer -= Time.deltaTime;
            yield return null;
        }
        // Disable collider and sprite - stay registered so rewind can restore us (like enemies)
        if (col != null) col.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    // ==========================================
    // --- REWIND INTERFACE IMPLEMENTATION ---
    // ==========================================

    public void OnStartRewind()
    {
        isRewinding = true;
        StopAllCoroutines(); // Stop HideAfterImpact so we don't hide during rewind

        // Stop physics from interfering with the rewind path
        originalBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public void OnStopRewind()
    {
        isRewinding = false;
        
        // Return physics to normal
        rb.bodyType = originalBodyType;
    }

    public RewindState CaptureState()
    {
        // Save Physics State
        var state = RewindState.CreateWithPhysics(
            transform.position, 
            transform.rotation, 
            rb.linearVelocity, 
            rb.angularVelocity, 
            Time.time
        );
        
        // Save Custom Logic State
        state.SetCustomData("hasHit", hasHit);
        state.SetCustomData("lifetime", currentLifetime);
        
        // Save Animation State
        if (anim != null)
        {
            var animState = anim.GetCurrentAnimatorStateInfo(0);
            state.AnimatorStateHash = animState.fullPathHash;
            state.AnimatorNormalizedTime = animState.normalizedTime;
        }
        
        return state;
    }

    public void ApplyState(RewindState state)
    {
        // Restore Physics
        transform.position = state.Position;
        transform.rotation = state.Rotation;
        rb.linearVelocity = state.Velocity;

        // Restore Logic
        bool wasHit = hasHit;
        hasHit = state.GetCustomData<bool>("hasHit");
        currentLifetime = state.GetCustomData<float>("lifetime");

        // Comment out these two lines if we want spells to presist after rewinding to before they were spawned in
        if(currentLifetime - 0.05f <= startLifetime) gameObject.SetActive(false);
        else gameObject.SetActive(true);

        // Revive when rewinding to a state where projectile was still active (like enemies)
        if (wasHit && !hasHit)
        {
            if (col != null) col.enabled = true;
            if (spriteRenderer != null) spriteRenderer.enabled = true;
        }

        // Restore Animation
        if (anim != null && state.AnimatorStateHash != 0)
        {
            anim.Play(state.AnimatorStateHash, 0, state.AnimatorNormalizedTime);
            anim.Update(0f);
        }
    }

}
