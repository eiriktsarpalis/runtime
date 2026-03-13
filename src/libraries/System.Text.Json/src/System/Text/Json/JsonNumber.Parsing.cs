// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Text.Json
{
    public readonly partial struct JsonNumber
#if NET
        : ISpanParsable<JsonNumber>, IUtf8SpanParsable<JsonNumber>
#endif
    {
        /// <summary>
        /// Parses a JSON number from its UTF-8 representation.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 bytes representing a JSON number.</param>
        /// <returns>A <see cref="JsonNumber"/> representing the parsed value.</returns>
        /// <exception cref="FormatException"><paramref name="utf8Text"/> is not a valid JSON number.</exception>
        public static JsonNumber Parse(ReadOnlySpan<byte> utf8Text)
        {
            if (!TryParse(utf8Text, out JsonNumber result))
            {
                ThrowHelper.ThrowFormatException_BadJsonNumber();
            }

            return result;
        }

        /// <summary>
        /// Attempts to parse a JSON number from its UTF-8 representation.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 bytes representing a JSON number.</param>
        /// <param name="result">When this method returns, contains the parsed value if parsing succeeded.</param>
        /// <returns><see langword="true"/> if <paramref name="utf8Text"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, out JsonNumber result)
        {
            if (utf8Text.IsEmpty)
            {
                result = default;
                return false;
            }

            // Validate strict JSON number grammar before trying any fast path.
            // This prevents accepting inputs like "+1", ".5", "1.", "01" that
            // decimal.TryParse would accept but are not valid JSON.
            if (!IsValidJsonNumberGrammar(utf8Text))
            {
                result = default;
                return false;
            }

            // First, try decimal — this covers the vast majority of real-world JSON numbers.
            if (Utf8Parser.TryParse(utf8Text, out decimal decimalValue, out int bytesConsumed) &&
                bytesConsumed == utf8Text.Length &&
                DecimalFastPathIsFaithful(utf8Text, decimalValue))
            {
                result = new JsonNumber(decimalValue);
                return true;
            }

            // Decimal failed — parse as a big number.
            return TryParseBig(utf8Text, out result);
        }

        /// <summary>
        /// Validates that the input follows strict JSON number grammar:
        /// <c>number = [ '-' ] int [ frac ] [ exp ]</c>
        /// <c>int    = '0' | ( digit1-9 *digit )</c>
        /// <c>frac   = '.' 1*digit</c>
        /// <c>exp    = ('e' | 'E') ['+' | '-'] 1*digit</c>
        /// </summary>
        private static bool IsValidJsonNumberGrammar(ReadOnlySpan<byte> utf8Text)
        {
            if (utf8Text.IsEmpty)
            {
                return false;
            }

            int pos = 0;

            if (utf8Text[pos] == '-')
            {
                pos++;
                if (pos >= utf8Text.Length)
                {
                    return false;
                }
            }

            // Leading '+' is not valid JSON.
            if (utf8Text[pos] == '+')
            {
                return false;
            }

            // Integer part must start with a digit.
            if (utf8Text[pos] < '0' || utf8Text[pos] > '9')
            {
                return false;
            }

            if (utf8Text[pos] == '0')
            {
                pos++;
                // After '0', next must be '.', 'e', 'E', or end — no leading zeros.
                if (pos < utf8Text.Length && utf8Text[pos] >= '0' && utf8Text[pos] <= '9')
                {
                    return false;
                }
            }
            else
            {
                while (pos < utf8Text.Length && utf8Text[pos] >= '0' && utf8Text[pos] <= '9')
                {
                    pos++;
                }
            }

            // Optional fractional part.
            if (pos < utf8Text.Length && utf8Text[pos] == '.')
            {
                pos++;
                // '.' must be followed by at least one digit.
                if (pos >= utf8Text.Length || utf8Text[pos] < '0' || utf8Text[pos] > '9')
                {
                    return false;
                }

                while (pos < utf8Text.Length && utf8Text[pos] >= '0' && utf8Text[pos] <= '9')
                {
                    pos++;
                }
            }

            // Optional exponent.
            if (pos < utf8Text.Length && (utf8Text[pos] == 'e' || utf8Text[pos] == 'E'))
            {
                pos++;
                if (pos >= utf8Text.Length)
                {
                    return false;
                }

                if (utf8Text[pos] == '+' || utf8Text[pos] == '-')
                {
                    pos++;
                    if (pos >= utf8Text.Length)
                    {
                        return false;
                    }
                }

                if (utf8Text[pos] < '0' || utf8Text[pos] > '9')
                {
                    return false;
                }

                while (pos < utf8Text.Length && utf8Text[pos] >= '0' && utf8Text[pos] <= '9')
                {
                    pos++;
                }
            }

            return pos == utf8Text.Length;
        }

        /// <summary>
        /// Parses a JSON number from its string representation.
        /// </summary>
        /// <param name="text">The string representing a JSON number.</param>
        /// <returns>A <see cref="JsonNumber"/> representing the parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not a valid JSON number.</exception>
        public static JsonNumber Parse(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return Parse(text.AsSpan());
        }

        /// <summary>
        /// Parses a JSON number from its character representation.
        /// </summary>
        /// <param name="text">The characters representing a JSON number.</param>
        /// <returns>A <see cref="JsonNumber"/> representing the parsed value.</returns>
        /// <exception cref="FormatException"><paramref name="text"/> is not a valid JSON number.</exception>
        public static JsonNumber Parse(ReadOnlySpan<char> text)
        {
            if (!TryParse(text, out JsonNumber result))
            {
                ThrowHelper.ThrowFormatException_BadJsonNumber();
            }

            return result;
        }

        /// <summary>
        /// Attempts to parse a JSON number from its string representation.
        /// </summary>
        /// <param name="text">The string representing a JSON number.</param>
        /// <param name="result">When this method returns, contains the parsed value if parsing succeeded.</param>
        /// <returns><see langword="true"/> if <paramref name="text"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse([NotNullWhen(true)] string? text, out JsonNumber result)
        {
            if (text is null)
            {
                result = default;
                return false;
            }

            return TryParse(text.AsSpan(), out result);
        }

        /// <summary>
        /// Attempts to parse a JSON number from its character representation.
        /// </summary>
        /// <param name="text">The characters representing a JSON number.</param>
        /// <param name="result">When this method returns, contains the parsed value if parsing succeeded.</param>
        /// <returns><see langword="true"/> if <paramref name="text"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<char> text, out JsonNumber result)
        {
            if (text.IsEmpty)
            {
                result = default;
                return false;
            }

            // Transcode to UTF-8 on the stack for small inputs, heap for large.
            int maxUtf8Len = text.Length * 3;
            byte[]? rented = null;
            Span<byte> utf8Buffer = maxUtf8Len <= 256
                ? stackalloc byte[256]
                : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxUtf8Len));
            try
            {
                int written = Encoding.UTF8.GetBytes(text, utf8Buffer);
                return TryParse(utf8Buffer.Slice(0, written), out result);
            }
            finally
            {
                if (rented is not null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

#if NET
        // ISpanParsable<JsonNumber>
        static JsonNumber ISpanParsable<JsonNumber>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
        static bool ISpanParsable<JsonNumber>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out JsonNumber result) => TryParse(s, out result);

        // IParsable<JsonNumber>
        static JsonNumber IParsable<JsonNumber>.Parse(string s, IFormatProvider? provider) => Parse(s);
        static bool IParsable<JsonNumber>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out JsonNumber result) => TryParse(s, out result);

        // IUtf8SpanParsable<JsonNumber>
        static JsonNumber IUtf8SpanParsable<JsonNumber>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
        static bool IUtf8SpanParsable<JsonNumber>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out JsonNumber result) => TryParse(utf8Text, out result);
