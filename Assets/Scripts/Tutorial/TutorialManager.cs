using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public enum TutorialStep
    {
        None,
        Movement,
        Dash, 
        DoubleJump,
        Jump,
        Attack,
        Rewind, 
        Complete
    }

    public TutorialStep currentStep = TutorialStep.None; 

    // references to hint UI elements 
    public GameObject rewindHint;
    public GameObject attackHint;
    public GameObject movementHint;
    public GameObject jumpHint; 
    public GameObject dashHint;
    public GameObject doubleJumpHint;  

    // references to movement and health systems to use for triggering hint pop-ups 
    public PlayerPlatformer player;
    public PlayerHealth playerHealth;
    public Transform enemy;

    // jump and attack distance are used to check proximity to objects like the enemy or platform
    // once close enough, hints for attack and jump will trigger
    public float attackDistance = 5f;
    public float jumpDistance = 5.5f; 

    bool moveCompleted = false;
    bool attackCompleted = false;
    bool rewindCompleted = false;
    bool jumpCompleted = false;
    bool dashCompleted = false;
    bool doubleJumpCompleted = false; 

    public Typewriter typewriter;
    public TextMeshProUGUI movementText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI rewindText;
    public TextMeshProUGUI jumpText;
    public TextMeshProUGUI dashText; 
    public TextMeshProUGUI doubleJumpText; 

    private string movementMessage;
    private string attackMessage;
    private string rewindMessage;
    private string jumpMessage; 
    private string dashMessage; 
    private string doubleJumpMessage; 

    [SerializeField] float idleTimeThreshold = 2f;
    float idleTimer = 0f;
    Vector2 lastPlayerPosition;
    [SerializeField] private float movementGracePeriod = 4f; 
    private float gameStartTime; 

    private int hitCount = 0; 
    private int previousHealth;
    private bool jumpAttempted = false;         
    private bool jumpSucceeded = false; 
    [SerializeField] private float doubleJumpHintDuration = 4f;             // temporary trigger time for double jump hint

    void Start()
    {
        // messages are assigned to the corresponding UI text component 
        movementMessage = movementText.text;
        attackMessage = attackText.text;
        rewindMessage = rewindText.text;
        jumpMessage = jumpText.text; 
        dashMessage = dashText.text; 
        doubleJumpMessage = doubleJumpText.text;

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

        if (TimeRewind.TimeRewindManager.Instance != null)
        {
            TimeRewind.TimeRewindManager.Instance.OnRewindStart += HandleRewindStarted;
        }
    }

    void Update()
    {
        /* on every update, check if the player: 
        - ... is idle (movement check)
        - ... and enemy are close together (attack check)
        */ 
        CheckPlayerIdle();
        // CheckAttackDistance();
        
        // if all hints have been completed, tutorial completed 
        // TODO: add back attack completed once combat has been added
        if (moveCompleted && rewindCompleted && jumpCompleted && rewindCompleted && dashCompleted)
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

    public void TriggerJumpHint()
    {
        if (currentStep == TutorialStep.DoubleJump) return;

        if (jumpCompleted) return;
        if (currentStep == TutorialStep.Jump) return; 

        jumpAttempted = true; 
        jumpSucceeded = false; 
        SetStep(TutorialStep.Jump); 
    }

    public void TriggerDoubleJumpHint()
    {
        if (doubleJumpCompleted) return;
        if (currentStep == TutorialStep.DoubleJump) return; 

        SetStep(TutorialStep.DoubleJump); 

        // TODO: change so double jump hint is hidden after a certain trigger
        // hides the double jump hint after the timer runs out
        CancelInvoke(nameof(HideDoubleJumpHint));
        Invoke(nameof(HideDoubleJumpHint), doubleJumpHintDuration);
    } 

    public void TriggerDashHint()
    {
        if (dashCompleted) return;
        if (currentStep == TutorialStep.Dash) return; 

        SetStep(TutorialStep.Dash); 
    }

    public void OnJumpSucceeded()
    {
        jumpSucceeded = true;
        jumpAttempted = false;
        Debug.Log("Jump successful"); 
    }

    // handles when the health changes, triggers either the rewind or dash hint 
    private void HandleHealthChanged(int current, int max)
    {
       if (current >= previousHealth)
        {
            previousHealth = current; 
            return; 
        }

        if (current == 1 && !rewindCompleted)
        {
            SetStep(TutorialStep.Rewind); 
        }
       
       previousHealth = current; 
    }

    // keeps track of the hit count and uses it to show the dash hint 
    private void HandlePlayerDamaged()
    {
        hitCount++; 
    }

    private void HandleRewindStarted()
    {
        OnPlayerRewind();
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
    
    // commented out until combat logic is added
    /* public void OnPlayerAttack()
    {
        if (currentStep == TutorialStep.Attack && !attackCompleted)
        {
            attackCompleted = true;
            attackHint.SetActive(false);
            Debug.Log("Player attack tutorial complete");
        }
    } */ 

    public void OnPlayerJump()
    {
        if (currentStep == TutorialStep.Jump && !jumpCompleted)
        {
            jumpCompleted = true;
            jumpHint.SetActive(false); 
            Debug.Log("Player jump tutorial complete");
        }
    }

    public void OnPlayerRewind()
    {
        if (currentStep == TutorialStep.Rewind && !rewindCompleted)
        {
            rewindCompleted = true;
            rewindHint.SetActive(false);
            Debug.Log("Player rewind tutorial complete");
        }
    }

    public void OnPlayerDash()
    {
        if (currentStep == TutorialStep.Dash && !dashCompleted)
        {
            dashCompleted = true;
            dashHint.SetActive(false);
            Debug.Log("Player dash tutorial completed"); 
        } 
    }

    public void OnPlayerDoubleJump()
    {
        if (currentStep == TutorialStep.DoubleJump)
        {
            doubleJumpHint.SetActive(false); 
            Debug.Log("Player double jump completed"); 
        }
    }

    private void HideDoubleJumpHint()
    {
        doubleJumpHint.SetActive(false);
        Debug.Log("Double jump hint hidden");
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
            case TutorialStep.DoubleJump:
                doubleJumpHint.SetActive(true); 
                doubleJumpText.text = doubleJumpMessage;
                typewriter.StartTyping(doubleJumpText); 
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
        dashHint.SetActive(false); 
        doubleJumpHint.SetActive(false); 
    }
}
