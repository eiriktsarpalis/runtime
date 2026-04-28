// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// Provides serialization metadata about a union type.
    /// </summary>
    /// <typeparam name="T">The union type to serialize or deserialize.</typeparam>
    /// <remarks>This API is for use by the output of the System.Text.Json source generator and should not be called directly.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class JsonUnionInfoValues<T>
    {
        /// <summary>
        /// Gets or sets the list of union case type metadata for the current union type.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Json source generator and should not be called directly.</remarks>
        public IList<JsonUnionCaseInfo>? UnionCases { get; init; }

        /// <summary>
        /// Gets or sets the delegate used to construct a union instance from a case type and case value.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Json source generator and should not be called directly.</remarks>
        public Func<Type, object?, T>? UnionConstructor { get; init; }

        /// <summary>
        /// Gets or sets the delegate used to deconstruct a union instance into its case type and case value.
        /// The returned case type can be <see langword="null"/> when the case value is <see langword="null"/>.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Json source generator and should not be called directly.</remarks>
        public Func<T, (Type? CaseType, object? CaseValue)>? UnionDeconstructor { get; init; }

        /// <summary>
        /// Gets or sets the delegate used to classify JSON payloads during union deserialization.
        /// </summary>
        /// <remarks>This API is for use by the output of the System.Text.Json source generator and should not be called directly.</remarks>
        public JsonTypeClassifier? TypeClassifier { get; init; }
    }
}
