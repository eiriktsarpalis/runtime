// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;

namespace System.Numerics
{
    public readonly partial struct BigNumber
    {
        /// <summary>Implicitly converts a <see cref="byte"/> to a <see cref="BigNumber"/>.</summary>
        public static implicit operator BigNumber(byte value) => new(value);

        /// <summary>Implicitly converts an <see cref="sbyte"/> to a <see cref="BigNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator BigNumber(sbyte value) => new(value);

        /// <summary>Implicitly converts a <see cref="short"/> to a <see cref="BigNumber"/>.</summary>
        public static implicit operator BigNumber(short value) => new(value);

        /// <summary>Implicitly converts a <see cref="ushort"/> to a <see cref="BigNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator BigNumber(ushort value) => new(value);

        /// <summary>Implicitly converts an <see cref="int"/> to a <see cref="BigNumber"/>.</summary>
        public static implicit operator BigNumber(int value) => new(value);

        /// <summary>Implicitly converts a <see cref="uint"/> to a <see cref="BigNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator BigNumber(uint value) => new(value);

        /// <summary>Implicitly converts a <see cref="long"/> to a <see cref="BigNumber"/>.</summary>
        public static implicit operator BigNumber(long value) => new(value);

        /// <summary>Implicitly converts a <see cref="ulong"/> to a <see cref="BigNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator BigNumber(ulong value) => new(value);

        /// <summary>Implicitly converts a <see cref="decimal"/> to a <see cref="BigNumber"/>.</summary>
        public static implicit operator BigNumber(decimal value) => new(value);

        /// <summary>Explicitly converts a <see cref="float"/> to a <see cref="BigNumber"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> is not finite.</exception>
        public static explicit operator BigNumber(float value)
        {
            if (!IsFinite(value))
            {
                ThrowNonFiniteNumber();
            }

            if (value == 0f && 1f / value < 0f)
            {
                return new BigNumber(new decimal(0, 0, 0, isNegative: true, scale: 0));
            }

            return Parse(value.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>Explicitly converts a <see cref="double"/> to a <see cref="BigNumber"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> is not finite.</exception>
        public static explicit operator BigNumber(double value)
        {
            if (!IsFinite(value))
            {
                ThrowNonFiniteNumber();
            }

            if (value == 0d && 1d / value < 0d)
            {
                return new BigNumber(new decimal(0, 0, 0, isNegative: true, scale: 0));
            }

            return Parse(value.ToString("R", CultureInfo.InvariantCulture));
        }

#if NET
        /// <summary>Explicitly converts a <see cref="Half"/> to a <see cref="BigNumber"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> is not finite.</exception>
        public static explicit operator BigNumber(Half value) => (BigNumber)(float)value;

        /// <summary>Implicitly converts an <see cref="Int128"/> to a <see cref="BigNumber"/>.</summary>
        public static implicit operator BigNumber(Int128 value)
        {
            if (value >= (Int128)decimal.MinValue && value <= (Int128)decimal.MaxValue)
            {
                return new BigNumber((decimal)value);
            }

            Span<char> buffer = stackalloc char[40];
            bool formatted = value.TryFormat(buffer, out int charsWritten, provider: CultureInfo.InvariantCulture);
            Debug.Assert(formatted);
            return Parse(buffer.Slice(0, charsWritten));
        }

        /// <summary>Implicitly converts a <see cref="UInt128"/> to a <see cref="BigNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator BigNumber(UInt128 value)
        {
            if (value <= (UInt128)decimal.MaxValue)
            {
                return new BigNumber((decimal)value);
            }

            Span<char> buffer = stackalloc char[39];
            bool formatted = value.TryFormat(buffer, out int charsWritten, provider: CultureInfo.InvariantCulture);
            Debug.Assert(formatted);
            return Parse(buffer.Slice(0, charsWritten));
        }
#endif

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="byte"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a <see cref="byte"/>.</exception>
        public static explicit operator byte(BigNumber value)
        {
            if (!value.TryGetByte(out byte result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to an <see cref="sbyte"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as an <see cref="sbyte"/>.</exception>
        [CLSCompliant(false)]
        public static explicit operator sbyte(BigNumber value)
        {
            if (!value.TryGetSByte(out sbyte result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="short"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a <see cref="short"/>.</exception>
        public static explicit operator short(BigNumber value)
        {
            if (!value.TryGetInt16(out short result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="ushort"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a <see cref="ushort"/>.</exception>
        [CLSCompliant(false)]
        public static explicit operator ushort(BigNumber value)
        {
            if (!value.TryGetUInt16(out ushort result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to an <see cref="int"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as an <see cref="int"/>.</exception>
        public static explicit operator int(BigNumber value)
        {
            if (!value.TryGetInt32(out int result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="uint"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a <see cref="uint"/>.</exception>
        [CLSCompliant(false)]
        public static explicit operator uint(BigNumber value)
        {
            if (!value.TryGetUInt32(out uint result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="long"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a <see cref="long"/>.</exception>
        public static explicit operator long(BigNumber value)
        {
            if (!value.TryGetInt64(out long result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="ulong"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a <see cref="ulong"/>.</exception>
        [CLSCompliant(false)]
        public static explicit operator ulong(BigNumber value)
        {
            if (!value.TryGetUInt64(out ulong result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="float"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a finite <see cref="float"/>.</exception>
        public static explicit operator float(BigNumber value)
        {
            if (!value.TryGetSingle(out float result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="double"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a finite <see cref="double"/>.</exception>
        public static explicit operator double(BigNumber value)
        {
            if (!value.TryGetDouble(out double result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="decimal"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented without loss as a <see cref="decimal"/>.</exception>
        public static explicit operator decimal(BigNumber value)
        {
            if (!value.TryGetDecimal(out decimal result))
            {
                throw new OverflowException();
            }

            return result;
        }

#if NET
        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="Half"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a finite <see cref="Half"/>.</exception>
        public static explicit operator Half(BigNumber value)
        {
            if (!value.TryGetHalf(out Half result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to an <see cref="Int128"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as an <see cref="Int128"/>.</exception>
        public static explicit operator Int128(BigNumber value)
        {
            if (!value.TryGetInt128(out Int128 result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="BigNumber"/> to a <see cref="UInt128"/>.</summary>
        /// <exception cref="OverflowException"><paramref name="value"/> cannot be represented as a <see cref="UInt128"/>.</exception>
        [CLSCompliant(false)]
        public static explicit operator UInt128(BigNumber value)
        {
            if (!value.TryGetUInt128(out UInt128 result))
            {
                throw new OverflowException();
            }

            return result;
        }
#endif

        /// <summary>Attempts to represent this number as a <see cref="byte"/> without loss.</summary>
        public bool TryGetByte(out byte value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= byte.MinValue &&
                decimalValue <= byte.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (byte)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as an <see cref="sbyte"/> without loss.</summary>
        [CLSCompliant(false)]
        public bool TryGetSByte(out sbyte value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= sbyte.MinValue &&
                decimalValue <= sbyte.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (sbyte)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as a <see cref="short"/> without loss.</summary>
        public bool TryGetInt16(out short value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= short.MinValue &&
                decimalValue <= short.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (short)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as a <see cref="ushort"/> without loss.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt16(out ushort value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= ushort.MinValue &&
                decimalValue <= ushort.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (ushort)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as an <see cref="int"/> without loss.</summary>
        public bool TryGetInt32(out int value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= int.MinValue &&
                decimalValue <= int.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (int)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as a <see cref="uint"/> without loss.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt32(out uint value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= uint.MinValue &&
                decimalValue <= uint.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (uint)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as a <see cref="long"/> without loss.</summary>
        public bool TryGetInt64(out long value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= long.MinValue &&
                decimalValue <= long.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (long)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as a <see cref="ulong"/> without loss.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt64(out ulong value)
        {
            if (TryGetDecimal(out decimal decimalValue) &&
                decimalValue >= ulong.MinValue &&
                decimalValue <= ulong.MaxValue &&
                decimalValue == decimal.Truncate(decimalValue))
            {
                value = (ulong)decimalValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as a finite <see cref="float"/>.</summary>
        public bool TryGetSingle(out float value)
        {
            if (_significand is null)
            {
                value = (float)_value;
                return IsFinite(value);
            }

            return TryGetFloatingPoint(out value);
        }

        /// <summary>Attempts to represent this number as a finite <see cref="double"/>.</summary>
        public bool TryGetDouble(out double value)
        {
            if (_significand is null)
            {
                value = (double)_value;
                return IsFinite(value);
            }

            return TryGetFloatingPoint(out value);
        }

        /// <summary>Attempts to represent this number as a <see cref="decimal"/> without loss.</summary>
        public bool TryGetDecimal(out decimal value)
        {
            if (_significand is null)
            {
                value = _value;
                return true;
            }

            if (TryGetFormatted(out value) && new BigNumber(value).Equals(this))
            {
                return true;
            }

            value = default;
            return false;
        }

#if NET
        /// <summary>Attempts to represent this number as a finite <see cref="Half"/>.</summary>
        public bool TryGetHalf(out Half value)
        {
            if (TryGetSingle(out float singleValue))
            {
                value = (Half)singleValue;
                return Half.IsFinite(value);
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent this number as an <see cref="Int128"/> without loss.</summary>
        public bool TryGetInt128(out Int128 value) => TryGetFormatted(out value);

        /// <summary>Attempts to represent this number as a <see cref="UInt128"/> without loss.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt128(out UInt128 value) => TryGetFormatted(out value);
#endif

        private bool TryGetFloatingPoint(out float value)
        {
            if (TryGetFormatted(out value) && IsFinite(value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private bool TryGetFloatingPoint(out double value)
        {
            if (TryGetFormatted(out value) && IsFinite(value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private bool TryGetFormatted<T>(out T value) where T : struct
        {
            int maximumLength = GetMaxUtf8Length();
            byte[]? rented = null;
            Span<byte> buffer = maximumLength <= 256
                ? stackalloc byte[256]
                : (rented = ArrayPool<byte>.Shared.Rent(maximumLength));

            try
            {
                int written = WriteUtf8(buffer);
                ReadOnlySpan<byte> utf8Text = buffer.Slice(0, written);

                if (typeof(T) == typeof(float))
                {
                    bool success = Utf8Parser.TryParse(utf8Text, out float result, out int consumed) &&
                        consumed == written;
                    value = (T)(object)result;
                    return success;
                }

                if (typeof(T) == typeof(double))
                {
                    bool success = Utf8Parser.TryParse(utf8Text, out double result, out int consumed) &&
                        consumed == written;
                    value = (T)(object)result;
                    return success;
                }

                if (typeof(T) == typeof(decimal))
                {
                    bool success = Utf8Parser.TryParse(utf8Text, out decimal result, out int consumed) &&
                        consumed == written;
                    value = (T)(object)result;
                    return success;
                }

#if NET
                if (typeof(T) == typeof(Int128))
                {
                    bool success = Int128.TryParse(
                        utf8Text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out Int128 result);
                    value = (T)(object)result;
                    return success;
                }

                if (typeof(T) == typeof(UInt128))
                {
                    bool success = UInt128.TryParse(
                        utf8Text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out UInt128 result);
                    value = (T)(object)result;
                    return success;
                }
#endif

                value = default;
                return false;
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        private static bool IsFinite(float value)
        {
#if NET
            return float.IsFinite(value);
#else
            return !float.IsNaN(value) && !float.IsInfinity(value);
#endif
        }

        private static bool IsFinite(double value)
        {
#if NET
            return double.IsFinite(value);
#else
            return !double.IsNaN(value) && !double.IsInfinity(value);
#endif
        }
    }
}
