using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public TutorialManager tutorialManager;
    
    public int maxHealth = 100;
    public int currentHealth;
    bool lowHealthWarningShown = false;


    public float moveSpeed = 5f;                    // speed of player, will likely be changed when animations are added to tutorial
    public float attackLunge = 1f; 
    
    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        // temporary player movement logic on key press for tutorial purposes 
        float move = Input.GetAxis("Horizontal");

        if (move != 0)
        {
            transform.Translate(Vector2.right * move * moveSpeed * Time.deltaTime);

            if (tutorialManager != null)
            {
                tutorialManager.OnPlayerMoved();
            }
        }

        // trigger attack on space bar key press
        if (Input.GetKeyDown(KeyCode.Space))
            StartAttack();
        
        // trigger rewind on R key press
        if (Input.GetKeyDown(KeyCode.R))
            TriggerRewind();
    }

    void StartAttack()
    {
        // ** attack logic here **

        AttackFeedback();

        if (tutorialManager != null)
        {
            tutorialManager.OnPlayerAttack();
        }

        // placeholder until attack logic is implemented
        Debug.Log("Player Attacked");
    }

    void AttackFeedback()
    {
        // will be replaced with proper attack animation later
        transform.Translate(Vector2.right * attackLunge);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Debug.Log("Player Defeated");

            // player defeat logic, try again screen? function to deal with this?
        }

        if (!lowHealthWarningShown && currentHealth <= maxHealth * 0.3f)
        {
            lowHealthWarningShown = true;
            Debug.Log("Warning: Low Health!");
        }

        if (currentHealth == 0)
        {
            GameOver();
        }
    }

    void OnLowHealth()
    {
        // warning logic here
    }

    void TriggerRewind()
    {
        /* rewind logic here
        if the player has been attacked or health is low etc., trigger the rewind effect
        */

        if (tutorialManager != null)
        {
            tutorialManager.OnPlayerRewind();
        }

        // placeholder until rewind logic is implemented
        Debug.Log("Player Rewind Triggered");
    }

    void GameOver()
    {
        // game over logic here, likely to be a screen where the user can try again
        Debug.Log("Game Over!"); 
    }
}

