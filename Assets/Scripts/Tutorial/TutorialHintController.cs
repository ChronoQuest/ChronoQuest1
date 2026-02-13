using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialHintController : MonoBehaviour
{
    public TutorialManager tutorialManager;
    public PlayerPlatformer player; 
    
    public int maxHealth = 100;
    public int currentHealth;
    bool lowHealthWarningShown = false;

    public float moveSpeed = 5f;                    // speed of player, will likely be changed when animations are added to tutorial
    public float attackLunge = 1f; 

    private bool moveTriggered;
    private bool jumpTriggered;          
    
    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (!moveTriggered && Mathf.Abs(player.horizontalInput) > 0.1f)
        {
            moveTriggered = true;
            tutorialManager?.OnPlayerMoved();
        } 
    }

    void StartAttack()
    {
        // ** attack logic here **

        AttackFeedback();

        if (tutorialManager != null)
        {
            tutorialManager.OnPlayerAttack();
        }

        Debug.Log("Player Attacked");
    }

    void AttackFeedback()
    {
        // will be replaced with proper attack animation later
        transform.Translate(Vector2.right * attackLunge);
    }

    // ** might be able to delete this ** 
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Debug.Log("Player Defeated");

            // player defeat logic, game over
        }

        if (!lowHealthWarningShown && currentHealth <= maxHealth * 0.3f)
        {
            lowHealthWarningShown = true;
            Debug.Log("Warning: Low Health!");
        }
    }

    void TriggerRewind()
    {
        /* rewind logic here
        if the player health is low, trigger the rewind effect
        */

        if (tutorialManager != null)
        {
            tutorialManager.OnPlayerRewind();
        }

        // placeholder until rewind logic is implemented
        Debug.Log("Player Rewind Triggered");
    }
}

