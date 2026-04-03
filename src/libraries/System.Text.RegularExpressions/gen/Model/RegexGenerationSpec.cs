// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SourceGenerators;

namespace System.Text.RegularExpressions.Generator
{
    public partial class RegexGenerator
    {
        /// <summary>
        /// Top-level incremental model. The regular <see cref="RegexMethod"/> instances are wrapped
        /// in an equatable envelope so Roslyn can compare successive results without us needing to
        /// maintain a mirrored immutable object graph for the regex tree.
        /// </summary>
        private sealed record RegexGenerationSpec
        {
            public required ImmutableEquatableSet<Equatable<RegexMethod, RegexMethodComparer>> RegexMethods { get; init; }
        }
    }
}
