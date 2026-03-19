// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed on a property, field, or constructor parameter of type <see cref="string"/>,
    /// indicates that the polymorphic type discriminator from the JSON payload
    /// should be bound to this member during deserialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The annotated member will receive the type discriminator value read from the wire
    /// during deserialization. During serialization, the value of the annotated member
    /// takes precedence over the type-level discriminator mapping specified by
    /// <see cref="JsonDerivedTypeAttribute"/> if non-null, enabling roundtripping of
    /// unrecognized discriminator values.
    /// </para>
    /// <para>
    /// Only one member per type may be annotated with this attribute.
    /// The property type must be <see cref="string"/>.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class JsonTypeDiscriminatorAttribute : JsonAttribute
    {
    }
}
