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
    /// The context carries two parallel candidate lists corresponding to the two
    /// classifier scenarios. A type is treated as a union when <see cref="UnionCases"/>
    /// is non-empty, and as a polymorphic type when <see cref="DerivedTypes"/> is
    /// non-empty. Exactly one of the two lists is populated for any given context.
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
        /// <param name="unionCases">The union cases of the declaring type, or an empty list.</param>
        /// <param name="derivedTypes">The derived types of the declaring type, or an empty list.</param>
        /// <param name="typeDiscriminatorPropertyName">The JSON property name used for type discrimination, or <see langword="null"/>.</param>
        public JsonTypeClassifierContext(
            Type declaringType,
            IReadOnlyList<JsonUnionCaseInfo> unionCases,
            IReadOnlyList<JsonDerivedType> derivedTypes,
            string? typeDiscriminatorPropertyName)
        {
            DeclaringType = declaringType;
            UnionCases = unionCases;
            DerivedTypes = derivedTypes;
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
        /// Gets the union cases of <see cref="DeclaringType"/>.
        /// </summary>
        /// <remarks>
        /// Non-empty when <see cref="DeclaringType"/> is configured as a union type. The
        /// list mirrors <see cref="JsonTypeInfo.UnionCases"/> on the resolved
        /// <see cref="JsonTypeInfo"/>.
        /// </remarks>
        public IReadOnlyList<JsonUnionCaseInfo> UnionCases { get; }

        /// <summary>
        /// Gets the derived types of <see cref="DeclaringType"/>.
        /// </summary>
        /// <remarks>
        /// Non-empty when <see cref="DeclaringType"/> is configured as a polymorphic
        /// type. The list mirrors
        /// <see cref="Metadata.JsonPolymorphismOptions.DerivedTypes"/>; each entry may
        /// carry a <see cref="JsonDerivedType.TypeDiscriminator"/> string or integer.
        /// </remarks>
        public IReadOnlyList<JsonDerivedType> DerivedTypes { get; }

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
