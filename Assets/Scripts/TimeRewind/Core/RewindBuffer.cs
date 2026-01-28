using System;
using UnityEngine;

namespace TimeRewind
{
    public class RewindBuffer<T> where T : struct
    {
        private readonly T[] _buffer;
        private readonly int _capacity;
        private int _head;
        private int _tail;
        private int _count;

        public int Count => _count;
        public int Capacity => _capacity;
        public bool HasStates => _count > 0;
        public bool IsFull => _count == _capacity;

        public RewindBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

            _capacity = capacity;
            _buffer = new T[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public void Add(T state)
        {
            _buffer[_head] = state;
            _head = (_head + 1) % _capacity;

            if (_count < _capacity)
            {
                _count++;
            }
            else
            {
                _tail = (_tail + 1) % _capacity;
            }
        }

        public T Get(int index)
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            int actualIndex = (_tail + index) % _capacity;
            return _buffer[actualIndex];
        }

        public T GetNewest()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");

            int newestIndex = (_head - 1 + _capacity) % _capacity;
            return _buffer[newestIndex];
        }

        public T GetOldest()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");

            return _buffer[_tail];
        }

        public T PopNewest()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");

            _head = (_head - 1 + _capacity) % _capacity;
            _count--;
            return _buffer[_head];
        }

        public void TrimToCount(int keepCount)
        {
            if (keepCount < 0)
                throw new ArgumentOutOfRangeException(nameof(keepCount));

            if (keepCount >= _count)
                return;

            _count = keepCount;
            _head = (_tail + keepCount) % _capacity;
        }

        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public int FindClosestIndex(float targetTimestamp, Func<T, float> getTimestamp)
        {
            if (_count == 0)
                return -1;

            int left = 0;
            int right = _count - 1;

            while (left < right)
            {
                int mid = (left + right) / 2;
                float midTimestamp = getTimestamp(Get(mid));

                if (midTimestamp < targetTimestamp)
                    left = mid + 1;
                else
                    right = mid;
            }

            if (left > 0)
            {
                float leftTimestamp = getTimestamp(Get(left));
                float prevTimestamp = getTimestamp(Get(left - 1));
                
                if (Mathf.Abs(prevTimestamp - targetTimestamp) < Mathf.Abs(leftTimestamp - targetTimestamp))
                    return left - 1;
            }

            return left;
        }

        public bool GetInterpolationStates(
            float targetTimestamp, 
            Func<T, float> getTimestamp,
            out T before, 
            out T after, 
            out float t)
        {
            before = default;
            after = default;
            t = 0f;

            if (_count == 0)
                return false;

            if (_count == 1)
            {
                before = Get(0);
                after = before;
                t = 0f;
                return true;
            }

            int afterIndex = FindClosestIndex(targetTimestamp, getTimestamp);
            afterIndex = Mathf.Clamp(afterIndex, 0, _count - 1);
            
            float afterTimestamp = getTimestamp(Get(afterIndex));
            
            if (afterIndex == 0 && targetTimestamp <= afterTimestamp)
            {
                before = Get(0);
                after = before;
                t = 0f;
                return true;
            }

            if (afterIndex == _count - 1 && targetTimestamp >= afterTimestamp)
            {
                before = Get(_count - 1);
                after = before;
                t = 0f;
                return true;
            }

            int beforeIndex = afterIndex > 0 ? afterIndex - 1 : afterIndex;
            
            before = Get(beforeIndex);
            after = Get(afterIndex);

            float beforeTimestamp = getTimestamp(before);
            afterTimestamp = getTimestamp(after);

            float duration = afterTimestamp - beforeTimestamp;
            if (duration > 0)
                t = (targetTimestamp - beforeTimestamp) / duration;
            else
                t = 0f;

            t = Mathf.Clamp01(t);
            return true;
        }
    }
}
