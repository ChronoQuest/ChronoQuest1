using UnityEngine;

namespace TimeRewind
{
    // destroys an IRewindable when a rewind goes past its spawn time, so the boss
    // (or any caster) can re-cast without leaving orphans behind
    public static class RewindSpawnLifecycle
    {
        // Grace window: applied states with a timestamp this close to (or earlier than) spawn
        // are treated as "rewound past spawn". The window exists because capture runs at a
        // finite rate, so the oldest captured state lands slightly after spawnTime.
        public const float DespawnGraceSeconds = 0.1f;

        public static bool TryDespawnIfRewoundBeforeSpawn(MonoBehaviour behaviour, float spawnTime, RewindState state)
        {
            if (state.Timestamp <= spawnTime + DespawnGraceSeconds)
            {
                Object.Destroy(behaviour.gameObject);
                return true;
            }
            return false;
        }
    }
}
