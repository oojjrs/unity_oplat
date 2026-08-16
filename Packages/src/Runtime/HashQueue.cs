using System.Collections.Generic;

namespace oojjrs.oplat
{
    internal class HashQueue<T>
    {
        private readonly HashSet<T> _hashSet = new();
        private readonly Queue<T> _queue = new();

        public int Count => _queue.Count;

        public void Clear()
        {
            _hashSet.Clear();
            _queue.Clear();
        }

        public bool Contains(T item)
        {
            return _hashSet.Contains(item);
        }

        public bool Enqueue(T item)
        {
            if (_hashSet.Add(item) == false)
                return false;

            _queue.Enqueue(item);
            return true;
        }

        public bool TryDequeue(out T item)
        {
            if (_queue.Count == 0)
            {
                item = default;
                return false;
            }

            item = _queue.Dequeue();
            _hashSet.Remove(item);
            return true;
        }
    }
}
