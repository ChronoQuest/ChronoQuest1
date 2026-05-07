using UnityEngine;

// timeline signal target for the intro cutscene. exposes NotifyCutsceneEnd which
// the SignalReceiver's UnityEvent can call at the end of the timeline
public class CutsceneSignalReceiver : MonoBehaviour
{
    [Header("Cutscene Reference")]
    [Tooltip("IntroCutscene controller. Its OnCutsceneEnd() is called when the Signal fires.")]
    [SerializeField] private IntroCutscene introCutscene;

    // hook into the SignalReceiver's UnityEvent. forwards to IntroCutscene.OnCutsceneEnd
    public void NotifyCutsceneEnd()
    {
        if (introCutscene != null)
        {
            introCutscene.OnCutsceneEnd();
        }
        else
        {
            Debug.LogWarning("[CutsceneSignalReceiver] No IntroCutscene reference assigned, " +
                             "cutscene end signal ignored.");
        }
    }
}
