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
        Complete
    }

    public TutorialStep currentStep = TutorialStep.None; 

    public GameObject rewindHint;
    public GameObject attackHint;
    public GameObject movementHint;
    public GameObject jumpHint; 

    public PlayerPlatformer player;
    public Transform enemy;
    public Transform platform; 
    public float attackDistance = 5f;
    public float jumpDistance = 5.5f; 

    bool moveCompleted = false;
    bool attackCompleted = false;
    bool rewindCompleted = false;
    bool jumpCompleted = false;

    public Typewriter typewriter;
    public TextMeshProUGUI movementText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI rewindText;
    public TextMeshProUGUI jumpText;

    private string movementMessage;
    private string attackMessage;
    private string rewindMessage;
    private string jumpMessage; 

    [SerializeField] float idleTimeThreshold = 2f;
    float idleTimer = 0f;
    Vector2 lastPlayerPosition;


    void Start()
    {
        movementMessage = movementText.text;
        attackMessage = attackText.text;
        rewindMessage = rewindText.text;
        jumpMessage = jumpText.text; 

        lastPlayerPosition = player.transform.position;
        
        DisableHints();
    }

    void Update()
    {
        // on every update, check if player is within distance to trigger the attack tutorial 
        CheckPlayerIdle();
        CheckAttackDistance();
        CheckJumpPrompt();
        
        if (moveCompleted && attackCompleted && rewindCompleted && jumpCompleted)
        {
            SetStep(TutorialStep.Complete);
            Debug.Log("Tutorial Complete!");
            DisableHints();
        }
    }

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

   void CheckJumpPrompt()
    {
        if (jumpCompleted) return;
        if (!player.isGrounded) return;

        // perform idle and proximity check
        float movementDelta = Vector2.Distance(player.transform.position, lastPlayerPosition);
        if (movementDelta > 0.01f) return;

        float distance = Vector2.Distance(player.transform.position, platform.position);
        if (distance > jumpDistance) return;

        SetStep(TutorialStep.Jump);
    }



    void CheckPlayerIdle()
    {
        float movementDelta = Vector2.Distance(
            player.transform.position,
            lastPlayerPosition
        );
        
        if (movementDelta < 0.01f)
        {
            idleTimer += Time.deltaTime;
        } else
        {
            idleTimer = 0f;

            if (!moveCompleted)
            {
                OnPlayerMoved();
            }
        }

        if (!moveCompleted && idleTimer >= idleTimeThreshold)
        {
            SetStep(TutorialStep.Movement);
        }

        lastPlayerPosition = player.transform.position;
    }

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

    public void OnPlayerRewind()
    {
        if (currentStep == TutorialStep.Rewind && !rewindCompleted)
        {
            rewindCompleted = true;
            rewindHint.SetActive(false);
            Debug.Log("Player rewind tutorial complete");
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
        }
    }

    // hints disabled once tutorial is complete
    void DisableHints()
    {
        rewindHint.SetActive(false);
        attackHint.SetActive(false);
        movementHint.SetActive(false);
        jumpHint.SetActive(false);
    }
}
