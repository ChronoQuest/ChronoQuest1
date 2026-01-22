using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int health = 100;
    public float moveSpeed = 5f;                    // speed of player, will likely be changed when animations are added to tutorial
    public TutorialManager tutorialManager;
    
    void Update()
    {
        // temporary player movement logic on key press
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
        // attack logic here

        if (tutorialManager != null)
        {
            tutorialManager.OnPlayerAttack();
        }

        // placeholder until attack logic is implemented
        Debug.Log("Player Attacked");
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
}

