// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Base class for dictionary converters such as IDictionary, Hashtable, Dictionary{,} IDictionary{,} and SortedList.
    /// </summary>
    internal abstract class JsonDictionaryConverter<TDictionary> : JsonResumableConverter<TDictionary>
    {
        internal override bool SupportsCreateObjectDelegate => true;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Dictionary;

        protected internal abstract bool OnWriteResume(Utf8JsonWriter writer, TDictionary dictionary, JsonSerializerOptions options, ref WriteStack state);
    }

    /// <summary>
    /// Base class for dictionary converters such as IDictionary, Hashtable, Dictionary{,} IDictionary{,} and SortedList.
    /// </summary>
    internal abstract class JsonDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary>
        where TKey : notnull
    {
        /// <summary>
        /// When overridden, adds the value to the collection.
        /// </summary>
        protected abstract void Add(TKey key, in TValue value, JsonSerializerOptions options, ref ReadStack state);

        /// <summary>
        /// When overridden, converts the temporary collection held in state.Current.ReturnValue to the final collection.
        /// This is used with immutable collections.
        /// </summary>
        protected virtual void ConvertCollection(ref ReadStack state, JsonSerializerOptions options) { }

        /// <summary>
        /// When overridden, create the collection. It may be a temporary collection or the final collection.
        /// </summary>
        protected virtual void CreateCollection(ref Utf8JsonReader reader, scoped ref ReadStack state)
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
            Debug.Assert(state.Current.ReturnValue is TDictionary);
        }

        internal override Type ElementType => typeof(TValue);

        internal override Type KeyType => typeof(TKey);


        protected JsonConverter<TKey>? _keyConverter;
        protected JsonConverter<TValue>? _valueConverter;

        protected static JsonConverter<T> GetConverter<T>(JsonTypeInfo typeInfo)
        {
            return ((JsonTypeInfo<T>)typeInfo).EffectiveConverter;
        }

        internal sealed override bool OnTryRead(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options,
            scoped ref ReadStack state,
            [MaybeNullWhen(false)] out TDictionary value)
        {
            JsonTypeInfo jsonTypeInfo = state.Current.JsonTypeInfo;
            JsonTypeInfo keyTypeInfo = jsonTypeInfo.KeyTypeInfo!;
            JsonTypeInfo elementTypeInfo = jsonTypeInfo.ElementTypeInfo!;

            if (!state.SupportContinuation && !state.Current.CanContainMetadata)
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
                }

                CreateCollection(ref reader, ref state);

                jsonTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                _keyConverter ??= GetConverter<TKey>(keyTypeInfo);
                _valueConverter ??= GetConverter<TValue>(elementTypeInfo);

                if (_valueConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling == null)
                {
                    // Process all elements.
                    while (true)
                    {
                        // Read the key name.
                        reader.ReadWithVerify();

                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

                        state.Current.JsonPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        TKey key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);

                        // Read the value and add.
                        reader.ReadWithVerify();
                        state.Current.JsonPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        TValue? element = _valueConverter.Read(ref reader, ElementType, options);
                        Add(key, element!, options, ref state);
                    }
                }
                else
                {
                    // Process all elements.
                    while (true)
                    {
                        // Read the key name.
                        reader.ReadWithVerify();

                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
                        state.Current.JsonPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        TKey key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);

                        reader.ReadWithVerify();

                        // Get the value from the converter and add it.
                        state.Current.JsonPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        _valueConverter.TryRead(ref reader, ElementType, options, ref state, out TValue? element, out _);
                        Add(key, element!, options, ref state);
                    }
                }
            }
            else
            {
                // Slower path that supports continuation and reading metadata.
                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
                    }

                    state.Current.ObjectState = StackFrameObjectState.StartToken;
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
                        value = JsonSerializer.ResolveReferenceId<TDictionary>(ref state);
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
                    value = (TDictionary)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                // Create the dictionary.
                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        JsonSerializer.ValidateMetadataForObjectConverter(ref state);
                    }

                    CreateCollection(ref reader, ref state);

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId != null);
                        Debug.Assert(options.ReferenceHandlingStrategy == JsonKnownReferenceHandler.Preserve);
                        Debug.Assert(state.Current.ReturnValue is TDictionary);
                        state.ReferenceResolver.AddReference(state.ReferenceId, state.Current.ReturnValue);
                        state.ReferenceId = null;
                    }

                    jsonTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                }

                // Process all elements.
                _keyConverter ??= GetConverter<TKey>(keyTypeInfo);
                _valueConverter ??= GetConverter<TValue>(elementTypeInfo);
                while (true)
                {
                    if (state.Current.PropertyState == StackFramePropertyState.None)
                    {
                        // Read the key name.
                        if (!reader.Read())
                        {
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadName;
                    }

                    // Determine the property.
                    TKey key;
                    if (state.Current.PropertyState < StackFramePropertyState.Name)
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

                        state.Current.PropertyState = StackFramePropertyState.Name;

                        if (state.Current.CanContainMetadata)
                        {
                            ReadOnlySpan<byte> propertyName = reader.GetUnescapedSpan();
                            if (JsonSerializer.IsMetadataPropertyName(propertyName, state.Current.BaseJsonTypeInfo.PolymorphicTypeResolver))
                            {
                                if (options.AllowOutOfOrderMetadataProperties)
                                {
                                    reader.SkipWithVerify();
                                    state.Current.EndElement();
                                    continue;
                                }
                                else
                                {
                                    ThrowHelper.ThrowUnexpectedMetadataException(propertyName, ref reader, ref state);
                                }
                            }
                        }

                        state.Current.JsonPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);
                    }
                    else
                    {
                        // DictionaryKey is assigned before all return false cases, null value is unreachable
                        key = (TKey)state.Current.DictionaryKey!;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                    {
                        if (!reader.TryAdvanceWithOptionalReadAhead(_valueConverter.RequiresReadAhead))
                        {
                            state.Current.DictionaryKey = key;
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadValue;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                    {
                        // Get the value from the converter and add it.
                        state.Current.JsonPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        bool success = _valueConverter.TryRead(ref reader, typeof(TValue), options, ref state, out TValue? element, out _);
                        if (!success)
                        {
                            state.Current.DictionaryKey = key;
                            value = default;
                            return false;
                        }

                        Add(key, element!, options, ref state);
                        state.Current.EndElement();
                    }
                }
            }

            ConvertCollection(ref state, options);
            object result = state.Current.ReturnValue!;
            jsonTypeInfo.OnDeserialized?.Invoke(result);
            value = (TDictionary)result;

            return true;

            static TKey ReadDictionaryKey(JsonConverter<TKey> keyConverter, ref Utf8JsonReader reader, scoped ref ReadStack state, JsonSerializerOptions options)
            {
                TKey key;
                string unescapedPropertyNameAsString = reader.GetString()!;
                state.Current.JsonPropertyNameAsString = unescapedPropertyNameAsString; // Copy key name for JSON Path support in case of error.

                // Special case string to avoid calling GetString twice and save one allocation.
                if (keyConverter.IsInternalConverter && keyConverter.Type == typeof(string))
                {
                    key = (TKey)(object)unescapedPropertyNameAsString;
                }
                else
                {
                    key = keyConverter.ReadAsPropertyNameCore(ref reader, keyConverter.Type, options);
                }

                return key;
            }
        }

        /// <summary>
        /// Fully async dictionary deserialization. No ReadStack state machine.
        /// Peeks for EndObject, reads keys from the live reader, dispatches values to child.
        /// </summary>
        internal override async ValueTask<TDictionary?> OnReadCoreAsync(
            JsonStreamReader streamReader,
            Stream stream,
            JsonTypeInfo jsonTypeInfo,
            JsonSerializerOptions options,
            CancellationToken cancellationToken)
        {
            // Fall back for metadata, polymorphism, reference handling, duplicates,
            // convertible collections, or types without a CreateObject delegate
            if (options.ReferenceHandlingStrategy != JsonKnownReferenceHandler.Unspecified ||
                jsonTypeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators == true ||
                !options.AllowDuplicateProperties ||
                jsonTypeInfo.CreateObject is null ||
                IsConvertibleCollection ||
                CanHaveMetadata)
            {
                return await base.OnReadCoreAsync(streamReader, stream, jsonTypeInfo, options, cancellationToken).ConfigureAwait(false);
            }

            try
            {

            // Read StartObject
            JsonTokenType tokenType;
            while (!streamReader.TryReadToken(out tokenType, out _))
            {
                if (streamReader.IsFinalBlock) ThrowHelper.ThrowJsonException();
                await streamReader.RefillBufferAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            if (tokenType == JsonTokenType.Null)
            {
                if (default(TDictionary) is not null)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
                }

                return default;
            }

            if (tokenType != JsonTokenType.StartObject)
            {
                ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Type);
            }

            // Create dictionary
            ReadStack tempState = default;
            tempState.Initialize(jsonTypeInfo);
            {
                Utf8JsonReader reader = streamReader.GetReader();
                CreateCollection(ref reader, ref tempState);
            }

            jsonTypeInfo.OnDeserializing?.Invoke(tempState.Current.ReturnValue!);

            JsonTypeInfo keyTypeInfo = jsonTypeInfo.KeyTypeInfo!;
            JsonTypeInfo valueTypeInfo = jsonTypeInfo.ElementTypeInfo!;
            JsonConverter<TKey> keyConverter = _keyConverter ??= GetConverter<TKey>(keyTypeInfo);
            JsonConverter valueConverter = ((JsonTypeInfo<TValue>)valueTypeInfo).EffectiveConverter;

            // Read key-value pairs
            while (true)
            {
                // Peek: EndObject is consumed; PropertyName leaves buffer for key reading
                while (!streamReader.TryPeekTokenType(out tokenType))
                {
                    if (streamReader.IsFinalBlock) ThrowHelper.ThrowJsonException();
                    await streamReader.RefillBufferAsync(stream, cancellationToken).ConfigureAwait(false);
                }

                if (tokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                Debug.Assert(tokenType == JsonTokenType.PropertyName);

                // Read the key from the live reader (peek left buffer unchanged)
                TKey key;
                {
                    Utf8JsonReader reader = streamReader.GetReader();
                    while (!reader.Read())
                    {
                        streamReader.SaveReader(ref reader);
                        if (streamReader.IsFinalBlock) ThrowHelper.ThrowJsonException();
                        await streamReader.RefillBufferAsync(stream, cancellationToken).ConfigureAwait(false);
                        reader = streamReader.GetReader();
                    }

                    string keyString = reader.GetString()!;
                    if (keyConverter.IsInternalConverter && keyConverter.Type == typeof(string))
                    {
                        key = (TKey)(object)keyString;
                    }
                    else
                    {
                        key = keyConverter.ReadAsPropertyNameCore(ref reader, typeof(TKey), options);
                    }

                    streamReader.SaveReader(ref reader);
                }

                // Read value via child converter
                TValue? dictValue = (TValue?)await valueConverter.ReadCoreAsyncAsObject(
                    streamReader, stream, valueTypeInfo, options, cancellationToken).ConfigureAwait(false);

                Add(key, dictValue!, options, ref tempState);
            }

            ConvertCollection(ref tempState, options);
            object returnValue = tempState.Current.ReturnValue!;
            jsonTypeInfo.OnDeserialized?.Invoke(returnValue);
            return (TDictionary)returnValue;

            }
            catch (JsonReaderException ex)
            {
                throw new JsonException(ex.Message, ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
            }
        }

        internal sealed override bool OnTryWrite(
            Utf8JsonWriter writer,
            TDictionary dictionary,
            JsonSerializerOptions options,
            ref WriteStack state)
        {
            if (dictionary == null)
            {
                writer.WriteNullValue();
                return true;
            }

            JsonTypeInfo jsonTypeInfo = state.Current.JsonTypeInfo;

            if (!state.Current.ProcessedStartToken)
            {
                state.Current.ProcessedStartToken = true;

                jsonTypeInfo.OnSerializing?.Invoke(dictionary);

                writer.WriteStartObject();

                if (state.CurrentContainsMetadata && CanHaveMetadata)
                {
                    JsonSerializer.WriteMetadataForObject(this, ref state, writer);
                }

                state.Current.JsonPropertyInfo = jsonTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            }

            bool success = OnWriteResume(writer, dictionary, options, ref state);
            if (success)
            {
                if (!state.Current.ProcessedEndToken)
                {
                    state.Current.ProcessedEndToken = true;
                    writer.WriteEndObject();
                }

                jsonTypeInfo.OnSerialized?.Invoke(dictionary);
            }

            return success;
        }
    }
}
