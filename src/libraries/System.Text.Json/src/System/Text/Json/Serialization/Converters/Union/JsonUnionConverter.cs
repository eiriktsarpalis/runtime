// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
        // Override the framework's default null short-circuit so that JSON null
        // is delivered to Read where it is dispatched to the case constructor
        // accepting null (or rejected when no case opts in).
        public override bool HandleNull => true;

        public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonTypeInfo<TUnion> typeInfo = options.GetTypeInfo<TUnion>();

            Func<Type, object?, TUnion>? constructor = typeInfo.UnionConstructor;
            if (constructor is null)
            {
                ThrowHelper.ThrowJsonException($"Union type '{typeToConvert}' does not have a UnionConstructor configured. Ensure the type is annotated with [JsonUnion] or configure the constructor via contract customization.");
                return default;
            }

            if (reader.TokenType is JsonTokenType.Null)
            {
                // Null short-circuit: bypass the classifier entirely. Per the union
                // semantics, every nullable case constructor produces the same
                // canonical null-holding union value, so the case type is irrelevant
                // for null payloads. The constructor delegate encapsulates the
                // null-handling policy: if any case is nullable it returns the
                // canonical null union, otherwise it throws UnionDoesNotAcceptNull.
                // This relieves custom classifier authors from having to handle the
                // Null token themselves.
                return constructor(null!, null);
            }

            Type? caseType = null;
            JsonTypeClassifier? classifier = typeInfo.TypeClassifier;
            if (classifier is not null)
            {
                // Custom classifier path: checkpoint reader and delegate to the classifier.
                Utf8JsonReader checkpoint = reader;
                caseType = classifier(ref checkpoint);
            }
            else
            {
                // Default path: token-type matching. No read-ahead, no buffering.
                typeInfo.UnionTokenTypeMap?.TryGetValue(reader.TokenType, out caseType);
            }

            if (caseType is null)
            {
                ThrowHelper.ThrowJsonException($"Unable to classify JSON payload to a union case type for union '{typeToConvert}'. No case type matched the JSON token type '{reader.TokenType}'.");
                return default;
            }

            object? caseValue = JsonSerializer.Deserialize(ref reader, caseType, options);
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

            Func<TUnion, (Type?, object?)>? deconstructor = typeInfo.UnionDeconstructor;
            if (deconstructor is null)
            {
                ThrowHelper.ThrowJsonException($"Union type '{typeof(TUnion)}' does not have a UnionDeconstructor configured. Ensure the type is annotated with [JsonUnion] or configure the deconstructor via contract customization.");
                return;
            }

            (Type? caseType, object? caseValue) = deconstructor(value);

            if (caseValue is null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, caseValue, caseType ?? caseValue.GetType(), options);
        }
    }
}