#endif

        /// <summary>
        /// Checks whether the decimal fast-path result faithfully represents the input.
        /// Returns false if decimal underflowed to zero or lost significant digits.
        /// </summary>
        private static bool DecimalFastPathIsFaithful(ReadOnlySpan<byte> utf8Text, decimal value)
        {
            // If result is zero, verify the input significand has no non-zero digits.
            // This catches cases like "1e-1024" which Utf8Parser silently underflows to 0m.
            if (value == 0m)
            {
                for (int i = 0; i < utf8Text.Length; i++)
                {
                    byte c = utf8Text[i];
                    if (c is (byte)'e' or (byte)'E')
                    {
                        break;
                    }

                    if (c >= '1' && c <= '9')
                    {
                        return false;
                    }
                }

                return true;
            }

            // Count significant digits in the significand (before exponent).
            // Leading zeros (including those after a decimal point) are not significant.
            // If the count exceeds 28 (decimal's guaranteed precision range),
            // the value may have lost trailing digits due to mantissa overflow.
            int significantDigits = 0;
            bool seenNonZeroDigit = false;
            for (int i = 0; i < utf8Text.Length; i++)
            {
                byte c = utf8Text[i];
                if (c is (byte)'e' or (byte)'E')
                {
                    break;
                }

                if (c >= '0' && c <= '9')
                {
                    if (c != '0')
                    {
                        seenNonZeroDigit = true;
                    }

                    if (seenNonZeroDigit)
                    {
                        significantDigits++;
                    }
                }
            }

            return significantDigits <= 28;
        }

        /// <summary>
        /// Parses a big number from UTF-8 bytes when decimal parsing fails.
        /// Follows the JSON number grammar: [-] integral [. fractional] [(e|E) [+|-] exponent]
        /// </summary>
        private static bool TryParseBig(ReadOnlySpan<byte> utf8Text, out JsonNumber result)
        {
            result = default;

            if (utf8Text.IsEmpty)
            {
                return false;
            }

            int pos = 0;
            bool isNegative = false;

            // Parse optional sign.
            if (utf8Text[pos] == '-')
            {
                isNegative = true;
                pos++;
                if (pos >= utf8Text.Length)
                {
                    return false;
                }
            }

            // JSON doesn't allow leading '+'.
            if (utf8Text[pos] == '+')
            {
                return false;
            }

            // Parse integral part.
            int integralStart = pos;
            while (pos < utf8Text.Length && utf8Text[pos] >= '0' && utf8Text[pos] <= '9')
            {
                pos++;
            }

            int integralLength = pos - integralStart;
            if (integralLength == 0)
            {
                return false;
            }

            // JSON doesn't allow leading zeros in integral part (except "0" itself).
            if (integralLength > 1 && utf8Text[integralStart] == '0')
            {
                return false;
            }

            ReadOnlySpan<byte> integralDigits = utf8Text.Slice(integralStart, integralLength);

            // Parse optional fractional part.
            ReadOnlySpan<byte> fractionalDigits = default;
            if (pos < utf8Text.Length && utf8Text[pos] == '.')
            {
                pos++;
                int fracStart = pos;
                while (pos < utf8Text.Length && utf8Text[pos] >= '0' && utf8Text[pos] <= '9')
                {
                    pos++;
                }

                int fracLength = pos - fracStart;
                if (fracLength == 0)
                {
                    return false;
                }

                fractionalDigits = utf8Text.Slice(fracStart, fracLength);
            }

            // Parse optional exponent.
            int explicitExponent = 0;
            if (pos < utf8Text.Length && (utf8Text[pos] == 'e' || utf8Text[pos] == 'E'))
            {
                pos++;
                if (pos >= utf8Text.Length)
                {
                    return false;
                }

                bool expNegative = false;
                if (utf8Text[pos] == '+')
                {
                    pos++;
                }
                else if (utf8Text[pos] == '-')
                {
                    expNegative = true;
                    pos++;
                }

                if (pos >= utf8Text.Length)
                {
                    return false;
                }

                int expStart = pos;
                while (pos < utf8Text.Length && utf8Text[pos] >= '0' && utf8Text[pos] <= '9')
                {
                    pos++;
                }

                if (pos == expStart)
                {
                    return false;
                }

                // Parse the exponent value — capped to int range.
                ReadOnlySpan<byte> expDigits = utf8Text.Slice(expStart, pos - expStart);
                if (!Utf8Parser.TryParse(expDigits, out int parsedExp, out int expConsumed) ||
                    expConsumed != expDigits.Length)
                {
                    // Exponent doesn't fit in int.
                    return false;
                }

                explicitExponent = expNegative ? -parsedExp : parsedExp;
            }

            // Must have consumed all input.
            if (pos != utf8Text.Length)
            {
                return false;
            }

            // Build the significand from all digits (integral + fractional).
            // The effective exponent is: explicitExponent - fractionalDigits.Length.
            int totalDigits = integralDigits.Length + fractionalDigits.Length;
            byte[]? rentedDigits = null;
            Span<byte> allDigits = totalDigits <= 256
                ? stackalloc byte[256].Slice(0, totalDigits)
                : (rentedDigits = System.Buffers.ArrayPool<byte>.Shared.Rent(totalDigits)).AsSpan(0, totalDigits);

            try
            {
                integralDigits.CopyTo(allDigits);
                fractionalDigits.CopyTo(allDigits.Slice(integralDigits.Length));

                uint[] significand = BigArithmetic.FromDecimalDigits(allDigits);

                // Use long to avoid integer overflow when combining explicit exponent
                // with fractional digit count.
                long exponentLong = (long)explicitExponent - fractionalDigits.Length;
                if (exponentLong < int.MinValue || exponentLong > int.MaxValue)
                {
                    return false;
                }

                int exponent = (int)exponentLong;

                // Normalize: if significand is zero, normalize sign and exponent.
                if (BigArithmetic.IsZero(significand))
                {
                    isNegative = false;
                    exponent = 0;
                }
                else
                {
                    // Strip trailing zeros from significand, incrementing exponent.
                    // This ensures IsInteger returns correct results for inputs like "100.0"
                    // (significand=1000, exp=-1 → significand=1, exp=2).
                    while (!BigArithmetic.IsZero(significand))
                    {
                        uint[] quotient = BigArithmetic.DivRem(significand, 10, out uint remainder);
                        if (remainder != 0)
                        {
                            break;
                        }

                        significand = quotient;
                        exponent++;
                    }
                }

                result = new JsonNumber(new BigDecimalData(significand, exponent, isNegative));
                return true;
            }
            finally
            {
                if (rentedDigits is not null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedDigits);
                }
            }
        }
    }
}
