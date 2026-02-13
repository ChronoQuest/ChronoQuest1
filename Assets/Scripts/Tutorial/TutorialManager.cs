using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public enum TutorialStep
    {
        None,
        Movement,
        Jump,
        Attack,
        Rewind, 
        Dash, 
        Complete
    }

    public TutorialStep currentStep = TutorialStep.None; 

    // references to hint UI elements 
    public GameObject rewindHint;
    public GameObject attackHint;
    public GameObject movementHint;
    public GameObject jumpHint; 
    public GameObject dashHint; 

    // references to movement and health systems to use for triggering hint pop-ups 
    public PlayerPlatformer player;
    public PlayerHealth playerHealth;
    public Transform enemy;
    public Transform platform; 

    // jump and attack distance are used to check proximity to objects like the enemy or platform
    // once close enough, hints for attack and jump will trigger
    public float attackDistance = 5f;
    public float jumpDistance = 5.5f; 

    bool moveCompleted = false;
    bool attackCompleted = false;
    bool rewindCompleted = false;
    bool jumpCompleted = false;
    bool dashCompleted = false;

    public Typewriter typewriter;
    public TextMeshProUGUI movementText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI rewindText;
    public TextMeshProUGUI jumpText;
    public TextMeshProUGUI dashText; 

    private string movementMessage;
    private string attackMessage;
    private string rewindMessage;
    private string jumpMessage; 
    private string dashMessage; 

    [SerializeField] float idleTimeThreshold = 2f;
    float idleTimer = 0f;
    Vector2 lastPlayerPosition;
    [SerializeField] private float movementGracePeriod = 6f; 
    private float gameStartTime; 

    private int hitCount = 0; 
    private int previousHealth;

    void Start()
    {
        // messages are assigned to the corresponding UI text component 
        movementMessage = movementText.text;
        attackMessage = attackText.text;
        rewindMessage = rewindText.text;
        jumpMessage = jumpText.text; 

        // player position is noted for checks (e.g. jump)
        lastPlayerPosition = player.transform.position;

        // track the start of the game, used for the idle check in the movement hint
        gameStartTime = Time.time; 
        
        DisableHints();

        if (playerHealth != null)
        {
            previousHealth = playerHealth.CurrentHealth;
            playerHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    void Update()
    {
        /* on every update, check if the player: 
        - ... is idle (movement check)
        - ... and enemy are close together (attack check)
        - ... is close enough to trigger the jump hint
        */ 
        CheckPlayerIdle();
        CheckAttackDistance();
        CheckJumpPrompt();
        
        // if all hints have been completed, tutorial completed 
        if (moveCompleted && attackCompleted && rewindCompleted && jumpCompleted && rewindCompleted && dashCompleted)
        {
            SetStep(TutorialStep.Complete);
            Debug.Log("Tutorial Complete!");
            DisableHints();
        }
    }

    // checks the distance between the enemy and the player
    void CheckAttackDistance()
    {
        if (attackCompleted)
            return;
        
        float distance = Vector2.Distance(player.transform.position, enemy.position);

        if (distance <= attackDistance)
        {
            SetStep(TutorialStep.Attack);
        }
    }

    // checks the distance between the platform and the player 
   void CheckJumpPrompt()
    {
        if (jumpCompleted) return;
        if (!player.isGrounded) return;

        // perform proximity check 
        float movementDelta = Vector2.Distance(player.transform.position, lastPlayerPosition);
        if (movementDelta > 0.01f) return;
        
        float distance = Vector2.Distance(player.transform.position, platform.position);
        if (distance > jumpDistance) return;

        SetStep(TutorialStep.Jump);
    }

    // checks if the player has been idle for the first n seconds of the game to trigger 
    void CheckPlayerIdle()
    {
        float movementDelta = Vector2.Distance(player.transform.position, lastPlayerPosition);
        
        if (movementDelta < 0.01f)
        {
            idleTimer += Time.deltaTime;
        } else {
            idleTimer = 0f;

            if (!moveCompleted)
            {
                OnPlayerMoved();
            }
        }

        // if the move tutorial hasn't been completed, the time conditions are met, trigger the movement tutorial step 
        if (!moveCompleted && Time.time - gameStartTime <= movementGracePeriod && idleTimer >= idleTimeThreshold)
        {
            SetStep(TutorialStep.Movement);
        }

        lastPlayerPosition = player.transform.position;
    }

    // handles when the health changes, triggers either the rewind or dash hint 
    private void HandleHealthChanged(int current, int max)
    {
       if (current < previousHealth)
       {
            hitCount++;
            Debug.Log($"[Tutorial] Player hit count = {hitCount}");

            if (!rewindCompleted)
            {
                SetStep(TutorialStep.Rewind); 
            }

            if (hitCount == 2 && !dashCompleted)
            {
                Debug.Log("Dash hint trigger");
                SetStep(TutorialStep.Dash); 
            }
       }

       previousHealth = current; 
    }

    // keeps track of the hit count and uses it to show the dash hint 
    private void HandlePlayerDamaged()
    {
        hitCount++; 

        if (hitCount >= 2 && !dashCompleted)
        {
            SetStep(TutorialStep.Dash); 
        }
    }

    // ** the following functions "OnPlayer..." mark tutorial steps as completed on certain player actions
    public void OnPlayerMoved()
    {
        if (currentStep == TutorialStep.Movement && !moveCompleted)
        {
            moveCompleted = true; 
            movementHint.SetActive(false);
            Debug.Log("Player movement tutorial complete");
        }
    }
    
    public void OnPlayerAttack()
    {
        if (currentStep == TutorialStep.Attack && !attackCompleted)
        {
            attackCompleted = true;
            attackHint.SetActive(false);
            Debug.Log("Player attack tutorial complete");
        }
    }

    public void OnPlayerJump()
    {
        if (currentStep != TutorialStep.Jump) return;
        if (jumpCompleted) return;

        jumpCompleted = true;
        jumpHint.SetActive(false); 
        Debug.Log("Player jump tutorial complete");
    }

    public void OnPlayerRewind()
    {
        if (currentStep != TutorialStep.Rewind) return;
        if (rewindCompleted) return;

        if (playerHealth.CurrentHealth < playerHealth.MaxHealth)
            return; 

        rewindCompleted = true;
        rewindHint.SetActive(false);
        Debug.Log("Player rewind tutorial complete");
    }

    public void OnPlayerDash()
    {
        if (currentStep != TutorialStep.Dash) return; 
        if (dashCompleted) return; 

        dashCompleted = true; 
        dashHint.SetActive(false); 
        Debug.Log("Player dash tutorial complete"); 
    }


    // setting the current tutorial step and showing corresponding hint
    void SetStep(TutorialStep step)
    {   
        if (currentStep == step)
            return;
        
        DisableHints(); 
        currentStep = step;

        // based on the current step, show the corresponding tutorial hint using the typewriter effect
        switch (step)
        {
            case TutorialStep.Movement:
                movementHint.SetActive(true);
                movementText.text = movementMessage;
                typewriter.StartTyping(movementText);
                break;
            case TutorialStep.Attack:
                attackHint.SetActive(true);
                attackText.text = attackMessage;
                typewriter.StartTyping(attackText);
                break;
            case TutorialStep.Rewind:
                rewindHint.SetActive(true);
                rewindText.text = rewindMessage;
                typewriter.StartTyping(rewindText);
                break;
            case TutorialStep.Jump:
                jumpHint.SetActive(true);
                jumpText.text = jumpMessage; 
                typewriter.StartTyping(jumpText); 
                break; 
            case TutorialStep.Dash:
                dashHint.SetActive(true); 
                dashText.text = dashMessage;
                typewriter.StartTyping(dashText); 
                break;
        }
    }

    // hints are disabled once tutorial is complete
    void DisableHints()
    {
        rewindHint.SetActive(false);
        attackHint.SetActive(false);
        movementHint.SetActive(false);
        jumpHint.SetActive(false);
    }
}
