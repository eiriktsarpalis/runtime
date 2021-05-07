// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Class used to define what types should be serialized polymorphically.
    /// Note that this abstraction only governs serialization and provides no mechanism for deserialization.
    /// </summary>
    public abstract class PolymorphicSerializationResolver
    {
        /// <summary>
        /// Specifies that only the <see cref="object"/> type should be serialized using polymorphism.
        /// This is the default behavior of System.Text.Json.
        /// </summary>
        public static PolymorphicSerializationResolver ObjectOnly { get; } = new ObjectOnlyPolymorphicTypeResolver();

        /// <summary>
        /// Determines whether the specified type should be serialized polymorphically.
        /// </summary>
        /// <param name="type">The type to be checked for polymorphic support.</param>
        /// <returns>
        /// <see langword="true"/> if type should be serialized polymorphically or
        /// <see langword="false"/> if it should not be serialized polymorphically.
        /// </returns>
        public abstract bool CanBePolymorphic(Type type);

        private sealed class ObjectOnlyPolymorphicTypeResolver : PolymorphicSerializationResolver
        {
            public override bool CanBePolymorphic(Type type) => JsonTypeInfo.ObjectType == type;
        }
    }
}
