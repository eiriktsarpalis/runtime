// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SourceGenerators;

namespace System.Text.Json.SourceGeneration
{
    /// <summary>
    /// Represents the <c>AssemblyDefaultJsonSerializerContext</c> generation spec,
    /// combining a standard <see cref="ContextGenerationSpec"/> for the synthetic context
    /// with metadata about the individual POCO types that need <c>IJsonSerializable&lt;T&gt;</c>.
    /// </summary>
    public sealed record AssemblyDefaultContextGenerationSpec
    {
        /// <summary>
        /// The context spec for the synthetic assembly-default context class.
        /// Used to emit the standard context files via the existing <see cref="JsonSourceGenerator.Emitter"/>.
        /// </summary>
        public required ContextGenerationSpec ContextSpec { get; init; }

        /// <summary>
        /// The POCO types that need per-type <c>IJsonSerializable&lt;T&gt;</c> implementations.
        /// </summary>
        public required ImmutableEquatableArray<PocoSerializableSpec> PocoTypes { get; init; }
    }
}
