// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Base class for all collections. Collections are assumed to implement <see cref="IEnumerable{T}"/>
    /// or a variant thereof e.g. <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    internal abstract class JsonCollectionConverter<TCollection, TElement> : JsonResumableConverter<TCollection>
    {
        internal override bool SupportsCreateObjectDelegate => true;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Enumerable;
        internal override Type ElementType => typeof(TElement);

        protected abstract void Add(in TElement value, ref ReadStack state);

        /// <summary>
        /// When overridden, create the collection. It may be a temporary collection or the final collection.
        /// </summary>
        protected virtual void CreateCollection(ref Utf8JsonReader reader, scoped ref ReadStack state, JsonSerializerOptions options)
        {
            if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
            {
                return;
            }

            JsonTypeInfo typeInfo = state.Current.JsonTypeInfo;

            if (typeInfo.CreateObject is null)
            {
                ThrowHelper.ThrowNotSupportedException_DeserializeNoConstructor(typeInfo, ref reader, ref state);
            }

            state.Current.ReturnValue = typeInfo.CreateObject();
            Debug.Assert(state.Current.ReturnValue is TCollection);
        }

        /// <summary>
        /// When overridden, converts the temporary collection held in state.Current.ReturnValue to the final collection.
        /// The <see cref="JsonConverter.IsConvertibleCollection"/> property must also be set to <see langword="true"/>.
        /// </summary>
        protected virtual void ConvertCollection(ref ReadStack state, JsonSerializerOptions options) { }

        protected static JsonConverter<TElement> GetElementConverter(JsonTypeInfo elementTypeInfo)
        {
            return ((JsonTypeInfo<TElement>)elementTypeInfo).EffectiveConverter;
        }

        protected static JsonConverter<TElement> GetElementConverter(ref WriteStack state)
        {
            Debug.Assert(state.Current.JsonPropertyInfo != null);
            return (JsonConverter<TElement>)state.Current.JsonPropertyInfo.EffectiveConverter;
        }

        internal override bool OnTryRead(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options,
            scoped ref ReadStack state,
            [MaybeNullWhen(false)] out TCollection value)
        {
            JsonTypeInfo jsonTypeInfo = state.Current.JsonTypeInfo;
            JsonTypeInfo elementTypeInfo = jsonTypeInfo.ElementTypeInfo!;

            if (!state.SupportContinuation && !state.Current.CanContainMetadata)
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
                }

                CreateCollection(ref reader, ref state, options);

                jsonTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                state.Current.JsonPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                JsonConverter<TElement> elementConverter = GetElementConverter(elementTypeInfo);
                if (elementConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling == null)
                {
                    // Fast path that avoids validation and extra indirection.
                    while (true)
                    {
                        reader.ReadWithVerify();
                        if (reader.TokenType == JsonTokenType.EndArray)
                        {
                            break;
                        }

                        // Obtain the CLR value from the JSON and apply to the object.
                        TElement? element = elementConverter.Read(ref reader, elementConverter.Type, options);
                        Add(element!, ref state);
                    }
                }
                else
                {
                    // Process all elements.
                    while (true)
                    {
                        reader.ReadWithVerify();
                        if (reader.TokenType == JsonTokenType.EndArray)
                        {
                            break;
                        }

                        // Get the value from the converter and add it.
                        elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _);
                        Add(element!, ref state);
                    }
                }
            }
            else
            {
                // Slower path that supports continuation and reading metadata.
                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        state.Current.ObjectState = StackFrameObjectState.ReadMetadata;
                    }
                    else if (state.Current.CanContainMetadata)
                    {
                        if (reader.TokenType != JsonTokenType.StartObject)
                        {
                            ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
                        }

                        state.Current.ObjectState = StackFrameObjectState.StartToken;
                    }
                    else
                    {
                        ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
                    }
                }

                // Handle the metadata properties.
                if (state.Current.CanContainMetadata && state.Current.ObjectState < StackFrameObjectState.ReadMetadata)
                {
                    if (!JsonSerializer.TryReadMetadata(this, jsonTypeInfo, ref reader, ref state))
                    {
                        value = default;
                        return false;
                    }

                    if (state.Current.MetadataPropertyNames == MetadataPropertyName.Ref)
                    {
                        value = JsonSerializer.ResolveReferenceId<TCollection>(ref state);
                        return true;
                    }

                    state.Current.ObjectState = StackFrameObjectState.ReadMetadata;
                }

                // Dispatch to any polymorphic converters: should always be entered regardless of ObjectState progress
                if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Type) != 0 &&
                    state.Current.PolymorphicSerializationState != PolymorphicSerializationState.PolymorphicReEntryStarted &&
                    ResolvePolymorphicConverter(jsonTypeInfo, ref state) is JsonConverter polymorphicConverter)
                {
                    Debug.Assert(!IsValueType);
                    bool success = polymorphicConverter.OnTryReadAsObject(ref reader, polymorphicConverter.Type!, options, ref state, out object? objectResult);
                    value = (TCollection)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        JsonSerializer.ValidateMetadataForArrayConverter(this, ref reader, ref state);
                    }

                    CreateCollection(ref reader, ref state, options);

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId != null);
                        Debug.Assert(options.ReferenceHandlingStrategy == JsonKnownReferenceHandler.Preserve);
                        Debug.Assert(state.Current.ReturnValue is TCollection);
                        state.ReferenceResolver.AddReference(state.ReferenceId, state.Current.ReturnValue);
                        state.ReferenceId = null;
                    }

                    jsonTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                }

                if (state.Current.ObjectState < StackFrameObjectState.ReadElements)
                {
                    JsonConverter<TElement> elementConverter = GetElementConverter(elementTypeInfo);
                    state.Current.JsonPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;

                    // Process all elements.
                    while (true)
                    {
                        if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                        {
                            if (!reader.TryAdvanceWithOptionalReadAhead(elementConverter.RequiresReadAhead))
                            {
                                value = default;
                                return false;
                            }

                            state.Current.PropertyState = StackFramePropertyState.ReadValue;
                        }

                        if (state.Current.PropertyState < StackFramePropertyState.ReadValueIsEnd)
                        {
                            if (reader.TokenType == JsonTokenType.EndArray)
                            {
                                break;
                            }

                            state.Current.PropertyState = StackFramePropertyState.ReadValueIsEnd;
                        }

                        if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                        {
                            // Get the value from the converter and add it.
                            if (!elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _))
                            {
                                value = default;
                                return false;
                            }

                            Add(element!, ref state);

                            // No need to set PropertyState to TryRead since we're done with this element now.
                            state.Current.EndElement();
                        }
                    }

                    state.Current.ObjectState = StackFrameObjectState.ReadElements;
                }

                if (state.Current.ObjectState < StackFrameObjectState.EndToken)
                {
                    // Array payload is nested inside a $values metadata property.
                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                    {
                        if (!reader.Read())
                        {
                            value = default;
                            return false;
                        }
                    }

                    state.Current.ObjectState = StackFrameObjectState.EndToken;
                }

                if (state.Current.ObjectState < StackFrameObjectState.EndTokenValidation)
                {
                    // Array payload is nested inside a $values metadata property.
                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                    {
                        if (reader.TokenType != JsonTokenType.EndObject)
                        {
                            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
                            if (options.AllowOutOfOrderMetadataProperties)
                            {
                                Debug.Assert(JsonSerializer.IsMetadataPropertyName(reader.GetUnescapedSpan(), (state.Current.BaseJsonTypeInfo ?? jsonTypeInfo).PolymorphicTypeResolver), "should only be hit if metadata property.");
                                bool result = reader.TrySkipPartial(reader.CurrentDepth - 1); // skip to the end of the object
                                Debug.Assert(result, "Metadata reader must have buffered all contents.");
                                Debug.Assert(reader.TokenType is JsonTokenType.EndObject);
                            }
                            else
                            {
                                ThrowHelper.ThrowJsonException_MetadataInvalidPropertyInArrayMetadata(ref state, typeToConvert, reader);
                            }
                        }
                    }
                }
            }

            ConvertCollection(ref state, options);
            object returnValue = state.Current.ReturnValue!;
            jsonTypeInfo.OnDeserialized?.Invoke(returnValue);
            value = (TCollection)returnValue;

            return true;
        }

        /// <summary>
        /// Fully async collection deserialization. No ReadStack state machine.
        /// Reads StartArray, peeks for EndArray, dispatches elements to child converters.
        /// </summary>
        internal override async ValueTask<TCollection?> OnReadCoreAsync(
            JsonStreamReader streamReader,
            Stream stream,
            JsonTypeInfo jsonTypeInfo,
            JsonSerializerOptions options,
            CancellationToken cancellationToken)
        {
            // Fall back for metadata, polymorphism, reference handling, populate,
            // or types without a CreateObject delegate (e.g., struct collections)
            if (options.ReferenceHandlingStrategy != JsonKnownReferenceHandler.Unspecified ||
                jsonTypeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators == true ||
                jsonTypeInfo.CreateObject is null ||
                IsConvertibleCollection ||
                CanHaveMetadata)
            {
                return await base.OnReadCoreAsync(streamReader, stream, jsonTypeInfo, options, cancellationToken).ConfigureAwait(false);
            }

            // Read StartArray
            try
            {
            JsonTokenType tokenType;
            while (!streamReader.TryReadToken(out tokenType, out _))
            {
                if (streamReader.IsFinalBlock) ThrowHelper.ThrowJsonException();
                await streamReader.RefillBufferAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            if (tokenType == JsonTokenType.Null)
            {
                if (default(TCollection) is not null)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
                }

                return default;
            }

            if (tokenType != JsonTokenType.StartArray)
            {
                ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
            }

            // Create collection
            ReadStack tempState = default;
            tempState.Initialize(jsonTypeInfo);
            {
                Utf8JsonReader reader = streamReader.GetReader();
                CreateCollection(ref reader, ref tempState, options);
            }

            jsonTypeInfo.OnDeserializing?.Invoke(tempState.Current.ReturnValue!);
            JsonTypeInfo elementTypeInfo = jsonTypeInfo.ElementTypeInfo!;
            JsonConverter elementConverter = ((JsonTypeInfo<TElement>)elementTypeInfo).EffectiveConverter;

            // Read elements: peek for EndArray, dispatch non-end tokens to child
            while (true)
            {
                // Peek: if EndArray it's consumed; otherwise buffer stays for child
                while (!streamReader.TryPeekTokenType(out tokenType))
                {
                    if (streamReader.IsFinalBlock) ThrowHelper.ThrowJsonException();
                    await streamReader.RefillBufferAsync(stream, cancellationToken).ConfigureAwait(false);
                }

                if (tokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                // Child reads its own first token (buffer unchanged by peek)
                TElement? element = (TElement?)await elementConverter.ReadCoreAsyncAsObject(
                    streamReader, stream, elementTypeInfo, options, cancellationToken).ConfigureAwait(false);
                Add(element!, ref tempState);
            }

            ConvertCollection(ref tempState, options);
            object returnValue = tempState.Current.ReturnValue!;
            jsonTypeInfo.OnDeserialized?.Invoke(returnValue);
            return (TCollection)returnValue;

            }
            catch (JsonReaderException ex)
            {
                throw new JsonException(ex.Message, ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
            }
        }

        internal override bool OnTryWrite(
            Utf8JsonWriter writer,
            TCollection value,
            JsonSerializerOptions options,
            ref WriteStack state)
        {
            bool success;

            if (value == null)
            {
                writer.WriteNullValue();
                success = true;
            }
            else
            {
                JsonTypeInfo jsonTypeInfo = state.Current.JsonTypeInfo;

                if (!state.Current.ProcessedStartToken)
                {
                    state.Current.ProcessedStartToken = true;

                    jsonTypeInfo.OnSerializing?.Invoke(value);

                    if (state.CurrentContainsMetadata && CanHaveMetadata)
                    {
                        state.Current.MetadataPropertyName = JsonSerializer.WriteMetadataForCollection(this, ref state, writer);
                    }

                    // Writing the start of the array must happen after any metadata
                    writer.WriteStartArray();
                    state.Current.JsonPropertyInfo = jsonTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
                }

                success = OnWriteResume(writer, value, options, ref state);
                if (success)
                {
                    if (!state.Current.ProcessedEndToken)
                    {
                        state.Current.ProcessedEndToken = true;
                        writer.WriteEndArray();

                        if (state.Current.MetadataPropertyName != 0)
                        {
                            // Write the EndObject for $values.
                            writer.WriteEndObject();
                        }
                    }

                    jsonTypeInfo.OnSerialized?.Invoke(value);
                }
            }

            return success;
        }

        protected abstract bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state);
    }
}
