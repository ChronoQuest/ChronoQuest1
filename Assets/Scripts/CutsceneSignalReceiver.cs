using UnityEngine;

/// <summary>
/// Sits on the player GameObject and acts as the Timeline Signal target for
/// the intro cutscene. Timeline Signals fire UnityEvents on a SignalReceiver
/// component on the bound GameObject — this script exposes a single method,
/// <see cref="NotifyCutsceneEnd"/>, that the Signal's UnityEvent can call.
///
/// Wiring:
///  1. Add a SignalReceiver component to the player GameObject alongside
///     this script.
///  2. In the Timeline, add a Signal Track bound to the player and drop a
///     Signal Emitter at the end of the cutscene.
///  3. On the SignalReceiver, map that signal to
///     CutsceneSignalReceiver.NotifyCutsceneEnd().
/// </summary>
public class CutsceneSignalReceiver : MonoBehaviour
{
    [Header("Cutscene Reference")]
    [Tooltip("The IntroCutscene controller in the scene. Its OnCutsceneEnd() " +
             "will be called when the Timeline Signal fires.")]
    [SerializeField] private IntroCutscene introCutscene;

    /// <summary>
    /// Hook this into a SignalReceiver's UnityEvent entry. When the Timeline
    /// Signal fires at the end of the cutscene, this forwards the call to
    /// IntroCutscene.OnCutsceneEnd().
    /// </summary>
    public void NotifyCutsceneEnd()
    {
        if (introCutscene != null)
        {
            introCutscene.OnCutsceneEnd();
        }
        else
        {
            Debug.LogWarning("[CutsceneSignalReceiver] No IntroCutscene " +
                             "reference assigned — cutscene end signal " +
                             "ignored.");
        }
    }
}
