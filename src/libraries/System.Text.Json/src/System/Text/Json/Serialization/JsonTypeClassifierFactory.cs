// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When implemented in a derived class, creates a <see cref="JsonTypeClassifier"/>
    /// delegate that classifies JSON payloads to determine the target type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This factory is the extension point for users who want to customize how JSON payloads
    /// are matched to union case types or polymorphic derived types during deserialization.
    /// It is referenced by the <see cref="JsonUnionAttribute.TypeClassifier"/> and
    /// <see cref="JsonPolymorphicAttribute.TypeClassifier"/> properties.
    /// </para>
    /// <para>
    /// The factory's <see cref="CreateJsonClassifier"/> method is called once during
    /// <see cref="JsonTypeInfo"/> configuration, and the returned delegate is invoked
    /// on every deserialization call.
    /// </para>
    /// </remarks>
    public abstract class JsonTypeClassifierFactory
    {
        /// <summary>
        /// Creates a delegate that classifies JSON payloads to determine the target type.
        /// </summary>
        /// <param name="context">
        /// An immutable snapshot of metadata including the declaring type, candidate types
        /// (with optional discriminator values), and the discriminator property name.
        /// </param>
        /// <param name="options">The serializer options for the current context.</param>
        /// <returns>A <see cref="JsonTypeClassifier"/> delegate.</returns>
        public abstract JsonTypeClassifier CreateJsonClassifier(
            JsonTypeClassifierContext context,
            JsonSerializerOptions options);
    }
}
