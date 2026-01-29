using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TimeRewind;

namespace Tests.PlayMode
{
    [TestFixture]
    public class TimeRewindManagerTests
    {
        private TimeRewindManager _manager;
        private TestRewindable _testRewindable;

        [SetUp]
        public void SetUp()
        {
            _manager = TimeRewindManager.Instance;
            _manager.ClearHistory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null && _manager.IsRewinding)
            {
                _manager.StopRewind();
            }
            
            if (_testRewindable != null)
            {
                _manager.Unregister(_testRewindable);
                Object.DestroyImmediate(_testRewindable.gameObject);
                _testRewindable = null;
            }
            
            if (_manager != null)
            {
                _manager.ClearHistory();
            }
            
            Time.timeScale = 1f;
        }

        [Test]
        public void Register_ValidRewindable_DoesNotThrow()
        {
            var rewindable = CreateTestRewindable();
            Assert.DoesNotThrow(() => _manager.Register(rewindable));
        }

        [Test]
        public void Register_NullRewindable_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Register(null));
        }

        [Test]
        public void Unregister_NullRewindable_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Unregister(null));
        }

        [Test]
        public void IsRewinding_Initially_IsFalse()
        {
            Assert.IsFalse(_manager.IsRewinding);
        }

        [Test]
        public void CanRewind_NoRegisteredRewindables_IsFalse()
        {
            Assert.IsFalse(_manager.CanRewind);
        }

        [Test]
        public void StartRewind_NoRecordedStates_DoesNotStartRewinding()
        {
            _manager.StartRewind();
            Assert.IsFalse(_manager.IsRewinding);
        }

        [Test]
        public void StopRewind_NotRewinding_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.StopRewind());
        }

        [UnityTest]
        public IEnumerator CanRewind_AfterRecording_IsTrue()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(_manager.CanRewind, "CanRewind should be true after recording");
        }

        [UnityTest]
        public IEnumerator StartRewind_AfterRecording_SetsIsRewindingTrue()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            _manager.StartRewind();
            bool isRewinding = _manager.IsRewinding;
            _manager.StopRewind();

            Assert.IsTrue(isRewinding, "IsRewinding should be true after StartRewind");
        }

        [UnityTest]
        public IEnumerator StopRewind_AfterStarting_SetsIsRewindingFalse()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            _manager.StartRewind();
            yield return null;
            _manager.StopRewind();

            Assert.IsFalse(_manager.IsRewinding);
        }

        [UnityTest]
        public IEnumerator ClearHistory_RemovesRecordedStates()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            bool hadStates = _manager.CanRewind;
            _manager.ClearHistory();
            bool hasStatesAfterClear = _manager.CanRewind;

            Assert.IsTrue(hadStates, "Should have had states before clear");
            Assert.IsFalse(hasStatesAfterClear, "Should not have states after clear");
        }

        [UnityTest]
        public IEnumerator OnRewindStart_EventFires()
        {
            bool eventFired = false;
            _manager.OnRewindStart += () => eventFired = true;

            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            _manager.StartRewind();
            bool fired = eventFired;
            _manager.StopRewind();

            Assert.IsTrue(fired, "OnRewindStart should have fired");
        }

        [UnityTest]
        public IEnumerator OnRewindStop_EventFires()
        {
            bool eventFired = false;
            _manager.OnRewindStop += () => eventFired = true;

            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            _manager.StartRewind();
            yield return null;
            _manager.StopRewind();

            Assert.IsTrue(eventFired, "OnRewindStop should have fired");
        }

        [UnityTest]
        public IEnumerator Rewind_MovesObjectBackTowardsPreviousPosition()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            Vector3 startPosition = Vector3.zero;
            rewindable.transform.position = startPosition;

            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector3 newPosition = new Vector3(10f, 0f, 0f);
            rewindable.transform.position = newPosition;

            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            _manager.StartRewind();

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Vector3 finalPosition = rewindable.transform.position;
            _manager.StopRewind();

            float distanceToStart = Vector3.Distance(finalPosition, startPosition);
            float distanceToNew = Vector3.Distance(finalPosition, newPosition);

            Assert.Less(distanceToStart, distanceToNew,
                $"Object should have rewound closer to start. Final: {finalPosition}");
        }

        private TestRewindable CreateTestRewindable(string name = "TestRewindable")
        {
            var go = new GameObject(name);
            _testRewindable = go.AddComponent<TestRewindable>();
            return _testRewindable;
        }
    }

    public class TestRewindable : MonoBehaviour, IRewindable
    {
        public void OnStartRewind() { }
        public void OnStopRewind() { }

        public RewindState CaptureState()
        {
            return RewindState.Create(transform.position, transform.rotation, Time.time);
        }

        public void ApplyState(RewindState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
        }
    }
}
