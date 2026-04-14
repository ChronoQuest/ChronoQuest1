using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; 
using TMPro;
using System.Collections; 
using System.Collections.Generic; 
using TimeRewind;

public class TutorialManager : MonoBehaviour
{
    #region Enums and State
    public enum TutorialStep
    {
        None,
        Movement,
        Dash, 
        Jump,
        Attack,
        Rewind, 
        Spell,
        Foresight,
        WallJump, 
        RainSpell,
        SpikeHint, 
        Complete
    }

    public TutorialStep currentStep = TutorialStep.None; 
    #endregion

    #region Serialized Fields and References
    private GameObject activeHint = null;
    private TutorialStep pendingStep = TutorialStep.None;
    private bool isFading = false; 
    private float baseOrthoSize;

    // references to hint UI elements 
    public GameObject rewindHint;
    public GameObject attackHint;
    public GameObject movementHint;
    public GameObject jumpHint; 
    public GameObject dashHint;
    public GameObject spellHint; 
    public GameObject foresightHint;
    public GameObject wallJumpHint;  
    public GameObject rainSpellHint;
    public GameObject spikeHint; 
    // public GameObject spotlight; 

    // references to movement and health systems to use for triggering hint pop-ups 
    public PlayerPlatformer player;
    public PlayerHealth playerHealth;

    bool moveCompleted = false;
    bool attackCompleted = false;
    public bool rewindCompleted = false;
    bool jumpCompleted = false;
    bool dashCompleted = false;
    bool spellCompleted = false; 
    bool foresightCompleted = false;
    bool wallJumpCompleted = false; 
    bool rainSpellCompleted = false; 
    private bool pendingForesightAfterRewind = false;
    private RewindMusicController musicController;
    bool spikeCompleted = false;
    public SkeletonArcher tutorialSkeleton;
    public BatEnemyAI tutorialBat;

    public Typewriter typewriter;
    public TextMeshProUGUI movementText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI rewindText;
    public TextMeshProUGUI jumpText;
    public TextMeshProUGUI dashText; 
    public TextMeshProUGUI spellText;
    public TextMeshProUGUI foresightText;
    public TextMeshProUGUI wallJumpText; 
    public TextMeshProUGUI rainSpellText; 
    public TextMeshProUGUI spikeText; 

    private string movementMessage;
    private string attackMessage;
    private string rewindMessage;
    private string jumpMessage; 
    private string dashMessage;   
    private string spellMessage;
    private string foresightMessage;
    private string wallJumpMessage; 
    private string rainSpellMessage; 
    private string spikeMessage; 

    Vector2 lastPlayerPosition; 
    private float gameStartTime; 

    private int hitCount = 0; 
    private int previousHealth;
    private bool jumpAttempted = false;         
    private bool jumpSucceeded = false; 
    private bool firstSpellCast = false;
    private float spellCastTime;
    [SerializeField] private float attackHintDuration = 2f;                // temporary trigger time for attack hint
    [SerializeField] private UIFollowPlayer rewindFollow; 
    [SerializeField] private float hintFadeDuration = 0.3f;

    // private bool attackEnemyCleared = false;      // flag to check if player has cleared the first enemy  
    [SerializeField] private Collider2D attackTutorialArea;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private GameObject spellBlocker;
    private float previousZoom;
    private CameraFollow2D cam; 
    private bool inRewindArea = false; 
    private bool rewindZoomApplied = false; 
    private bool isWaitingToCompleteForesight = false;

    // zoom fixes
    private bool tempZoomActive = false;
    private float tempZoomPrevious; 

    // unlock system
    private PlayerAction unlockedActions = PlayerAction.None; 


    // dictionaries for freezing enemies during rewind tutorial hint
    Dictionary<Rigidbody2D, Vector2> slowedBodies = new Dictionary<Rigidbody2D, Vector2>();
    Dictionary<BatEnemyAI, float> slowedBats = new Dictionary<BatEnemyAI, float>();
    Dictionary<Animator, float> slowedAnimators = new Dictionary<Animator, float>();
    #endregion

