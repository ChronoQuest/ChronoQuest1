using UnityEngine;

public class TutorialHintTrigger : MonoBehaviour
{
    // TODO: track how many times player enters the collider so it doesnt retrigger
    public enum HintType { Jump, Dash }

    [SerializeField] private HintType hintType; 
    [SerializeField] private TutorialManager tutorial; 

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Entered hint trigger: " + hintType); 
        if (!other.CompareTag("Player")) return; 

        switch (hintType)
        {
            case HintType.Jump:
                tutorial.TriggerJumpHint();
                break;
            case HintType.Dash:
                tutorial.TriggerDashHint(); 
                break; 
        }
    }
}
