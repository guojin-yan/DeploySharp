using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    // Exact-length bounded pooling keeps decoder scratch arrays compatible with the full
    // Visual target-framework matrix without adding System.Buffers as a package dependency.
    internal static class VisualArrayPool<T>
    {
        private const int MaximumRetainedElements = 4 * 1024 * 1024;
        private const int MaximumArraysPerLength = 4;
        private static readonly object Gate = new object();
        private static readonly Dictionary<int, Stack<T[]>> Buckets = new Dictionary<int, Stack<T[]>>();
        private static int _retainedElements;

        internal static T[] Rent(int length)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            lock (Gate)
            {
                if (Buckets.TryGetValue(length, out Stack<T[]>? bucket) && bucket.Count != 0)
                {
                    T[] result = bucket.Pop();
                    _retainedElements -= length;
                    if (bucket.Count == 0) Buckets.Remove(length);
                    return result;
                }
            }
            return new T[length];
        }

        internal static void Return(T[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (array.Length == 0 || array.Length > MaximumRetainedElements) return;
            lock (Gate)
            {
                if (_retainedElements > MaximumRetainedElements - array.Length) return;
                if (!Buckets.TryGetValue(array.Length, out Stack<T[]>? bucket))
                {
                    bucket = new Stack<T[]>();
                    Buckets.Add(array.Length, bucket);
                }
                if (bucket.Count >= MaximumArraysPerLength) return;
                bucket.Push(array);
                _retainedElements += array.Length;
            }
        }
    }
}
