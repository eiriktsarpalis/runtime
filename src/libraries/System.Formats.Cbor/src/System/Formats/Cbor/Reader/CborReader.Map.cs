// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Formats.Cbor
{
    public partial class CborReader
    {
        private KeyEncodingComparer? _keyEncodingComparer;
        private Stack<HashSet<(int Offset, int Length)>>? _pooledKeyEncodingRangeAllocations;

        /// <summary>Reads the next data item as the start of a map (major type 5).</summary>
        /// <returns>The number of key-value pairs in a definite-length map, or <see langword="null" /> if the map is indefinite-length.</returns>
        /// <exception cref="InvalidOperationException">The next data item does not have the correct major type.</exception>
        /// <exception cref="CborContentException"><para>The next value has an invalid CBOR encoding.</para>
        /// <para>-or-</para>
        /// <para>There was an unexpected end of CBOR encoding data.</para>
        /// <para>-or-</para>
        /// <para>The next value uses a CBOR encoding that is not valid under the current conformance mode.</para></exception>
        /// <remarks>
        /// Map contents are consumed as if they were arrays twice the length of the map's declared size.
        /// For instance, a map of size 1 containing a key of type <see cref="int" /> with a value of type <see cref="string" />
        /// must be consumed by successive calls to <see cref="ReadInt32" /> and <see cref="ReadTextString" />.
        /// It is up to the caller to keep track of whether the next value is a key or a value.
        /// Fundamentally, this is a technical restriction stemming from the fact that CBOR allows keys of arbitrary type,
        /// for instance a map can contain keys that are maps themselves.
        /// </remarks>
        public int? ReadStartMap()
        {
            int? length;
            CborInitialByte header = PeekInitialByte(expectedType: CborMajorType.Map);
            EnsureMaxDepthNotExceeded();

            if (header.AdditionalInfo == CborAdditionalInfo.IndefiniteLength)
            {
                if (_isConformanceModeCheckEnabled && CborConformanceModeHelpers.RequiresDefiniteLengthItems(ConformanceMode))
                {
                    throw new CborContentException(SR.Format(SR.Cbor_ConformanceMode_RequiresDefiniteLengthItems, ConformanceMode));
                }

                AdvanceBuffer(1);
                PushDataItem(CborMajorType.Map, null);
                length = null;
            }
            else
            {
                ReadOnlySpan<byte> buffer = GetRemainingBytes();

                int mapSize = DecodeCollectionLength(header, buffer, out int bytesRead);

                if (mapSize > int.MaxValue / 2 ||
                    _isFinalBlock && 2 * (ulong)mapSize > (ulong)(buffer.Length - bytesRead))
                {
                    throw new CborContentException(SR.Cbor_Reader_DefiniteLengthExceedsBufferSize);
                }

                AdvanceBuffer(bytesRead);
                PushDataItem(CborMajorType.Map, 2 * mapSize);
                length = (int)mapSize;
            }

            _currentKeyOffset = _offset;
            return length;
        }

        /// <summary>Reads the end of a map (major type 5).</summary>
        /// <exception cref="InvalidOperationException"><para>The current context is not a map.</para>
        /// <para>-or-</para>
        /// <para>The reader is not at the end of the map.</para></exception>
        /// <exception cref="CborContentException"><para>The next value has an invalid CBOR encoding.</para>
        /// <para>-or-</para>
        /// <para>There was an unexpected end of CBOR encoding data.</para>
        /// <para>-or-</para>
        /// <para>The next value uses a CBOR encoding that is not valid under the current conformance mode.</para></exception>
        public void ReadEndMap()
        {
            if (_definiteLength is null)
            {
                ValidateNextByteIsBreakByte();

                if (_itemsRead % 2 != 0)
                {
                    throw new CborContentException(SR.Cbor_Reader_InvalidCbor_KeyMissingValue);
                }

                PopDataItem(expectedType: CborMajorType.Map);
                AdvanceDataItemCounters();
                AdvanceBuffer(1);
            }
            else
            {
                PopDataItem(expectedType: CborMajorType.Map);
                AdvanceDataItemCounters();
            }
        }

        //
        // Map decoding conformance
        //

        // conformance book-keeping after a key data item has been read
        private void HandleMapKeyRead()
        {
            Debug.Assert(_currentKeyOffset != null && _itemsRead % 2 == 0);

            if (_keyEncodingState is MapKeyEncodingState mapKeyEncodingState)
            {
                if (_isConformanceModeCheckEnabled)
                {
                    byte[] currentKeyEncoding = GetCurrentKeyEncoding(mapKeyEncodingState);

                    if (CborConformanceModeHelpers.RequiresSortedKeys(ConformanceMode))
                    {
                        ValidateSortedKeyEncoding(mapKeyEncodingState, currentKeyEncoding);
                    }
                    else
                    {
                        ValidateKeyUniqueness(mapKeyEncodingState, currentKeyEncoding);
                    }
                }

                mapKeyEncodingState.CurrentKeyEncoding = null;
                mapKeyEncodingState.CurrentKeyEncodingLength = 0;
                return;
            }

            if (!_isConformanceModeCheckEnabled ||
                !CborConformanceModeHelpers.RequiresUniqueKeys(ConformanceMode))
            {
                return;
            }

            (int Offset, int Length) currentKeyRange = (_currentKeyOffset.Value, _offset - _currentKeyOffset.Value);

            if (CborConformanceModeHelpers.RequiresSortedKeys(ConformanceMode))
            {
                ValidateSortedKeyEncoding(currentKeyRange);
            }
            else
            {
                ValidateKeyUniqueness(currentKeyRange);
            }
        }

        // conformance book-keeping after a value data item has been read
        private void HandleMapValueRead()
        {
            Debug.Assert(_currentKeyOffset != null && _itemsRead % 2 != 0);

            _currentKeyOffset = _offset;

            if (_keyEncodingState is MapKeyEncodingState mapKeyEncodingState)
            {
                mapKeyEncodingState.CurrentKeyOffset = _offset;
            }
        }

        private void CaptureMapKeyEncodings()
        {
            if (!CborConformanceModeHelpers.RequiresUniqueKeys(ConformanceMode))
            {
                return;
            }

            if (_currentMajorType == CborMajorType.Map && (_itemsRead & 1) == 0)
            {
                CaptureMapKeyEncoding(_keyEncodingState as MapKeyEncodingState);
            }

            if (_nestedDataItems is null)
            {
                return;
            }

            foreach (StackFrame frame in _nestedDataItems)
            {
                if (frame.MajorType == CborMajorType.Map && (frame.ItemsRead & 1) == 0)
                {
                    CaptureMapKeyEncoding(frame.KeyEncodingState as MapKeyEncodingState);
                }
            }
        }

        private void CaptureMapKeyEncoding(MapKeyEncodingState? state)
        {
            if (state is null)
            {
                return;
            }

            int segmentLength = _offset - state.CurrentKeyOffset;
            Debug.Assert(segmentLength >= 0);

            if (segmentLength > 0)
            {
                int requiredLength = checked(state.CurrentKeyEncodingLength + segmentLength);
                byte[]? keyEncoding = state.CurrentKeyEncoding;

                if (keyEncoding is null)
                {
                    keyEncoding = state.CurrentKeyEncoding = new byte[segmentLength];
                }
                else if (requiredLength > keyEncoding.Length)
                {
                    int doubledCapacity = keyEncoding.Length <= int.MaxValue / 2 ? keyEncoding.Length * 2 : int.MaxValue;
                    Array.Resize(ref keyEncoding, Math.Max(requiredLength, doubledCapacity));
                    state.CurrentKeyEncoding = keyEncoding;
                }

                _data.Span.Slice(state.CurrentKeyOffset, segmentLength).CopyTo(keyEncoding.AsSpan(state.CurrentKeyEncodingLength));
                state.CurrentKeyEncodingLength = requiredLength;
            }

            state.CurrentKeyOffset = 0;
        }

        private byte[] GetCurrentKeyEncoding(MapKeyEncodingState state)
        {
            int currentSegmentLength = _offset - state.CurrentKeyOffset;
            Debug.Assert(currentSegmentLength >= 0);

            ReadOnlySpan<byte> currentSegment = _data.Span.Slice(state.CurrentKeyOffset, currentSegmentLength);
            byte[]? previousSegments = state.CurrentKeyEncoding;

            if (previousSegments is null)
            {
                return currentSegment.ToArray();
            }

            byte[] keyEncoding = new byte[checked(state.CurrentKeyEncodingLength + currentSegmentLength)];
            previousSegments.AsSpan(0, state.CurrentKeyEncodingLength).CopyTo(keyEncoding);
            currentSegment.CopyTo(keyEncoding.AsSpan(state.CurrentKeyEncodingLength));
            return keyEncoding;
        }

        private void ValidateSortedKeyEncoding(MapKeyEncodingState state, byte[] currentKeyEncoding)
        {
            byte[]? previousKeyEncoding = state.PreviousKeyEncoding;

            if (previousKeyEncoding is not null)
            {
                int cmp = CborConformanceModeHelpers.CompareKeyEncodings(previousKeyEncoding, currentKeyEncoding, ConformanceMode);

                if (cmp > 0)
                {
                    ResetBuffer(state.CurrentKeyOffset);
                    throw new CborContentException(SR.Format(SR.Cbor_ConformanceMode_KeysNotInSortedOrder, ConformanceMode));
                }
                else if (cmp == 0)
                {
                    ResetBuffer(state.CurrentKeyOffset);
                    throw new CborContentException(SR.Format(SR.Cbor_ConformanceMode_ContainsDuplicateKeys, ConformanceMode));
                }
            }

            state.PreviousKeyEncoding = currentKeyEncoding;
        }

        private void ValidateKeyUniqueness(MapKeyEncodingState state, byte[] currentKeyEncoding)
        {
            HashSet<byte[]> keyEncodings = state.KeyEncodings ??=
                new HashSet<byte[]>(OwnedKeyEncodingComparer.Instance);

            if (!keyEncodings.Add(currentKeyEncoding))
            {
                ResetBuffer(state.CurrentKeyOffset);
                throw new CborContentException(SR.Format(SR.Cbor_ConformanceMode_ContainsDuplicateKeys, ConformanceMode));
            }

            (state.KeyEncodingOrder ??= new List<byte[]>()).Add(currentKeyEncoding);
        }

        private void RestoreMapKeyEncodingCheckpoint(MapKeyEncodingCheckpoint? checkpoint)
        {
            if (checkpoint is null)
            {
                return;
            }

            MapKeyEncodingState state = checkpoint.State;
            Debug.Assert(ReferenceEquals(_keyEncodingState, state));

            if (state.KeyEncodingOrder is List<byte[]> keyEncodingOrder)
            {
                Debug.Assert(state.KeyEncodings is not null);

                while (keyEncodingOrder.Count > checkpoint.KeyEncodingCount)
                {
                    int index = keyEncodingOrder.Count - 1;
                    byte[] keyEncoding = keyEncodingOrder[index];
                    keyEncodingOrder.RemoveAt(index);
                    bool removed = state.KeyEncodings.Remove(keyEncoding);
                    Debug.Assert(removed);
                }
            }

            state.CurrentKeyOffset = checkpoint.CurrentKeyOffset;
            state.CurrentKeyEncoding = checkpoint.CurrentKeyEncoding;
            state.CurrentKeyEncodingLength = checkpoint.CurrentKeyEncodingLength;
            state.PreviousKeyEncoding = checkpoint.PreviousKeyEncoding;
        }

        private void ValidateSortedKeyEncoding((int Offset, int Length) currentKeyEncodingRange)
        {
            Debug.Assert(_currentKeyOffset != null);

            if (_previousKeyEncodingRange != null)
            {
                (int Offset, int Length) previousKeyEncodingRange = _previousKeyEncodingRange.Value;

                ReadOnlySpan<byte> buffer = _data.Span;
                ReadOnlySpan<byte> previousKeyEncoding = buffer.Slice(previousKeyEncodingRange.Offset, previousKeyEncodingRange.Length);
                ReadOnlySpan<byte> currentKeyEncoding = buffer.Slice(currentKeyEncodingRange.Offset, currentKeyEncodingRange.Length);

                int cmp = CborConformanceModeHelpers.CompareKeyEncodings(previousKeyEncoding, currentKeyEncoding, ConformanceMode);
                if (cmp > 0)
                {
                    ResetBuffer(currentKeyEncodingRange.Offset);
                    throw new CborContentException(SR.Format(SR.Cbor_ConformanceMode_KeysNotInSortedOrder, ConformanceMode));
                }
                else if (cmp == 0 && CborConformanceModeHelpers.RequiresUniqueKeys(ConformanceMode))
                {
                    ResetBuffer(currentKeyEncodingRange.Offset);
                    throw new CborContentException(SR.Format(SR.Cbor_ConformanceMode_ContainsDuplicateKeys, ConformanceMode));
                }
            }

            _previousKeyEncodingRange = currentKeyEncodingRange;
        }

        private void ValidateKeyUniqueness((int Offset, int Length) currentKeyEncodingRange)
        {
            Debug.Assert(_currentKeyOffset != null);

            HashSet<(int Offset, int Length)> keyEncodingRanges = GetKeyEncodingRanges();

            if (!keyEncodingRanges.Add(currentKeyEncodingRange))
            {
                ResetBuffer(currentKeyEncodingRange.Offset);
                throw new CborContentException(SR.Format(SR.Cbor_ConformanceMode_ContainsDuplicateKeys, ConformanceMode));
            }
        }

        private HashSet<(int Offset, int Length)> GetKeyEncodingRanges()
        {
            if (_keyEncodingState is HashSet<(int Offset, int Length)> keyEncodingRanges)
            {
                return keyEncodingRanges;
            }

            if (_pooledKeyEncodingRangeAllocations != null &&
                _pooledKeyEncodingRangeAllocations.TryPop(out HashSet<(int Offset, int Length)>? result))
            {
                result.Clear();
                _keyEncodingState = result;
                return result;
            }

            _keyEncodingComparer ??= new KeyEncodingComparer(this);
            var newKeyEncodingRanges = new HashSet<(int Offset, int Length)>(_keyEncodingComparer);
            _keyEncodingState = newKeyEncodingRanges;
            return newKeyEncodingRanges;
        }

        private void ReturnKeyEncodingRangeAllocation(HashSet<(int Offset, int Length)>? allocation)
        {
            if (allocation != null)
            {
                _pooledKeyEncodingRangeAllocations ??= new Stack<HashSet<(int Offset, int Length)>>();
                _pooledKeyEncodingRangeAllocations.Push(allocation);
            }
        }

        // Comparing buffer slices up to their binary content
        private sealed class KeyEncodingComparer : IEqualityComparer<(int Offset, int Length)>
        {
            private readonly CborReader _reader;

            public KeyEncodingComparer(CborReader reader)
            {
                _reader = reader;
            }

            private ReadOnlySpan<byte> GetKeyEncoding((int Offset, int Length) range)
            {
                return _reader._data.Span.Slice(range.Offset, range.Length);
            }

            public int GetHashCode((int Offset, int Length) value)
            {
                return CborConformanceModeHelpers.GetKeyEncodingHashCode(GetKeyEncoding(value));
            }

            public bool Equals((int Offset, int Length) x, (int Offset, int Length) y)
            {
                return CborConformanceModeHelpers.AreEqualKeyEncodings(GetKeyEncoding(x), GetKeyEncoding(y));
            }
        }

        private sealed class OwnedKeyEncodingComparer : IEqualityComparer<byte[]>
        {
            private OwnedKeyEncodingComparer()
            {
            }

            public static OwnedKeyEncodingComparer Instance { get; } = new OwnedKeyEncodingComparer();

            public bool Equals(byte[]? x, byte[]? y)
            {
                Debug.Assert(x is not null);
                Debug.Assert(y is not null);
                return CborConformanceModeHelpers.AreEqualKeyEncodings(x, y);
            }

            public int GetHashCode(byte[] obj)
            {
                return CborConformanceModeHelpers.GetKeyEncodingHashCode(obj);
            }
        }

        private sealed class MapKeyEncodingState
        {
            public MapKeyEncodingState(int currentKeyOffset)
            {
                CurrentKeyOffset = currentKeyOffset;
            }

            public int CurrentKeyOffset { get; set; }
            public byte[]? CurrentKeyEncoding { get; set; }
            public int CurrentKeyEncodingLength { get; set; }
            public byte[]? PreviousKeyEncoding { get; set; }
            public HashSet<byte[]>? KeyEncodings { get; set; }
            public List<byte[]>? KeyEncodingOrder { get; set; }
        }

        private sealed class MapKeyEncodingCheckpoint
        {
            public MapKeyEncodingCheckpoint(MapKeyEncodingState state)
            {
                State = state;
                CurrentKeyOffset = state.CurrentKeyOffset;
                CurrentKeyEncoding = state.CurrentKeyEncoding;
                CurrentKeyEncodingLength = state.CurrentKeyEncodingLength;
                PreviousKeyEncoding = state.PreviousKeyEncoding;
                KeyEncodingCount = state.KeyEncodingOrder?.Count ?? 0;
            }

            public MapKeyEncodingState State { get; }
            public int CurrentKeyOffset { get; }
            public byte[]? CurrentKeyEncoding { get; }
            public int CurrentKeyEncodingLength { get; }
            public byte[]? PreviousKeyEncoding { get; }
            public int KeyEncodingCount { get; }
        }
    }
}
