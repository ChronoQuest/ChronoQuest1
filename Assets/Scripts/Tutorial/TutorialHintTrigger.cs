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
        Dodge,
        SpikeRewind
    }

    [SerializeField] private HintType hintType; 
    [SerializeField] private TutorialManager tutorial; 
    private bool hasTriggered = false;          // bool variable to ensure hints only activate once

    private void OnTriggerEnter2D(Collider2D other)
    {
       if (hasTriggered) return;

       Debug.Log("Entered hint trigger: " + hintType); 
       if (!other.CompareTag("Player")) return; 

       hasTriggered = true; 

        switch (hintType)
        {
            case HintType.Jump:
                tutorial.TriggerJumpHint();
                break;
            case HintType.Dash:
                tutorial.TriggerDashHint(); 
                break;
            case HintType.Spell:
                tutorial.TriggerSpellHint(); 
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
                tutorial.SetInRewindRegion(true);
                break;
            case HintType.Dodge:
                tutorial.TriggerDodgeHint();
                break;
            case HintType.SpikeRewind:
                tutorial.TriggerSpikeHint();
                break; 
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return; 

        if (hintType == HintType.RewindRegion)
        {
            tutorial.SetInRewindRegion(false); 
        }
    }
}
