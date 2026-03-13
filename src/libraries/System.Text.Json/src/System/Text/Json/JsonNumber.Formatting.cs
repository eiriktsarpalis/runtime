// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;

namespace System.Text.Json
{
    public readonly partial struct JsonNumber
#if NET
        : ISpanFormattable, IUtf8SpanFormattable
#endif
    {
        /// <summary>
        /// Returns the JSON representation of this number.
        /// </summary>
        /// <returns>A string containing the JSON number.</returns>
        public override string ToString()
        {
            if (_bigData is null)
            {
                return _smallValue.ToString(CultureInfo.InvariantCulture);
            }

            return FormatBig(_bigData);
        }

        /// <summary>
        /// Formats the number as a string using the specified format and provider.
        /// </summary>
        /// <remarks>
        /// The format parameter is ignored; JSON numbers always use the standard JSON representation.
        /// </remarks>
        public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

#if NET
        /// <summary>
        /// Tries to format the number into a character span.
        /// </summary>
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        {
            if (_bigData is null)
            {
                return _smallValue.TryFormat(destination, out charsWritten, format: default, CultureInfo.InvariantCulture);
            }

            return TryFormatBig(_bigData, destination, out charsWritten);
        }

        /// <summary>
        /// Tries to format the number into a UTF-8 byte span.
        /// </summary>
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        {
            if (_bigData is null)
            {
                return Utf8Formatter.TryFormat(_smallValue, utf8Destination, out bytesWritten);
            }

            return TryFormatBigUtf8(_bigData, utf8Destination, out bytesWritten);
        }
#endif

        /// <summary>
        /// Writes the UTF-8 representation of this number to the specified buffer.
        /// </summary>
        /// <returns>The number of bytes written.</returns>
        internal int WriteUtf8(Span<byte> destination)
        {
            if (_bigData is null)
            {
                if (!Utf8Formatter.TryFormat(_smallValue, destination, out int written))
                {
                    Debug.Fail("Buffer too small for decimal formatting.");
                    return 0;
                }

                return written;
            }

            if (!TryFormatBigUtf8(_bigData, destination, out int bytesWritten))
            {
                Debug.Fail("Buffer too small for big number formatting.");
                return 0;
            }

            return bytesWritten;
        }

        /// <summary>
        /// Returns the maximum number of UTF-8 bytes needed to represent this number.
        /// </summary>
        internal int GetMaxUtf8Length()
        {
            if (_bigData is null)
            {
                // decimal max is 29 digits + sign + decimal point = 31.
                return 31;
            }

            // For big numbers: sign + digits + 'e' + sign + exponent (up to 10 digits).
            int digitCount = BigArithmetic.GetDecimalDigitCount(_bigData.Significand);
            return digitCount + 15; // sign + '.' + 'e' + sign + exponent
        }

        private static string FormatBig(BigDecimalData data)
        {
            if (BigArithmetic.IsZero(data.Significand))
            {
                return "0";
            }

            // Get decimal digits of significand.
            int digitCount = BigArithmetic.GetDecimalDigitCount(data.Significand);
            char[] buffer = new char[digitCount + 20]; // extra for sign, '.', 'e', exponent
            int digitsWritten = BigArithmetic.ToDecimalDigits(data.Significand, buffer.AsSpan());

            return FormatBigCore(data.IsNegative, buffer.AsSpan(0, digitsWritten), data.Exponent);
        }

        private static string FormatBigCore(bool isNegative, ReadOnlySpan<char> digits, int exponent)
        {
            if (digits.IsEmpty || (digits.Length == 1 && digits[0] == '0'))
            {
                return "0";
            }

            // The full number is: digits × 10^exponent.
            // We want to produce a clean representation.
            // If the number is an integer with a small-enough digit count, write without exponent.
            // Otherwise, use scientific notation: d.dddddEn

            // Use long to avoid overflow when combining digits.Length and exponent.
            long totalMagnitude = (long)digits.Length + exponent;

            // For reasonable-size integers (exponent >= 0 and total digits <= ~30), expand.
            if (exponent >= 0 && totalMagnitude <= 30)
            {
                // Write as integer: digits followed by zeros.
                var sb = new ValueStringBuilder(stackalloc char[64]);
                if (isNegative)
                {
                    sb.Append('-');
                }

                sb.Append(digits);
                for (int i = 0; i < exponent; i++)
                {
                    sb.Append('0');
                }

                return sb.ToString();
            }

            // For small decimals where exponent is negative and brings the number close to normal form.
            if (exponent < 0 && -exponent < digits.Length && totalMagnitude > 0)
            {
                // We can write as: integral.fractional
                var sb = new ValueStringBuilder(stackalloc char[64]);
                if (isNegative)
                {
                    sb.Append('-');
                }

                int integralLen = digits.Length + exponent;
                sb.Append(digits.Slice(0, integralLen));
                sb.Append('.');
                sb.Append(digits.Slice(integralLen));

                return sb.ToString();
            }

            // Otherwise, use scientific notation: d.dddddEn
            {
                var sb = new ValueStringBuilder(stackalloc char[64]);
                if (isNegative)
                {
                    sb.Append('-');
                }

                sb.Append(digits[0]);
                if (digits.Length > 1)
                {
                    sb.Append('.');
                    sb.Append(digits.Slice(1));
                }

                // Exponent in scientific notation: the decimal point is after the first digit,
                // so the exponent is: (digits.Length - 1) + exponent.
                // Use long to prevent overflow.
                long sciExponent = (long)(digits.Length - 1) + exponent;
                if (sciExponent != 0)
                {
                    sb.Append('E');
                    if (sciExponent > 0)
                    {
                        sb.Append('+');
                    }

#if NET
                    Span<char> expBuf = stackalloc char[21]; // enough for long
                    sciExponent.TryFormat(expBuf, out int expLen, default, CultureInfo.InvariantCulture);
                    sb.Append(expBuf.Slice(0, expLen));
#else
                    sb.Append(sciExponent.ToString(CultureInfo.InvariantCulture));
#endif
                }

                return sb.ToString();
            }
        }

        private static bool TryFormatBig(BigDecimalData data, Span<char> destination, out int charsWritten)
        {
            string formatted = FormatBig(data);
            if (formatted.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            formatted.AsSpan().CopyTo(destination);
            charsWritten = formatted.Length;
            return true;
        }

        private static bool TryFormatBigUtf8(BigDecimalData data, Span<byte> destination, out int bytesWritten)
        {
            string formatted = FormatBig(data);
            if (formatted.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            for (int i = 0; i < formatted.Length; i++)
            {
                destination[i] = (byte)formatted[i];
            }

            bytesWritten = formatted.Length;
            return true;
        }
    }
}
