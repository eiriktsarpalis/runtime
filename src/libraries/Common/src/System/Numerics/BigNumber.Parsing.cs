// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

namespace System.Numerics
{
    public readonly partial struct BigNumber
    {
        /// <summary>Parses a number from its UTF-8 representation.</summary>
        /// <param name="utf8Text">The UTF-8 bytes representing a number.</param>
        /// <returns>The parsed number.</returns>
        /// <exception cref="FormatException"><paramref name="utf8Text"/> is not a valid number.</exception>
        /// <exception cref="OverflowException">The exponent is outside the range supported by <see cref="BigNumber"/>.</exception>
        public static BigNumber Parse(ReadOnlySpan<byte> utf8Text)
        {
            ParseStatus status = TryParseCore(utf8Text, out BigNumber result);
            if (status != ParseStatus.Success)
            {
                if (status == ParseStatus.Overflow)
                {
                    ThrowExponentOverflow();
                }

                ThrowFormatException();
            }

            return result;
        }

        /// <summary>Parses a number from its UTF-8 representation.</summary>
        /// <param name="utf8Text">The UTF-8 bytes representing a number.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <returns>The parsed number.</returns>
        /// <exception cref="FormatException"><paramref name="utf8Text"/> is not a valid number.</exception>
        /// <exception cref="OverflowException">The exponent is outside the range supported by <see cref="BigNumber"/>.</exception>
        public static BigNumber Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);

        /// <summary>Attempts to parse a number from its UTF-8 representation.</summary>
        /// <param name="utf8Text">The UTF-8 bytes representing a number.</param>
        /// <param name="result">
        /// When this method returns, contains the parsed number if parsing succeeded or zero if parsing failed.
        /// </param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, out BigNumber result) =>
            TryParseCore(utf8Text, out result) == ParseStatus.Success;

        private static ParseStatus TryParseCore(ReadOnlySpan<byte> utf8Text, out BigNumber result)
        {
            if (!IsValidNumberGrammar(utf8Text))
            {
                result = default;
                return ParseStatus.FormatError;
            }

            if (Utf8Parser.TryParse(utf8Text, out decimal decimalValue, out int bytesConsumed) &&
                bytesConsumed == utf8Text.Length &&
                DecimalFastPathIsFaithful(utf8Text, decimalValue))
            {
                if (decimalValue == 0m && utf8Text[0] == '-')
                {
                    decimalValue = WithNegativeSign(decimalValue);
                }

                result = new BigNumber(decimalValue);
                return ParseStatus.Success;
            }

            return TryParseBig(utf8Text, out result);
        }

