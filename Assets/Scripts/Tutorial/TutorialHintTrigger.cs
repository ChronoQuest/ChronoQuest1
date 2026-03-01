using UnityEngine;

public class TutorialHintTrigger : MonoBehaviour
{
    public enum HintType { 
        Jump, 
        Dash,
        DoubleJump, 
        JumpSuccess, 
        Spell, 
        WallJump,
        WallJumpSuccess, 
        Attack,
        HideAttack
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
            case HintType.DoubleJump:
                tutorial.TriggerDoubleJumpHint();
                break; 
            case HintType.JumpSuccess: 
                tutorial.OnJumpSucceeded(); 
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
        }
    }
}
