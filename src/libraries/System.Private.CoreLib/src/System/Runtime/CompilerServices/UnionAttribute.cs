// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that a class or struct is a union type. Union types have a closed set of case types,
    /// identified by single-parameter constructors or factory methods, and wrap their content in an
    /// <c>object? Value</c> property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class UnionAttribute : Attribute
    {
    }
}
