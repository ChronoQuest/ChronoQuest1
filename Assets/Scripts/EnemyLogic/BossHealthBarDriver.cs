using UnityEngine;

public class BossHealthBarDriver : MonoBehaviour
{
    [Header("References")]
    public BossHealthBar healthBarUI;   // The screen-space UI canvas

    [Header("Boss Info")]
    public string bossName = "Boss";

    private EnemyBase enemy;
    private int lastKnownHealth;
    private bool fightStarted = false;

    void Awake()
    {
        enemy = GetComponent<EnemyBase>();
    }

    void Start()
    {
        lastKnownHealth = enemy != null ? enemy.health : 0;
        Debug.Log($"startHealth = {enemy.startHealth}, health = {enemy.health}");
    }

    void LateUpdate()
    {
        if (enemy == null || healthBarUI == null || !fightStarted) return;

        int currentHealth = enemy.health;
        int maxHealth = enemy.startHealth;

        // Guard against uninitialized startHealth
        if (maxHealth <= 0) return;

        if (currentHealth != lastKnownHealth)
        {
            lastKnownHealth = currentHealth;
            float fraction = Mathf.Clamp01((float)currentHealth / maxHealth);
            healthBarUI.SetHealth(fraction);
        }

        if (enemy.IsDead)
        {
            fightStarted = false;
            healthBarUI.SetHealth(0f);
            healthBarUI.Hide();
        }
    }

    public void StartFight()
    {
        if (healthBarUI == null || enemy == null) return;
        if (enemy.startHealth <= 0) return; // not ready yet
        fightStarted = true;
        lastKnownHealth = enemy.health;
        healthBarUI.Show(bossName, 1);
    }

    // Refills the bar and updates the phase label.
    public void AdvancePhase(int newPhase)
    {
        if (healthBarUI == null) return;
        healthBarUI.StartNewPhase(newPhase);
    }

    public void SyncAfterRewind()
    {
        if (enemy == null || healthBarUI == null) return;
        lastKnownHealth = enemy.health;
        float fraction = Mathf.Clamp01((float)enemy.health / enemy.startHealth);
        healthBarUI.SyncImmediate(fraction);
    }
}