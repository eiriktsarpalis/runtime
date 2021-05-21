// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Converts an object or value to or from JSON.
    /// </summary>
    /// <typeparam name="T">The <see cref="Type"/> to convert.</typeparam>
    public abstract partial class JsonConverter<T> : JsonConverter
    {
        /// <summary>
        /// When overidden, constructs a new <see cref="JsonConverter{T}"/> instance.
        /// </summary>
        protected internal JsonConverter()
        {
            IsValueType = TypeToConvert.IsValueType;
            IsSealedType = TypeToConvert.IsSealed;
            IsInternalConverter = GetType().Assembly == typeof(JsonConverter).Assembly;

            if (HandleNull)
            {
                HandleNullOnRead = true;
                HandleNullOnWrite = true;
            }
        }

        /// <summary>
        /// Determines whether the type can be converted.
        /// </summary>
        /// <remarks>
        /// The default implementation is to return True when <paramref name="typeToConvert"/> equals typeof(T).
        /// </remarks>
        /// <param name="typeToConvert"></param>
        /// <returns>True if the type can be converted, False otherwise.</returns>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(T);
        }

        internal override ConverterStrategy ConverterStrategy => ConverterStrategy.Value;

        internal sealed override JsonPropertyInfo CreateJsonPropertyInfo()
        {
            return new JsonPropertyInfo<T>();
        }

        internal override sealed JsonParameterInfo CreateJsonParameterInfo()
        {
            return new JsonParameterInfo<T>();
        }

        internal override Type? KeyType => null;

        internal override Type? ElementType => null;

        /// <summary>
        /// Indicates whether <see langword="null"/> should be passed to the converter on serialization,
        /// and whether <see cref="JsonTokenType.Null"/> should be passed on deserialization.
        /// </summary>
        /// <remarks>
        /// The default value is <see langword="true"/> for converters for value types, and <see langword="false"/> for converters for reference types.
        /// </remarks>
        public virtual bool HandleNull
        {
            get
            {
                // HandleNull is only called by the framework once during initialization and any
                // subsequent calls elsewhere would just re-initialize to the same values (we don't
                // track a "hasInitialized" flag since that isn't necessary).

                // If the type doesn't support null, allow the converter a chance to modify.
                // These semantics are backwards compatible with 3.0.
                HandleNullOnRead = !CanBeNull();

                // The framework handles null automatically on writes.
                HandleNullOnWrite = false;

                return false;
            }
        }

        /// <summary>
        /// Does the converter want to be called when reading null tokens.
        /// </summary>
        internal bool HandleNullOnRead { get; private set; }

        /// <summary>
        /// Does the converter want to be called for null values.
        /// </summary>
        internal bool HandleNullOnWrite { get; private set; }

        /// <summary>
        /// Can <see langword="null"/> be assigned to <see cref="TypeToConvert"/>?
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool CanBeNull() => default(T) is null;

        // This non-generic API is sealed as it just forwards to the generic version.
        internal sealed override bool OnTryWriteAsObject(Utf8JsonWriter writer, object? value, JsonSerializerOptions options, ref WriteStack state)
        {
            T valueOfT = (T)value!;
            return OnTryWrite(writer, valueOfT, options, ref state);
        }

        // Provide a default implementation for value converters.
        internal virtual bool OnTryWrite(Utf8JsonWriter writer, T value, JsonSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(ConverterStrategy == ConverterStrategy.Value);

            if (IsInternalConverterForNumberType && state.Current.NumberHandling is not null)
            {
                Debug.Assert(!state.IsContinuation);
                WriteNumberWithCustomHandling(writer, value, state.Current.NumberHandling.Value);
            }
            else
            {
                Write(writer, value, options);
            }

            return true;
        }

        // This non-generic API is sealed as it just forwards to the generic version.
        internal sealed override bool OnTryReadWithValidationAsObject(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, bool wasContinuation, out object? value)
        {
            bool result = OnTryReadWithValidation(ref reader, typeToConvert, options, ref state, wasContinuation, out T? tValue);
            value = tValue;
            return result;
        }

        // Provide a default implementation for value converters.
        internal virtual bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, out T? value)
        {
            Debug.Assert(ConverterStrategy == ConverterStrategy.Value);
            // A value converter should never be within a continuation.
            Debug.Assert(!state.IsContinuation);

            if (state.Current.NumberHandling != null)
            {
                value = ReadNumberWithCustomHandling(ref reader, state.Current.NumberHandling.Value, options);
            }
            else
            {
                value = Read(ref reader, typeToConvert, options);
            }

            if (options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.Preserve &&
                TypeToConvert == JsonTypeInfo.ObjectType && value is JsonElement element)
            {
                // Edge case where we want to lookup for a reference when parsing into typeof(object)
                // instead of return `value` as a JsonElement.
                if (JsonSerializer.TryGetReferenceFromJsonElement(ref state, element, out object? referenceValue))
                {
                    value = (T?)referenceValue;
                }
            }

            return true;
        }

        // Wrapper for OnTryRead method that performs read validation if required.
        // Only intented for use by the core TryRead method and helpers.
        // Marked for inlining to minimize the stack size.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool OnTryReadWithValidation(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, bool wasContinuation, out T? value)
        {
            bool success;
#if !DEBUG
            if (IsInternalConverter)
            {
                // For performance, do not verify reads of internal converters on Release builds.
                success = OnTryRead(ref reader, typeToConvert, options, ref state, out value);
            }
            else
#endif
            if (ConverterStrategy == ConverterStrategy.Value)
            {
                // A value converter should never be within a continuation.
                Debug.Assert(!state.IsContinuation);

                JsonTokenType originalPropertyTokenType = reader.TokenType;
                int originalPropertyDepth = reader.CurrentDepth;
                long originalPropertyBytesConsumed = reader.BytesConsumed;

                success = OnTryRead(ref reader, typeToConvert, options, ref state, out value);

                VerifyRead(
                    originalPropertyTokenType,
                    originalPropertyDepth,
                    originalPropertyBytesConsumed,
                    isValueConverter: true,
                    ref reader);
            }
            else
            {
                if (!wasContinuation)
                {
                    Debug.Assert(state.Current.OriginalTokenType == JsonTokenType.None);
                    state.Current.OriginalTokenType = reader.TokenType;

                    Debug.Assert(state.Current.OriginalDepth == 0);
                    state.Current.OriginalDepth = reader.CurrentDepth;
                }

                success = OnTryRead(ref reader, typeToConvert, options, ref state, out value);
                if (success)
                {
                    if (state.IsContinuation)
                    {
                        // The resumable converter did not forward to the next converter that previously returned false.
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }

                    VerifyRead(
                        state.Current.OriginalTokenType,
                        state.Current.OriginalDepth,
                        bytesConsumed: 0,
                        isValueConverter: false,
                        ref reader);

                    // No need to clear state.Current.* since a stack pop will occur.
                }
            }

            return success;
        }

        /// <summary>
        /// Read and convert the JSON to T.
        /// </summary>
        /// <remarks>
        /// A converter may throw any Exception, but should throw <cref>JsonException</cref> when the JSON is invalid.
        /// </remarks>
        /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
        /// <param name="typeToConvert">The <see cref="Type"/> being converted.</param>
        /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
        /// <returns>The value that was converted.</returns>
        public abstract T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options);

        internal bool TryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, out T? value)
        {
            bool success;
            // Remember if we were a continuation here since Push() may affect IsContinuation.
            bool isContinuation = state.IsContinuation;

            if (reader.TokenType == JsonTokenType.Null && !HandleNullOnRead && !isContinuation)
            {
                if (!CanBeNull())
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                }

                // For perf and converter simplicity, handle null here instead of forwarding to the converter.
                value = default;
                return true;
            }

            if (ConverterStrategy == ConverterStrategy.Value && IsSealedType)
            {
                Debug.Assert(!isContinuation);
                return OnTryReadWithValidation(ref reader, typeToConvert, options, ref state, isContinuation, out value);
            }

            state.Push();

            if (!typeof(T).IsValueType && state.Current.IsPolymorphicReEntryStarted)
            {
                var converter = state.Current.GetPolymorphicConverterForResumedContinuation();
                success = converter.OnTryReadWithValidationAsObject(ref reader, typeToConvert, options, ref state, isContinuation, out object? objectValue);
                value = (T?)objectValue;
                goto Done;
            }

            if (!typeof(T).IsValueType && state.Current.JsonTypeInfo.TaggedPolymorphismResolver is not null)
            {
                JsonTypeInfo jsonTypeInfo = state.Current.JsonTypeInfo;

                // Need to read ahead for the type discriminator before dispatching to the relevant polymorphic converter
                // Use a copy of the reader to avoid advancing the buffer.
                Utf8JsonReader readerCopy = reader;
                if (!JsonSerializer.TryReadTypeDiscriminator(ref readerCopy, out string? typeId))
                {
                    // Insufficient data in the buffer to read the type discriminator.
                    // Signal to the state that only the read-ahead operation requires more data
                    // and that the original reader state should not be advanced.
                    state.IsConverterReadAheadOperationPendingBytes = true;
                    value = default;
                    success = false;
                    goto Done;
                }

                if (state.IsConverterReadAheadOperationPendingBytes)
                {
                    Debug.Assert(isContinuation);

                    // the converter was suspended while attempting to read ahead the type discrimator.
                    // Unset the continuation the flag since for all intents and purposes this is the first run of the converter.
                    state.IsConverterReadAheadOperationPendingBytes = false;
                    isContinuation = false;
                }

                if (typeId is not null &&
                    jsonTypeInfo.TaggedPolymorphismResolver.TryResolveTypeByTypeId(typeId, out Type? type) &&
                    type != TypeToConvert)
                {
                    JsonConverter jsonConverter = state.InitializeReEntry(type, options);
                    Debug.Assert(jsonConverter != this);

                    success = jsonConverter.OnTryReadWithValidationAsObject(ref reader, typeToConvert, options, ref state, isContinuation, out object? objectValue);
                    value = (T?)objectValue;

                    goto Done;
                }
            }

            success = OnTryReadWithValidation(ref reader, typeToConvert, options, ref state, isContinuation, out value);

        Done:
            state.Pop(success);
            return success;
        }

        internal override sealed bool TryReadAsObject(ref Utf8JsonReader reader, JsonSerializerOptions options, ref ReadStack state, out object? value)
        {
            bool success = TryRead(ref reader, TypeToConvert, options, ref state, out T? typedValue);
            value = typedValue;
            return success;
        }

        internal bool TryWrite(Utf8JsonWriter writer, in T value, JsonSerializerOptions options, ref WriteStack state)
        {
            bool success;

            if (writer.CurrentDepth >= options.EffectiveMaxDepth)
            {
                ThrowHelper.ThrowJsonException_SerializerCycleDetected(options.EffectiveMaxDepth);
            }

            if (value is null && !HandleNullOnWrite)
            {
                // We do not pass null values to converters unless HandleNullOnWrite is true. Null values for properties were
                // already handled in GetMemberAndWriteJson() so we don't need to check for IgnoreNullValues here.
                writer.WriteNullValue();
                return true;
            }

            if (ConverterStrategy == ConverterStrategy.Value && IsSealedType)
            {
                Debug.Assert(!state.IsContinuation);
                int originalDepth = writer.CurrentDepth;
                success = OnTryWrite(writer, value, options, ref state);
                Debug.Assert(success);
                VerifyWrite(originalDepth, writer);
                return true;
            }

            //JsonTypeInfo jsonTypeInfo = state.PeekNextJsonTypeInfo();

            bool isReferencePushedForCycleDetection = false;

            if (!typeof(T).IsValueType && value is not null &&
                options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.IgnoreCycles &&
                // .NET types that are serialized as JSON primitive values don't need to be tracked for cycle detection e.g: string.
                (ConverterStrategy != ConverterStrategy.Value || this is ObjectConverter)) // TODO : remove latter condition
            {
                // Custom (user) converters shall not track references
                //  it is responsibility of the user to break cycles in case there's any
                //  if we compare against Preserve, objects don't get preserved when a custom converter exists
                //  given that the custom converter executes prior to the preserve logic.
                Debug.Assert(IsInternalConverter);

                ReferenceResolver resolver = state.ReferenceResolver;

                // Write null to break reference cycles.
                if (resolver.ContainsReferenceForCycleDetection(value))
                {
                    writer.WriteNullValue();
                    return true;
                }

                //// For boxed reference types: do not push when boxed in order to avoid false positives
                ////   when we run the ContainsReferenceForCycleDetection check for the converter of the unboxed value.
                //Debug.Assert(!jsonTypeInfo.CanBePolymorphic);
                resolver.PushReferenceForCycleDetection(value);
                isReferencePushedForCycleDetection = true;
            }

            bool isContinuation = state.IsContinuation;
            state.Push();

            if (isContinuation)
            {
                if (!typeof(T).IsValueType && state.Current.IsPolymorphicReEntryStarted)
                {
                    var converter = state.Current.GetPolymorphicConverterForResumedContinuation();
                    success = converter.OnTryWriteAsObject(writer, value, options, ref state);
                    goto Done;
                }
            }
            else
            {
                Debug.Assert(state.Current.OriginalDepth == 0);
                state.Current.OriginalDepth = writer.CurrentDepth;
            }

            if (!typeof(T).IsValueType && value is not null && state.Current.JsonTypeInfo.CanBePolymorphic)
            {
                JsonTypeInfo jsonTypeInfo = state.Current.JsonTypeInfo;

                //if (value is null)
                //{
                //    Debug.Assert(ConverterStrategy == ConverterStrategy.Value);
                //    Debug.Assert(!state.IsContinuation);
                //    Debug.Assert(HandleNullOnWrite);

                //    int originalPropertyDepth = writer.CurrentDepth;
                //    Write(writer, value, options);
                //    VerifyWrite(originalPropertyDepth, writer);

                //    return true;
                //}

                Type type = value.GetType();

                if (jsonTypeInfo.TaggedPolymorphismResolver is not null)
                {
                    // Prepare serialization for tagged polymorphism:
                    // if the resolver yields a valid typeId dispatch to the converter for the resolved type,
                    // otherwise revert back to using the current converter type and do not serialize polymorphically.

                    if (jsonTypeInfo.TaggedPolymorphismResolver.TryResolvePolymorphicSubtype(type, out Type? resolvedType, out string? typeId))
                    {
                        type = resolvedType;
                        state.Current.TaggedPolymorphicTypeId = typeId;
                    }
                    else
                    {
                        type = TypeToConvert;
                    }
                }

                // TODO: this is checking for a very rare use case in our main serialization method
                // change so that this logic is handled by the ObjectConverter
                if (type == JsonTypeInfo.ObjectType)
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                    success = true;
                    goto Done;
                }

                if (type != TypeToConvert)
                {
                    // For internal converter only: Handle polymorphic case and get the new converter.
                    // Custom converter, even though polymorphic converter, get called for reading AND writing.
                    JsonConverter jsonConverter = state.Current.InitializeReEntry(type, options);

                    //if (options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.IgnoreCycles &&
                    //    jsonConverter.IsValueType)
                    //{
                    //    // For boxed value types: push the value before it gets unboxed on TryWriteAsObject.
                    //    state.ReferenceResolver.PushReferenceForCycleDetection(value);
                    //    ignoreCyclesPopReference = true;
                    //}

                    // We found a different converter; forward to that.
                    success = jsonConverter.OnTryWriteAsObject(writer, value, options, ref state);
                    goto Done;

                    //if (ignoreCyclesPopReference)
                    //{
                    //    state.ReferenceResolver.PopReferenceForCycleDetection();
                    //}

                    //state.Current.IsPolymorphicReEntryStarted = false;
                    //state.TaggedPolymorphicTypeId = null;
                    //return success2;
                }
            }

            success = OnTryWrite(writer, value, options, ref state);

        Done:
            if (success)
            {
                VerifyWrite(state.Current.OriginalDepth, writer);
                // No need to clear state.Current.OriginalDepth since a stack pop will occur.
            }

            state.Pop(success);

            if (isReferencePushedForCycleDetection)
            {
                state.ReferenceResolver.PopReferenceForCycleDetection();
            }

            return success;
        }

        internal bool TryWriteDataExtensionProperty(Utf8JsonWriter writer, T value, JsonSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(value != null);

            if (!IsInternalConverter)
            {
                return TryWrite(writer, value, options, ref state);
            }

            if (!(this is JsonDictionaryConverter<T> dictionaryConverter))
            {
                // If not JsonDictionaryConverter<T> then we are JsonObject.
                // Avoid a type reference to JsonObject and its converter to support trimming.
                Debug.Assert(TypeToConvert == typeof(Nodes.JsonObject));
                return TryWrite(writer, value, options, ref state);
            }

            if (writer.CurrentDepth >= options.EffectiveMaxDepth)
            {
                ThrowHelper.ThrowJsonException_SerializerCycleDetected(options.EffectiveMaxDepth);
            }

            bool isContinuation = state.IsContinuation;
            bool success;

            state.Push();

            if (!isContinuation)
            {
                Debug.Assert(state.Current.OriginalDepth == 0);
                state.Current.OriginalDepth = writer.CurrentDepth;
            }

            // Ignore the naming policy for extension data.
            state.Current.IgnoreDictionaryKeyPolicy = true;
            state.Current.DeclaredJsonPropertyInfo = state.Current.JsonTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;

            success = dictionaryConverter.OnWriteResume(writer, value, options, ref state);
            if (success)
            {
                VerifyWrite(state.Current.OriginalDepth, writer);
            }

            state.Pop(success);

            return success;
        }

        internal sealed override Type TypeToConvert => typeof(T);

        internal void VerifyRead(JsonTokenType tokenType, int depth, long bytesConsumed, bool isValueConverter, ref Utf8JsonReader reader)
        {
            switch (tokenType)
            {
                case JsonTokenType.StartArray:
                    if (reader.TokenType != JsonTokenType.EndArray)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }
                    else if (depth != reader.CurrentDepth)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }

                    break;

                case JsonTokenType.StartObject:
                    if (reader.TokenType != JsonTokenType.EndObject)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }
                    else if (depth != reader.CurrentDepth)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }

                    break;

                default:
                    // A non-value converter (object or collection) should always have Start and End tokens.
                    // A value converter should not make any reads.
                    if (!isValueConverter || reader.BytesConsumed != bytesConsumed)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }

                    // Should not be possible to change token type.
                    Debug.Assert(reader.TokenType == tokenType);

                    break;
            }
        }

        internal void VerifyWrite(int originalDepth, Utf8JsonWriter writer)
        {
            if (originalDepth != writer.CurrentDepth)
            {
                ThrowHelper.ThrowJsonException_SerializationConverterWrite(this);
            }
        }

        /// <summary>
        /// Write the value as JSON.
        /// </summary>
        /// <remarks>
        /// A converter may throw any Exception, but should throw <cref>JsonException</cref> when the JSON
        /// cannot be created.
        /// </remarks>
        /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
        public abstract void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options);

        internal virtual T ReadWithQuotes(ref Utf8JsonReader reader)
        {
            ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(TypeToConvert, this);
            return default;
        }

        internal virtual void WriteWithQuotes(Utf8JsonWriter writer, [DisallowNull] T value, JsonSerializerOptions options, ref WriteStack state)
            => ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(TypeToConvert, this);

        internal sealed override void WriteWithQuotesAsObject(Utf8JsonWriter writer, object value, JsonSerializerOptions options, ref WriteStack state)
            => WriteWithQuotes(writer, (T)value, options, ref state);

        internal virtual T ReadNumberWithCustomHandling(ref Utf8JsonReader reader, JsonNumberHandling handling, JsonSerializerOptions options)
            => throw new InvalidOperationException();

        internal virtual void WriteNumberWithCustomHandling(Utf8JsonWriter writer, T value, JsonNumberHandling handling)
            => throw new InvalidOperationException();
    }
}
