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

            // Interpolate animator CustomData for smooth rewind
            if (a.CustomData != null || b.CustomData != null)
            {
                // Player
                result.SetCustomData("VerticalNormal", Mathf.Lerp(
                    a.GetCustomData<float>("VerticalNormal", 0f),
                    b.GetCustomData<float>("VerticalNormal", 0f), t));
                result.SetCustomData("Speed", Mathf.Lerp(
                    a.GetCustomData<float>("Speed", 0f),
                    b.GetCustomData<float>("Speed", 0f), t));
                result.SetCustomData("isGrounded", t < 0.5f ? a.GetCustomData<bool>("isGrounded", true) : b.GetCustomData<bool>("isGrounded", true));
                result.SetCustomData("isWallSliding", t < 0.5f ? a.GetCustomData<bool>("isWallSliding", false) : b.GetCustomData<bool>("isWallSliding", false));
                result.SetCustomData("IsFlipped", t < 0.5f ? a.GetCustomData<bool>("IsFlipped", false) : b.GetCustomData<bool>("IsFlipped", false));

                // FlyingEnemy (bats) - facing via localScale
                result.SetCustomData("FacingDirection", t < 0.5f ? a.GetCustomData<Vector3>("FacingDirection", Vector3.one) : b.GetCustomData<Vector3>("FacingDirection", Vector3.one));
                result.SetCustomData("EnemyState", t < 0.5f ? a.GetCustomData<int>("EnemyState", 0) : b.GetCustomData<int>("EnemyState", 0));
                result.SetCustomData("DetectRange", Mathf.Lerp(a.GetCustomData<float>("DetectRange", 10f), b.GetCustomData<float>("DetectRange", 10f), t));

                // SlimeEnemy - facing via flipX
                result.SetCustomData("flipX", t < 0.5f ? a.GetCustomData<bool>("flipX", false) : b.GetCustomData<bool>("flipX", false));
                result.SetCustomData("midJump", t < 0.5f ? a.GetCustomData<bool>("midJump", false) : b.GetCustomData<bool>("midJump", false));
                result.SetCustomData("frameIndex", Mathf.RoundToInt(Mathf.Lerp(a.GetCustomData<int>("frameIndex", 0), b.GetCustomData<int>("frameIndex", 0), t)));
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
