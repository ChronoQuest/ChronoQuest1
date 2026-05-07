using UnityEngine;
using System.Collections;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The foreground fill Image. Pivot should be (0, 0.5).")]
    public RectTransform fillRect;

    [Tooltip("The root Canvas or parent GameObject for the health bar UI.")]
    public GameObject healthBarRoot;

    [Header("Appearance")]
    [Tooltip("Hide the bar when the enemy is at full health.")]
    public bool hideWhenFull = true;

    [Tooltip("How long to keep the bar visible after taking damage.")]
    public float hideDelay = 2.5f;

    [Tooltip("How fast the bar fills/drains visually (lerp speed).")]
    public float lerpSpeed = 8f;

    [Header("Colors")]
    public Color highHealthColor  = new Color(0.18f, 0.85f, 0.35f);
    public Color midHealthColor   = new Color(1.00f, 0.80f, 0.15f);
    public Color lowHealthColor   = new Color(0.90f, 0.18f, 0.18f);

    [Header("Flash on Damage")]
    public UnityEngine.UI.Image fillImage;   // same as fillRect but as Image for color changes
    public Color flashColor = Color.white;
    public float flashDuration = 0.12f;

    private EnemyBase enemy;

    private float displayedFill;    // what the bar currently shows  (0-1)
    private float targetFill;       // what it's lerping toward
    private int   lastKnownHealth;

    private float hideTimer;
    private bool  isHidden;

    private Coroutine flashRoutine;

    // full width of fillRect at scale 1, captured once so we can set localScale.x
    private float originalFillWidth;

    // Authored local scale of the healthbar root, captured so we can counter-flip
    // when the enemy's transform.localScale.x flips for facing direction.
    private Vector3 originalHealthBarLocalScale = Vector3.one;


    void Awake()
    {
        enemy = GetComponentInParent<EnemyBase>();
        if (enemy == null)
            enemy = GetComponent<EnemyBase>();

        if (fillRect != null)
            originalFillWidth = fillRect.sizeDelta.x; // store, not used directly; we scale instead

        if (healthBarRoot != null)
            originalHealthBarLocalScale = healthBarRoot.transform.localScale;

        if (healthBarRoot != null && hideWhenFull)
            healthBarRoot.SetActive(false);

        isHidden = hideWhenFull;
    }

    void Start()
    {
        if (enemy == null) return;

        // Initialise to full health so there's no pop-in on first damage
        displayedFill = 1f;
        targetFill    = 1f;
        lastKnownHealth = enemy.health;

        ApplyFillImmediate(1f);
    }

    void LateUpdate()
    {
        if (enemy == null) return;

        if (TimeRewind.TimeRewindManager.Instance != null && TimeRewind.TimeRewindManager.Instance.IsRewinding)
        {
            if (healthBarRoot.activeSelf) healthBarRoot.SetActive(false);
            return; 
        }

        int currentHealth = enemy.health;
        bool dead         = enemy.IsDead;

        if (currentHealth != lastKnownHealth)
        {
            bool tookDamage = currentHealth < lastKnownHealth;

            lastKnownHealth = currentHealth;
            targetFill = Mathf.Clamp01((float)currentHealth / enemy.startHealth);

            if (hideWhenFull)
            {
                ShowBar();
                hideTimer = hideDelay;
            }

            if (tookDamage && fillImage != null)
                TriggerFlash();
        }

        displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * lerpSpeed);
        ApplyFill(displayedFill);

        CounterFlipHealthBar();

        if (hideWhenFull && !isHidden)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f && targetFill >= 1f)
                HideBar();
        }

        // ── Always hide when dead (sprite is disabled anyway) ─────────────────
        if (dead && !isHidden)
            HideBar();
        else if (!dead && isHidden && currentHealth < enemy.startHealth)
            ShowBar();
    }

    public void SyncImmediate()
    {
        if (enemy == null) return;

        lastKnownHealth = enemy.health;
        targetFill      = Mathf.Clamp01((float)enemy.health / enemy.startHealth);
        displayedFill   = targetFill;
        ApplyFillImmediate(displayedFill);

        // Show or hide based on current health
        if (hideWhenFull && targetFill >= 1f)
            HideBar();
        else if (!enemy.IsDead)
            ShowBar();
    }

    // Some enemies face by negating transform.localScale.x; the healthbar is a child
    // and would otherwise mirror with them. Counter the parent's world flip so the
    // bar always reads left-to-right.
    void CounterFlipHealthBar()
    {
        if (healthBarRoot == null) return;
        Transform hbParent = healthBarRoot.transform.parent;
        float parentLossyX = hbParent != null ? hbParent.lossyScale.x : 1f;
        float wantSign = parentLossyX < 0f ? -1f : 1f;
        Vector3 s = originalHealthBarLocalScale;
        s.x = Mathf.Abs(originalHealthBarLocalScale.x) * wantSign;
        healthBarRoot.transform.localScale = s;
    }

    void ApplyFill(float t)
    {
        if (fillRect == null) return;

        // scale the fill rect on the X axis. pivot must be (0, 0.5) in the inspector
        Vector3 s = fillRect.localScale;
        s.x = Mathf.Clamp01(t);
        fillRect.localScale = s;

        if (fillImage != null && flashRoutine == null)
            fillImage.color = HealthColor(t);
    }

    void ApplyFillImmediate(float t)
    {
        displayedFill = t;
        ApplyFill(t);
    }

    Color HealthColor(float t)
    {
        if (t > 0.6f)  return Color.Lerp(midHealthColor,  highHealthColor, (t - 0.6f) / 0.4f);
        if (t > 0.3f)  return Color.Lerp(lowHealthColor,  midHealthColor,  (t - 0.3f) / 0.3f);
        return lowHealthColor;
    }

    void ShowBar()
    {
        if (healthBarRoot != null) healthBarRoot.SetActive(true);
        isHidden = false;
    }

    void HideBar()
    {
        if (healthBarRoot != null) healthBarRoot.SetActive(false);
        isHidden = true;
    }

    void TriggerFlash()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        fillImage.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        fillImage.color = HealthColor(displayedFill);
        flashRoutine = null;
    }
}