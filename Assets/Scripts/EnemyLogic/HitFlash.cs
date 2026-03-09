using UnityEngine;
using System.Collections;

public class HitFlash : MonoBehaviour
{
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;
    private SpriteRenderer sprite;
    public bool IsFlashing { get; private set; }

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    public void Flash()
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        IsFlashing = true;
        sprite.color = flashColor;
        
        yield return new WaitForSeconds(flashDuration);
        
        // After red is done, check if the enemy is currently stunned
        EnemyBase enemy = GetComponent<EnemyBase>();
        if (enemy != null && enemy.GetIsStunned())
        {
            sprite.color = new Color(0.7f, 0.7f, 0.7f); // Stay Grey
        }
        else
        {
            sprite.color = Color.white; // Return to White
        }
        
        IsFlashing = false;
    }
}
