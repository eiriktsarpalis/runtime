// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.Json
{
    public readonly partial struct JsonNumber
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
            internal static bool IsZero(uint[] value)
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
            /// Removes leading zero elements from a magnitude array (little-endian, so trailing in the array).
            /// Returns the trimmed array, or the original if no trimming is needed.
            /// </summary>
            internal static uint[] TrimLeadingZeros(uint[] value)
            {
                int length = value.Length;
                while (length > 0 && value[length - 1] == 0)
                {
                    length--;
                }

                if (length == value.Length)
                {
                    return value;
                }

                if (length == 0)
                {
                    return [];
                }

                uint[] trimmed = new uint[length];
                Array.Copy(value, trimmed, length);
                return trimmed;
            }

            /// <summary>
            /// Multiplies a magnitude by a uint multiplier and adds a uint addend.
            /// result = value * multiplier + addend.
            /// Used for building the magnitude from decimal digit strings: for each digit,
            /// magnitude = magnitude * 10 + digit.
            /// </summary>
            internal static uint[] MultiplyAdd(uint[] value, uint multiplier, uint addend)
            {
                int length = value.Length;
                // Allocate one extra element in case of carry.
                uint[] result = new uint[length + 1];
                ulong carry = addend;

                for (int i = 0; i < length; i++)
                {
                    ulong product = (ulong)value[i] * multiplier + carry;
                    result[i] = (uint)product;
                    carry = product >> 32;
                }

                result[length] = (uint)carry;
                return TrimLeadingZeros(result);
            }

            /// <summary>
            /// Divides a magnitude by a uint divisor, returning the quotient and remainder.
            /// Used for extracting decimal digits from the magnitude.
            /// </summary>
            internal static uint[] DivRem(uint[] value, uint divisor, out uint remainder)
            {
                Debug.Assert(divisor > 0);
                int length = value.Length;
                uint[] quotient = new uint[length];
                ulong rem = 0;

                // Process from most significant to least significant.
                for (int i = length - 1; i >= 0; i--)
                {
                    ulong dividend = (rem << 32) | value[i];
                    quotient[i] = (uint)(dividend / divisor);
                    rem = dividend % divisor;
                }

                remainder = (uint)rem;
                return TrimLeadingZeros(quotient);
            }

            /// <summary>
            /// Compares two magnitudes. Returns negative if left &lt; right, zero if equal, positive if left &gt; right.
            /// Both arrays are little-endian (least significant element first).
            /// </summary>
            internal static int Compare(uint[] left, uint[] right)
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
            /// Multiplies a magnitude by a uint multiplier (no addend).
            /// Used for scaling the significand when aligning exponents for comparison.
            /// </summary>
            internal static uint[] Multiply(uint[] value, uint multiplier)
            {
                return MultiplyAdd(value, multiplier, 0);
            }

            /// <summary>
            /// Multiplies a magnitude by a power of 10.
            /// </summary>
            internal static uint[] MultiplyByPowerOf10(uint[] value, int power)
            {
                Debug.Assert(power >= 0);

                if (power == 0 || IsZero(value))
                {
                    return value;
                }

                uint[] result = value;

                // Multiply by 10^9 chunks to minimize iterations (10^9 fits in uint).
                const uint billion = 1_000_000_000;
                while (power >= 9)
                {
                    result = Multiply(result, billion);
                    power -= 9;
                }

                if (power > 0)
                {
                    uint smallPow = 1;
                    for (int i = 0; i < power; i++)
                    {
                        smallPow *= 10;
                    }

                    result = Multiply(result, smallPow);
                }

                return result;
            }

            /// <summary>
            /// Converts a decimal digit string (UTF-8 bytes) to a uint[] magnitude.
            /// </summary>
            internal static uint[] FromDecimalDigits(ReadOnlySpan<byte> utf8Digits)
            {
                if (utf8Digits.IsEmpty)
                {
                    return [];
                }

                uint[] result = [0];

                // Process 9 digits at a time for efficiency (10^9 fits in uint).
                int i = 0;
                while (i < utf8Digits.Length)
                {
                    int chunkLen = Math.Min(9, utf8Digits.Length - i);
                    uint chunkValue = 0;
                    uint chunkMultiplier = 1;

                    for (int j = 0; j < chunkLen; j++)
                    {
                        chunkMultiplier *= 10;
                        chunkValue = chunkValue * 10 + (uint)(utf8Digits[i + j] - '0');
                    }

                    result = MultiplyAdd(result, chunkMultiplier, chunkValue);
                    i += chunkLen;
                }

                return TrimLeadingZeros(result);
            }

            /// <summary>
            /// Converts a uint[] magnitude to a decimal digit string in a char buffer.
            /// Returns the number of characters written.
            /// </summary>
            internal static int ToDecimalDigits(uint[] magnitude, Span<char> destination)
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

                // Extract digits by repeated division by 10^9.
                // Bound the chunk buffer: each uint contributes ~1.07 billion-chunks.
                const uint billion = 1_000_000_000;
                int estimatedChunks = magnitude.Length + (magnitude.Length >> 1) + 2;
                uint[]? rentedChunks = null;
                Span<uint> chunks = estimatedChunks <= 128
                    ? stackalloc uint[128]
                    : (rentedChunks = new uint[estimatedChunks]);

                int chunkCount = 0;
                uint[] working = (uint[])magnitude.Clone();

                try
                {
                    while (!IsZero(working))
                    {
                        working = DivRem(working, billion, out uint rem);
                        Debug.Assert(chunkCount < chunks.Length, "Chunk buffer too small.");
                        if (chunkCount < chunks.Length)
                        {
                            chunks[chunkCount++] = rem;
                        }
                    }

                    // Write the most significant chunk without leading zeros.
                    int pos = 0;
                    uint firstChunk = chunks[chunkCount - 1];
                    bool started = false;

                    for (int d = 8; d >= 0; d--)
                    {
                        uint pow = PowerOf10((uint)d);
                        uint digit = firstChunk / pow;
                        firstChunk %= pow;

                        if (digit != 0 || started)
                        {
                            started = true;
                            Debug.Assert(pos < destination.Length, "Destination buffer too small for digit conversion.");
                            if (pos < destination.Length)
                            {
                                destination[pos++] = (char)('0' + digit);
                            }
                        }
                    }

                    if (!started && pos < destination.Length)
                    {
                        destination[pos++] = '0';
                    }

                    // Write remaining chunks with leading zeros (9 digits each).
                    for (int c = chunkCount - 2; c >= 0; c--)
                    {
                        uint chunk = chunks[c];
                        for (int d = 8; d >= 0; d--)
                        {
                            uint pow = PowerOf10((uint)d);
                            uint digit = chunk / pow;
                            chunk %= pow;

                            Debug.Assert(pos < destination.Length, "Destination buffer too small for digit conversion.");
                            if (pos < destination.Length)
                            {
                                destination[pos++] = (char)('0' + digit);
                            }
                        }
                    }

                    return pos;
                }
                finally
                {
                    // rentedChunks is a plain new[] allocation, no ArrayPool return needed.
                }
            }

            /// <summary>
            /// Converts a uint[] magnitude to UTF-8 decimal digit bytes.
            /// Returns the number of bytes written.
            /// </summary>
            internal static int ToDecimalDigits(uint[] magnitude, Span<byte> destination)
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

                const uint billion = 1_000_000_000;
                int estimatedChunks = magnitude.Length + (magnitude.Length >> 1) + 2;
                uint[]? rentedChunks = null;
                Span<uint> chunks = estimatedChunks <= 128
                    ? stackalloc uint[128]
                    : (rentedChunks = new uint[estimatedChunks]);

                int chunkCount = 0;
                uint[] working = (uint[])magnitude.Clone();

                try
                {
                    while (!IsZero(working))
                    {
                        working = DivRem(working, billion, out uint rem);
                        Debug.Assert(chunkCount < chunks.Length, "Chunk buffer too small.");
                        if (chunkCount < chunks.Length)
                        {
                            chunks[chunkCount++] = rem;
                        }
                    }

                    int pos = 0;
                    uint firstChunk = chunks[chunkCount - 1];
                    bool started = false;

                    for (int d = 8; d >= 0; d--)
                    {
                        uint pow = PowerOf10((uint)d);
                        uint digit = firstChunk / pow;
                        firstChunk %= pow;

                        if (digit != 0 || started)
                        {
                            started = true;
                            Debug.Assert(pos < destination.Length, "Destination buffer too small for digit conversion.");
                            if (pos < destination.Length)
                            {
                                destination[pos++] = (byte)('0' + digit);
                            }
                        }
                    }

                    if (!started && pos < destination.Length)
                    {
                        destination[pos++] = (byte)'0';
                    }

                    for (int c = chunkCount - 2; c >= 0; c--)
                    {
                        uint chunk = chunks[c];
                        for (int d = 8; d >= 0; d--)
                        {
                            uint pow = PowerOf10((uint)d);
                            uint digit = chunk / pow;
                            chunk %= pow;

                            Debug.Assert(pos < destination.Length, "Destination buffer too small for digit conversion.");
                            if (pos < destination.Length)
                            {
                                destination[pos++] = (byte)('0' + digit);
                            }
                        }
                    }

                    return pos;
                }
                finally
                {
                    // rentedChunks is a plain new[] allocation, no ArrayPool return needed.
                }
            }

            /// <summary>
            /// Returns the number of decimal digits in the magnitude.
            /// </summary>
            internal static int GetDecimalDigitCount(uint[] magnitude)
            {
                if (IsZero(magnitude))
                {
                    return 1;
                }

                // Upper bound: each uint element contributes at most ~9.6 digits.
                // Use log10 approximation: bits * log10(2) ≈ bits * 0.30103.
                int bits = (GetEffectiveLength(magnitude) - 1) * 32;
                uint topElement = magnitude[GetEffectiveLength(magnitude) - 1];
#if NET
                bits += 32 - int.LeadingZeroCount((int)topElement);
#else
                while (topElement > 0)
                {
                    bits++;
                    topElement >>= 1;
                }
#endif

                // Approximate digit count, then refine.
                int approx = (int)(bits * 0.30103) + 1;
                // Add a safety margin.
                return approx + 2;
            }

            private static int GetEffectiveLength(uint[] value)
            {
                int length = value.Length;
                while (length > 0 && value[length - 1] == 0)
                {
                    length--;
                }

                return length;
            }

            private static uint PowerOf10(uint exponent)
            {
                uint result = 1;
                for (uint i = 0; i < exponent; i++)
                {
                    result *= 10;
                }

                return result;
            }
        }
    }
}