    void Start()
    {
        // messages are assigned to the corresponding UI text component 
        movementMessage = movementText.text;
        attackMessage = attackText.text;
        rewindMessage = rewindText.text;
        jumpMessage = jumpText.text; 
        dashMessage = dashText.text; 
        spellMessage = spellText.text;
        foresightMessage = foresightText.text;
        wallJumpMessage = wallJumpText.text;
        rainSpellMessage = rainSpellText.text; 
        spikeMessage = spikeText.text; 

        // player position is noted for checks (e.g. jump)
        lastPlayerPosition = player.transform.position;
        player.allowedActions = PlayerAction.None; 

        cam = Camera.main.GetComponent<CameraFollow2D>();
        baseOrthoSize = Camera.main.orthographicSize;

        // track the start of the game, used for the idle check in the movement hint
        gameStartTime = Time.time; 

        // Start tutorial music if a music controller exists in the scene.
        musicController = FindFirstObjectByType<RewindMusicController>();
        if (musicController != null)
        {
            musicController.PlayTutorialMusic();
        }
        
        DisableHints();

        currentStep = TutorialStep.None;

        if (SceneManager.GetActiveScene().name == "GameScene")
        {
            player.allowedActions = PlayerAction.Movement; 
            SetStep(TutorialStep.Movement); 
        }

        if (SceneManager.GetActiveScene().name == "GameScene_2")
        {
            player.allowedActions = PlayerAction.SpikeHint;
            SetStep(TutorialStep.SpikeHint);
        }

        moveCompleted = false;

        // subscribe to health change event to trigger the rewind hint
         if (playerHealth != null)
        {
            previousHealth = playerHealth.CurrentHealth;
            playerHealth.OnHealthChanged += HandleHealthChanged;
        }

        if (TimeRewind.TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.OnRewindStop += HandleRewindStopped;
            TimeRewind.TimeRewindManager.Instance.OnRewindStart += HandleRewindStarted;
        }
    }

    private IEnumerator WaitAndCompleteForesight(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        OnPlayerForesightDemonstrated();
        isWaitingToCompleteForesight = false; // Reset the flag just in case
    }
    void Update()
    {   
        // Advance from foresight hint if we detect input
        if (currentStep == TutorialStep.Foresight && !foresightCompleted && !isWaitingToCompleteForesight)
        {
            isWaitingToCompleteForesight = true;
            StartCoroutine(WaitAndCompleteForesight(2f));
        }

        // if all hints have been completed, tutorial completed 
        if (moveCompleted && rewindCompleted && jumpCompleted && dashCompleted && spellCompleted && foresightCompleted && attackCompleted && wallJumpCompleted && rainSpellCompleted)
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
            t += Time.unscaledDeltaTime / hintFadeDuration; 
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

    #region Zoom Methods
    void ApplyTempZoom(float amount)
    {
        Debug.Log("Applying Zoom"); 
        
        if (cam == null) return; 

        if (!tempZoomActive)
        {
            tempZoomPrevious = Camera.main.orthographicSize; 
            tempZoomActive = true; 
        } 

        cam.SetZoom(tempZoomPrevious - amount); 
    }

    void RestoreTempZoom()
    {
        Debug.Log("Restoring Zoom"); 

        if (cam == null || !tempZoomActive) return; 

        cam.SetZoom(tempZoomPrevious); 
        tempZoomActive = false; 
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

    void UnlockAction(PlayerAction action)
    {
        unlockedActions |= action; 
        player.allowedActions |= unlockedActions; 
    }

    void AllowAll()
    {
        player.allowedActions = PlayerAction.All;
    }

    // slows enemies when the rewind hint is triggered
    void SlowingEnemies(float radius, float slowMultiplier)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);

        foreach (var hit in hits)
        {
            if (((1 << hit.gameObject.layer) & enemyLayer) == 0)
                continue;

            // if (tutorialBat != null && hit.GetComponentInParent<BatEnemyAI>() == tutorialBat)
            // {
            //     continue;
            // }

            Animator anim = hit.GetComponentInParent<Animator>(); 
            if (anim != null && !slowedAnimators.ContainsKey(anim))
            {
                slowedAnimators.Add(anim, anim.speed);
                anim.speed *= slowMultiplier; 
            }

            BatEnemyAI bat = hit.GetComponent<BatEnemyAI>();
            if (bat != null)
            {
                if (!slowedBats.ContainsKey(bat))
                {
                    slowedBats.Add(bat, bat.moveSpeed);
                    bat.moveSpeed *= slowMultiplier; 

                    if (slowMultiplier <= 0f)
                    {
                        bat.isTutorialPaused = true;                        
                        Rigidbody2D batRb = bat.GetComponent<Rigidbody2D>();
                        if (batRb != null) batRb.linearVelocity = Vector2.zero; 
                    }
                }
                continue; 
            }
            
            Rigidbody2D rb = hit.attachedRigidbody;
            if (rb == null) continue;

            if (!slowedBodies.ContainsKey(rb))
            {
                slowedBodies.Add(rb, rb.linearVelocity);
                rb.linearVelocity *= slowMultiplier;
                rb.angularVelocity *= slowMultiplier; 
            }
        }
    }

