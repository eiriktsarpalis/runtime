// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed on a type, indicates that the type should be serialized polymorphically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class JsonPolymorphicTypeAttribute
        : JsonAttribute
    {
        /// <summary>
        /// Indicates that the declared type should be serialized polymorphically without emitting type discriminator metadata on the wire.
        /// This mode does not support polymorphic deserialization.
        /// </summary>
        public JsonPolymorphicTypeAttribute()
        {
        }

        /// <summary>
        /// Indicates that the declared type should be serialized polymorphically,
        /// emitting type discriminator metadata on the wire using the specified property name.
        /// This mode supports both polymorphic serialization and deserialization.
        /// </summary>
        /// <param name="typeDiscriminatorPropertyName">The JSON property name used to write or read type discriminator metadata.</param>
        public JsonPolymorphicTypeAttribute(string typeDiscriminatorPropertyName)
        {
            TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName;
        }

        /// <summary>
        /// Gets the type discriminator metadata property name, if not null.
        /// </summary>
        public string? TypeDiscriminatorPropertyName { get; }
    }
}
