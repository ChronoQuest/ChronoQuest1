using System;
using NUnit.Framework;
using UnityEngine;
using TimeRewind;

namespace Tests.EditMode
{
    [TestFixture]
    public class RewindBufferTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithValidCapacity_CreatesBuffer()
        {
            var buffer = new RewindBuffer<RewindState>(10);

            Assert.AreEqual(10, buffer.Capacity);
            Assert.AreEqual(0, buffer.Count);
            Assert.IsFalse(buffer.HasStates);
            Assert.IsFalse(buffer.IsFull);
        }

        [Test]
        public void Constructor_WithZeroCapacity_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RewindBuffer<RewindState>(0));
        }

        [Test]
        public void Constructor_WithNegativeCapacity_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RewindBuffer<RewindState>(-5));
        }

        #endregion

        #region Add Tests

        [Test]
        public void Add_SingleState_IncreasesCount()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            var state = RewindState.Create(Vector3.zero, Quaternion.identity, 0f);

            buffer.Add(state);

            Assert.AreEqual(1, buffer.Count);
            Assert.IsTrue(buffer.HasStates);
        }

        [Test]
        public void Add_MultipleStates_TracksCorrectCount()
        {
            var buffer = new RewindBuffer<RewindState>(10);

            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));

            Assert.AreEqual(3, buffer.Count);
        }

        [Test]
        public void Add_ExceedsCapacity_MaintainsCapacityLimit()
        {
            var buffer = new RewindBuffer<RewindState>(3);

            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));
            buffer.Add(RewindState.Create(Vector3.right, Quaternion.identity, 3f));

            Assert.AreEqual(3, buffer.Count);
            Assert.IsTrue(buffer.IsFull);
        }

        [Test]
        public void Add_ExceedsCapacity_DropsOldestState()
        {
            var buffer = new RewindBuffer<RewindState>(3);

            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));
            buffer.Add(RewindState.Create(Vector3.right, Quaternion.identity, 3f));

            // Oldest should now be timestamp 1f (0f was dropped)
            Assert.AreEqual(1f, buffer.GetOldest().Timestamp);
            Assert.AreEqual(3f, buffer.GetNewest().Timestamp);
        }

        #endregion

        #region Get Tests

        [Test]
        public void Get_ValidIndex_ReturnsCorrectState()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));

            var state = buffer.Get(1);

            Assert.AreEqual(1f, state.Timestamp);
            Assert.AreEqual(Vector3.one, state.Position);
        }

        [Test]
        public void Get_NegativeIndex_ThrowsArgumentOutOfRangeException()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Get(-1));
        }

        [Test]
        public void Get_IndexExceedsCount_ThrowsArgumentOutOfRangeException()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Get(5));
        }

        #endregion

        #region GetNewest/GetOldest Tests

        [Test]
        public void GetNewest_WithStates_ReturnsLastAdded()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(new Vector3(5f, 5f, 5f), Quaternion.identity, 2f));

            var newest = buffer.GetNewest();

            Assert.AreEqual(2f, newest.Timestamp);
            Assert.AreEqual(new Vector3(5f, 5f, 5f), newest.Position);
        }

        [Test]
        public void GetNewest_EmptyBuffer_ThrowsInvalidOperationException()
        {
            var buffer = new RewindBuffer<RewindState>(10);

            Assert.Throws<InvalidOperationException>(() => buffer.GetNewest());
        }

        [Test]
        public void GetOldest_WithStates_ReturnsFirstAdded()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(new Vector3(1f, 1f, 1f), Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));

            var oldest = buffer.GetOldest();

            Assert.AreEqual(0f, oldest.Timestamp);
            Assert.AreEqual(new Vector3(1f, 1f, 1f), oldest.Position);
        }

        [Test]
        public void GetOldest_EmptyBuffer_ThrowsInvalidOperationException()
        {
            var buffer = new RewindBuffer<RewindState>(10);

            Assert.Throws<InvalidOperationException>(() => buffer.GetOldest());
        }

        #endregion

        #region PopNewest Tests

        [Test]
        public void PopNewest_RemovesAndReturnsNewest()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));

            var popped = buffer.PopNewest();

            Assert.AreEqual(2f, popped.Timestamp);
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1f, buffer.GetNewest().Timestamp);
        }

        [Test]
        public void PopNewest_EmptyBuffer_ThrowsInvalidOperationException()
        {
            var buffer = new RewindBuffer<RewindState>(10);

            Assert.Throws<InvalidOperationException>(() => buffer.PopNewest());
        }

        #endregion

        #region Clear Tests

        [Test]
        public void Clear_RemovesAllStates()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
            Assert.IsFalse(buffer.HasStates);
        }

        #endregion

        #region TrimToCount Tests

        [Test]
        public void TrimToCount_ReducesBufferSize()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));
            buffer.Add(RewindState.Create(Vector3.right, Quaternion.identity, 3f));

            buffer.TrimToCount(2);

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(0f, buffer.GetOldest().Timestamp);
            Assert.AreEqual(1f, buffer.GetNewest().Timestamp);
        }

        [Test]
        public void TrimToCount_LargerThanCount_NoChange()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));

            buffer.TrimToCount(10);

            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void TrimToCount_NegativeValue_ThrowsArgumentOutOfRangeException()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.TrimToCount(-1));
        }

        #endregion

        #region FindClosestIndex Tests

        [Test]
        public void FindClosestIndex_ExactMatch_ReturnsCorrectIndex()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));

            int index = buffer.FindClosestIndex(1f, s => s.Timestamp);

            Assert.AreEqual(1, index);
        }

        [Test]
        public void FindClosestIndex_BetweenStates_ReturnsClosest()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.up, Quaternion.identity, 2f));

            // 0.8 is closer to 1.0 than to 0.0
            int index = buffer.FindClosestIndex(0.8f, s => s.Timestamp);

            Assert.AreEqual(1, index);
        }

        [Test]
        public void FindClosestIndex_EmptyBuffer_ReturnsNegativeOne()
        {
            var buffer = new RewindBuffer<RewindState>(10);

            int index = buffer.FindClosestIndex(1f, s => s.Timestamp);

            Assert.AreEqual(-1, index);
        }

        #endregion

        #region GetInterpolationStates Tests

        [Test]
        public void GetInterpolationStates_MidwayTime_ReturnsCorrectPair()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 0f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));

            bool success = buffer.GetInterpolationStates(0.5f, s => s.Timestamp, out var before, out var after, out float t);

            Assert.IsTrue(success);
            Assert.AreEqual(0f, before.Timestamp);
            Assert.AreEqual(1f, after.Timestamp);
            Assert.AreEqual(0.5f, t, 0.01f);
        }

        [Test]
        public void GetInterpolationStates_EmptyBuffer_ReturnsFalse()
        {
            var buffer = new RewindBuffer<RewindState>(10);

            bool success = buffer.GetInterpolationStates(0.5f, s => s.Timestamp, out _, out _, out _);

            Assert.IsFalse(success);
        }

        [Test]
        public void GetInterpolationStates_SingleState_ReturnsSameStateForBoth()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 1f));

            bool success = buffer.GetInterpolationStates(0.5f, s => s.Timestamp, out var before, out var after, out float t);

            Assert.IsTrue(success);
            Assert.AreEqual(before.Timestamp, after.Timestamp);
            Assert.AreEqual(0f, t);
        }

        [Test]
        public void GetInterpolationStates_BeforeAllStates_ReturnsOldest()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 2f));

            bool success = buffer.GetInterpolationStates(0f, s => s.Timestamp, out var before, out var after, out _);

            Assert.IsTrue(success);
            Assert.AreEqual(1f, before.Timestamp);
        }

        [Test]
        public void GetInterpolationStates_AfterAllStates_ReturnsNewest()
        {
            var buffer = new RewindBuffer<RewindState>(10);
            buffer.Add(RewindState.Create(Vector3.zero, Quaternion.identity, 1f));
            buffer.Add(RewindState.Create(Vector3.one, Quaternion.identity, 2f));

            bool success = buffer.GetInterpolationStates(5f, s => s.Timestamp, out var before, out _, out _);

            Assert.IsTrue(success);
            Assert.AreEqual(2f, before.Timestamp);
        }

        #endregion

        #region Circular Buffer Behavior Tests

        [Test]
        public void CircularBuffer_AfterWrap_MaintainsCorrectOrder()
        {
            var buffer = new RewindBuffer<RewindState>(3);

            // Add 5 states to a buffer of capacity 3
            for (int i = 0; i < 5; i++)
            {
                buffer.Add(RewindState.Create(new Vector3(i, 0, 0), Quaternion.identity, i));
            }

            // Should contain states 2, 3, 4
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(2f, buffer.GetOldest().Timestamp);
            Assert.AreEqual(4f, buffer.GetNewest().Timestamp);

            // Verify order
            Assert.AreEqual(2f, buffer.Get(0).Timestamp);
            Assert.AreEqual(3f, buffer.Get(1).Timestamp);
            Assert.AreEqual(4f, buffer.Get(2).Timestamp);
        }

        #endregion
    }
}
