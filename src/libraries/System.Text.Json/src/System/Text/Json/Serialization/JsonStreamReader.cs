// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Manages async reading from a stream for use by async converter methods.
    /// Wraps buffer management and provides methods to create Utf8JsonReaders
    /// that can be saved/restored across async buffer refill boundaries.
    /// </summary>
    internal sealed class JsonStreamReader : IDisposable
    {
        private byte[] _buffer;
        private int _offset;
        private int _count;
        private int _maxCount;
        private bool _isFinalBlock;
        private bool _isFirstBlock;
        private JsonReaderState _readerState;

        public bool IsFinalBlock => _isFinalBlock;
        public JsonReaderState ReaderState => _readerState;

        public JsonStreamReader(JsonSerializerOptions options)
        {
            int bufferSize = options.DefaultBufferSize;
            _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(bufferSize, JsonConstants.Utf8Bom.Length));
            _readerState = new JsonReaderState(options.GetReaderOptions());
            _isFirstBlock = true;
        }

        /// <summary>
        /// Creates a Utf8JsonReader for the current buffer contents.
        /// After reading, call <see cref="SaveReader"/> to persist state,
        /// then <see cref="RefillBufferAsync"/> if more data is needed.
        /// </summary>
        public Utf8JsonReader GetReader()
        {
            return new Utf8JsonReader(
                _buffer.AsSpan(_offset, _count),
                _isFinalBlock,
                _readerState);
        }

        /// <summary>
        /// Saves the reader's state after synchronous reading.
        /// Must be called before any await (buffer refill) or return.
        /// </summary>
        public void SaveReader(ref Utf8JsonReader reader)
        {
            _readerState = reader.CurrentState;
            AdvanceBuffer((int)reader.BytesConsumed);
        }

        /// <summary>
        /// Attempts to read the next token from the current buffer.
        /// On success, captures the token type and (for property names/strings)
        /// the unescaped value bytes, then advances the buffer.
        /// On failure (buffer exhausted), saves partial progress for refill.
        /// </summary>
        public bool TryReadToken(out JsonTokenType tokenType, out byte[]? valueBytes)
        {
            Utf8JsonReader reader = GetReader();
            if (reader.Read())
            {
                tokenType = reader.TokenType;
                valueBytes = tokenType is JsonTokenType.PropertyName or JsonTokenType.String
                    ? (reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray())
                    : null;
                SaveReader(ref reader);
                return true;
            }

            tokenType = default;
            valueBytes = null;
            SaveReader(ref reader);
            return false;
        }

        /// <summary>
        /// Peeks at the next token type without advancing the buffer.
        /// The next GetReader/TryReadToken will see the same token.
        /// Returns false if buffer is exhausted and needs refilling.
        /// </summary>
        public bool TryPeekTokenType(out JsonTokenType tokenType)
        {
            Utf8JsonReader reader = GetReader();
            if (reader.Read())
            {
                tokenType = reader.TokenType;
                if (tokenType is JsonTokenType.EndArray or JsonTokenType.EndObject)
                {
                    // Consume end markers so the parent doesn't need to re-read
                    SaveReader(ref reader);
                }
                // For non-end tokens: DON'T save, leave position for child to read
                return true;
            }

            tokenType = default;
            SaveReader(ref reader); // save any partial whitespace progress
            return false;
        }
        public bool TrySkipValue()
        {
            Utf8JsonReader reader = GetReader();
            bool success = reader.TrySkip();
            SaveReader(ref reader);
            return success;
        }

        /// <summary>
        /// Reads more data from the stream into the buffer.
        /// Call after reader.Read() returns false and IsFinalBlock is false.
        /// </summary>
        public async ValueTask RefillBufferAsync(Stream stream, CancellationToken cancellationToken)
        {
            do
            {
                int bytesRead = await stream.ReadAsync(
                    _buffer.AsMemory(_count, _buffer.Length - _count),
                    cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    _isFinalBlock = true;
                    break;
                }

                _count += bytesRead;
            }
            while (_count < _buffer.Length);

            if (_count > _maxCount)
            {
                _maxCount = _count;
            }

            if (_isFirstBlock)
            {
                _isFirstBlock = false;

                Debug.Assert(_buffer.Length >= JsonConstants.Utf8Bom.Length);
                if (_buffer.AsSpan(0, _count).StartsWith(JsonConstants.Utf8Bom))
                {
                    _offset = JsonConstants.Utf8Bom.Length;
                    _count -= JsonConstants.Utf8Bom.Length;
                }
            }
        }

        // ---- Buffer management ----

        private void AdvanceBuffer(int bytesConsumed)
        {
            Debug.Assert(bytesConsumed <= _count);

            _count -= bytesConsumed;

            if (!_isFinalBlock)
            {
                if ((uint)_count > ((uint)_buffer.Length / 2))
                {
                    byte[] oldBuffer = _buffer;
                    int oldMaxCount = _maxCount;
                    byte[] newBuffer = ArrayPool<byte>.Shared.Rent(
                        (_buffer.Length < (int.MaxValue / 2)) ? _buffer.Length * 2 : int.MaxValue);

                    Buffer.BlockCopy(oldBuffer, _offset + bytesConsumed, newBuffer, 0, _count);
                    _buffer = newBuffer;
                    _maxCount = _count;

                    new Span<byte>(oldBuffer, 0, oldMaxCount).Clear();
                    ArrayPool<byte>.Shared.Return(oldBuffer);
                }
                else if (_count != 0)
                {
                    Buffer.BlockCopy(_buffer, _offset + bytesConsumed, _buffer, 0, _count);
                }

                _offset = 0;
            }
            else
            {
                // On the final block, don't shift — just advance the offset.
                _offset += bytesConsumed;
            }
        }

        public void Dispose()
        {
            if (_buffer is not null)
            {
                new Span<byte>(_buffer, 0, _maxCount).Clear();
                byte[] toReturn = _buffer;
                _buffer = null!;
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }
    }
}
