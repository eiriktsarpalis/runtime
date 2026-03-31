// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization
{
    public partial class JsonConverter<T>
    {
        /// <summary>
        /// Root-level async entry point for stream deserialization.
        /// Calls OnReadCoreAsync then consumes trailing whitespace.
        /// </summary>
        internal async ValueTask<T?> ReadCoreAsync(
            JsonStreamReader streamReader,
            Stream stream,
            JsonTypeInfo jsonTypeInfo,
            JsonSerializerOptions options,
            CancellationToken cancellationToken)
        {
            T? result = await OnReadCoreAsync(streamReader, stream, jsonTypeInfo, options, cancellationToken).ConfigureAwait(false);

            // Consume trailing whitespace (mirrors ReadCore's trailing check).
            do
            {
                bool trailingDone;
                {
                    Utf8JsonReader reader = streamReader.GetReader();

                    try
                    {
                        bool readResult = reader.Read();
                        trailingDone = readResult || reader.IsFinalBlock;
                    }
                    catch (JsonReaderException ex)
                    {
                        streamReader.SaveReader(ref reader);
                        throw new JsonException(ex.Message, ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
                    }

                    streamReader.SaveReader(ref reader);
                }

                if (trailingDone)
                {
                    break;
                }

                await streamReader.RefillBufferAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            while (true);

            return result;
        }

        /// <summary>
        /// Converter-specific async deserialization. Default implementation uses
        /// TryRead with exception wrapping but no trailing whitespace consumption.
        /// Converters override this with native async implementations.
        /// </summary>
        internal virtual async ValueTask<T?> OnReadCoreAsync(
            JsonStreamReader streamReader,
            Stream stream,
            JsonTypeInfo jsonTypeInfo,
            JsonSerializerOptions options,
            CancellationToken cancellationToken)
        {
            // Default wrapper: TryAdvanceWithOptionalReadAhead + TryRead with
            // exception wrapping matching ReadCore, but NO trailing whitespace
            // consumption (safe for both root and child contexts).
            ReadStack state = default;
            state.Initialize(jsonTypeInfo, supportContinuation: true);

            while (true)
            {
                bool success;
                T? value;
                {
                    Utf8JsonReader reader = streamReader.GetReader();

                    try
                    {
                        if (!state.IsContinuation)
                        {
                            if (!reader.TryAdvanceWithOptionalReadAhead(RequiresReadAhead))
                            {
                                streamReader.SaveReader(ref reader);
                                goto NeedMoreData;
                            }
                        }

                        success = TryRead(ref reader, jsonTypeInfo.Type, options, ref state, out value, out _);
                        streamReader.SaveReader(ref reader);
                    }
                    catch (Exception ex)
                    {
                        // Mirror ReadCore's exception wrapping for proper JsonException types.
                        // NotSupportedException is NOT wrapped here — let it propagate to the
                        // parent converter which has the property context for path info.
                        switch (ex)
                        {
                            case JsonReaderException jsonReaderEx:
                                ThrowHelper.ReThrowWithPath(ref state, jsonReaderEx);
                                break;

                            case FormatException when ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsJsonException:
                                ThrowHelper.ReThrowWithPath(ref state, reader, ex);
                                break;

                            case InvalidOperationException when ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsJsonException:
                                ThrowHelper.ReThrowWithPath(ref state, reader, ex);
                                break;

                            case JsonException jsonEx when jsonEx.Path is null:
                                ThrowHelper.AddJsonExceptionInformation(ref state, reader, jsonEx);
                                break;
                        }

                        streamReader.SaveReader(ref reader);
                        throw;
                    }
                }

                if (success)
                {
                    return value;
                }

            NeedMoreData:
                if (streamReader.IsFinalBlock)
                {
                    ThrowHelper.ThrowJsonException();
                }

                await streamReader.RefillBufferAsync(stream, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Object-typed bridge for child converter dispatch.
        /// Calls OnReadCoreAsync so native async overrides are used.
        /// </summary>
        internal sealed override async ValueTask<object?> ReadCoreAsyncAsObject(
            JsonStreamReader streamReader,
            Stream stream,
            JsonTypeInfo jsonTypeInfo,
            JsonSerializerOptions options,
            CancellationToken cancellationToken)
        {
            T? value = await OnReadCoreAsync(streamReader, stream, jsonTypeInfo, options, cancellationToken).ConfigureAwait(false);
            return value;
        }
    }
}
