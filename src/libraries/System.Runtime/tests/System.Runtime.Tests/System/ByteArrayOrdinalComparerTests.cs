// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Tests;

public class ByteArrayOrdinalComparerTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, new byte[0], false)]
    [InlineData(new byte[0], null, false)]
    [InlineData(new byte[0], new byte[0], true)]
    [InlineData(new byte[] { 0 }, new byte[0], false)]
    [InlineData(new byte[] { 0 }, new byte[] { 0 }, true)]
    [InlineData(new byte[] { 0, 1, 255 }, new byte[] { 0, 1, 255 }, true)]
    [InlineData(new byte[] { 0, 1, 255 }, new byte[] { 0, 255, 1 }, false)]
    [InlineData(new byte[] { 0, 1 }, new byte[] { 0, 1, 0 }, false)]
    public void Equals_ExpectedResult(byte[] x, byte[] y, bool expected)
    {
        ByteArrayOrdinalComparer comparer = ByteArrayOrdinalComparer.Instance;
        Assert.Same(comparer, ByteArrayOrdinalComparer.Instance);
        Assert.Equal(expected, comparer.Equals(x, y));
        Assert.Equal(expected, comparer.Equals(y, x));
        Assert.True(comparer.Equals(x, x));
    }

    [Fact]
    public void GetHashCode_Null_Throws()
    {
        AssertExtensions.Throws<ArgumentNullException>("obj", () => ByteArrayOrdinalComparer.Instance.GetHashCode(null));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(new byte[0], true)]
    [InlineData(new byte[] { 0 }, false)]
    public void AlternateComparer_EmptySpanEquality(byte[] other, bool expected)
    {
        IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]> comparer = ByteArrayOrdinalComparer.Instance;
        Assert.Equal(expected, comparer.Equals([], other));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(255)]
    public void Dictionary_ContentEqualityAndAlternateLookup(int length)
    {
        byte[] key = new byte[length];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)i;
        }

        byte[] copy = key.AsSpan().ToArray();
        ByteArrayOrdinalComparer comparer = ByteArrayOrdinalComparer.Instance;
        Assert.Equal(comparer.GetHashCode(key), comparer.GetHashCode(copy));

        Dictionary<byte[], int> dictionary = new(comparer) { [key] = 42 };
        Assert.Equal(42, dictionary[copy]);
        Assert.False(dictionary.TryAdd(copy, 43));
        Assert.False(dictionary.ContainsKey(new byte[length + 1]));
        Assert.True(dictionary.TryGetAlternateLookup<ReadOnlySpan<byte>>(out var lookup));
        Assert.True(lookup.TryGetValue(copy, out byte[] actualKey, out int value));
        Assert.Same(key, actualKey);
        Assert.Equal(42, value);

        if (length > 0)
        {
            copy[length / 2] ^= 0xFF;
            Assert.False(lookup.ContainsKey(copy));
            Assert.False(dictionary.ContainsKey(copy));
        }

        Assert.True(lookup.Remove(key));
        Assert.Empty(dictionary);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(8, 3)]
    [InlineData(17, 1)]
    [InlineData(64, 2)]
    public void AlternateLookup_InsertionCopiesSpan(int length, int offset)
    {
        byte[] buffer = new byte[length + offset + 1];
        Span<byte> key = buffer.AsSpan(offset, length);
        key.Fill(42);
        byte[] expected = key.ToArray();

        Dictionary<byte[], int> dictionary = new(ByteArrayOrdinalComparer.Instance);
        var lookup = dictionary.GetAlternateLookup<ReadOnlySpan<byte>>();
        Assert.True(lookup.TryAdd(key, 17));
        Assert.False(lookup.TryAdd(expected, 18));
        Assert.True(lookup.TryGetValue(expected, out byte[] actualKey, out int value));
        Assert.NotSame(buffer, actualKey);
        Assert.Equal(expected, actualKey);
        Assert.Equal(17, value);

        key.Fill(255);
        Assert.Equal(17, dictionary[expected]);
        Assert.Equal(expected, actualKey);

        lookup[expected] = 19;
        Assert.Single(dictionary);
        Assert.Equal(19, dictionary[expected]);
        Assert.True(lookup.Remove(expected));
        Assert.False(lookup.TryGetValue(expected, out _));
    }

    [Fact]
    public void HashSet_DistinguishesNullAndEmpty()
    {
        HashSet<byte[]> set = new(ByteArrayOrdinalComparer.Instance);
        Assert.True(set.Add(null));
        Assert.True(set.Add([]));
        Assert.False(set.Add([]));
        Assert.True(set.Add([1, 2, 3]));
        Assert.False(set.Add([1, 2, 3]));

        var lookup = set.GetAlternateLookup<ReadOnlySpan<byte>>();
        Assert.True(lookup.TryGetValue([], out byte[] actual));
        Assert.NotNull(actual);
        Assert.Empty(actual);
        Assert.True(lookup.Remove([]));
        Assert.False(lookup.Contains([]));
        Assert.True(set.Contains(null));
        Assert.True(lookup.TryGetValue([1, 2, 3], out actual));
        Assert.Equal(new byte[] { 1, 2, 3 }, actual);
        Assert.Equal(2, set.Count);
    }
}
