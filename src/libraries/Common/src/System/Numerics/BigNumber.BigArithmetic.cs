// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Numerics
{
    public readonly partial struct BigNumber
    {
        /// <summary>
        /// Internal arithmetic helpers for operating on uint[] magnitude arrays.
        /// These provide BigInteger-like operations without a BigInteger dependency.
        /// </summary>
        internal static class BigArithmetic
        {
            /// <summary>
            /// Returns true if the magnitude represents zero.
            /// </summary>
            internal static bool IsZero(ReadOnlySpan<uint> value)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] != 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Compares two magnitudes. Returns negative if left &lt; right, zero if equal, positive if left &gt; right.
            /// Both arrays are little-endian (least significant element first).
            /// </summary>
            internal static int Compare(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
            {
                // First compare lengths (after trimming, longer is bigger).
                int leftLen = GetEffectiveLength(left);
                int rightLen = GetEffectiveLength(right);

                if (leftLen != rightLen)
                {
                    return leftLen.CompareTo(rightLen);
                }

                // Compare from most significant element.
                for (int i = leftLen - 1; i >= 0; i--)
                {
                    if (left[i] != right[i])
                    {
                        return left[i].CompareTo(right[i]);
                    }
                }

                return 0;
            }

            /// <summary>
            /// Converts decimal digits to a little-endian base-1,000,000,000 magnitude.
            /// </summary>
            internal static uint[] FromDecimalDigits(ReadOnlySpan<byte> utf8Digits)
            {
                int firstNonZero = 0;
                while (firstNonZero < utf8Digits.Length && utf8Digits[firstNonZero] == '0')
                {
                    firstNonZero++;
                }

                utf8Digits = utf8Digits.Slice(firstNonZero);
                if (utf8Digits.IsEmpty)
                {
                    return [];
                }

                int chunkCount = checked((utf8Digits.Length + 8) / 9);
                uint[] result = new uint[chunkCount];
                int end = utf8Digits.Length;

                for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                {
                    int start = Math.Max(0, end - 9);
                    uint chunkValue = 0;
                    for (int i = start; i < end; i++)
                    {
                        chunkValue = (chunkValue * 10) + (uint)(utf8Digits[i] - '0');
                    }

                    result[chunkIndex] = chunkValue;
                    end = start;
                }

                Debug.Assert(result[result.Length - 1] != 0);
                return result;
            }

            /// <summary>
            /// Converts a uint[] magnitude to a decimal digit string in a char buffer.
            /// Returns the number of characters written.
            /// </summary>
            internal static int ToDecimalDigits(ReadOnlySpan<uint> magnitude, Span<char> destination)
            {
                if (IsZero(magnitude))
                {
                    if (destination.Length < 1)
                    {
                        return 0;
                    }

                    destination[0] = '0';
                    return 1;
                }

                int effectiveLength = GetEffectiveLength(magnitude);
                int requiredLength = GetDecimalDigitCount(magnitude.Slice(0, effectiveLength));
                if (destination.Length < requiredLength)
                {
                    return 0;
                }

                int position = WriteFirstChunk(magnitude[effectiveLength - 1], destination);
                for (int i = effectiveLength - 2; i >= 0; i--)
                {
                    Debug.Assert(magnitude[i] < 1_000_000_000);
                    position += WritePaddedChunk(magnitude[i], destination.Slice(position));
                }

                return position;
            }

            /// <summary>
            /// Converts a uint[] magnitude to UTF-8 decimal digit bytes.
            /// Returns the number of bytes written.
            /// </summary>
            internal static int ToDecimalDigits(ReadOnlySpan<uint> magnitude, Span<byte> destination)
            {
                if (IsZero(magnitude))
                {
                    if (destination.Length < 1)
                    {
                        return 0;
                    }

                    destination[0] = (byte)'0';
                    return 1;
                }

                int effectiveLength = GetEffectiveLength(magnitude);
                int requiredLength = GetDecimalDigitCount(magnitude.Slice(0, effectiveLength));
                if (destination.Length < requiredLength)
                {
                    return 0;
                }

                int position = WriteFirstChunk(magnitude[effectiveLength - 1], destination);
                for (int i = effectiveLength - 2; i >= 0; i--)
                {
                    Debug.Assert(magnitude[i] < 1_000_000_000);
                    position += WritePaddedChunk(magnitude[i], destination.Slice(position));
                }

                return position;
            }

            /// <summary>
            /// Returns the number of decimal digits in the magnitude.
            /// </summary>
            internal static int GetDecimalDigitCount(ReadOnlySpan<uint> magnitude)
            {
                int effectiveLength = GetEffectiveLength(magnitude);
                if (effectiveLength == 0)
                {
                    return 1;
                }

                uint mostSignificantChunk = magnitude[effectiveLength - 1];
                Debug.Assert(mostSignificantChunk < 1_000_000_000);
                return checked(((effectiveLength - 1) * 9) + GetDigitCount(mostSignificantChunk));
            }

            internal static int GetDecimalDigitCountFromBinary(ReadOnlySpan<uint> magnitude)
            {
                Span<uint> chunks = stackalloc uint[4];
                int chunkCount = ConvertBinaryToBase1E9Chunks(magnitude, chunks);
                return chunkCount == 0
                    ? 1
                    : checked(((chunkCount - 1) * 9) + GetDigitCount(chunks[chunkCount - 1]));
            }

            internal static int ToDecimalDigitsFromBinary(ReadOnlySpan<uint> magnitude, Span<char> destination)
            {
                Span<uint> chunks = stackalloc uint[4];
                int chunkCount = ConvertBinaryToBase1E9Chunks(magnitude, chunks);
                if (chunkCount == 0)
                {
                    if (destination.IsEmpty)
                    {
                        return 0;
                    }

                    destination[0] = '0';
                    return 1;
                }

                int requiredLength = checked(
                    ((chunkCount - 1) * 9) + GetDigitCount(chunks[chunkCount - 1]));
                if (destination.Length < requiredLength)
                {
                    return 0;
                }

                int position = WriteFirstChunk(chunks[chunkCount - 1], destination);
                for (int i = chunkCount - 2; i >= 0; i--)
                {
                    position += WritePaddedChunk(chunks[i], destination.Slice(position));
                }

                return position;
            }

            private static int GetEffectiveLength(ReadOnlySpan<uint> value)
            {
                int length = value.Length;
                while (length > 0 && value[length - 1] == 0)
                {
                    length--;
                }

                return length;
            }

            private static int ConvertBinaryToBase1E9Chunks(
                ReadOnlySpan<uint> magnitude,
                Span<uint> chunks)
            {
                Debug.Assert(magnitude.Length <= 3);
                Span<uint> working = stackalloc uint[3];
                magnitude.CopyTo(working);
                int length = GetEffectiveLength(magnitude);
                int chunkCount = 0;

                while (length > 0)
                {
                    Debug.Assert(chunkCount < chunks.Length);
                    chunks[chunkCount++] = DivRemInPlace(working, ref length, 1_000_000_000);
                }

                return chunkCount;
            }

            private static int GetDigitCount(uint value)
            {
                int digitCount = 1;
                while (value >= 10)
                {
                    value /= 10;
                    digitCount++;
                }

                return digitCount;
            }

            private static uint DivRemInPlace(Span<uint> value, ref int length, uint divisor)
            {
                ulong remainder = 0;
                for (int i = length - 1; i >= 0; i--)
                {
                    ulong dividend = (remainder << 32) | value[i];
                    value[i] = (uint)(dividend / divisor);
                    remainder = dividend % divisor;
                }

                while (length > 0 && value[length - 1] == 0)
                {
                    length--;
                }

                return (uint)remainder;
            }

            private static int WriteFirstChunk(uint chunk, Span<char> destination)
            {
                int digitCount = 1;
                for (uint remaining = chunk; remaining >= 10; remaining /= 10)
                {
                    digitCount++;
                }

                Debug.Assert(destination.Length >= digitCount);
                for (int i = digitCount - 1; i >= 0; i--)
                {
                    destination[i] = (char)('0' + (chunk % 10));
                    chunk /= 10;
                }

                return digitCount;
            }

            private static int WriteFirstChunk(uint chunk, Span<byte> destination)
            {
                int digitCount = 1;
                for (uint remaining = chunk; remaining >= 10; remaining /= 10)
                {
                    digitCount++;
                }

                Debug.Assert(destination.Length >= digitCount);
                for (int i = digitCount - 1; i >= 0; i--)
                {
                    destination[i] = (byte)('0' + (chunk % 10));
                    chunk /= 10;
                }

                return digitCount;
            }

            private static int WritePaddedChunk(uint chunk, Span<char> destination)
            {
                Debug.Assert(destination.Length >= 9);
                for (int i = 8; i >= 0; i--)
                {
                    destination[i] = (char)('0' + (chunk % 10));
                    chunk /= 10;
                }

                return 9;
            }

            private static int WritePaddedChunk(uint chunk, Span<byte> destination)
            {
                Debug.Assert(destination.Length >= 9);
                for (int i = 8; i >= 0; i--)
                {
                    destination[i] = (byte)('0' + (chunk % 10));
                    chunk /= 10;
                }

                return 9;
            }

        }
    }
}
