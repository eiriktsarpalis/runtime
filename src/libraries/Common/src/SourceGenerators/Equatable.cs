// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace SourceGenerators
{
    /// <summary>
    /// Wraps a value and delegates equality to a custom comparer, allowing it to participate
    /// in APIs that require <see cref="IEquatable{T}"/>.
    /// </summary>
    internal readonly struct Equatable<T, TComparer> : IEquatable<Equatable<T, TComparer>>
        where TComparer : IEqualityComparer<T>, new()
    {
        private static readonly TComparer s_comparer = new();

        public Equatable(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public bool Equals(Equatable<T, TComparer> other) => s_comparer.Equals(Value, other.Value);

        public override bool Equals(object? obj) => obj is Equatable<T, TComparer> other && Equals(other);

        public override int GetHashCode() => Value is null ? 0 : s_comparer.GetHashCode(Value);
    }
}
