// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;

namespace System.Numerics
{
    public readonly partial struct BigNumber
    {
        /// <summary>Returns the invariant string representation of this number.</summary>
        /// <returns>The invariant string representation of this number.</returns>
        public override string ToString()
        {
            if (_significand is null)
            {
                if (_value == 0m && IsNegativeDecimal(_value))
                {
                    return "-" + Math.Abs(_value).ToString(CultureInfo.InvariantCulture);
                }

                return _value.ToString(CultureInfo.InvariantCulture);
            }

            return FormatBig();
        }

        /// <summary>Returns the invariant string representation of this number.</summary>
        /// <param name="format">The format to use. Only <c>G</c> and <c>R</c> are supported.</param>
        /// <param name="formatProvider">This parameter is ignored.</param>
        /// <returns>The invariant string representation of this number.</returns>
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            ValidateFormat(format);
            return ToString();
        }

        /// <summary>Tries to format this number into a span of characters.</summary>
        /// <param name="destination">The span in which to write this number.</param>
        /// <param name="charsWritten">
        /// When this method returns, contains the number of characters written to <paramref name="destination"/>.
        /// </param>
        /// <param name="format">The format to use. Only <c>G</c> and <c>R</c> are supported.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <returns>
        /// <see langword="true"/> if the formatting operation succeeded; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryFormat(
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format = default,
            IFormatProvider? provider = null)
        {
            ValidateFormat(format);

#if NET
            if (_significand is null)
            {
                if (_value == 0m && IsNegativeDecimal(_value))
                {
                    if (destination.IsEmpty)
                    {
                        charsWritten = 0;
                        return false;
                    }

                    if (!Math.Abs(_value).TryFormat(
                        destination.Slice(1),
                        out int valueCharsWritten,
                        format: default,
                        CultureInfo.InvariantCulture))
                    {
                        charsWritten = 0;
                        return false;
                    }

                    destination[0] = '-';
                    charsWritten = valueCharsWritten + 1;
                    return true;
                }

                return _value.TryFormat(
                    destination,
                    out charsWritten,
                    format: default,
                    CultureInfo.InvariantCulture);
            }

            return TryFormatBig(destination, out charsWritten);
#else
            string formatted = ToString();
            if (formatted.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            formatted.AsSpan().CopyTo(destination);
            charsWritten = formatted.Length;
            return true;
#endif
        }

        /// <summary>Tries to format this number into a span of UTF-8 bytes.</summary>
        /// <param name="utf8Destination">The span in which to write this number.</param>
        /// <param name="bytesWritten">
        /// When this method returns, contains the number of bytes written to <paramref name="utf8Destination"/>.
        /// </param>
        /// <param name="format">The format to use. Only <c>G</c> and <c>R</c> are supported.</param>
        /// <param name="provider">This parameter is ignored.</param>
        /// <returns>
        /// <see langword="true"/> if the formatting operation succeeded; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryFormat(
            Span<byte> utf8Destination,
            out int bytesWritten,
            ReadOnlySpan<char> format = default,
            IFormatProvider? provider = null)
        {
            ValidateFormat(format);

            if (_significand is null)
            {
                if (_value == 0m && IsNegativeDecimal(_value))
                {
                    if (utf8Destination.IsEmpty ||
                        !Utf8Formatter.TryFormat(Math.Abs(_value), utf8Destination.Slice(1), out int valueBytesWritten))
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    utf8Destination[0] = (byte)'-';
                    bytesWritten = valueBytesWritten + 1;
                    return true;
                }

                return Utf8Formatter.TryFormat(_value, utf8Destination, out bytesWritten);
            }

            return TryFormatBigUtf8(utf8Destination, out bytesWritten);
        }

        internal int WriteUtf8(Span<byte> destination)
        {
            if (_significand is null)
            {
                int offset = 0;
                decimal value = _value;
                if (value == 0m && IsNegativeDecimal(value))
                {
                    destination[0] = (byte)'-';
                    offset = 1;
                    value = Math.Abs(value);
                }

                if (!Utf8Formatter.TryFormat(value, destination.Slice(offset), out int written))
                {
                    Debug.Fail("Buffer too small for decimal formatting.");
                    return 0;
                }

                return written + offset;
            }

            if (!TryFormatBigUtf8(destination, out int bytesWritten))
            {
                Debug.Fail("Buffer too small for BigNumber formatting.");
                return 0;
            }

            return bytesWritten;
        }

        internal int GetMaxUtf8Length()
        {
            if (_significand is null)
            {
                return 32;
            }

            return checked(BigArithmetic.GetDecimalDigitCount(_significand) + 33);
        }

        private string FormatBig()
        {
            int length = GetBigFormatInfo(out _, out _, out _, out _);
#if NET
            return string.Create(length, this, static (destination, value) =>
            {
                bool success = value.TryFormatBig(destination, out int charsWritten);
                Debug.Assert(success && charsWritten == destination.Length);
            });
#else
            char[] chars = new char[length];
            bool success = TryFormatBig(chars, out int charsWritten);
            Debug.Assert(success && charsWritten == chars.Length);
            return new string(chars);
#endif
        }

        private bool TryFormatBig(Span<char> destination, out int charsWritten)
        {
            Debug.Assert(_significand is not null);

            int requiredLength = GetBigFormatInfo(
                out int digitCount,
                out decimal exponent,
                out bool isNegative,
                out BigFormatKind formatKind);

            if (requiredLength > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            int position = 0;
            if (isNegative)
            {
                destination[position++] = '-';
            }

            Span<char> digits = destination.Slice(position);
            int digitsWritten = BigArithmetic.ToDecimalDigits(_significand, digits);
            Debug.Assert(digitsWritten == digitCount);

            bool writeExponent = false;
            switch (formatKind)
            {
                case BigFormatKind.Integer:
                    int trailingZeros = (int)exponent;
                    destination.Slice(position + digitCount, trailingZeros).Fill('0');
                    position += digitCount + trailingZeros;
                    break;

                case BigFormatKind.Decimal:
                    int integralDigits = digitCount + (int)exponent;
                    digits.Slice(integralDigits, digitCount - integralDigits)
                        .CopyTo(digits.Slice(integralDigits + 1));
                    digits[integralDigits] = '.';
                    position += digitCount + 1;
                    break;

                case BigFormatKind.LeadingDecimal:
                    int leadingZeros = (int)(-exponent - digitCount);
                    digits.Slice(0, digitCount).CopyTo(digits.Slice(leadingZeros + 2));
                    digits[0] = '0';
                    digits[1] = '.';
                    digits.Slice(2, leadingZeros).Fill('0');
                    position += digitCount + leadingZeros + 2;
                    break;

                case BigFormatKind.Scientific:
                    if (digitCount > 1)
                    {
                        digits.Slice(1, digitCount - 1).CopyTo(digits.Slice(2));
                        digits[1] = '.';
                        position += digitCount + 1;
                    }
                    else
                    {
                        position += digitCount;
                    }

                    writeExponent = true;
                    break;

                case BigFormatKind.UnnormalizedScientific:
                    position += digitCount;
                    writeExponent = true;
                    break;
            }

            if (writeExponent)
            {
                destination[position++] = 'E';
                if (exponent > 0m)
                {
                    destination[position++] = '+';
                }

#if NET
                bool success = exponent.TryFormat(
                    destination.Slice(position),
                    out int exponentCharsWritten,
                    format: default,
                    CultureInfo.InvariantCulture);
                Debug.Assert(success);
                position += exponentCharsWritten;
#else
                string exponentText = exponent.ToString("0", CultureInfo.InvariantCulture);
                exponentText.AsSpan().CopyTo(destination.Slice(position));
                position += exponentText.Length;
#endif
            }

            Debug.Assert(position == requiredLength);
            charsWritten = position;
            return true;
        }

        private bool TryFormatBigUtf8(Span<byte> destination, out int bytesWritten)
        {
            Debug.Assert(_significand is not null);

            int requiredLength = GetBigFormatInfo(
                out int digitCount,
                out decimal exponent,
                out bool isNegative,
                out BigFormatKind formatKind);

            if (requiredLength > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            int position = 0;
            if (isNegative)
            {
                destination[position++] = (byte)'-';
            }

            Span<byte> digits = destination.Slice(position);
            int digitsWritten = BigArithmetic.ToDecimalDigits(_significand, digits);
            Debug.Assert(digitsWritten == digitCount);

            bool writeExponent = false;
            switch (formatKind)
            {
                case BigFormatKind.Integer:
                    int trailingZeros = (int)exponent;
                    destination.Slice(position + digitCount, trailingZeros).Fill((byte)'0');
                    position += digitCount + trailingZeros;
                    break;

                case BigFormatKind.Decimal:
                    int integralDigits = digitCount + (int)exponent;
                    digits.Slice(integralDigits, digitCount - integralDigits)
                        .CopyTo(digits.Slice(integralDigits + 1));
                    digits[integralDigits] = (byte)'.';
                    position += digitCount + 1;
                    break;

                case BigFormatKind.LeadingDecimal:
                    int leadingZeros = (int)(-exponent - digitCount);
                    digits.Slice(0, digitCount).CopyTo(digits.Slice(leadingZeros + 2));
                    digits[0] = (byte)'0';
                    digits[1] = (byte)'.';
                    digits.Slice(2, leadingZeros).Fill((byte)'0');
                    position += digitCount + leadingZeros + 2;
                    break;

                case BigFormatKind.Scientific:
                    if (digitCount > 1)
                    {
                        digits.Slice(1, digitCount - 1).CopyTo(digits.Slice(2));
                        digits[1] = (byte)'.';
                        position += digitCount + 1;
                    }
                    else
                    {
                        position += digitCount;
                    }

                    writeExponent = true;
                    break;

                case BigFormatKind.UnnormalizedScientific:
                    position += digitCount;
                    writeExponent = true;
                    break;
            }

            if (writeExponent)
            {
                destination[position++] = (byte)'E';
                if (exponent > 0m)
                {
                    destination[position++] = (byte)'+';
                }

                bool success = Utf8Formatter.TryFormat(
                    exponent,
                    destination.Slice(position),
                    out int exponentBytesWritten);
                Debug.Assert(success);
                position += exponentBytesWritten;
            }

            Debug.Assert(position == requiredLength);
            bytesWritten = position;
            return true;
        }

        private int GetBigFormatInfo(
            out int digitCount,
            out decimal exponent,
            out bool isNegative,
            out BigFormatKind formatKind)
        {
            Debug.Assert(_significand is not null);

            digitCount = BigArithmetic.GetDecimalDigitCount(_significand);
            exponent = LargeExponent;
            isNegative = GetLargeIsNegative(_value);

            int valueLength;
            if (exponent >= 0m && exponent <= 30m)
            {
                formatKind = BigFormatKind.Integer;
                valueLength = checked(digitCount + (int)exponent);
            }
            else if (exponent < 0m && -exponent < digitCount)
            {
                formatKind = BigFormatKind.Decimal;
                valueLength = checked(digitCount + 1);
            }
            else if (exponent < 0m && -exponent - digitCount <= 30m)
            {
                formatKind = BigFormatKind.LeadingDecimal;
                valueLength = checked(digitCount + (int)(-exponent - digitCount) + 2);
            }
            else
            {
                try
                {
                    exponent += digitCount - 1;
                    formatKind = BigFormatKind.Scientific;
                }
                catch (OverflowException)
                {
                    formatKind = BigFormatKind.UnnormalizedScientific;
                }

                valueLength = checked(
                    digitCount +
                    (formatKind == BigFormatKind.Scientific && digitCount > 1 ? 1 : 0) +
                    1 +
                    (exponent > 0m ? 1 : 0) +
                    GetExponentTextLength(exponent));
            }

            return checked(valueLength + (isNegative ? 1 : 0));
        }

        private static int GetExponentTextLength(decimal exponent)
        {
            int length = exponent < 0m ? 1 : 0;
            decimal remaining = Math.Abs(exponent);
            do
            {
                length++;
                remaining = decimal.Truncate(remaining / 10m);
            }
            while (remaining != 0m);

            return length;
        }

        private static void ValidateFormat(ReadOnlySpan<char> format)
        {
            if (!format.IsEmpty &&
                (format.Length != 1 || format[0] is not ('G' or 'g' or 'R' or 'r')))
            {
                ThrowFormatException();
            }
        }

        private enum BigFormatKind
        {
            Integer,
            Decimal,
            LeadingDecimal,
            Scientific,
            UnnormalizedScientific,
        }
    }
}
