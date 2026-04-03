// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SourceGenerators;

namespace System.Text.RegularExpressions.Generator
{
    /// <summary>
    /// Immutable snapshot of a parsed regex tree used as the incremental cache boundary.
    /// <see cref="RegexTree"/> is mutable and reference-based, so the generator needs a
    /// value-equatable representation in order for Roslyn to cache emission correctly.
    /// </summary>
    internal sealed record RegexTreeSpec(
        RegexNodeSpec Root,
        RegexOptions Options,
        int CaptureCount,
        string? CultureName,
        ImmutableEquatableArray<string>? CaptureNames,
        ImmutableEquatableDictionary<string, int>? CaptureNameToNumberMapping,
        ImmutableEquatableDictionary<int, int>? CaptureNumberSparseMapping,
        FindOptimizationsSpec FindOptimizations,
        bool HasIgnoreCase,
        bool HasRightToLeft);
}
