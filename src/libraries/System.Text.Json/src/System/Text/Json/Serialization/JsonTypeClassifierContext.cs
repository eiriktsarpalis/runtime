// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Provides immutable metadata to a <see cref="JsonTypeClassifierFactory"/> when
    /// creating a <see cref="JsonTypeClassifier"/> delegate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This context is scenario-neutral: it works for both polymorphic types (where
    /// <see cref="TypeDiscriminatorPropertyName"/> and per-type discriminator values are
    /// populated) and union types (where discriminator metadata is absent).
    /// </para>
    /// <para>
    /// Instances are created internally by the serialization infrastructure. Users
    /// interact with the context through a <see cref="JsonTypeClassifierFactory"/>
    /// implementation.
    /// </para>
    /// </remarks>
    public sealed class JsonTypeClassifierContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonTypeClassifierContext"/> class.
        /// </summary>
        /// <param name="declaringType">The type being configured for classification.</param>
        /// <param name="candidateTypes">The candidate types for classification, with optional discriminator metadata.</param>
        /// <param name="typeDiscriminatorPropertyName">The JSON property name used for type discrimination, or <see langword="null"/>.</param>
        public JsonTypeClassifierContext(
            Type declaringType,
            IReadOnlyList<JsonDerivedType> candidateTypes,
            string? typeDiscriminatorPropertyName)
        {
            DeclaringType = declaringType;
            CandidateTypes = candidateTypes;
            TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName;
        }

        /// <summary>
        /// Gets the type being configured for classification.
        /// </summary>
        /// <remarks>
        /// For polymorphic types, this is the base class (e.g., <c>Animal</c>).
        /// For union types, this is the union type (e.g., <c>IntOrString</c>).
        /// </remarks>
        public Type DeclaringType { get; }

        /// <summary>
        /// Gets the candidate types for classification, with optional discriminator metadata.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For polymorphic types, each <see cref="JsonDerivedType"/> may carry a
        /// <see cref="JsonDerivedType.TypeDiscriminator"/> value (string or int).
        /// </para>
        /// <para>
        /// For union types, each entry has a <see langword="null"/>
        /// <see cref="JsonDerivedType.TypeDiscriminator"/> (unions don't use discriminator values).
        /// </para>
        /// </remarks>
        public IReadOnlyList<JsonDerivedType> CandidateTypes { get; }

        /// <summary>
        /// Gets the JSON property name used for type discrimination (e.g., <c>"$type"</c>, <c>"kind"</c>).
        /// </summary>
        /// <remarks>
        /// Populated from <see cref="Metadata.JsonPolymorphismOptions.TypeDiscriminatorPropertyName"/>
        /// for polymorphic types. <see langword="null"/> for union types (unions don't use
        /// discriminator properties by default).
        /// </remarks>
        public string? TypeDiscriminatorPropertyName { get; }
    }
}
