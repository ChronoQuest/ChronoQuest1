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
        SpikeRewind
    }

    [SerializeField] private HintType hintType; 
    [SerializeField] private TutorialManager tutorial; 
    private bool hasTriggered = false;          // bool variable to ensure hints only activate once
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
        if (hasTriggered) return;
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
                tutorial.TryTriggerFirstRewindHint();
                break;
            case HintType.SpikeRewind:
                tutorial.TriggerSpikeHint();
                break; 
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return; 
    }
}
