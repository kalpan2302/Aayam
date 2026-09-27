using NUnit.Framework;
using UnityEngine;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class ModelCacheTests
    {
        [Test]
        public void LRU_OldestEvictedFirst()
        {
            var cache = new TestCache(maxSize: 3);

            cache.Add("A", 1);
            cache.Add("B", 1);
            cache.Add("C", 1);
            cache.Add("D", 1);

            Assert.IsFalse(cache.Contains("A"), "A should be evicted");
            Assert.IsTrue(cache.Contains("B"));
            Assert.IsTrue(cache.Contains("C"));
            Assert.IsTrue(cache.Contains("D"));
        }

        [Test]
        public void LRU_AccessUpdatesOrder()
        {
            var cache = new TestCache(maxSize: 3);

            cache.Add("A", 1);
            cache.Add("B", 1);
            cache.Add("C", 1);

            cache.Access("A");

            cache.Add("D", 1);

            Assert.IsTrue(cache.Contains("A"), "A was accessed, should survive");
            Assert.IsFalse(cache.Contains("B"), "B should be evicted");
            Assert.IsTrue(cache.Contains("C"));
            Assert.IsTrue(cache.Contains("D"));
        }

        [Test]
        public void Cache_DuplicateKey_UpdatesInPlace()
        {
            var cache = new TestCache(maxSize: 5);

            cache.Add("A", 1);
            cache.Add("A", 2);

            Assert.AreEqual(1, cache.Count, "Should not create duplicate entries");
            Assert.AreEqual(2, cache.GetSize("A"), "Size should be updated");
        }

        [Test]
        public void Cache_ClearAll_Empty()
        {
            var cache = new TestCache(maxSize: 5);
            cache.Add("A", 1);
            cache.Add("B", 2);

            cache.Clear();

            Assert.AreEqual(0, cache.Count);
            Assert.IsFalse(cache.Contains("A"));
            Assert.IsFalse(cache.Contains("B"));
        }

        [Test]
        public void Cache_SizeTracking_Accurate()
        {
            var cache = new TestCache(maxSize: 100);
            cache.Add("A", 10);
            cache.Add("B", 20);
            cache.Add("C", 30);

            Assert.AreEqual(60, cache.TotalSize);
        }

        [Test]
        public void Cache_Evict_ReducesSize()
        {
            var cache = new TestCache(maxSize: 3);
            cache.Add("A", 10);
            cache.Add("B", 20);
            cache.Add("C", 30);

            cache.Evict("B");

            Assert.AreEqual(2, cache.Count);
            Assert.AreEqual(40, cache.TotalSize);
            Assert.IsFalse(cache.Contains("B"));
        }

        private class TestCache
        {
            private readonly int _maxSize;
            private readonly System.Collections.Generic.List<(string key, long size, int accessOrder)> _entries = new();
            private int _accessCounter;

            public int  Count     => _entries.Count;
            public long TotalSize => _entries.ConvertAll(e => e.size).FindAll(_ => true).Count > 0 ? Sum() : 0;

            public TestCache(int maxSize) { _maxSize = maxSize; }

            public void Add(string key, long size)
            {
                _entries.RemoveAll(e => e.key == key);

                while (_entries.Count >= _maxSize)
                {
                    int minIdx = 0;
                    for (int i = 1; i < _entries.Count; i++)
                        if (_entries[i].accessOrder < _entries[minIdx].accessOrder)
                            minIdx = i;
                    _entries.RemoveAt(minIdx);
                }

                _entries.Add((key, size, ++_accessCounter));
            }

            public void Access(string key)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].key == key)
                    {
                        _entries[i] = (_entries[i].key, _entries[i].size, ++_accessCounter);
                        return;
                    }
                }
            }

            public bool Contains(string key) => _entries.Exists(e => e.key == key);

            public long GetSize(string key)
            {
                var entry = _entries.Find(e => e.key == key);
                return entry.size;
            }

            public void Evict(string key) => _entries.RemoveAll(e => e.key == key);

            public void Clear() => _entries.Clear();

            private long Sum()
            {
                long s = 0;
                foreach (var e in _entries) s += e.size;
                return s;
            }
        }
    }
}
