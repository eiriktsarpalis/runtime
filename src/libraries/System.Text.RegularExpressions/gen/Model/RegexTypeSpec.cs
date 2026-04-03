// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.RegularExpressions.Generator
{
    /// <summary>
    /// Immutable snapshot of the type hierarchy that declares a generated regex member.
    /// This provides a value-equatable cache key for Roslyn while the emitter continues to
    /// use its existing mutable <c>RegexType</c> helper.
    /// </summary>
    internal sealed record RegexTypeSpec(
        string Keyword,
        string Namespace,
        string Name,
        RegexTypeSpec? Parent)
    {
        public string FullName { get; } = Parent is null ? Name : $"{Parent.FullName}.{Name}";
    }
}
