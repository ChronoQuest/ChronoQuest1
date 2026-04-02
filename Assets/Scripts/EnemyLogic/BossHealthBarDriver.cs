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
    }

    void LateUpdate()
    {
        if (enemy == null || healthBarUI == null) return;
        if (!fightStarted) return;

        int currentHealth = enemy.health;

        if (currentHealth != lastKnownHealth)
        {
            lastKnownHealth = currentHealth;
            float fraction = Mathf.Clamp01((float)currentHealth / enemy.startHealth);
            healthBarUI.SetHealth(fraction);
        }

        // Hide bar when boss dies
        if (enemy.IsDead && fightStarted)
        {
            fightStarted = false;
            healthBarUI.Hide();
        }
    }

    //Call this when the player enters the boss arena / fight triggers.
    public void StartFight()
    {
        if (healthBarUI == null || enemy == null) return;
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