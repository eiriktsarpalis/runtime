// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    public partial class JsonConverter
    {
        /// <summary>
        /// Initializes the state for polymorphic cases and returns the appropriate converter.
        /// </summary>
        internal JsonConverter? ResolvePolymorphicConverter(object value, JsonTypeInfo jsonTypeInfo, JsonSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(value != null && TypeToConvert.IsAssignableFrom(value.GetType()));
            Debug.Assert(jsonTypeInfo.CanBePolymorphic);
            Debug.Assert(state.PolymorphicTypeDiscriminator is null);

            ref WriteStackFrame current = ref state.Current;
            JsonConverter? polymorphicConverter = null;

            switch (current.PolymorphicSerializationState)
            {
                case PolymorphicSerializationState.None:
                    Type runtimeType = value.GetType();

                    if (jsonTypeInfo.HasTypeDiscriminatorResolver)
                    {
                        // Prepare serialization for type discriminator polymorphism:
                        // if the resolver yields a valid typeId dispatch to the converter for the resolved type,
                        // otherwise revert back to using the current converter type and do not serialize polymorphically.

                        Debug.Assert(jsonTypeInfo.TypeDiscriminatorResolver != null);
                        if (jsonTypeInfo.TypeDiscriminatorResolver.TryResolvePolymorphicSubtype(runtimeType, out Type? resolvedType, out string? typeId))
                        {
                            Debug.Assert(resolvedType.IsAssignableFrom(runtimeType));

                            polymorphicConverter = resolvedType != TypeToConvert ?
                                current.InitializePolymorphicReEntry(resolvedType, options) :
                                null;

                            if (polymorphicConverter?.CanHaveMetadata ?? CanHaveMetadata)
                            {
                                state.PolymorphicTypeDiscriminator = typeId;
                            }
                        }
                    }
                    else if (runtimeType != TypeToConvert)
                    {
                        polymorphicConverter = current.InitializePolymorphicReEntry(runtimeType, options);
                    }
                    break;

                case PolymorphicSerializationState.PolymorphicReEntrySuspended:
                    Debug.Assert(state.IsContinuation);
                    polymorphicConverter = current.ResumePolymorphicReEntry();
                    Debug.Assert(TypeToConvert.IsAssignableFrom(polymorphicConverter.TypeToConvert));
                    break;

                default:
                    Debug.Fail("Unexpected PolymorphicSerializationState.");
                    break;
            }

            return polymorphicConverter;
        }

        internal bool TryHandleSerializedObjectReference(Utf8JsonWriter writer, object value, JsonSerializerOptions options, JsonConverter? polymorphicConverter, ref WriteStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(!state.IsContinuation);
            Debug.Assert(value != null);

            switch (options.ReferenceHandlingStrategy)
            {
                case ReferenceHandlingStrategy.IgnoreCycles:
                    ReferenceResolver resolver = state.ReferenceResolver;
                    if (resolver.ContainsReferenceForCycleDetection(value))
                    {
                        writer.WriteNullValue();
                        return true;
                    }

                    resolver.PushReferenceForCycleDetection(value);
                    // WriteStack reuses root-level stackframes for its children as a performance optimization;
                    // we want to avoid writing any data for the root-level object to avoid corrupting the stack.
                    // This is fine since popping the root object at the end of serialization is not essential.
                    state.Current.IsPushedReferenceForCycleDetection = state.CurrentDepth > 0;
                    break;

                case ReferenceHandlingStrategy.Preserve:
                    bool canHaveIdMetata = polymorphicConverter?.CanHaveMetadata ?? CanHaveMetadata;
                    if (canHaveIdMetata && JsonSerializer.TryGetReferenceForValue(value, ref state, writer))
                    {
                        // We found a repeating reference and wrote the relevant metadata; serialization complete.
                        return true;
                    }
                    break;

                default:
                    Debug.Fail("Unexpected ReferenceHandlingStrategy.");
                    break;
            }

            return false;
        }
    }
}
