using System.Collections.Generic;
using UnityEngine;

namespace TimeRewind
{
    [System.Serializable]
    public struct RewindState
    {
        public float Timestamp;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 Velocity;
        public float AngularVelocity;
        public int Health;
        public int AnimatorStateHash;
        public float AnimatorNormalizedTime;
        public Dictionary<string, object> CustomData;

        public static RewindState Create(Vector3 position, Quaternion rotation, float timestamp)
        {
            return new RewindState
            {
                Timestamp = timestamp,
                Position = position,
                Rotation = rotation,
                Velocity = Vector2.zero,
                AngularVelocity = 0f,
                Health = 0,
                AnimatorStateHash = 0,
                AnimatorNormalizedTime = 0f,
                CustomData = null
            };
        }

        public static RewindState CreateWithPhysics(
            Vector3 position, 
            Quaternion rotation, 
            Vector2 velocity, 
            float angularVelocity,
            float timestamp)
        {
            return new RewindState
            {
                Timestamp = timestamp,
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
                AngularVelocity = angularVelocity,
                AnimatorStateHash = 0,
                AnimatorNormalizedTime = 0f,
                CustomData = null
            };
        }

        public static RewindState Lerp(RewindState a, RewindState b, float t)
        {
            var result = new RewindState
            {
                Timestamp = Mathf.Lerp(a.Timestamp, b.Timestamp, t),
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
                Velocity = Vector2.Lerp(a.Velocity, b.Velocity, t),
                AngularVelocity = Mathf.Lerp(a.AngularVelocity, b.AngularVelocity, t),
                Health = Mathf.RoundToInt(Mathf.Lerp(a.Health, b.Health, t)),
                AnimatorStateHash = t < 0.5f ? a.AnimatorStateHash : b.AnimatorStateHash,
                AnimatorNormalizedTime = Mathf.Lerp(a.AnimatorNormalizedTime, b.AnimatorNormalizedTime, t),
                CustomData = null
            };

            if (a.CustomData != null || b.CustomData != null)
            {
                // Player
                result.SetCustomData("VerticalNormal", Mathf.Lerp(a.GetCustomData<float>("VerticalNormal", 0f), b.GetCustomData<float>("VerticalNormal", 0f), t));
                result.SetCustomData("Speed", Mathf.Lerp(a.GetCustomData<float>("Speed", 0f), b.GetCustomData<float>("Speed", 0f), t));
                result.SetCustomData("isGrounded", t < 0.5f ? a.GetCustomData<bool>("isGrounded", true) : b.GetCustomData<bool>("isGrounded", true));
                result.SetCustomData("isWallSliding", t < 0.5f ? a.GetCustomData<bool>("isWallSliding", false) : b.GetCustomData<bool>("isWallSliding", false));
                result.SetCustomData("IsFlipped", t < 0.5f ? a.GetCustomData<bool>("IsFlipped", false) : b.GetCustomData<bool>("IsFlipped", false));

                // FlyingEnemy
                result.SetCustomData("FacingDirection", t < 0.5f ? a.GetCustomData<Vector3>("FacingDirection", Vector3.one) : b.GetCustomData<Vector3>("FacingDirection", Vector3.one));
                result.SetCustomData("EnemyState", t < 0.5f ? a.GetCustomData<int>("EnemyState", 0) : b.GetCustomData<int>("EnemyState", 0));
                result.SetCustomData("DetectRange", Mathf.Lerp(a.GetCustomData<float>("DetectRange", 10f), b.GetCustomData<float>("DetectRange", 10f), t));
                result.SetCustomData("spriteVisible", a.GetCustomData<bool>("spriteVisible"));
                result.SetCustomData("colEnabled", a.GetCustomData<bool>("colEnabled"));

                // SlimeEnemy
                result.SetCustomData("flipX", t < 0.5f ? a.GetCustomData<bool>("flipX", false) : b.GetCustomData<bool>("flipX", false));
                result.SetCustomData("midJump", t < 0.5f ? a.GetCustomData<bool>("midJump", false) : b.GetCustomData<bool>("midJump", false));
                result.SetCustomData("frameIndex", Mathf.RoundToInt(Mathf.Lerp(a.GetCustomData<int>("frameIndex", 0), b.GetCustomData<int>("frameIndex", 0), t)));

                // Player cast layer
                result.SetCustomData("Layer1Hash", t < 0.5f ? a.GetCustomData<int>("Layer1Hash", 0) : b.GetCustomData<int>("Layer1Hash", 0));
                result.SetCustomData("Layer1Time", Mathf.Lerp(a.GetCustomData<float>("Layer1Time", 0f), b.GetCustomData<float>("Layer1Time", 0f), t));

                // SpellProjectile
                result.SetCustomData("hasHit", t < 0.5f ? a.GetCustomData<bool>("hasHit", false) : b.GetCustomData<bool>("hasHit", false));
                result.SetCustomData("lifetime", Mathf.Lerp(a.GetCustomData<float>("lifetime", 0f), b.GetCustomData<float>("lifetime", 0f), t));

                // NecromancerSpell / ArrowProjectile
                result.SetCustomData("visible", t < 0.5f ? a.GetCustomData<bool>("visible", false) : b.GetCustomData<bool>("visible", false));
                result.SetCustomData("isActive", t < 0.5f ? a.GetCustomData<bool>("isActive", false) : b.GetCustomData<bool>("isActive", false));
                result.SetCustomData("elapsedLifetime", Mathf.Lerp(a.GetCustomData<float>("elapsedLifetime", 0f), b.GetCustomData<float>("elapsedLifetime", 0f), t));
                // Necromancer
                result.SetCustomData("lastReviveTime",  Mathf.Lerp(a.GetCustomData<float>("lastReviveTime", 0f), b.GetCustomData<float>("lastReviveTime", 0f), t));
                result.SetCustomData("isReviving",      t < 0.5f ? a.GetCustomData<bool>("isReviving", false) : b.GetCustomData<bool>("isReviving", false));
                result.SetCustomData("reviveTimer",     Mathf.Lerp(a.GetCustomData<float>("reviveTimer", 0f), b.GetCustomData<float>("reviveTimer", 0f), t));
                result.SetCustomData("lastAttackTime",  Mathf.Lerp(a.GetCustomData<float>("lastAttackTime", 0f), b.GetCustomData<float>("lastAttackTime", 0f), t));
                result.SetCustomData("isAttacking",     t < 0.5f ? a.GetCustomData<bool>("isAttacking", false) : b.GetCustomData<bool>("isAttacking", false));
                result.SetCustomData("attackTimer",     Mathf.Lerp(a.GetCustomData<float>("attackTimer", 0f), b.GetCustomData<float>("attackTimer", 0f), t));
                result.SetCustomData("localScale",      t < 0.5f ? a.GetCustomData<Vector3>("localScale", Vector3.one) : b.GetCustomData<Vector3>("localScale", Vector3.one));

                // Necromancer navigation state
                result.SetCustomData("currentWaypointIndex", t < 0.5f ? a.GetCustomData<int>("currentWaypointIndex", 0) : b.GetCustomData<int>("currentWaypointIndex", 0));
                result.SetCustomData("finalStand",           t < 0.5f ? a.GetCustomData<bool>("finalStand", false) : b.GetCustomData<bool>("finalStand", false));
                result.SetCustomData("stuckTimer",           Mathf.Lerp(a.GetCustomData<float>("stuckTimer", 0f), b.GetCustomData<float>("stuckTimer", 0f), t));
                result.SetCustomData("lastCheckedPos",       Vector2.Lerp(a.GetCustomData<Vector2>("lastCheckedPos", Vector2.zero), b.GetCustomData<Vector2>("lastCheckedPos", Vector2.zero), t));
                result.SetCustomData("corneredUntilTime",    Mathf.Lerp(a.GetCustomData<float>("corneredUntilTime", 0f), b.GetCustomData<float>("corneredUntilTime", 0f), t));
                result.SetCustomData("isJumping",            t < 0.5f ? a.GetCustomData<bool>("isJumping", false) : b.GetCustomData<bool>("isJumping", false));
                result.SetCustomData("lastJumpTime",         Mathf.Lerp(a.GetCustomData<float>("lastJumpTime", -99f), b.GetCustomData<float>("lastJumpTime", -99f), t));
                result.SetCustomData("seekLaunchDir",        t < 0.5f ? a.GetCustomData<float>("seekLaunchDir", 0f) : b.GetCustomData<float>("seekLaunchDir", 0f));
                result.SetCustomData("jumpTargetSurfaceY",   Mathf.Lerp(a.GetCustomData<float>("jumpTargetSurfaceY", float.MinValue), b.GetCustomData<float>("jumpTargetSurfaceY", float.MinValue), t));
                result.SetCustomData("jumpMoveDir",          t < 0.5f ? a.GetCustomData<float>("jumpMoveDir", 0f) : b.GetCustomData<float>("jumpMoveDir", 0f));
                result.SetCustomData("nextFleeCastTime",     Mathf.Lerp(a.GetCustomData<float>("nextFleeCastTime", 0f), b.GetCustomData<float>("nextFleeCastTime", 0f), t));
                result.SetCustomData("waypointFleeTimer",    Mathf.Lerp(a.GetCustomData<float>("waypointFleeTimer", 0f), b.GetCustomData<float>("waypointFleeTimer", 0f), t));
                // --- NECROMANCER DYNAMIC MINION TIMERS ---
                float[] aTimers = a.GetCustomData<float[]>("minionDeadTimers");
                float[] bTimers = b.GetCustomData<float[]>("minionDeadTimers");

                // Smoothly interpolate the time each minion has been dead
                if (aTimers != null && bTimers != null && aTimers.Length == bTimers.Length) 
                {
                    float[] lerpedTimers = new float[aTimers.Length];
                    for(int i = 0; i < aTimers.Length; i++) 
                    {
                        lerpedTimers[i] = Mathf.Lerp(aTimers[i], bTimers[i], t);
                    }
                    result.SetCustomData("minionDeadTimers", lerpedTimers);
                } 
                else 
                {
                    // Fallback if array lengths mismatch
                    result.SetCustomData("minionDeadTimers", t < 0.5f ? aTimers : bTimers);
                }
                // --- UNIQUE SKELETON DATA ---
                result.SetCustomData("isDying", t < 0.5f ? a.GetCustomData<bool>("isDying", false) : b.GetCustomData<bool>("isDying", false));
                result.SetCustomData("hasHitFloor", t < 0.5f ? a.GetCustomData<bool>("hasHitFloor", false) : b.GetCustomData<bool>("hasHitFloor", false));
                // slime data
                result.SetCustomData("spriteEnabled", t < 0.5f ? a.GetCustomData<bool>("spriteEnabled", false) : b.GetCustomData<bool>("spriteEnabled", false));
                result.SetCustomData("isLaunched", t < 0.5f ? a.GetCustomData<bool>("isLaunched", false) : b.GetCustomData<bool>("isLaunched", false));
                // PlayerSpellSystem
                result.SetCustomData("nextFireTime", Mathf.Lerp(a.GetCustomData<float>("nextFireTime", 0f), b.GetCustomData<float>("nextFireTime", 0f), t));

                // Boss projectiles
                result.SetCustomData("IsActive", t < 0.5f ? a.GetCustomData<bool>("IsActive", true) : b.GetCustomData<bool>("IsActive", true));
                result.SetCustomData("GrowSize", Mathf.Lerp(a.GetCustomData<float>("GrowSize", 1f), b.GetCustomData<float>("GrowSize", 1f), t));
                result.SetCustomData("Age", Mathf.Lerp(a.GetCustomData<float>("Age", 0f), b.GetCustomData<float>("Age", 0f), t));
                result.SetCustomData("FullSize", t < 0.5f ? a.GetCustomData<bool>("FullSize", false) : b.GetCustomData<bool>("FullSize", false));
                result.SetCustomData("IsEnding", t < 0.5f ? a.GetCustomData<bool>("IsEnding", false) : b.GetCustomData<bool>("IsEnding", false));
                result.SetCustomData("IsKinematic", t < 0.5f ? a.GetCustomData<bool>("IsKinematic", false) : b.GetCustomData<bool>("IsKinematic", false));
                result.SetCustomData("ColOffset", t < 0.5f ? a.GetCustomData<Vector2>("ColOffset", Vector2.zero) : b.GetCustomData<Vector2>("ColOffset", Vector2.zero));

                // Homing Fireball
                result.SetCustomData("LockedDir", Vector2.Lerp(a.GetCustomData<Vector2>("LockedDir", Vector2.zero), b.GetCustomData<Vector2>("LockedDir", Vector2.zero), t));
                result.SetCustomData("DirLocked", t < 0.5f ? a.GetCustomData<bool>("DirLocked", false) : b.GetCustomData<bool>("DirLocked", false));

                // Falling Platforms
                result.SetCustomData("IsFalling", t < 0.5f ? a.GetCustomData<bool>("IsFalling", false) : b.GetCustomData<bool>("IsFalling", false));

                // Trap
                result.SetCustomData("TrapBroken", t < 0.5f ? a.GetCustomData<bool>("TrapBroken", false) : b.GetCustomData<bool>("TrapBroken", false));

                float aProgress = a.GetCustomData<float>("Progress", 0f);
                float bProgress = b.GetCustomData<float>("Progress", 0f);
                result.SetCustomData("Progress", Mathf.Lerp(aProgress, bProgress, t));

                float aWait = a.GetCustomData<float>("TopWaitProgress", 0f);
                float bWait = b.GetCustomData<float>("TopWaitProgress", 0f);
                result.SetCustomData("TopWaitProgress", Mathf.Lerp(aWait, bWait, t));

                // health pickup
                result.SetCustomData("IsCollected", t < 0.5f ? a.GetCustomData<bool>("IsCollected", false) : b.GetCustomData<bool>("IsCollected", false));

                // Snap the booleans and fixed positions to match state 'a'
                result.SetCustomData("MovingUp", a.GetCustomData<bool>("MovingUp", true));
                result.SetCustomData("WaitingAtTop", a.GetCustomData<bool>("WaitingAtTop", false));
                result.SetCustomData("StartPos", a.GetCustomData<Vector3>("StartPos", a.Position));
                result.SetCustomData("TargetPos", a.GetCustomData<Vector3>("TargetPos", a.Position));
                result.SetCustomData("CycleComplete", t < 0.5f ? a.GetCustomData<bool>("CycleComplete", false) : b.GetCustomData<bool>("CycleComplete", false));

                // Boss enums
                result.SetCustomData("Phase", t < 0.5f ? a.GetCustomData<int>("Phase", 0) : b.GetCustomData<int>("Phase", 0));
                result.SetCustomData("PosType", t < 0.5f ? a.GetCustomData<int>("PosType", 0) : b.GetCustomData<int>("PosType", 0));
                result.SetCustomData("OffType", t < 0.5f ? a.GetCustomData<int>("OffType", 0) : b.GetCustomData<int>("OffType", 0));
                result.SetCustomData("ResType", t < 0.5f ? a.GetCustomData<int>("ResType", 0) : b.GetCustomData<int>("ResType", 0));
                result.SetCustomData("OffIndex", t < 0.5f ? a.GetCustomData<int>("OffIndex", 0) : b.GetCustomData<int>("OffIndex", 0));
                result.SetCustomData("ResIndex", t < 0.5f ? a.GetCustomData<int>("ResIndex", 0) : b.GetCustomData<int>("ResIndex", 0));
                result.SetCustomData("Facing", t < 0.5f ? a.GetCustomData<int>("Facing", 1) : b.GetCustomData<int>("Facing", 1));

                // Boss pre-rolled action queue (two deep). Must be carried through Lerp
                // verbatim or the boss loses its queued attacks across any rewind.
                result.SetCustomData("NextIsPositional",     t < 0.5f ? a.GetCustomData<bool>("NextIsPositional", false) : b.GetCustomData<bool>("NextIsPositional", false));
                result.SetCustomData("NextPosMove",          t < 0.5f ? a.GetCustomData<int>("NextPosMove", 0) : b.GetCustomData<int>("NextPosMove", 0));
                result.SetCustomData("NextOff",              t < 0.5f ? a.GetCustomData<int>("NextOff", 0) : b.GetCustomData<int>("NextOff", 0));
                result.SetCustomData("NextRes",              t < 0.5f ? a.GetCustomData<int>("NextRes", 0) : b.GetCustomData<int>("NextRes", 0));
                result.SetCustomData("NextNextIsPositional", t < 0.5f ? a.GetCustomData<bool>("NextNextIsPositional", false) : b.GetCustomData<bool>("NextNextIsPositional", false));
                result.SetCustomData("NextNextPosMove",      t < 0.5f ? a.GetCustomData<int>("NextNextPosMove", 0) : b.GetCustomData<int>("NextNextPosMove", 0));
                result.SetCustomData("NextNextOff",          t < 0.5f ? a.GetCustomData<int>("NextNextOff", 0) : b.GetCustomData<int>("NextNextOff", 0));
                result.SetCustomData("NextNextRes",          t < 0.5f ? a.GetCustomData<int>("NextNextRes", 0) : b.GetCustomData<int>("NextNextRes", 0));
                result.SetCustomData("LastOff",              t < 0.5f ? a.GetCustomData<int>("LastOff", 0) : b.GetCustomData<int>("LastOff", 0));
                result.SetCustomData("LastRes",              t < 0.5f ? a.GetCustomData<int>("LastRes", 0) : b.GetCustomData<int>("LastRes", 0));
                result.SetCustomData("RepeatCount",          t < 0.5f ? a.GetCustomData<int>("RepeatCount", 0) : b.GetCustomData<int>("RepeatCount", 0));

                // Boss movement targets
                result.SetCustomData("TargetX", t < 0.5f ? a.GetCustomData<float>("TargetX", 0f) : b.GetCustomData<float>("TargetX", 0f));
                result.SetCustomData("MoveStart", t < 0.5f ? a.GetCustomData<Vector2>("MoveStart", Vector2.zero) : b.GetCustomData<Vector2>("MoveStart", Vector2.zero));
                result.SetCustomData("MovePeak", t < 0.5f ? a.GetCustomData<Vector2>("MovePeak", Vector2.zero) : b.GetCustomData<Vector2>("MovePeak", Vector2.zero));
                result.SetCustomData("MoveTarget", t < 0.5f ? a.GetCustomData<Vector2>("MoveTarget", Vector2.zero) : b.GetCustomData<Vector2>("MoveTarget", Vector2.zero));

                // Boss timers
                result.SetCustomData("IdleTimer", Mathf.Lerp(a.GetCustomData<float>("IdleTimer", 0f), b.GetCustomData<float>("IdleTimer", 0f), t));
                result.SetCustomData("PosTimer", Mathf.Lerp(a.GetCustomData<float>("PosTimer", 0f), b.GetCustomData<float>("PosTimer", 0f), t));
                result.SetCustomData("OffTimer", Mathf.Lerp(a.GetCustomData<float>("OffTimer", 0f), b.GetCustomData<float>("OffTimer", 0f), t));
                result.SetCustomData("ResTimer", Mathf.Lerp(a.GetCustomData<float>("ResTimer", 0f), b.GetCustomData<float>("ResTimer", 0f), t));

                // Boss bools
                result.SetCustomData("OffSpawned", t < 0.5f ? a.GetCustomData<bool>("OffSpawned", false) : b.GetCustomData<bool>("OffSpawned", false));
                result.SetCustomData("ResSpawned", t < 0.5f ? a.GetCustomData<bool>("ResSpawned", false) : b.GetCustomData<bool>("ResSpawned", false));
            }

            return result;
        }

        public void SetCustomData(string key, object value)
        {
            CustomData ??= new Dictionary<string, object>();
            CustomData[key] = value;
        }

        public T GetCustomData<T>(string key, T defaultValue = default)
        {
            if (CustomData == null || !CustomData.TryGetValue(key, out var value))
                return defaultValue;
            
            return (T)value;
        }
    }
}
