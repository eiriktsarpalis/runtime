// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization.Converters
{
    /// <summary>
    /// Converter for union types. Fully stateless — reads/writes using the classifier,
    /// deconstructor, and constructor delegates configured on <see cref="JsonTypeInfo{T}"/>.
    /// All configuration is performed by the resolver, not the converter.
    /// </summary>
    [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
    internal sealed class JsonUnionConverter<TUnion> : JsonConverter<TUnion>
    {
        public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
            {
                return default;
            }

            JsonTypeInfo<TUnion> typeInfo = options.GetTypeInfo<TUnion>();

            JsonTypeClassifier? classifier = typeInfo.TypeClassifier;
            if (classifier is null)
            {
                ThrowHelper.ThrowJsonException($"Union type '{typeToConvert}' does not have a TypeClassifier configured. Ensure the type is annotated with [JsonUnion] or configure the classifier via contract customization.");
                return default;
            }

            // Checkpoint the reader: pass a by-value copy to the classifier
            // so the original reader stays positioned for deserialization.
            Utf8JsonReader checkpoint = reader;
            Type? caseType = classifier(ref checkpoint);
            if (caseType is null)
            {
                ThrowHelper.ThrowJsonException($"Unable to classify JSON payload to a union case type for union '{typeToConvert}'. No case type matched the payload structure.");
                return default;
            }

            // Deserialize using the original reader positioned at the value start.
            object? caseValue = JsonSerializer.Deserialize(ref reader, caseType, options);

            Func<Type, object?, TUnion>? constructor = typeInfo.UnionConstructor;
            if (constructor is null)
            {
                ThrowHelper.ThrowJsonException($"Union type '{typeToConvert}' does not have a UnionConstructor configured. Ensure the type is annotated with [JsonUnion] or configure the constructor via contract customization.");
                return default;
            }

            return constructor(caseType, caseValue);
        }

        public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonTypeInfo<TUnion> typeInfo = options.GetTypeInfo<TUnion>();

            Func<TUnion, (Type, object?)>? deconstructor = typeInfo.UnionDeconstructor;
            if (deconstructor is null)
            {
                ThrowHelper.ThrowJsonException($"Union type '{typeof(TUnion)}' does not have a UnionDeconstructor configured. Ensure the type is annotated with [JsonUnion] or configure the deconstructor via contract customization.");
                return;
            }

            (Type caseType, object? caseValue) = deconstructor(value);

            if (caseValue is null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, caseValue, caseType, options);
        }
    }
}
