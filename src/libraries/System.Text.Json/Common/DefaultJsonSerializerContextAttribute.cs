// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Specifies the default <see cref="JsonSerializerContext"/> for the assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When applied to an assembly, this attribute designates the specified <see cref="JsonSerializerContext"/>
    /// as the canonical source of source-generated metadata for all serializable types in that assembly.
    /// </para>
    /// <para>
    /// The System.Text.Json source generator uses this attribute to avoid re-generating metadata for types
    /// that already have source-generated metadata in their declaring assembly. When a source generator context
    /// in a consuming assembly encounters types from an assembly annotated with this attribute, it will
    /// delegate to the specified canonical context instead of generating its own metadata.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]

#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    sealed class DefaultJsonSerializerContextAttribute : JsonAttribute
    {
#pragma warning disable IDE0060
        /// <summary>
        /// Initializes a new instance of <see cref="DefaultJsonSerializerContextAttribute"/> with the specified context type.
        /// </summary>
        /// <param name="contextType">
        /// The type of the <see cref="JsonSerializerContext"/>-derived class that serves as
        /// the default source-generated context for this assembly.
        /// </param>
        public DefaultJsonSerializerContextAttribute(Type contextType) { }
#pragma warning restore IDE0060
    }
}
