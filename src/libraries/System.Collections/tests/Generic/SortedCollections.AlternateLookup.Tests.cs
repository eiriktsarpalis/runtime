// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace System.Collections.Tests
{
    public class SortedSet_AlternateLookup_Tests
    {
        [Fact]
        public void GetAlternateLookup_FailsWhenIncompatible()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);

            set.GetAlternateLookup<ReadOnlySpan<char>>();
            Assert.True(set.TryGetAlternateLookup<ReadOnlySpan<char>>(out _));

            Assert.Throws<InvalidOperationException>(() => set.GetAlternateLookup<ReadOnlySpan<byte>>());
            Assert.Throws<InvalidOperationException>(() => set.GetAlternateLookup<string>());
            Assert.Throws<InvalidOperationException>(() => set.GetAlternateLookup<int>());

            Assert.False(set.TryGetAlternateLookup<ReadOnlySpan<byte>>(out _));
            Assert.False(set.TryGetAlternateLookup<string>(out _));
            Assert.False(set.TryGetAlternateLookup<int>(out _));
        }

        public static IEnumerable<object[]> Comparers_MemberData()
        {
            yield return new object[] { StringComparer.Ordinal };
            yield return new object[] { StringComparer.OrdinalIgnoreCase };
            yield return new object[] { StringComparer.InvariantCulture };
            yield return new object[] { StringComparer.InvariantCultureIgnoreCase };
            yield return new object[] { StringComparer.CurrentCulture };
            yield return new object[] { StringComparer.CurrentCultureIgnoreCase };
        }

        [Theory]
        [MemberData(nameof(Comparers_MemberData))]
        public void SortedSet_GetAlternateLookup_OperationsMatchUnderlyingSet(IComparer<string> comparer)
        {
            var set = new SortedSet<string>(comparer);
            SortedSet<string>.AlternateLookup<ReadOnlySpan<char>> lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
            Assert.Same(set, lookup.Set);

            set.Add("hello");
            Assert.True(lookup.Contains("hello".AsSpan()));
            Assert.True(lookup.TryGetValue("hello".AsSpan(), out string? actual));
            Assert.Equal("hello", actual);

            Assert.False(lookup.Add("hello".AsSpan()));
            Assert.Equal(1, set.Count);

            Assert.True(lookup.Remove("hello".AsSpan()));
            Assert.False(set.Contains("hello"));
            Assert.Empty(set);

            Assert.True(lookup.Add("world".AsSpan()));
            Assert.True(set.Contains("world"));
            Assert.Single(set);

            Assert.False(lookup.Contains("missing".AsSpan()));
            Assert.False(lookup.TryGetValue("missing".AsSpan(), out _));
            Assert.False(lookup.Remove("missing".AsSpan()));
        }

        [Theory]
        [MemberData(nameof(Comparers_MemberData))]
        public void SortedSet_AlternateLookup_CaseSensitivity(IComparer<string> comparer)
        {
            var set = new SortedSet<string>(comparer);
            SortedSet<string>.AlternateLookup<ReadOnlySpan<char>> lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();

            lookup.Add("abc".AsSpan());

            bool isCaseSensitive =
                comparer.Equals(StringComparer.Ordinal) ||
                comparer.Equals(StringComparer.InvariantCulture) ||
                comparer.Equals(StringComparer.CurrentCulture);

            if (isCaseSensitive)
            {
                Assert.True(lookup.Contains("abc".AsSpan()));
                Assert.False(lookup.Contains("ABC".AsSpan()));
                Assert.True(lookup.Add("ABC".AsSpan()));
                Assert.Equal(2, set.Count);
            }
            else
            {
                Assert.True(lookup.Contains("abc".AsSpan()));
                Assert.True(lookup.Contains("ABC".AsSpan()));
                Assert.False(lookup.Add("ABC".AsSpan()));
                Assert.Single(set);
            }
        }

        [Fact]
        public void SortedSet_AlternateLookup_MultipleItems()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            SortedSet<string>.AlternateLookup<ReadOnlySpan<char>> lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(i, set.Count);
                Assert.True(lookup.Add(i.ToString().AsSpan()));
                Assert.False(lookup.Add(i.ToString().AsSpan()));
            }

            Assert.Equal(20, set.Count);

            for (int i = 0; i < 20; i++)
            {
                Assert.True(lookup.Contains(i.ToString().AsSpan()));
                Assert.True(lookup.TryGetValue(i.ToString().AsSpan(), out string? actual));
                Assert.Equal(i.ToString(), actual);
            }

            Assert.False(lookup.Contains("20".AsSpan()));
        }
    }

    public class SortedDictionary_AlternateLookup_Tests
    {
        [Fact]
        public void GetAlternateLookup_FailsWhenIncompatible()
        {
            var dictionary = new SortedDictionary<string, int>(StringComparer.Ordinal);

            dictionary.GetAlternateLookup<ReadOnlySpan<char>>();
            Assert.True(dictionary.TryGetAlternateLookup<ReadOnlySpan<char>>(out _));

            Assert.Throws<InvalidOperationException>(() => dictionary.GetAlternateLookup<ReadOnlySpan<byte>>());
            Assert.Throws<InvalidOperationException>(() => dictionary.GetAlternateLookup<string>());
            Assert.Throws<InvalidOperationException>(() => dictionary.GetAlternateLookup<int>());

            Assert.False(dictionary.TryGetAlternateLookup<ReadOnlySpan<byte>>(out _));
            Assert.False(dictionary.TryGetAlternateLookup<string>(out _));
            Assert.False(dictionary.TryGetAlternateLookup<int>(out _));
        }

        public static IEnumerable<object[]> Comparers_MemberData()
        {
            yield return new object[] { StringComparer.Ordinal };
            yield return new object[] { StringComparer.OrdinalIgnoreCase };
            yield return new object[] { StringComparer.InvariantCulture };
            yield return new object[] { StringComparer.InvariantCultureIgnoreCase };
            yield return new object[] { StringComparer.CurrentCulture };
            yield return new object[] { StringComparer.CurrentCultureIgnoreCase };
        }

        [Theory]
        [MemberData(nameof(Comparers_MemberData))]
        public void SortedDictionary_GetAlternateLookup_OperationsMatchUnderlyingDictionary(IComparer<string> comparer)
        {
            var dictionary = new SortedDictionary<string, int>(comparer);
            SortedDictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> lookup = dictionary.GetAlternateLookup<ReadOnlySpan<char>>();
            Assert.Same(dictionary, lookup.Dictionary);

            dictionary["123"] = 123;
            Assert.True(lookup.ContainsKey("123".AsSpan()));
            Assert.True(lookup.TryGetValue("123".AsSpan(), out int value));
            Assert.Equal(123, value);
            Assert.Equal(123, lookup["123".AsSpan()]);
            Assert.False(lookup.TryAdd("123".AsSpan(), 321));
            Assert.True(lookup.Remove("123".AsSpan()));
            Assert.False(dictionary.ContainsKey("123"));
            Assert.Throws<KeyNotFoundException>(() => lookup["123".AsSpan()]);

            Assert.True(lookup.TryAdd("123".AsSpan(), 123));
            Assert.True(dictionary.ContainsKey("123"));
            lookup.TryGetValue("123".AsSpan(), out value);
            Assert.Equal(123, value);
            Assert.False(lookup.Remove("321".AsSpan(), out int removedValue));
            Assert.Equal(0, removedValue);
            Assert.True(lookup.Remove("123".AsSpan(), out removedValue));
            Assert.Equal(123, removedValue);

            lookup["a".AsSpan()] = 42;

            bool isCaseSensitive =
                comparer.Equals(StringComparer.Ordinal) ||
                comparer.Equals(StringComparer.InvariantCulture) ||
                comparer.Equals(StringComparer.CurrentCulture);

            if (isCaseSensitive)
            {
                Assert.True(lookup.TryGetValue("a".AsSpan(), out value));
                Assert.Equal(42, value);
                Assert.True(lookup.TryAdd("A".AsSpan(), 42));
                Assert.True(lookup.Remove("a".AsSpan()));
                Assert.False(lookup.Remove("a".AsSpan()));
                Assert.True(lookup.Remove("A".AsSpan()));
            }
            else
            {
                Assert.True(lookup.TryGetValue("A".AsSpan(), out value));
                Assert.Equal(42, value);
                Assert.False(lookup.TryAdd("A".AsSpan(), 42));
                Assert.True(lookup.Remove("A".AsSpan()));
                Assert.False(lookup.Remove("a".AsSpan()));
            }

            lookup["a".AsSpan()] = 42;
            Assert.Equal(42, dictionary["a"]);
            lookup["a".AsSpan()] = 43;
            Assert.True(lookup.Remove("a".AsSpan(), out value));
            Assert.Equal(43, value);

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(i, dictionary.Count);
                Assert.True(lookup.TryAdd(i.ToString().AsSpan(), i));
                Assert.False(lookup.TryAdd(i.ToString().AsSpan(), i));
            }

            Assert.Equal(10, dictionary.Count);

            for (int i = -1; i <= 10; i++)
            {
                Assert.Equal(dictionary.TryGetValue(i.ToString(), out int dv), lookup.TryGetValue(i.ToString().AsSpan(), out int lv));
                Assert.Equal(dv, lv);
            }
        }
    }

    public class SortedList_AlternateLookup_Tests
    {
        [Fact]
        public void GetAlternateLookup_FailsWhenIncompatible()
        {
            var list = new SortedList<string, int>(StringComparer.Ordinal);

            list.GetAlternateLookup<ReadOnlySpan<char>>();
            Assert.True(list.TryGetAlternateLookup<ReadOnlySpan<char>>(out _));

            Assert.Throws<InvalidOperationException>(() => list.GetAlternateLookup<ReadOnlySpan<byte>>());
            Assert.Throws<InvalidOperationException>(() => list.GetAlternateLookup<string>());
            Assert.Throws<InvalidOperationException>(() => list.GetAlternateLookup<int>());

            Assert.False(list.TryGetAlternateLookup<ReadOnlySpan<byte>>(out _));
            Assert.False(list.TryGetAlternateLookup<string>(out _));
            Assert.False(list.TryGetAlternateLookup<int>(out _));
        }

        public static IEnumerable<object[]> Comparers_MemberData()
        {
            yield return new object[] { StringComparer.Ordinal };
            yield return new object[] { StringComparer.OrdinalIgnoreCase };
            yield return new object[] { StringComparer.InvariantCulture };
            yield return new object[] { StringComparer.InvariantCultureIgnoreCase };
            yield return new object[] { StringComparer.CurrentCulture };
            yield return new object[] { StringComparer.CurrentCultureIgnoreCase };
        }

        [Theory]
        [MemberData(nameof(Comparers_MemberData))]
        public void SortedList_GetAlternateLookup_OperationsMatchUnderlyingList(IComparer<string> comparer)
        {
            var list = new SortedList<string, int>(comparer);
            SortedList<string, int>.AlternateLookup<ReadOnlySpan<char>> lookup = list.GetAlternateLookup<ReadOnlySpan<char>>();
            Assert.Same(list, lookup.List);

            list["123"] = 123;
            Assert.True(lookup.ContainsKey("123".AsSpan()));
            Assert.True(lookup.TryGetValue("123".AsSpan(), out int value));
            Assert.Equal(123, value);
            Assert.Equal(123, lookup["123".AsSpan()]);
            Assert.False(lookup.TryAdd("123".AsSpan(), 321));
            Assert.True(lookup.Remove("123".AsSpan()));
            Assert.False(list.ContainsKey("123"));
            Assert.Throws<KeyNotFoundException>(() => lookup["123".AsSpan()]);

            Assert.True(lookup.TryAdd("123".AsSpan(), 123));
            Assert.True(list.ContainsKey("123"));
            lookup.TryGetValue("123".AsSpan(), out value);
            Assert.Equal(123, value);
            Assert.False(lookup.Remove("321".AsSpan(), out int removedValue));
            Assert.Equal(0, removedValue);
            Assert.True(lookup.Remove("123".AsSpan(), out removedValue));
            Assert.Equal(123, removedValue);

            lookup["a".AsSpan()] = 42;

            bool isCaseSensitive =
                comparer.Equals(StringComparer.Ordinal) ||
                comparer.Equals(StringComparer.InvariantCulture) ||
                comparer.Equals(StringComparer.CurrentCulture);

            if (isCaseSensitive)
            {
                Assert.True(lookup.TryGetValue("a".AsSpan(), out value));
                Assert.Equal(42, value);
                Assert.True(lookup.TryAdd("A".AsSpan(), 42));
                Assert.True(lookup.Remove("a".AsSpan()));
                Assert.False(lookup.Remove("a".AsSpan()));
                Assert.True(lookup.Remove("A".AsSpan()));
            }
            else
            {
                Assert.True(lookup.TryGetValue("A".AsSpan(), out value));
                Assert.Equal(42, value);
                Assert.False(lookup.TryAdd("A".AsSpan(), 42));
                Assert.True(lookup.Remove("A".AsSpan()));
                Assert.False(lookup.Remove("a".AsSpan()));
            }

            lookup["a".AsSpan()] = 42;
            Assert.Equal(42, list["a"]);
            lookup["a".AsSpan()] = 43;
            Assert.True(lookup.Remove("a".AsSpan(), out value));
            Assert.Equal(43, value);

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(i, list.Count);
                Assert.True(lookup.TryAdd(i.ToString().AsSpan(), i));
                Assert.False(lookup.TryAdd(i.ToString().AsSpan(), i));
            }

            Assert.Equal(10, list.Count);

            for (int i = -1; i <= 10; i++)
            {
                Assert.Equal(list.TryGetValue(i.ToString(), out int dv), lookup.TryGetValue(i.ToString().AsSpan(), out int lv));
                Assert.Equal(dv, lv);
            }
        }

        [Theory]
        [MemberData(nameof(Comparers_MemberData))]
        public void SortedList_AlternateLookup_IndexOfKey(IComparer<string> comparer)
        {
            var list = new SortedList<string, int>(comparer);
            SortedList<string, int>.AlternateLookup<ReadOnlySpan<char>> lookup = list.GetAlternateLookup<ReadOnlySpan<char>>();

            list.Add("alpha", 1);
            list.Add("beta", 2);
            list.Add("gamma", 3);

            Assert.Equal(list.IndexOfKey("alpha"), lookup.IndexOfKey("alpha".AsSpan()));
            Assert.Equal(list.IndexOfKey("beta"), lookup.IndexOfKey("beta".AsSpan()));
            Assert.Equal(list.IndexOfKey("gamma"), lookup.IndexOfKey("gamma".AsSpan()));
            Assert.Equal(-1, lookup.IndexOfKey("delta".AsSpan()));
        }
    }
}
