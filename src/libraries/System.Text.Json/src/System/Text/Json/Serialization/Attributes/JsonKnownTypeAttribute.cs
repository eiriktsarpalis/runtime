// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed on a type declaration, indicates that the specified subtype should be opted into polymorphic serialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    public class JsonKnownTypeAttribute : JsonAttribute
    {
        /// <summary>
        /// Initializes a new attribute with specified parameters.
        /// </summary>
        /// <param name="subtype">The known subtype that should be serialized polymorphically.</param>
        public JsonKnownTypeAttribute(Type subtype)
        {
            Subtype = subtype;
        }

        /// <summary>
        /// Initializes a new attribute with specified parameters.
        /// </summary>
        /// <param name="subtype">The known subtype that should be serialized polymorphically.</param>
        /// <param name="typeDiscriminatorId">The type discriminator identifier to be used for the serialization of the subtype.</param>
        public JsonKnownTypeAttribute(Type subtype, string typeDiscriminatorId)
        {
            Subtype = subtype;
            TypeDiscriminatorId = typeDiscriminatorId;
        }

        /// <summary>
        /// The known subtype that should be serialized polymorphically.
        /// </summary>
        public Type Subtype { get; }

        /// <summary>
        /// The type discriminator identifier to be used for the serialization of the subtype.
        /// </summary>
        public string? TypeDiscriminatorId { get; }
    }
}