    // restores enemy speed after rewind hint is completed
    void RestoreEnemies()
    {
        foreach (var pair in slowedBodies)
        {
            if (pair.Key != null)
            {
                pair.Key.linearVelocity = pair.Value; 
            }
        }
        slowedBodies.Clear(); 

        foreach (var pair in slowedBats)
        {
            if (pair.Key != null)
            {
                pair.Key.moveSpeed = pair.Value; 
                pair.Key.isTutorialPaused = false;
            }
        }
        slowedBats.Clear(); 

        foreach (var pair in slowedAnimators)
        {
            if (pair.Key != null)
            {
                pair.Key.speed = pair.Value; 
            }
        }
        slowedAnimators.Clear(); 
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
    #endregion 

    #region Tutorial Triggers
    public void TriggerJumpHint()
    {
        if (jumpCompleted) return;
        if (currentStep == TutorialStep.Jump) return; 

        jumpAttempted = true; 
        jumpSucceeded = false; 
        SetStep(TutorialStep.Jump); 
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

        CancelInvoke(nameof(HideAttackHint));
        Invoke(nameof(HideAttackHint), attackHintDuration); 
    }

    public void TriggerSpellHint()
    {
        if (spellCompleted) return; 
        if (currentStep == TutorialStep.Spell) return; 

        SetStep(TutorialStep.Spell);
    }
    public void TriggerForesightHint()
    {
        if (foresightCompleted) return; 
        if (currentStep == TutorialStep.Foresight) return; 

        SetStep(TutorialStep.Foresight);
    }

    public void TriggerWallJumpHint()
    {
        if (wallJumpCompleted) return; 
        if (currentStep == TutorialStep.WallJump) return;

        SetStep(TutorialStep.WallJump);
    }

    public void TriggerRainSpell()
    {
        if (rainSpellCompleted) return;
        if (currentStep == TutorialStep.RainSpell) return;

        SetStep(TutorialStep.RainSpell);
    }

    public void TriggerSpikeHint()
    {
        if (spikeCompleted) return; 
        if (currentStep == TutorialStep.SpikeHint) return; 

        SetStep(TutorialStep.SpikeHint); 
    }

    // handles when the health changes, triggers either the rewind or dash hint 
    private void HandleHealthChanged(int current, int max)
    {
       if (current >= previousHealth)
        {
            previousHealth = current; 
            return; 
        }

        /* if (current < previousHealth && !rewindCompleted && inRewindArea)
        {
            TryTriggerRewindHint(); 
        } */ 
       
       previousHealth = current; 
    }

    // keeps track of the hit count and use it to show the dash hint 
    private void HandlePlayerDamaged()
    {
        hitCount++; 
    }

    private void HandleRewindStarted()
    {
        //Time.timeScale = 1f;
        if (currentStep != TutorialStep.Rewind && currentStep != TutorialStep.SpikeHint)
            return;  
        
        Debug.Log("REWIND STARTED");

        if (currentStep == TutorialStep.Rewind)
        {
            RestoreEnemies();
            AllowAll();

            OnPlayerRewind();
        }
        
        if (currentStep == TutorialStep.SpikeHint)
        {
            OnPlayerSpike(); 
        }
    }
    private void HandleRewindStopped()
    {
        if (pendingForesightAfterRewind)
        {
            pendingForesightAfterRewind = false;

            if (tutorialBat != null)
            {
                ForesightSystem foresight = tutorialBat.GetComponent<ForesightSystem>();
                if (foresight != null)
                {
                    foresight.ForceInstantForesight();
                }
            }

            TriggerForesightHint();
        }
    }
    #endregion

    #region Tutorial Completion Functions
    // the following functions "OnPlayer..." mark tutorial steps as completed on certain player actions
    public void OnPlayerMoved()
    {   
        if (currentStep == TutorialStep.Movement && !moveCompleted)
        {
            moveCompleted = true; 
            HideHint(movementHint);
            Debug.Log("Player movement tutorial complete");
        }
    }

    public void SetInRewindRegion(bool value)
    {
        inRewindArea = value; 

        if (value && playerHealth.CurrentHealth < playerHealth.MaxHealth && !rewindCompleted)
        {
            TryTriggerRewindHint();
        }
    }

    public void TryTriggerRewindHint()
    {
        // if (!inRewindArea) return; 
        if (rewindCompleted) return;
        if (currentStep == TutorialStep.Rewind) return;

        pendingStep = TutorialStep.None;  

        SetStep(TutorialStep.Rewind);
        //Time.timeScale = 0f;

        rewindFollow.SetTarget(player.transform); 
        rewindFollow.enabled = true;
    }

    public void OnPlayerSpell()
    {
        if (currentStep == TutorialStep.Spell && !spellCompleted)
        {
            spellCompleted = true;
            HideHint(spellHint);

            Debug.Log("Player spell tutorial completed");
            DataCollectionService.Instance?.RecordTutorialStepCompleted();
            AllowAll();

            spellCastTime = Time.time;
            if (spellBlocker != null) spellBlocker.SetActive(false);
            if (tutorialSkeleton != null) tutorialSkeleton.canShoot = true;
        }
    }
    public void OnPlayerForesightDemonstrated()
    {
        if (currentStep == TutorialStep.Foresight && !foresightCompleted)
        {
            foresightCompleted = true; 
            HideHint(foresightHint);
            RestoreEnemies();
            AllowAll(); 
            Debug.Log("Player foresight tutorial completed");
            player.GetComponent<PlayerRewindController>()?.SetRewindBlocked(false);
            DataCollectionService.Instance?.RecordTutorialStepCompleted();
            if (tutorialBat != null)
            {
                ForesightSystem batForesight = tutorialBat.GetComponent<ForesightSystem>();
                if (batForesight != null)
                {
                    batForesight.enabled = false; 
                }
            }
        }
    }
    
    public void OnPlayerAttack()
    {
        if (currentStep == TutorialStep.Attack || attackCompleted)
            return; 

        attackCompleted = true;
        HideHint(attackHint);
        AllowAll();
        Debug.Log("Player attack tutorial complete");
        DataCollectionService.Instance?.RecordTutorialStepCompleted();
    }

    public void OnPlayerJump()
    {
        if (currentStep == TutorialStep.Jump && !jumpCompleted)
        {
            jumpCompleted = true;
            HideHint(jumpHint);
            AllowAll(); 
            Debug.Log("Player jump tutorial complete");
            DataCollectionService.Instance?.RecordTutorialStepCompleted();
        }
    }

    public void OnPlayerRewind()
    {
        if (currentStep == TutorialStep.Rewind && !rewindCompleted)
        {
            rewindCompleted = true; 

            RestoreTempZoom(); 

            rewindZoomApplied = false;
            rewindFollow.enabled = false;
            HideHint(rewindHint);
            AllowAll();

            Debug.Log("Player rewind tutorial complete");
            DataCollectionService.Instance?.RecordTutorialStepCompleted();

            pendingForesightAfterRewind = true;
        }
    }

    // void PauseForFailedRewind()
    // {
    //     StartCoroutine(ExecuteFailedRewindPause());
    // }

    // private IEnumerator ExecuteFailedRewindPause()
    // {
    //     yield return null; 

    //     var rewindManager = TimeRewind.TimeRewindManager.Instance;
    //     if (rewindManager != null)
    //     {
    //         rewindManager.CancelTimeOverrides();
    //     }

    //     Time.timeScale = 0f;
    // }

    public void OnPlayerDash()
    {
        if (currentStep == TutorialStep.Dash && !dashCompleted)
        {
            dashCompleted = true;
            HideHint(dashHint);
            Debug.Log("Player dash tutorial completed"); 
            DataCollectionService.Instance?.RecordTutorialStepCompleted();
        } 
    }

    public void OnPlayerWallJump()
    {
        if (currentStep == TutorialStep.WallJump && !wallJumpCompleted)
        {
            wallJumpCompleted = true; 
            HideHint(wallJumpHint); 
            RestoreTempZoom(); 
            Debug.Log("Player wall jump tutorial completed"); 
            DataCollectionService.Instance?.RecordTutorialStepCompleted();
        }
    }

    public void OnPlayerRainSpell()
    {
        if (currentStep == TutorialStep.RainSpell && !rainSpellCompleted)
        {
            rainSpellCompleted = true;
            HideHint(rainSpellHint);
            AllowAll(); 
            Debug.Log("Player rain spell tutorial completed");
            DataCollectionService.Instance?.RecordTutorialStepCompleted();
        }
    }

    public void OnPlayerSpike()
    {
        if (currentStep == TutorialStep.SpikeHint && !spikeCompleted)
        {
            spikeCompleted = true; 
            HideHint(spikeHint);
            AllowAll(); 
            Debug.Log("Player spike tutorial completed"); 
        }
    }

    public void HideAttackHint()
    {
        CancelInvoke(nameof(HideAttackHint));
        HideHint(attackHint);
    }
    #endregion

    #region Step Logic
    // setting the current tutorial step and showing corresponding hint
    void SetStep(TutorialStep step)
    {   
        if (currentStep == step)
            return;

        // tempZoomActive = false; 

        if (activeHint != null || isFading)
        {
            if (pendingStep != step)   
                pendingStep = step;
        }
         
        currentStep = step;

        // based on the current step, show the corresponding tutorial hint using the typewriter effect
        switch (step)
        {
            case TutorialStep.Movement:
                AllowOnly(PlayerAction.Movement);
                activeHint = movementHint;
                ShowHint(movementHint);
                movementText.text = movementMessage;
                typewriter.StartTyping(movementText);
                break;
            case TutorialStep.Attack: 
                AllowOnly(PlayerAction.Movement | PlayerAction.Attack);
                activeHint = attackHint;
                ShowHint(attackHint);
                attackText.text = attackMessage;
                typewriter.StartTyping(attackText);
                break;
            case TutorialStep.Rewind:
                AllowOnly(PlayerAction.Rewind);
                SlowingEnemies(20f, 0.15f); 
                ApplyTempZoom(1.5f); 
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
                AllowOnly(PlayerAction.Dash | PlayerAction.Movement);
                activeHint = dashHint;
                ShowHint(dashHint); 
                dashText.text = dashMessage;
                typewriter.StartTyping(dashText); 
                break;
            case TutorialStep.Spell:
                AllowOnly(PlayerAction.Movement | PlayerAction.Spell | PlayerAction.Jump | PlayerAction.Dash);
                activeHint = spellHint;
                ShowHint(spellHint);
                spellText.text = spellMessage;
                typewriter.StartTyping(spellText);
                break;
            case TutorialStep.Foresight:
                //AllowOnly(PlayerAction.None); // Freeze the player to force them to read it
                //SlowingEnemies(20f, 0.15f); // Completely freeze enemies while reading
                //ApplyTempZoom(1.5f);
                AllowAll();
                activeHint = foresightHint;
                ShowHint(foresightHint);
                foresightText.text = foresightMessage;
                typewriter.StartTyping(foresightText);
                break;
            case TutorialStep.WallJump:
                AllowOnly(PlayerAction.Movement | PlayerAction.Jump | PlayerAction.WallJump); 
                
                if (rewindCompleted)
                {
                    ApplyTempZoom(3f); 
                } 

                activeHint = wallJumpHint; 
                ShowHint(wallJumpHint);
                wallJumpText.text = wallJumpMessage;
                typewriter.StartTyping(wallJumpText);
                break;
            case TutorialStep.RainSpell:
                AllowOnly(PlayerAction.Movement | PlayerAction.Attack | PlayerAction.RainSpell); 
                activeHint = rainSpellHint;
                ShowHint(rainSpellHint);
                rainSpellText.text = rainSpellMessage; 
                typewriter.StartTyping(rainSpellText); 
                break;
            case TutorialStep.SpikeHint:
                AllowOnly(PlayerAction.Rewind | PlayerAction.Movement);
                activeHint = spikeHint; 
                ShowHint(spikeHint); 
                spikeText.text = spikeMessage; 
                typewriter.StartTyping(spikeText); 
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
        HideHint(spellHint);
        HideHint(foresightHint);
        HideHint(wallJumpHint);
        HideHint(spikeHint); 
    }
    #endregion
}