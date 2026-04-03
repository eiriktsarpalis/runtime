// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SourceGenerators;

namespace System.Text.RegularExpressions.Generator
{
    /// <summary>
    /// Immutable snapshot of a regex parse tree node used as the incremental cache boundary.
    /// <see cref="RegexNode"/> is mutable and only provides reference identity, so the generator
    /// needs a value-equatable model in order for Roslyn to cache emission correctly.
    /// </summary>
    internal sealed record RegexNodeSpec(
        RegexNodeKind Kind,
        RegexOptions Options,
        char Ch,
        string? Str,
        int M,
        int N,
        ImmutableEquatableArray<RegexNodeSpec> Children,
        bool IsAtomicByAncestor,
        bool MayBacktrack,
        bool MayContainCapture,
        bool IsInLoop);
}
