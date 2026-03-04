// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Marks a type as a union type and provides runtime access to the union's contents.
    /// </summary>
    public interface IUnion
    {
        /// <summary>
        /// Gets the value contained in the union, or <see langword="null"/> if the union has no value.
        /// </summary>
        object? Value { get; }
    }
}
