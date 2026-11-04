// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System;

/// <summary>Compares byte arrays for equality by their contents.</summary>
/// <remarks>
/// Two arrays are equal if they have the same length and contain the same bytes in the same order.
/// A <see langword="null"/> array is equal only to another <see langword="null"/> array, not to an empty array.
/// This comparer supports alternate lookups using <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/>.
/// Arrays must not be modified while they are used as keys in a collection that uses this comparer.
/// </remarks>
public sealed class ByteArrayOrdinalComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    /// <summary>Gets the singleton comparer that compares byte arrays by their contents.</summary>
    /// <value>The singleton byte-array content comparer.</value>
    public static ByteArrayOrdinalComparer Instance { get; } = new();

    private ByteArrayOrdinalComparer()
    {
    }

    /// <summary>Determines whether two byte arrays have equal contents.</summary>
    /// <param name="x">The first array to compare.</param>
    /// <param name="y">The second array to compare.</param>
    /// <returns><see langword="true"/> if the arrays have equal contents or are both <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(byte[]? x, byte[]? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

    /// <summary>Returns a hash code based on the contents of the specified byte array.</summary>
    /// <param name="obj">The array for which to get a hash code.</param>
    /// <returns>A hash code for the contents of <paramref name="obj"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
    /// <remarks>The hash code is not guaranteed to be stable across processes or versions of .NET.</remarks>
    public int GetHashCode(byte[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return ComputeHashCode(obj);
    }

    /// <inheritdoc/>
    byte[] IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>.Create(ReadOnlySpan<byte> alternate) =>
        alternate.ToArray();

    /// <inheritdoc/>
    bool IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>.Equals(ReadOnlySpan<byte> alternate, byte[] other) =>
        other is not null && alternate.SequenceEqual(other);

    /// <inheritdoc/>
    int IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>.GetHashCode(ReadOnlySpan<byte> alternate) =>
        ComputeHashCode(alternate);

    private static int ComputeHashCode(ReadOnlySpan<byte> value) =>
        Marvin.ComputeHash32(value, Marvin.DefaultSeed);
}
