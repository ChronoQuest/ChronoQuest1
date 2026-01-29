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
            return new RewindState
            {
                Timestamp = Mathf.Lerp(a.Timestamp, b.Timestamp, t),
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
                Velocity = Vector2.Lerp(a.Velocity, b.Velocity, t),
                AngularVelocity = Mathf.Lerp(a.AngularVelocity, b.AngularVelocity, t),
                Health = Mathf.RoundToInt(Mathf.Lerp(a.Health, b.Health, t)),
                AnimatorStateHash = t < 0.5f ? a.AnimatorStateHash : b.AnimatorStateHash,
                AnimatorNormalizedTime = Mathf.Lerp(a.AnimatorNormalizedTime, b.AnimatorNormalizedTime, t),
                CustomData = t < 0.5f ? a.CustomData : b.CustomData
            };
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
