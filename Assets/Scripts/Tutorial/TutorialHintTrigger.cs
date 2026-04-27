using UnityEngine;

public class TutorialHintTrigger : MonoBehaviour
{
    public enum HintType { 
        Jump, 
        Dash,
        Spell, 
        WallJump,
        WallJumpSuccess, 
        Attack,
        HideAttack,
        RainSpell,
        RewindRegion,
        SpikeRewind,
        Mana
    }

    [SerializeField] private HintType hintType; 
    [SerializeField] private TutorialManager tutorial; 
    public bool hasTriggered = false;          // bool variable to ensure hints only activate once
    private PlayerPlatformer player;
    private bool waitingForSpellConditions = false;
    private void Update()
    {
        // Handles triggering spell hint
        if (!waitingForSpellConditions || hasTriggered || player == null) return;
        if (!player.isGrounded) return;
        player.ForceFaceRight();
        hasTriggered = true;
        waitingForSpellConditions = false;
        tutorial.TriggerSpellHint();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered)
        {
            if (hintType == HintType.Jump)
            {
                tutorial.OnJumpTriggerHitDuringRewind();
            }
            return;
        }
        if (!other.CompareTag("Player")) return;

        player = other.GetComponent<PlayerPlatformer>();

        Debug.Log("Entered hint trigger: " + hintType);

        if (hintType == HintType.Spell)
        {
            waitingForSpellConditions = true;
            return;
        }

        hasTriggered = true;

        switch (hintType)
        {
            case HintType.Jump:
                tutorial.TriggerJumpHint();
                break;
            case HintType.Dash:
                tutorial.TriggerDashHint(); 
                break;
            case HintType.WallJump:
                tutorial.TriggerWallJumpHint();
                break; 
            case HintType.WallJumpSuccess:
                tutorial.OnPlayerWallJump();
                break;
            case HintType.Attack:
                tutorial.TriggerAttackHint();
                break;
            case HintType.HideAttack:
                tutorial.HideAttackHint();
                break; 
            case HintType.RainSpell:
                tutorial.TriggerRainSpell(); 
                break;
            case HintType.RewindRegion:
                tutorial.TryTriggerRewindHint();
                break;
            case HintType.SpikeRewind:
                tutorial.TriggerSpikeHint();
                break;
            case HintType.Mana:
                tutorial.TriggerManaHint();
                break;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return; 
    }
}
