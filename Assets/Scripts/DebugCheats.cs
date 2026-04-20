using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class DebugCheats : MonoBehaviour
{
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
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame)
        {
            godModeOn = !godModeOn;
            if (!godModeOn) playerHealth?.SetInvincible(false);
            Debug.Log($"God mode: {godModeOn}");
        }

        if (kb.f2Key.wasPressedThisFrame)
        {
            infiniteManaOn = !infiniteManaOn;
            Debug.Log($"Infinite mana: {infiniteManaOn}");
        }

        if (godModeOn && playerHealth != null)
            playerHealth.SetInvincible(true);

        if (infiniteManaOn && playerMana != null)
            playerMana.SetMana(playerMana.MaxMana);

        if (kb.f3Key.wasPressedThisFrame)
            SceneManager.LoadScene("FinalBoss");

        if (kb.f4Key.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
