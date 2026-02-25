using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections; 

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
        Spell, 
        WallJump, 
        Complete
    }

    public TutorialStep currentStep = TutorialStep.None; 

    // states
    private GameObject activeHint = null;
    private TutorialStep pendingStep = TutorialStep.None;
    private bool isFading = false; 

    // references to hint UI elements 
    public GameObject rewindHint;
    public GameObject attackHint;
    public GameObject movementHint;
    public GameObject jumpHint; 
    public GameObject dashHint;
    public GameObject doubleJumpHint; 
    public GameObject spellHint; 
    public GameObject wallJumpHint;  

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
    bool spellCompleted = false; 
    bool wallJumpCompleted = false; 

    public Typewriter typewriter;
    public TextMeshProUGUI movementText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI rewindText;
    public TextMeshProUGUI jumpText;
    public TextMeshProUGUI dashText; 
    public TextMeshProUGUI doubleJumpText; 
    public TextMeshProUGUI spellText;
    public TextMeshProUGUI wallJumpText; 

    private string movementMessage;
    private string attackMessage;
    private string rewindMessage;
    private string jumpMessage; 
    private string dashMessage; 
    private string doubleJumpMessage; 
    private string spellMessage;
    private string wallJumpMessage; 

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
    [SerializeField] private float spellHintDuration = 5f;                 // temporary trigger time for spell hint
    [SerializeField] private float attackHintDuration = 5f;                // temporary trigger time for attack hint
    [SerializeField] private UIFollowPlayer rewindFollow; 
    [SerializeField] private float hintFadeDuration = 0.3f;

    // private bool attackEnemyCleared = false;      // flag to check if player has cleared the first enemy  
    [SerializeField] private Collider2D attackTutorialArea;
    [SerializeField] private LayerMask enemyLayer;
    private bool attackAreaActivated = false;

    [SerializeField] private GameObject attackGateBlocker; 

    void Start()
    {
        // messages are assigned to the corresponding UI text component 
        movementMessage = movementText.text;
        attackMessage = attackText.text;
        rewindMessage = rewindText.text;
        jumpMessage = jumpText.text; 
        dashMessage = dashText.text; 
        doubleJumpMessage = doubleJumpText.text;
        spellMessage = spellText.text;
        wallJumpMessage = wallJumpText.text;

        // player position is noted for checks (e.g. jump)
        lastPlayerPosition = player.transform.position;

        // track the start of the game, used for the idle check in the movement hint
        gameStartTime = Time.time; 
        
        DisableHints();

        currentStep = TutorialStep.None;
        moveCompleted = false;

        player.allowedActions = PlayerAction.Movement;
        player.FreezeMovement();
        SetStep(TutorialStep.Movement); 

         // subscribe to health change event to trigger the rewind hint
         if (playerHealth != null)
        {
            previousHealth = playerHealth.CurrentHealth;
            playerHealth.OnHealthChanged += HandleHealthChanged;
        }
        
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
       // CheckPlayerIdle();
        // CheckAttackDistance();

        /* if (currentStep == TutorialStep.Attack && attackAreaActivated && !attackCompleted)
        {
            if (!AreEnemiesRemainingInArea())
            {
                attackCompleted = true;
                attackAreaActivated = false;

                AllowAll();
                HideHint(attackHint);

                Debug.Log("Player attack tutorial complete");
            }
        } */ 
        
        // if all hints have been completed, tutorial completed 
        // TODO: add back attack completed once combat has been added
        if (moveCompleted && rewindCompleted && jumpCompleted && dashCompleted && spellCompleted)
        {
            SetStep(TutorialStep.Complete);
            Debug.Log("Tutorial Complete!");
            DisableHints();
        }
    }

    #region Fade Effect
    // ---- fade effects & showing and hiding hints ----
    void ShowHint(GameObject hint)
    {
        hint.SetActive(true);

        var cg = hint.GetComponent<CanvasGroup>();
        if (cg != null)        
            cg.alpha = 1f;
    }

    void HideHint(GameObject hint)
    {
        if (hint == null) return; 
        
        var cg = hint.GetComponent<CanvasGroup>(); 
        if (cg == null)
        {
            hint.SetActive(false); 
            OnHintHidden(hint);
            return; 
        }

        if (!isFading)
        {
            StartCoroutine(FadeOut(cg, hint)); 
        }
    }

    IEnumerator FadeOut(CanvasGroup cg, GameObject hint)
    {
        isFading = true;
        
        float start = cg.alpha; 
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / hintFadeDuration; 
            cg.alpha = Mathf.Lerp(start, 0f, t); 
            yield return null; 
        }

        cg.alpha = 0f; 
        hint.SetActive(false); 

        isFading = false; 
        OnHintHidden(hint);
    }

    void OnHintHidden(GameObject hint)
    {
        if (activeHint == hint)
        {
            activeHint = null; 
        }

        if (pendingStep != TutorialStep.None)
        {
            var step = pendingStep;
            pendingStep = TutorialStep.None; 
            SetStep(step);
        }
    }
    #endregion

    #region Freezing Player Movement
    // method only allows certain acitons to be performed by player
    void AllowOnly(PlayerAction action)
    {
        Debug.Log("AllowOnly called: " + action);
        player.allowedActions = action; 
        
        if (!action.HasFlag(PlayerAction.Movement))
        {
            player.FreezeMovement();
        }
    }

    // resets player movement so all actions are possible
    void AllowAll()
    {
        Debug.Log("AllowAll called");
        player.allowedActions = PlayerAction.All;
    }
    #endregion

    #region Attack Tutorial Area
    // TODO: implement this logic
    private bool AreEnemiesRemainingInArea()
    {
        Collider2D[] results = new Collider2D[10];
        
        int count = Physics2D.OverlapCollider(
            attackTutorialArea,
            new ContactFilter2D { layerMask = enemyLayer, useLayerMask = true },
            results
        );

        return count > 0; 
    }

    // don't let player progress to the dash tutorial until the slime has been killed
    public void UnlockCorridor()
    {
        if (attackGateBlocker != null)
        {
            attackGateBlocker.SetActive(false); 
            attackCompleted = true;
            AllowAll(); 
            Debug.Log("Attack gate unlocked"); 
        }
    }
    #endregion 

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

    public void TriggerAttackHint()
    {
        if (attackCompleted) return;
        if (currentStep == TutorialStep.Attack) return; 

        SetStep(TutorialStep.Attack); 

        // TODO: change so attack hint is hidden after the player hits the enemy for the first time
        CancelInvoke(nameof(HideAttackHint));
        Invoke(nameof(HideAttackHint), attackHintDuration);
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

        if (current == 3 && !rewindCompleted)
        {
            SetStep(TutorialStep.Rewind); 

            rewindFollow.SetTarget(player.transform);
            rewindFollow.enabled = true;
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
            Debug.Log("Hiding movement hint");
            moveCompleted = true; 
            HideHint(movementHint);
            AllowAll(); 
            Debug.Log("Player movement tutorial complete");
        }
    }

    public void TriggerSpellHint()
    {
        if (spellCompleted) return; 
        if (currentStep == TutorialStep.Spell) return; 

        SetStep(TutorialStep.Spell);

        // TODO: change so the spell hint is hidden after the a certain amount of time or after the player attacks the enemies
        CancelInvoke(nameof(HideSpellHint));
        Invoke(nameof(HideSpellHint), spellHintDuration);
    }

    public void TriggerWallJumpHint()
    {
        if (wallJumpCompleted) return; 
        if (currentStep == TutorialStep.WallJump) return;

        SetStep(TutorialStep.WallJump);
    }

    public void OnPlayerSpell()
    {
        if (currentStep == TutorialStep.Spell && !spellCompleted)
        {
            spellCompleted = true; 
            HideHint(spellHint);
            Debug.Log("Player spell tutorial completed");
        }
    }
    
    public void OnPlayerAttack()
    {
        if (currentStep == TutorialStep.Attack || !attackCompleted)
            return; 

        attackCompleted = true;
        HideHint(attackHint);
        AllowAll();
        Debug.Log("Player attack tutorial complete");
    }

    public void OnPlayerJump()
    {
        if (currentStep == TutorialStep.Jump && !jumpCompleted)
        {
            jumpCompleted = true;
            HideHint(jumpHint);
            Debug.Log("Player jump tutorial complete");
        }
    }

    public void OnPlayerRewind()
    {
        if (currentStep == TutorialStep.Rewind && !rewindCompleted)
        {
            rewindCompleted = true;
            rewindFollow.enabled = false;
            HideHint(rewindHint);
            Debug.Log("Player rewind tutorial complete");
        }
    }

    public void OnPlayerDash()
    {
        if (currentStep == TutorialStep.Dash && !dashCompleted)
        {
            dashCompleted = true;

            HideHint(dashHint);
            Debug.Log("Player dash tutorial completed"); 
        } 
    }

    public void OnPlayerWallJump()
    {
        if (currentStep == TutorialStep.WallJump && !wallJumpCompleted)
        {
            wallJumpCompleted = true; 
            HideHint(wallJumpHint); 
            Debug.Log("Player wall jump tutorial completed"); 
        }
    }

    public void OnPlayerDoubleJump()
    {
        if (currentStep == TutorialStep.DoubleJump)
        {
            HideHint(doubleJumpHint);
            Debug.Log("Player double jump completed"); 
        }
    }

    private void HideDoubleJumpHint()
    {
        HideHint(doubleJumpHint);
        Debug.Log("Double jump hint hidden");
    }

    private void HideSpellHint()
    {
        HideHint(spellHint);
        Debug.Log("Spell hint hidden");
    }

    private void HideAttackHint()
    {
        HideHint(attackHint);
        Debug.Log("Attack hint hidden");
    }

    // setting the current tutorial step and showing corresponding hint
    void SetStep(TutorialStep step)
    {   
        if (currentStep == step)
            return;

        if (activeHint != null || isFading)
        {
            pendingStep = step; 
            return; 
        }
         
        currentStep = step;

        // based on the current step, show the corresponding tutorial hint using the typewriter effect
        switch (step)
        {
            // TODO: testing to ensure these conditions are suitable for the tutorial, may need to be changed
            case TutorialStep.Movement:
                AllowOnly(PlayerAction.Movement);
                activeHint = movementHint;
                ShowHint(movementHint);
                movementText.text = movementMessage;
                typewriter.StartTyping(movementText);
                break;
            case TutorialStep.Attack:
                // attackAreaActivated = true; 
                AllowOnly(PlayerAction.Movement | PlayerAction.Attack);
                activeHint = attackHint;
                ShowHint(attackHint);
                attackText.text = attackMessage;
                typewriter.StartTyping(attackText);
                break;
            case TutorialStep.Rewind:
                activeHint = rewindHint;
                ShowHint(rewindHint);
                rewindText.text = rewindMessage;
                typewriter.StartTyping(rewindText);
                break;
            case TutorialStep.Jump:
                AllowOnly(PlayerAction.Jump | PlayerAction.Movement); 
                activeHint = jumpHint; 
                ShowHint(jumpHint);
                jumpText.text = jumpMessage; 
                typewriter.StartTyping(jumpText); 
                break; 
            case TutorialStep.Dash:
                AllowOnly(PlayerAction.Movement | PlayerAction.Dash);
                activeHint = dashHint;
                ShowHint(dashHint); 
                dashText.text = dashMessage;
                typewriter.StartTyping(dashText); 
                break;
            case TutorialStep.DoubleJump:
                AllowOnly(PlayerAction.Movement | PlayerAction.Jump);
                activeHint = doubleJumpHint; 
                ShowHint(doubleJumpHint); 
                doubleJumpText.text = doubleJumpMessage;
                typewriter.StartTyping(doubleJumpText); 
                break; 
            case TutorialStep.Spell:
                AllowOnly(PlayerAction.Movement | PlayerAction.Spell);
                activeHint = spellHint;
                ShowHint(spellHint);
                spellText.text = spellMessage;
                typewriter.StartTyping(spellText);
                break;
            case TutorialStep.WallJump:
                AllowOnly(PlayerAction.Movement | PlayerAction.Jump | PlayerAction.WallJump);
                activeHint = wallJumpHint; 
                ShowHint(wallJumpHint);
                wallJumpText.text = wallJumpMessage;
                typewriter.StartTyping(wallJumpText);
                break;
        }
    }

    // hints are disabled once tutorial is complete
    void DisableHints()
    {
        HideHint(rewindHint); 
        HideHint(attackHint);
        HideHint(movementHint);
        HideHint(jumpHint);
        HideHint(dashHint); 
        HideHint(doubleJumpHint); 
        HideHint(spellHint);
        HideHint(wallJumpHint);
    }
}