        /// <summary>Attempts to parse a number from its UTF-8 representation.</summary>
        /// <param name="utf8Text">The UTF-8 bytes representing a number.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <param name="result">
        /// When this method returns, contains the parsed number if parsing succeeded or zero if parsing failed.
        /// </param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(
            ReadOnlySpan<byte> utf8Text,
            IFormatProvider? provider,
            out BigNumber result) => TryParse(utf8Text, out result);

        /// <summary>Parses a number from its string representation.</summary>
        /// <param name="text">The string representing a number.</param>
        /// <returns>The parsed number.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not a valid number.</exception>
        /// <exception cref="OverflowException">The exponent is outside the range supported by <see cref="BigNumber"/>.</exception>
        public static BigNumber Parse(string text)
        {
#if NET
            ArgumentNullException.ThrowIfNull(text);
#else
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }
#endif
            return Parse(text.AsSpan());
        }

        /// <summary>Parses a number from its string representation.</summary>
        /// <param name="text">The string representing a number.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <returns>The parsed number.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not a valid number.</exception>
        /// <exception cref="OverflowException">The exponent is outside the range supported by <see cref="BigNumber"/>.</exception>
        public static BigNumber Parse(string text, IFormatProvider? provider) => Parse(text);

        /// <summary>Parses a number from its character representation.</summary>
        /// <param name="text">The characters representing a number.</param>
        /// <returns>The parsed number.</returns>
        /// <exception cref="FormatException"><paramref name="text"/> is not a valid number.</exception>
        /// <exception cref="OverflowException">The exponent is outside the range supported by <see cref="BigNumber"/>.</exception>
        public static BigNumber Parse(ReadOnlySpan<char> text)
        {
            ParseStatus status = TryParseCore(text, out BigNumber result);
            if (status != ParseStatus.Success)
            {
                if (status == ParseStatus.Overflow)
                {
                    ThrowExponentOverflow();
                }

                ThrowFormatException();
            }

            return result;
        }

        /// <summary>Parses a number from its character representation.</summary>
        /// <param name="text">The characters representing a number.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <returns>The parsed number.</returns>
        /// <exception cref="FormatException"><paramref name="text"/> is not a valid number.</exception>
        /// <exception cref="OverflowException">The exponent is outside the range supported by <see cref="BigNumber"/>.</exception>
        public static BigNumber Parse(ReadOnlySpan<char> text, IFormatProvider? provider) => Parse(text);

        /// <summary>Attempts to parse a number from its string representation.</summary>
        /// <param name="text">The string representing a number.</param>
        /// <param name="result">
        /// When this method returns, contains the parsed number if parsing succeeded or zero if parsing failed.
        /// </param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse([NotNullWhen(true)] string? text, out BigNumber result)
        {
            if (text is null)
            {
                result = default;
                return false;
            }

            return TryParse(text.AsSpan(), out result);
        }

        /// <summary>Attempts to parse a number from its string representation.</summary>
        /// <param name="text">The string representing a number.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <param name="result">
        /// When this method returns, contains the parsed number if parsing succeeded or zero if parsing failed.
        /// </param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(
            [NotNullWhen(true)] string? text,
            IFormatProvider? provider,
            out BigNumber result) => TryParse(text, out result);

        /// <summary>Attempts to parse a number from its character representation.</summary>
        /// <param name="text">The characters representing a number.</param>
        /// <param name="result">
        /// When this method returns, contains the parsed number if parsing succeeded or zero if parsing failed.
        /// </param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<char> text, out BigNumber result) =>
            TryParseCore(text, out result) == ParseStatus.Success;

        private static ParseStatus TryParseCore(ReadOnlySpan<char> text, out BigNumber result)
        {
            if (text.IsEmpty)
            {
                result = default;
                return ParseStatus.FormatError;
            }

            byte[]? rented = null;
            Span<byte> utf8Text = text.Length <= 256
                ? stackalloc byte[256]
                : (rented = ArrayPool<byte>.Shared.Rent(text.Length));

            try
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char character = text[i];
                    if (character > 0x7F)
                    {
                        result = default;
                        return ParseStatus.FormatError;
                    }

                    utf8Text[i] = (byte)character;
                }

                return TryParseCore(utf8Text.Slice(0, text.Length), out result);
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>Attempts to parse a number from its character representation.</summary>
        /// <param name="text">The characters representing a number.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <param name="result">
        /// When this method returns, contains the parsed number if parsing succeeded or zero if parsing failed.
        /// </param>
        /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(
            ReadOnlySpan<char> text,
            IFormatProvider? provider,
            out BigNumber result) => TryParse(text, out result);

        private static bool IsValidNumberGrammar(ReadOnlySpan<byte> utf8Text)
        {
            if (utf8Text.IsEmpty)
            {
                return false;
            }

            int position = 0;
            if (utf8Text[position] is (byte)'+' or (byte)'-')
            {
                position++;
                if (position == utf8Text.Length)
                {
                    return false;
                }
            }

            int integerStart = position;
            while (position < utf8Text.Length && IsDigit(utf8Text[position]))
            {
                position++;
            }

            if (position == integerStart)
            {
                return false;
            }

            if (position < utf8Text.Length && utf8Text[position] == '.')
            {
                position++;
                int fractionStart = position;
                while (position < utf8Text.Length && IsDigit(utf8Text[position]))
                {
                    position++;
                }

                if (position == fractionStart)
                {
                    return false;
                }
            }

            if (position < utf8Text.Length && utf8Text[position] is (byte)'e' or (byte)'E')
            {
                position++;
                if (position < utf8Text.Length && utf8Text[position] is (byte)'+' or (byte)'-')
                {
                    position++;
                }

                int exponentStart = position;
                while (position < utf8Text.Length && IsDigit(utf8Text[position]))
                {
                    position++;
                }

                if (position == exponentStart)
                {
                    return false;
                }
            }

            return position == utf8Text.Length;
        }

        private static bool DecimalFastPathIsFaithful(ReadOnlySpan<byte> utf8Text, decimal value)
        {
            if (value == 0m)
            {
                for (int i = 0; i < utf8Text.Length; i++)
                {
                    byte character = utf8Text[i];
                    if (character is (byte)'e' or (byte)'E')
                    {
                        break;
                    }

                    if (character is >= (byte)'1' and <= (byte)'9')
                    {
                        return false;
                    }
                }

                return true;
            }

            int significantDigits = 0;
            int fractionalDigits = 0;
            bool seenNonZeroDigit = false;
            int position = utf8Text[0] is (byte)'+' or (byte)'-' ? 1 : 0;
            while (position < utf8Text.Length && IsDigit(utf8Text[position]))
            {
                byte digit = utf8Text[position++];
                seenNonZeroDigit |= digit != '0';
                significantDigits += seenNonZeroDigit ? 1 : 0;
            }

            if (position < utf8Text.Length && utf8Text[position] == '.')
            {
                position++;
                while (position < utf8Text.Length && IsDigit(utf8Text[position]))
                {
                    byte digit = utf8Text[position++];
                    seenNonZeroDigit |= digit != '0';
                    significantDigits += seenNonZeroDigit ? 1 : 0;
                    fractionalDigits++;
                }
            }

            long explicitExponent = 0;
            if (position < utf8Text.Length)
            {
                position++;
                ReadOnlySpan<byte> exponentText = utf8Text.Slice(position);
                if (!exponentText.IsEmpty && exponentText[0] == '+')
                {
                    exponentText = exponentText.Slice(1);
                }

                if (!Utf8Parser.TryParse(exponentText, out explicitExponent, out int consumed) ||
                    consumed != exponentText.Length)
                {
                    return false;
                }
            }

            decimal effectiveScale = fractionalDigits - (decimal)explicitExponent;
            return significantDigits <= 28 && effectiveScale <= 28m;
        }

        private static ParseStatus TryParseBig(ReadOnlySpan<byte> utf8Text, out BigNumber result)
        {
            int position = 0;
            bool isNegative = false;
            if (utf8Text[position] is (byte)'+' or (byte)'-')
            {
                isNegative = utf8Text[position] == '-';
                position++;
            }

            int integerStart = position;
            while (position < utf8Text.Length && IsDigit(utf8Text[position]))
            {
                position++;
            }

            ReadOnlySpan<byte> integerDigits = utf8Text.Slice(integerStart, position - integerStart);
            ReadOnlySpan<byte> fractionalDigits = default;
            if (position < utf8Text.Length && utf8Text[position] == '.')
            {
                position++;
                int fractionStart = position;
                while (position < utf8Text.Length && IsDigit(utf8Text[position]))
                {
                    position++;
                }

                fractionalDigits = utf8Text.Slice(fractionStart, position - fractionStart);
            }

            decimal explicitExponent = 0m;
            if (position < utf8Text.Length)
            {
                position++;
                bool isNegativeExponent = false;
                if (utf8Text[position] is (byte)'+' or (byte)'-')
                {
                    isNegativeExponent = utf8Text[position] == '-';
                    position++;
                }

                ReadOnlySpan<byte> exponentDigits = utf8Text.Slice(position);
                int firstNonZero = 0;
                while (firstNonZero < exponentDigits.Length && exponentDigits[firstNonZero] == '0')
                {
                    firstNonZero++;
                }

                exponentDigits = exponentDigits.Slice(firstNonZero);
                if (!exponentDigits.IsEmpty &&
                    (!Utf8Parser.TryParse(exponentDigits, out explicitExponent, out int consumed) ||
                     consumed != exponentDigits.Length ||
                     explicitExponent != decimal.Truncate(explicitExponent)))
                {
                    result = default;
                    return ParseStatus.Overflow;
                }

                if (isNegativeExponent)
                {
                    explicitExponent = -explicitExponent;
                }
            }

            decimal exponent;
            try
            {
                exponent = explicitExponent - fractionalDigits.Length;
            }
            catch (OverflowException)
            {
                result = default;
                return ParseStatus.Overflow;
            }

            int totalDigits = integerDigits.Length + fractionalDigits.Length;
            byte[]? rented = null;
            Span<byte> allDigits = totalDigits <= 256
                ? stackalloc byte[256]
                : (rented = ArrayPool<byte>.Shared.Rent(totalDigits));

            try
            {
                integerDigits.CopyTo(allDigits);
                fractionalDigits.CopyTo(allDigits.Slice(integerDigits.Length));

                uint[] significand = BigArithmetic.FromDecimalDigits(allDigits.Slice(0, totalDigits));
                if (BigArithmetic.IsZero(significand))
                {
                    result = new BigNumber(new decimal(0, 0, 0, isNegative, 0));
                }
                else
                {
                    result = new BigNumber(significand, exponent, isNegative);
                }

                return ParseStatus.Success;
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        private static bool IsDigit(byte character) => character is >= (byte)'0' and <= (byte)'9';

        private enum ParseStatus
        {
            Success,
            FormatError,
            Overflow,
        }
    }
}
