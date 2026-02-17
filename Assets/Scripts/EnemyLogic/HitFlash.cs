using UnityEngine;
using System.Collections;

public class HitFlash : MonoBehaviour
{
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;

    private SpriteRenderer sprite;
    private Color originalColor;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        originalColor = sprite.color;
    }

    public void Flash()
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        sprite.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        sprite.color = originalColor;
    }
}
