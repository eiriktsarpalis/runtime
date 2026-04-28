// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed on a type, indicates that the type should be serialized as a union
    /// with a closed set of case types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Case types are discovered automatically from C# convention:
    /// implicit conversion operators targeting the union type, single-parameter constructors,
    /// or compiler-emitted <c>[ClosedSubtype]</c> attributes on the type.
    /// </para>
    /// <para>
    /// For types where convention-based discovery does not work, use contract customization
    /// to populate <see cref="Metadata.JsonTypeInfo.UnionCases"/> and set the
    /// deconstructor/constructor delegates on <see cref="Metadata.JsonTypeInfo{T}"/>.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class JsonUnionAttribute : JsonAttribute
    {
        /// <summary>
        /// Gets or sets the type of a <see cref="JsonTypeClassifierFactory"/> implementation
        /// used to classify JSON payloads during deserialization.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see langword="null"/>, the built-in structural matching classifier is used.
        /// </para>
        /// <para>
        /// The specified type must derive from <see cref="JsonTypeClassifierFactory"/>
        /// and have a public parameterless constructor.
        /// </para>
        /// <para>
        /// The classifier is not invoked for JSON <see cref="JsonTokenType.Null"/>
        /// payloads. Null is dispatched directly to the union's constructor delegate,
        /// which yields the canonical null union when at least one case is nullable
        /// and otherwise throws a <see cref="JsonException"/>. See
        /// <see cref="JsonTypeClassifier"/> for details.
        /// </para>
        /// </remarks>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type? TypeClassifier { get; set; }
    }
}
