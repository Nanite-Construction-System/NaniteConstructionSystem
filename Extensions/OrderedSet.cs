using System.Collections;
using System.Collections.Generic;

namespace NaniteConstructionSystem.Extensions
{
    public class OrderedSet<T> : ICollection<T>
    {
        private readonly IDictionary<T, LinkedListNode<T>> m_dictionary;
        private readonly LinkedList<T> m_linkedList;

        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        {
        }

        public OrderedSet(IEqualityComparer<T> comparer)
        {
            m_dictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
            m_linkedList = new LinkedList<T>();
        }

        public T this[int i]
        {
            get
            {
                int pos = 0;
                foreach(var item in m_linkedList)
                {
                    if (pos == i)
                        return item;

                    pos++;
                }

                return default(T);
            }
        }

        public int Count
        {
            get { return m_dictionary.Count; }
        }

        public virtual bool IsReadOnly
        {
            get { return m_dictionary.IsReadOnly; }
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public bool Add(T item)
        {
            if (m_dictionary.ContainsKey(item)) return false;
            LinkedListNode<T> node = m_linkedList.AddLast(item);
            m_dictionary.Add(item, node);
            return true;
        }

        public bool AddStart(T item)
        {
            if (m_dictionary.ContainsKey(item))
                return false;

            LinkedListNode<T> node = m_linkedList.AddFirst(item);
            m_dictionary.Add(item, node);
            return true;
        }

        public void Clear()
        {
            m_linkedList.Clear();
            m_dictionary.Clear();
        }

        public bool Remove(T item)
        {
            LinkedListNode<T> node;
            bool found = m_dictionary.TryGetValue(item, out node);
            if (!found) return false;
            m_dictionary.Remove(item);
            m_linkedList.Remove(node);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_linkedList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(T item)
        {
            return m_dictionary.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_linkedList.CopyTo(array, arrayIndex);
        }
    }
}
