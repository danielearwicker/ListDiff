using System;
using System.Collections;
using System.Collections.Generic;

namespace ListDiff
{
    internal class EmptyList<T> : IReadOnlyList<T>
    {
        public static readonly EmptyList<T> Instance = new EmptyList<T>();       

        public IEnumerator<T> GetEnumerator()
        {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => 0;

        public T this[int index]
        {
            get { throw new IndexOutOfRangeException(); }
        }
    }
}