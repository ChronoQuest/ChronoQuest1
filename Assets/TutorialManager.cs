using UnityEngine;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    // setting the order of tutorial steps
    public enum TutorialStep
    {
        None,
        Movement,
        Attack,
        Rewind, 
        Complete
    }

    public TutorialStep currentStep = TutorialStep.None; 
    
    public GameObject rewindHint;
    public GameObject attackHint;
    public GameObject movementHint;

    public Transform player;
    public Transform enemy; 
    public float attackDistance = 10f;

    bool moveCompleted = false;
    bool attackCompleted = false;
    bool rewindCompleted = false;

    public Typewriter typewriter;
    public TextMeshProUGUI movementText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI rewindText;

    private string movementMessage;
    private string attackMessage;
    private string rewindMessage;


    void Start()
    {
        movementMessage = movementText.text;
        attackMessage = attackText.text;
        rewindMessage = rewindText.text;
        
        DisableHints();
        SetStep(TutorialStep.Movement);         // set movement hint as first step in tutorial
    }

    void Update()
    {
        CheckAttackDistance();
        
        if (moveCompleted && attackCompleted && rewindCompleted)
        {
            SetStep(TutorialStep.Complete);
            Debug.Log("Tutorial Complete!");
            DisableHints();
        }
    }

    void CheckAttackDistance()
    {
        float distance = Vector2.Distance(player.position, enemy.position);

        if (distance <= attackDistance)
        {
            SetStep(TutorialStep.Attack);
        }
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

    // setting the current tutorial step and showing corresponding hint
    void SetStep(TutorialStep step)
    {   
        if (currentStep == step)
            return;
        currentStep = step;

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
        }
    }

    void DisableHints()
    {
        rewindHint.SetActive(false);
        attackHint.SetActive(false);
        movementHint.SetActive(false);
    }
}
