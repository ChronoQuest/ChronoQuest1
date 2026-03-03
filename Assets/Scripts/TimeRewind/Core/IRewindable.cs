using UnityEngine;

namespace TimeRewind
{
    public interface IRewindable
    {
        void OnStartRewind();
        void OnStopRewind();
        RewindState CaptureState();
        void ApplyState(RewindState state);
    }
}
