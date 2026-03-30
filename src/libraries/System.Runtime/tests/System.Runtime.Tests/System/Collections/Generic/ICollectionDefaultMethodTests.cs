// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace System.Collections.Generic.Tests
{
    public class ICollectionDefaultMethodTests
    {
        /// <summary>
        /// Minimal ICollection&lt;T&gt; that does not override the DIMs,
        /// so the default implementations are exercised.
        /// </summary>
        private sealed class SimpleCollection<T> : ICollection<T>
        {
            private readonly List<T> _inner = new();

            public int Count => _inner.Count;
            public bool IsReadOnly => false;
            public void Add(T item) => _inner.Add(item);
            public void Clear() => _inner.Clear();
            public bool Contains(T item) => _inner.Contains(item);
            public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
            public bool Remove(T item) => _inner.Remove(item);
            public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void AddRange_AddsAllElements()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            collection.AddRange(new[] { 1, 2, 3, 4, 5 });

            Assert.Equal(5, collection.Count);
            Assert.Contains(1, collection);
            Assert.Contains(5, collection);
        }

        [Fact]
        public void AddRange_EmptySource_NoChange()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            collection.Add(42);
            collection.AddRange(Array.Empty<int>());

            Assert.Equal(1, collection.Count);
        }

        [Fact]
        public void AddRange_NullCollection_ThrowsArgumentNullException()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            Assert.Throws<ArgumentNullException>("collection", () => collection.AddRange(null!));
        }

        [Fact]
        public void AddRange_AppendsToExisting()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            collection.Add(0);
            collection.AddRange(new[] { 1, 2, 3 });

            Assert.Equal(4, collection.Count);
        }

        [Fact]
        public void RemoveAll_RemovesMatchingElements()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            collection.AddRange(new[] { 1, 2, 3, 4, 5, 6 });

            int removed = collection.RemoveAll(x => x % 2 == 0);

            Assert.Equal(3, removed);
            Assert.Equal(3, collection.Count);
            Assert.DoesNotContain(2, collection);
            Assert.DoesNotContain(4, collection);
            Assert.DoesNotContain(6, collection);
            Assert.Contains(1, collection);
            Assert.Contains(3, collection);
            Assert.Contains(5, collection);
        }

        [Fact]
        public void RemoveAll_NoMatches_ReturnsZero()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            collection.AddRange(new[] { 1, 2, 3 });

            int removed = collection.RemoveAll(x => x > 100);

            Assert.Equal(0, removed);
            Assert.Equal(3, collection.Count);
        }

        [Fact]
        public void RemoveAll_AllMatch_RemovesAll()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            collection.AddRange(new[] { 2, 4, 6 });

            int removed = collection.RemoveAll(x => x % 2 == 0);

            Assert.Equal(3, removed);
            Assert.Equal(0, collection.Count);
        }

        [Fact]
        public void RemoveAll_EmptyCollection_ReturnsZero()
        {
            ICollection<int> collection = new SimpleCollection<int>();

            int removed = collection.RemoveAll(x => true);

            Assert.Equal(0, removed);
        }

        [Fact]
        public void RemoveAll_NullPredicate_ThrowsArgumentNullException()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            Assert.Throws<ArgumentNullException>("match", () => collection.RemoveAll(null!));
        }

        [Fact]
        public void RemoveAll_DuplicateElements_RemovesAllOccurrences()
        {
            ICollection<int> collection = new SimpleCollection<int>();
            collection.AddRange(new[] { 1, 2, 2, 3, 2, 4 });

            int removed = collection.RemoveAll(x => x == 2);

            Assert.Equal(3, removed);
            Assert.Equal(3, collection.Count);
            Assert.DoesNotContain(2, collection);
        }

        [Fact]
        public void AddRange_WorksWithList()
        {
            ICollection<int> list = new List<int> { 1, 2 };
            list.AddRange(new[] { 3, 4 });

            Assert.Equal(4, list.Count);
            Assert.Contains(3, list);
            Assert.Contains(4, list);
        }

        [Fact]
        public void RemoveAll_WorksWithList()
        {
            ICollection<int> list = new List<int> { 1, 2, 3, 4, 5 };
            int removed = list.RemoveAll(x => x > 3);

            Assert.Equal(2, removed);
            Assert.Equal(3, list.Count);
        }
    }

    public class IListDefaultMethodTests
    {
        /// <summary>
        /// Minimal IList&lt;T&gt; that does not override the DIMs,
        /// so the default implementations are exercised.
        /// </summary>
        private sealed class SimpleList<T> : IList<T>
        {
            private readonly List<T> _inner = new();

            public T this[int index]
            {
                get => _inner[index];
                set => _inner[index] = value;
            }

            public int Count => _inner.Count;
            public bool IsReadOnly => false;
            public void Add(T item) => _inner.Add(item);
            public void Clear() => _inner.Clear();
            public bool Contains(T item) => _inner.Contains(item);
            public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
            public int IndexOf(T item) => _inner.IndexOf(item);
            public void Insert(int index, T item) => _inner.Insert(index, item);
            public bool Remove(T item) => _inner.Remove(item);
            public void RemoveAt(int index) => _inner.RemoveAt(index);
            public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void InsertRange_InsertsAtCorrectPosition()
        {
            IList<int> list = new SimpleList<int>();
            list.Add(1);
            list.Add(4);
            list.InsertRange(1, new[] { 2, 3 });

            Assert.Equal(4, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
            Assert.Equal(3, list[2]);
            Assert.Equal(4, list[3]);
        }

        [Fact]
        public void InsertRange_AtStart()
        {
            IList<int> list = new SimpleList<int>();
            list.Add(3);
            list.InsertRange(0, new[] { 1, 2 });

            Assert.Equal(3, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
            Assert.Equal(3, list[2]);
        }

        [Fact]
        public void InsertRange_AtEnd()
        {
            IList<int> list = new SimpleList<int>();
            list.Add(1);
            list.InsertRange(1, new[] { 2, 3 });

            Assert.Equal(3, list.Count);
            Assert.Equal(2, list[1]);
            Assert.Equal(3, list[2]);
        }

        [Fact]
        public void InsertRange_EmptyCollection_NoChange()
        {
            IList<int> list = new SimpleList<int>();
            list.Add(1);
            list.InsertRange(0, Array.Empty<int>());

            Assert.Equal(1, list.Count);
        }

        [Fact]
        public void InsertRange_NullCollection_ThrowsArgumentNullException()
        {
            IList<int> list = new SimpleList<int>();
            Assert.Throws<ArgumentNullException>("collection", () => list.InsertRange(0, null!));
        }

        [Fact]
        public void RemoveRange_RemovesCorrectElements()
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in new[] { 1, 2, 3, 4, 5 }) list.Add(i);

            list.RemoveRange(1, 3);

            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(5, list[1]);
        }

        [Fact]
        public void RemoveRange_RemoveFromStart()
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in new[] { 1, 2, 3, 4 }) list.Add(i);

            list.RemoveRange(0, 2);

            Assert.Equal(2, list.Count);
            Assert.Equal(3, list[0]);
            Assert.Equal(4, list[1]);
        }

        [Fact]
        public void RemoveRange_RemoveFromEnd()
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in new[] { 1, 2, 3, 4 }) list.Add(i);

            list.RemoveRange(2, 2);

            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
        }

        [Fact]
        public void RemoveRange_ZeroCount_NoChange()
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in new[] { 1, 2, 3 }) list.Add(i);

            list.RemoveRange(1, 0);

            Assert.Equal(3, list.Count);
        }

        [Fact]
        public void RemoveAll_IList_RemovesMatchingElements()
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in new[] { 1, 2, 3, 4, 5, 6 }) list.Add(i);

            int removed = ((ICollection<int>)list).RemoveAll(x => x % 2 == 0);

            Assert.Equal(3, removed);
            Assert.Equal(3, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(3, list[1]);
            Assert.Equal(5, list[2]);
        }

        [Fact]
        public void RemoveAll_IList_NoMatches_ReturnsZero()
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in new[] { 1, 2, 3 }) list.Add(i);

            int removed = ((ICollection<int>)list).RemoveAll(x => x > 100);

            Assert.Equal(0, removed);
            Assert.Equal(3, list.Count);
        }

        [Fact]
        public void RemoveAll_IList_AllMatch_RemovesAll()
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in new[] { 2, 4, 6 }) list.Add(i);

            int removed = ((ICollection<int>)list).RemoveAll(x => x % 2 == 0);

            Assert.Equal(3, removed);
            Assert.Empty(list);
        }

        [Fact]
        public void RemoveAll_IList_NullPredicate_ThrowsArgumentNullException()
        {
            IList<int> list = new SimpleList<int>();
            Assert.Throws<ArgumentNullException>("match", () => ((ICollection<int>)list).RemoveAll(null!));
        }

        [Theory]
        [InlineData(new[] { 1, 2, 3, 4, 5 }, new[] { 10, 20 }, 2, new[] { 1, 2, 10, 20, 3, 4, 5 })]
        [InlineData(new[] { 1 }, new[] { 2, 3 }, 0, new[] { 2, 3, 1 })]
        [InlineData(new[] { 1 }, new[] { 2, 3 }, 1, new[] { 1, 2, 3 })]
        [InlineData(new int[0], new[] { 1, 2, 3 }, 0, new[] { 1, 2, 3 })]
        public void InsertRange_Theory(int[] initial, int[] toInsert, int index, int[] expected)
        {
            IList<int> list = new SimpleList<int>();
            foreach (int i in initial) list.Add(i);

            list.InsertRange(index, toInsert);

            Assert.Equal(expected.Length, list.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], list[i]);
            }
        }

        [Fact]
        public void InsertRange_WorksWithList()
        {
            IList<int> list = new List<int> { 1, 4 };
            list.InsertRange(1, new[] { 2, 3 });

            Assert.Equal(4, list.Count);
            Assert.Equal(2, list[1]);
            Assert.Equal(3, list[2]);
        }

        [Fact]
        public void RemoveRange_WorksWithList()
        {
            IList<int> list = new List<int> { 1, 2, 3, 4, 5 };
            list.RemoveRange(1, 3);

            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(5, list[1]);
        }

        [Fact]
        public void AddRange_WorksWithCollectionT()
        {
            ICollection<int> collection = new Collection<int> { 1, 2 };
            collection.AddRange(new[] { 3, 4, 5 });

            Assert.Equal(5, collection.Count);
        }

        [Fact]
        public void RemoveAll_WorksWithCollectionT()
        {
            ICollection<int> collection = new Collection<int> { 1, 2, 3, 4, 5 };
            int removed = collection.RemoveAll(x => x > 3);

            Assert.Equal(2, removed);
            Assert.Equal(3, collection.Count);
        }
    }
}
