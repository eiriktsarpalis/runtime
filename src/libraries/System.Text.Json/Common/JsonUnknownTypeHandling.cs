// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Defines how deserializing a type declared as an <see cref="object"/> is handled during deserialization.
    /// </summary>
    public enum JsonUnknownTypeHandling
    {
        /// <summary>
        /// A type declared as <see cref="object"/> is deserialized as a <see cref="JsonElement"/>.
        /// </summary>
        JsonElement = 0,
        /// <summary>
        /// A type declared as <see cref="object"/> is deserialized as a <see cref="JsonNode"/>.
        /// </summary>
        JsonNode = 1,
        /// <summary>
        /// A type declared as <see cref="object"/> is deserialized using natural .NET primitive types.
        /// <para>JSON booleans map to <see cref="bool"/>.</para>
        /// <para>JSON numbers map to <see cref="int"/>, <see cref="long"/>, <see cref="double"/>,
        /// or <see cref="decimal"/> depending on the magnitude and precision of the value.</para>
        /// <para>JSON strings map to <see cref="string"/>, or to <see cref="DateTimeOffset"/>,
        /// <see cref="DateTime"/>, or <see cref="Guid"/> if the string matches a recognized format.</para>
        /// <para>JSON arrays map to <see cref="T:object?[]"/>.</para>
        /// <para>JSON objects map to <see cref="T:Dictionary{string, object?}"/>.</para>
        /// </summary>
        Natural = 2
    }
}
