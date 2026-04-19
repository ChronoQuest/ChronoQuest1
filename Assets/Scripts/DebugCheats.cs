using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugCheats : MonoBehaviour
{
    [Header("Keybinds")]
    public KeyCode toggleGodModeKey    = KeyCode.F1;
    public KeyCode toggleInfiniteManaKey = KeyCode.F2;
    public KeyCode loadBossSceneKey    = KeyCode.F3;

    private PlayerHealth playerHealth;
    private PlayerMana   playerMana;

    private bool godModeOn;
    private bool infiniteManaOn;

    void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        playerMana   = FindFirstObjectByType<PlayerMana>();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleGodModeKey))
        {
            godModeOn = !godModeOn;
            if (!godModeOn) playerHealth?.SetInvincible(false);
            Debug.Log($"God mode: {godModeOn}");
        }

        if (Input.GetKeyDown(toggleInfiniteManaKey))
        {
            infiniteManaOn = !infiniteManaOn;
            Debug.Log($"Infinite mana: {infiniteManaOn}");
        }

        if (godModeOn && playerHealth != null)
            playerHealth.SetInvincible(true);

        if (infiniteManaOn && playerMana != null)
            playerMana.SetMana(playerMana.MaxMana);

        if (Input.GetKeyDown(loadBossSceneKey))
            SceneManager.LoadScene("FinalBoss");
    }
}
