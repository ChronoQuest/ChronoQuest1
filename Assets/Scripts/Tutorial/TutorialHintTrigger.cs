using UnityEngine;

public class TutorialHintTrigger : MonoBehaviour
{
    // TODO: track how many times player enters the collider so it doesnt retrigger
    public enum HintType { Jump, Dash, DoubleJump, JumpSuccess, Spell }

    [SerializeField] private HintType hintType; 
    [SerializeField] private TutorialManager tutorial; 
    private bool hasTriggered = false; 

    private void OnTriggerEnter2D(Collider2D other)
    {
       if (hasTriggered) return;

       Debug.Log("Entered hint trigger: " + hintType); 
       if (!other.CompareTag("Player")) return; 

       hasTriggered = true; 

        // TODO : trigger the spell hint when the player enters the corridor, use box collider and set as trigger
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
        }
    }
}
