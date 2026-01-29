using NUnit.Framework;
using UnityEngine;
using TimeRewind;

namespace Tests.EditMode
{
    [TestFixture]
    public class RewindStateTests
    {
        #region Create Tests

        [Test]
        public void Create_WithBasicParameters_SetsCorrectValues()
        {
            var position = new Vector3(1f, 2f, 3f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);
            float timestamp = 1.5f;

            var state = RewindState.Create(position, rotation, timestamp);

            Assert.AreEqual(position, state.Position);
            Assert.AreEqual(rotation, state.Rotation);
            Assert.AreEqual(timestamp, state.Timestamp);
            Assert.AreEqual(Vector2.zero, state.Velocity);
            Assert.AreEqual(0f, state.AngularVelocity);
        }

        [Test]
        public void Create_SetsDefaultsForOptionalFields()
        {
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);

            Assert.AreEqual(0, state.Health);
            Assert.AreEqual(0, state.AnimatorStateHash);
            Assert.AreEqual(0f, state.AnimatorNormalizedTime);
            Assert.IsNull(state.CustomData);
        }

        #endregion

        #region CreateWithPhysics Tests

        [Test]
        public void CreateWithPhysics_SetsPhysicsValues()
        {
            var position = new Vector3(1f, 2f, 3f);
            var rotation = Quaternion.identity;
            var velocity = new Vector2(5f, 10f);
            float angularVelocity = 45f;
            float timestamp = 2f;

            var state = RewindState.CreateWithPhysics(position, rotation, velocity, angularVelocity, timestamp);

            Assert.AreEqual(position, state.Position);
            Assert.AreEqual(rotation, state.Rotation);
            Assert.AreEqual(velocity, state.Velocity);
            Assert.AreEqual(angularVelocity, state.AngularVelocity);
            Assert.AreEqual(timestamp, state.Timestamp);
        }

        #endregion

        #region Lerp Tests

        [Test]
        public void Lerp_AtZero_ReturnsFirstState()
        {
            var stateA = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            var stateB = RewindState.Create(new Vector3(10f, 10f, 10f), Quaternion.Euler(0, 90, 0), 1f);

            var result = RewindState.Lerp(stateA, stateB, 0f);

            Assert.AreEqual(Vector3.zero, result.Position);
            Assert.AreEqual(0f, result.Timestamp);
        }

        [Test]
        public void Lerp_AtOne_ReturnsSecondState()
        {
            var stateA = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            var stateB = RewindState.Create(new Vector3(10f, 10f, 10f), Quaternion.Euler(0, 90, 0), 1f);

            var result = RewindState.Lerp(stateA, stateB, 1f);

            Assert.AreEqual(new Vector3(10f, 10f, 10f), result.Position);
            Assert.AreEqual(1f, result.Timestamp);
        }

        [Test]
        public void Lerp_AtHalf_InterpolatesPosition()
        {
            var stateA = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            var stateB = RewindState.Create(new Vector3(10f, 0f, 0f), Quaternion.identity, 1f);

            var result = RewindState.Lerp(stateA, stateB, 0.5f);

            Assert.AreEqual(new Vector3(5f, 0f, 0f), result.Position);
            Assert.AreEqual(0.5f, result.Timestamp);
        }

        [Test]
        public void Lerp_InterpolatesVelocity()
        {
            var stateA = RewindState.CreateWithPhysics(Vector3.zero, Quaternion.identity, new Vector2(0f, 0f), 0f, 0f);
            var stateB = RewindState.CreateWithPhysics(Vector3.zero, Quaternion.identity, new Vector2(10f, 20f), 100f, 1f);

            var result = RewindState.Lerp(stateA, stateB, 0.5f);

            Assert.AreEqual(new Vector2(5f, 10f), result.Velocity);
            Assert.AreEqual(50f, result.AngularVelocity);
        }

        [Test]
        public void Lerp_InterpolatesHealth()
        {
            var stateA = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            stateA.Health = 0;
            var stateB = RewindState.Create(Vector3.zero, Quaternion.identity, 1f);
            stateB.Health = 100;

            var result = RewindState.Lerp(stateA, stateB, 0.5f);

            Assert.AreEqual(50, result.Health);
        }

        [Test]
        public void Lerp_AnimatorStateHash_UsesFirstBeforeHalf()
        {
            var stateA = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            stateA.AnimatorStateHash = 111;
            var stateB = RewindState.Create(Vector3.zero, Quaternion.identity, 1f);
            stateB.AnimatorStateHash = 222;

            var result = RewindState.Lerp(stateA, stateB, 0.4f);

            Assert.AreEqual(111, result.AnimatorStateHash);
        }

        [Test]
        public void Lerp_AnimatorStateHash_UsesSecondAtOrAfterHalf()
        {
            var stateA = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            stateA.AnimatorStateHash = 111;
            var stateB = RewindState.Create(Vector3.zero, Quaternion.identity, 1f);
            stateB.AnimatorStateHash = 222;

            var result = RewindState.Lerp(stateA, stateB, 0.6f);

            Assert.AreEqual(222, result.AnimatorStateHash);
        }

        [Test]
        public void Lerp_InterpolatesRotation()
        {
            var rotA = Quaternion.Euler(0f, 0f, 0f);
            var rotB = Quaternion.Euler(0f, 90f, 0f);
            var stateA = RewindState.Create(Vector3.zero, rotA, 0f);
            var stateB = RewindState.Create(Vector3.zero, rotB, 1f);

            var result = RewindState.Lerp(stateA, stateB, 0.5f);

            var expectedRotation = Quaternion.Slerp(rotA, rotB, 0.5f);
            Assert.AreEqual(expectedRotation.eulerAngles.y, result.Rotation.eulerAngles.y, 0.1f);
        }

        #endregion

        #region CustomData Tests

        [Test]
        public void SetCustomData_StoresValue()
        {
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);

            state.SetCustomData("health", 100);

            Assert.IsNotNull(state.CustomData);
            Assert.IsTrue(state.CustomData.ContainsKey("health"));
        }

        [Test]
        public void GetCustomData_RetrievesStoredValue()
        {
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            state.SetCustomData("score", 500);

            var score = state.GetCustomData<int>("score");

            Assert.AreEqual(500, score);
        }

        [Test]
        public void GetCustomData_MissingKey_ReturnsDefault()
        {
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);

            var value = state.GetCustomData<int>("missing", -1);

            Assert.AreEqual(-1, value);
        }

        [Test]
        public void GetCustomData_NullCustomData_ReturnsDefault()
        {
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);

            var value = state.GetCustomData<string>("key", "default");

            Assert.AreEqual("default", value);
        }

        [Test]
        public void SetCustomData_MultipleValues_AllStored()
        {
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);

            state.SetCustomData("int_value", 42);
            state.SetCustomData("string_value", "test");
            state.SetCustomData("float_value", 3.14f);

            Assert.AreEqual(42, state.GetCustomData<int>("int_value"));
            Assert.AreEqual("test", state.GetCustomData<string>("string_value"));
            Assert.AreEqual(3.14f, state.GetCustomData<float>("float_value"), 0.001f);
        }

        [Test]
        public void SetCustomData_OverwriteExisting_UpdatesValue()
        {
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);
            state.SetCustomData("key", "original");

            state.SetCustomData("key", "updated");

            Assert.AreEqual("updated", state.GetCustomData<string>("key"));
        }

        #endregion
    }
}
