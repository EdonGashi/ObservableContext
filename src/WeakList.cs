using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ObservableContext
{
    internal class WeakList<T> : IList<T>
    {
        private readonly List<WeakReference<T>> innerList = new List<WeakReference<T>>();

        public int IndexOf(T item)
        {
            return IndexOf(innerList.Select(wr => wr.Target), item);
        }

        public void Insert(int index, T item)
        {
            innerList.Insert(index, new WeakReference<T>(item));
        }

        public void RemoveAt(int index)
        {
            innerList.RemoveAt(index);
        }

        public T this[int index]
        {
            get => innerList[index].Target;
            set => innerList[index] = new WeakReference<T>(value);
        }

        public void Add(T item)
        {
            innerList.Add(new WeakReference<T>(item));
        }

        public void Clear()
        {
            innerList.Clear();
        }

        public bool Contains(T item)
        {
            return innerList.Any(wr => Equals(wr.Target, item));
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            CopyTo(innerList.Select(wr => wr.Target), array, arrayIndex);
        }

        public int Count => innerList.Count;

        public bool IsReadOnly => false;

        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index > -1)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return innerList.Select(x => x.Target).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private static void CopyTo(IEnumerable<T> source, T[] array, int startIndex)
        {
            var lowerBound = array.GetLowerBound(0);
            var upperBound = array.GetUpperBound(0);
            if (startIndex < lowerBound)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex),
                    "The start index must be greater than or equal to the array lower bound");
            }

            if (startIndex > upperBound)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex),
                    "The start index must be less than or equal to the array upper bound");
            }

            var i = 0;
            foreach (var item in source)
            {
                if (startIndex + i > upperBound)
                {
                    throw new ArgumentException(
                        "The array capacity is insufficient to copy all items from the source sequence");
                }
                array[startIndex + i] = item;
                i++;
            }
        }

        private static int IndexOf(IEnumerable<T> source, T item)
        {
            var entry = source
                .Select((x, i) => new { Value = x, Index = i })
                .FirstOrDefault(x => Equals(x.Value, item));
            return entry?.Index ?? -1;
        }

        public void Purge()
        {
            innerList.RemoveAll(wr => !wr.IsAlive);
        }
    }
}
