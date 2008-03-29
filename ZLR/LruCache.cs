using System;
using System.Collections.Generic;
using System.Text;

namespace ZLR.VM
{
    /// <summary>
    /// Implements a cache which discards the least recently used items.
    /// </summary>
    /// <typeparam name="K">The type of keys in the cache.</typeparam>
    /// <typeparam name="V">The type of values being cached.</typeparam>
    public class LruCache<K,V>
    {
        private struct Entry
        {
            public readonly K Key;
            public readonly V Value;
            public readonly int Size;

            public Entry(K key, V value, int size)
            {
                this.Key = key;
                this.Value = value;
                this.Size = size;
            }
        }

        private readonly Dictionary<K, LinkedListNode<Entry>> dict;
        private readonly LinkedList<Entry> llist;
        private int maxSize, currentSize, peakSize;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cacheSize">The maximum total size that the cache can
        /// reach before it starts discarding items.</param>
        public LruCache(int cacheSize)
        {
            this.maxSize = cacheSize;

            dict = new Dictionary<K, LinkedListNode<Entry>>();
            llist = new LinkedList<Entry>();
        }

        public int Count
        {
            get { return dict.Count; }
        }

        public int CurrentSize
        {
            get { return currentSize; }
        }

        public int MaxSize
        {
            get { return maxSize; }
        }

        public int PeakSize
        {
            get { return peakSize; }
        }

        public void Add(K key, V value, int size)
        {
            LinkedListNode<Entry> node = new LinkedListNode<Entry>(new Entry(key, value, size));

            while (currentSize + size > maxSize && dict.Count > 0)
            {
                Entry lastEntry = llist.Last.Value;
                llist.RemoveLast();
                dict.Remove(lastEntry.Key);
                currentSize -= lastEntry.Size;
            }

            dict.Add(key, node);
            llist.AddFirst(node);
            currentSize += size;

            if (currentSize > peakSize)
                peakSize = currentSize;
        }

        public bool TryGetValue(K key, out V value)
        {
            LinkedListNode<Entry> node;
            if (dict.TryGetValue(key, out node) == false)
            {
                value = default(V);
                return false;
            }

            if (node != llist.First)
            {
                llist.Remove(node);
                llist.AddFirst(node);
            }

            value = node.Value.Value;
            return true;
        }

        public IEnumerable<K> Keys
        {
            get { return dict.Keys; }
        }

        public IEnumerable<V> Values
        {
            get
            {
                foreach (Entry e in llist)
                    yield return e.Value;
            }
        }
    }
}
