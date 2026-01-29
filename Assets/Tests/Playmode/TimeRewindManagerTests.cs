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
        private GameObject _managerObject;
        private TimeRewindManager _manager;

        [SetUp]
        public void SetUp()
        {
            // Create a fresh manager for each test
            _managerObject = new GameObject("TestTimeRewindManager");
            _manager = _managerObject.AddComponent<TimeRewindManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerObject != null)
            {
                Object.DestroyImmediate(_managerObject);
            }
            Time.timeScale = 1f;
        }

        #region Registration Tests

        [Test]
        public void Register_ValidRewindable_DoesNotThrow()
        {
            var rewindable = CreateTestRewindable();

            Assert.DoesNotThrow(() => _manager.Register(rewindable));

            Object.DestroyImmediate(rewindable.gameObject);
        }

        [Test]
        public void Register_NullRewindable_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Register(null));
        }

        [Test]
        public void Register_SameRewindableTwice_DoesNotThrow()
        {
            var rewindable = CreateTestRewindable();

            _manager.Register(rewindable);
            Assert.DoesNotThrow(() => _manager.Register(rewindable));

            Object.DestroyImmediate(rewindable.gameObject);
        }

        [Test]
        public void Unregister_RegisteredRewindable_DoesNotThrow()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            Assert.DoesNotThrow(() => _manager.Unregister(rewindable));

            Object.DestroyImmediate(rewindable.gameObject);
        }

        [Test]
        public void Unregister_NullRewindable_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Unregister(null));
        }

        #endregion

        #region State Tests

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

        [UnityTest]
        public IEnumerator CanRewind_WithRecordedStates_IsTrue()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            // Wait for multiple FixedUpdate cycles to ensure recording happens
            yield return WaitForRecording();

            Assert.IsTrue(_manager.CanRewind, "CanRewind should be true after recording states");

            Object.DestroyImmediate(rewindable.gameObject);
        }

        #endregion

        #region Rewind Flow Tests

        [UnityTest]
        public IEnumerator StartRewind_WithRecordedStates_SetsIsRewindingTrue()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            yield return WaitForRecording();

            _manager.StartRewind();

            Assert.IsTrue(_manager.IsRewinding, "IsRewinding should be true after StartRewind");

            _manager.StopRewind();
            Object.DestroyImmediate(rewindable.gameObject);
        }

        [Test]
        public void StartRewind_NoRecordedStates_DoesNotStartRewinding()
        {
            _manager.StartRewind();

            Assert.IsFalse(_manager.IsRewinding);
        }

        [UnityTest]
        public IEnumerator StopRewind_AfterStarting_SetsIsRewindingFalse()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            yield return WaitForRecording();

            _manager.StartRewind();
            yield return null;
            _manager.StopRewind();

            Assert.IsFalse(_manager.IsRewinding);

            Object.DestroyImmediate(rewindable.gameObject);
        }

        [UnityTest]
        public IEnumerator StartRewind_AlreadyRewinding_DoesNotRestartRewind()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            yield return WaitForRecording();

            _manager.StartRewind();

            // Try to start again while already rewinding
            _manager.StartRewind();

            // Should still be rewinding
            Assert.IsTrue(_manager.IsRewinding);

            _manager.StopRewind();
            Object.DestroyImmediate(rewindable.gameObject);
        }

        [Test]
        public void StopRewind_NotRewinding_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.StopRewind());
        }

        #endregion

        #region Event Tests

        [UnityTest]
        public IEnumerator OnRewindStart_EventFires_WhenRewindStarts()
        {
            bool eventFired = false;
            _manager.OnRewindStart += () => eventFired = true;

            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            yield return WaitForRecording();

            _manager.StartRewind();

            Assert.IsTrue(eventFired, "OnRewindStart event should have fired");

            _manager.StopRewind();
            Object.DestroyImmediate(rewindable.gameObject);
        }

        [UnityTest]
        public IEnumerator OnRewindStop_EventFires_WhenRewindStops()
        {
            bool eventFired = false;
            _manager.OnRewindStop += () => eventFired = true;

            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            yield return WaitForRecording();

            _manager.StartRewind();
            yield return null;
            _manager.StopRewind();

            Assert.IsTrue(eventFired, "OnRewindStop event should have fired");

            Object.DestroyImmediate(rewindable.gameObject);
        }

        [UnityTest]
        public IEnumerator OnRewindProgress_EventFires_DuringRewind()
        {
            bool eventFired = false;
            float lastProgress = -1f;
            _manager.OnRewindProgress += (progress) =>
            {
                eventFired = true;
                lastProgress = progress;
            };

            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            yield return WaitForRecording();

            _manager.StartRewind();
            
            // Wait a few frames for Update to run and fire progress events
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.IsTrue(eventFired, "OnRewindProgress event should have fired");
            Assert.GreaterOrEqual(lastProgress, 0f);
            Assert.LessOrEqual(lastProgress, 1f);

            _manager.StopRewind();
            Object.DestroyImmediate(rewindable.gameObject);
        }

        #endregion

        #region ClearHistory Tests

        [UnityTest]
        public IEnumerator ClearHistory_RemovesAllRecordedStates()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            yield return WaitForRecording();

            Assert.IsTrue(_manager.CanRewind, "Should have recorded states before clear");

            _manager.ClearHistory();

            Assert.IsFalse(_manager.CanRewind, "Should have no states after clear");

            Object.DestroyImmediate(rewindable.gameObject);
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator Rewind_ObjectMovement_ReturnsTowardsPreviousPosition()
        {
            var rewindable = CreateTestRewindable();
            _manager.Register(rewindable);

            Vector3 startPosition = Vector3.zero;
            rewindable.transform.position = startPosition;

            // Record start position for several frames
            yield return WaitForRecording();

            // Move object to new position
            Vector3 newPosition = new Vector3(10f, 5f, 0f);
            rewindable.transform.position = newPosition;

            // Record new position
            yield return WaitForRecording();

            // Start rewinding
            _manager.StartRewind();

            // Wait for rewind to happen (multiple Update frames)
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            // Position should have moved from newPosition toward startPosition
            float distanceToStart = Vector3.Distance(rewindable.transform.position, startPosition);
            float distanceToNew = Vector3.Distance(rewindable.transform.position, newPosition);

            _manager.StopRewind();
            Object.DestroyImmediate(rewindable.gameObject);

            // After rewinding, we should be closer to start position than to new position
            Assert.Less(distanceToStart, distanceToNew, 
                $"Expected position closer to start. Current: {rewindable.transform.position}, Start: {startPosition}, New: {newPosition}");
        }

        [UnityTest]
        public IEnumerator Rewind_MultipleObjects_AllRewindTogether()
        {
            var rewindable1 = CreateTestRewindable("Rewindable1");
            var rewindable2 = CreateTestRewindable("Rewindable2");

            _manager.Register(rewindable1);
            _manager.Register(rewindable2);

            rewindable1.transform.position = Vector3.zero;
            rewindable2.transform.position = Vector3.zero;

            yield return WaitForRecording();

            // Move both objects
            rewindable1.transform.position = new Vector3(10f, 0f, 0f);
            rewindable2.transform.position = new Vector3(-10f, 0f, 0f);

            yield return WaitForRecording();

            _manager.StartRewind();
            
            // Wait for rewind
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            float pos1X = Mathf.Abs(rewindable1.transform.position.x);
            float pos2X = Mathf.Abs(rewindable2.transform.position.x);

            _manager.StopRewind();
            Object.DestroyImmediate(rewindable1.gameObject);
            Object.DestroyImmediate(rewindable2.gameObject);

            // Both should have moved back toward origin (x should be less than 10)
            Assert.Less(pos1X, 10f, $"Rewindable1 should have rewound, but x={pos1X}");
            Assert.Less(pos2X, 10f, $"Rewindable2 should have rewound, but x={pos2X}");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Waits for enough FixedUpdate cycles to ensure states are recorded.
        /// </summary>
        private IEnumerator WaitForRecording()
        {
            // Wait for multiple FixedUpdate cycles
            // Default fixed timestep is 0.02s, so 20 cycles = 0.4s of recording
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private TestRewindable CreateTestRewindable(string name = "TestRewindable")
        {
            var go = new GameObject(name);
            return go.AddComponent<TestRewindable>();
        }

        #endregion
    }

    /// <summary>
    /// Simple test implementation of IRewindable for testing purposes.
    /// </summary>
    public class TestRewindable : MonoBehaviour, IRewindable
    {
        public bool StartRewindCalled { get; private set; }
        public bool StopRewindCalled { get; private set; }
        public int CaptureStateCallCount { get; private set; }
        public int ApplyStateCallCount { get; private set; }

        public void OnStartRewind()
        {
            StartRewindCalled = true;
        }

        public void OnStopRewind()
        {
            StopRewindCalled = true;
        }

        public RewindState CaptureState()
        {
            CaptureStateCallCount++;
            return RewindState.Create(transform.position, transform.rotation, Time.time);
        }

        public void ApplyState(RewindState state)
        {
            ApplyStateCallCount++;
            transform.position = state.Position;
            transform.rotation = state.Rotation;
        }

        public void Reset()
        {
            StartRewindCalled = false;
            StopRewindCalled = false;
            CaptureStateCallCount = 0;
            ApplyStateCallCount = 0;
        }
    }
}
