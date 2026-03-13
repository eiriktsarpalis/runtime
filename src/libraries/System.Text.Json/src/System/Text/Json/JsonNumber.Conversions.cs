// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;

namespace System.Text.Json
{
    public readonly partial struct JsonNumber
    {
        // Implicit conversions from lossless integer types.

        /// <summary>Implicitly converts a <see cref="byte"/> to a <see cref="JsonNumber"/>.</summary>
        public static implicit operator JsonNumber(byte value) => new((decimal)value);

        /// <summary>Implicitly converts an <see cref="sbyte"/> to a <see cref="JsonNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator JsonNumber(sbyte value) => new((decimal)value);

        /// <summary>Implicitly converts a <see cref="short"/> to a <see cref="JsonNumber"/>.</summary>
        public static implicit operator JsonNumber(short value) => new((decimal)value);

        /// <summary>Implicitly converts a <see cref="ushort"/> to a <see cref="JsonNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator JsonNumber(ushort value) => new((decimal)value);

        /// <summary>Implicitly converts an <see cref="int"/> to a <see cref="JsonNumber"/>.</summary>
        public static implicit operator JsonNumber(int value) => new((decimal)value);

        /// <summary>Implicitly converts a <see cref="uint"/> to a <see cref="JsonNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator JsonNumber(uint value) => new((decimal)value);

        /// <summary>Implicitly converts a <see cref="long"/> to a <see cref="JsonNumber"/>.</summary>
        public static implicit operator JsonNumber(long value) => new((decimal)value);

        /// <summary>Implicitly converts a <see cref="ulong"/> to a <see cref="JsonNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator JsonNumber(ulong value) => new((decimal)value);

        /// <summary>Implicitly converts a <see cref="decimal"/> to a <see cref="JsonNumber"/>.</summary>
        public static implicit operator JsonNumber(decimal value) => new(value);

        // Explicit conversions from floating-point types (potentially lossy source).

        /// <summary>Explicitly converts a <see cref="float"/> to a <see cref="JsonNumber"/>.</summary>
        public static explicit operator JsonNumber(float value)
        {
            if (!JsonHelpers.IsFinite(value))
            {
                ThrowHelper.ThrowArgumentException_ValueNotSupported(nameof(value));
            }

            try
            {
                return new((decimal)value);
            }
            catch (OverflowException)
            {
                string s = value.ToString("R", CultureInfo.InvariantCulture);
                return Parse(s);
            }
        }

        /// <summary>Explicitly converts a <see cref="double"/> to a <see cref="JsonNumber"/>.</summary>
        public static explicit operator JsonNumber(double value)
        {
            if (!JsonHelpers.IsFinite(value))
            {
                ThrowHelper.ThrowArgumentException_ValueNotSupported(nameof(value));
            }

            // Use decimal if it fits, otherwise parse from the string representation.
            try
            {
                return new((decimal)value);
            }
            catch (OverflowException)
            {
                // The double value is outside decimal range — parse from string.
                string s = value.ToString("R", CultureInfo.InvariantCulture);
                return Parse(s);
            }
        }

#if NET
        /// <summary>Explicitly converts a <see cref="Half"/> to a <see cref="JsonNumber"/>.</summary>
        public static explicit operator JsonNumber(Half value)
        {
            return (JsonNumber)(double)value;
        }

        /// <summary>Implicitly converts an <see cref="Int128"/> to a <see cref="JsonNumber"/>.</summary>
        public static implicit operator JsonNumber(Int128 value)
        {
            // Try decimal first (covers up to ~28 digits).
            try
            {
                return new((decimal)value);
            }
            catch (OverflowException)
            {
                string s = value.ToString(CultureInfo.InvariantCulture);
                return Parse(s);
            }
        }

        /// <summary>Implicitly converts a <see cref="UInt128"/> to a <see cref="JsonNumber"/>.</summary>
        [CLSCompliant(false)]
        public static implicit operator JsonNumber(UInt128 value)
        {
            try
            {
                return new((decimal)value);
            }
            catch (OverflowException)
            {
                string s = value.ToString(CultureInfo.InvariantCulture);
                return Parse(s);
            }
        }
#endif

        // Explicit conversions to numeric types (may overflow or lose precision).

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="byte"/>.</summary>
        public static explicit operator byte(JsonNumber value)
        {
            if (!value.TryGetByte(out byte result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to an <see cref="sbyte"/>.</summary>
        [CLSCompliant(false)]
        public static explicit operator sbyte(JsonNumber value)
        {
            if (!value.TryGetSByte(out sbyte result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="short"/>.</summary>
        public static explicit operator short(JsonNumber value)
        {
            if (!value.TryGetInt16(out short result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="ushort"/>.</summary>
        [CLSCompliant(false)]
        public static explicit operator ushort(JsonNumber value)
        {
            if (!value.TryGetUInt16(out ushort result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to an <see cref="int"/>.</summary>
        public static explicit operator int(JsonNumber value)
        {
            if (!value.TryGetInt32(out int result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="uint"/>.</summary>
        [CLSCompliant(false)]
        public static explicit operator uint(JsonNumber value)
        {
            if (!value.TryGetUInt32(out uint result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="long"/>.</summary>
        public static explicit operator long(JsonNumber value)
        {
            if (!value.TryGetInt64(out long result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="ulong"/>.</summary>
        [CLSCompliant(false)]
        public static explicit operator ulong(JsonNumber value)
        {
            if (!value.TryGetUInt64(out ulong result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="float"/>.</summary>
        public static explicit operator float(JsonNumber value)
        {
            if (!value.TryGetSingle(out float result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="double"/>.</summary>
        public static explicit operator double(JsonNumber value)
        {
            if (!value.TryGetDouble(out double result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="decimal"/>.</summary>
        public static explicit operator decimal(JsonNumber value)
        {
            if (!value.TryGetDecimal(out decimal result))
            {
                throw new OverflowException();
            }

            return result;
        }

#if NET
        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="Half"/>.</summary>
        public static explicit operator Half(JsonNumber value)
        {
            if (!value.TryGetHalf(out Half result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to an <see cref="Int128"/>.</summary>
        public static explicit operator Int128(JsonNumber value)
        {
            if (!value.TryGetInt128(out Int128 result))
            {
                throw new OverflowException();
            }

            return result;
        }

        /// <summary>Explicitly converts a <see cref="JsonNumber"/> to a <see cref="UInt128"/>.</summary>
        [CLSCompliant(false)]
        public static explicit operator UInt128(JsonNumber value)
        {
            if (!value.TryGetUInt128(out UInt128 result))
            {
                throw new OverflowException();
            }

            return result;
        }
#endif

        // TryGet methods for safe narrowing conversions.

        /// <summary>Attempts to represent the value as a <see cref="byte"/>.</summary>
        public bool TryGetByte(out byte value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (byte)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as an <see cref="sbyte"/>.</summary>
        [CLSCompliant(false)]
        public bool TryGetSByte(out sbyte value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (sbyte)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="short"/>.</summary>
        public bool TryGetInt16(out short value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (short)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="ushort"/>.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt16(out ushort value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (ushort)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as an <see cref="int"/>.</summary>
        public bool TryGetInt32(out int value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (int)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="uint"/>.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt32(out uint value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (uint)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="long"/>.</summary>
        public bool TryGetInt64(out long value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (long)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="ulong"/>.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt64(out ulong value)
        {
            if (_bigData is null)
            {
                try
                {
                    value = (ulong)_smallValue;
                    return _smallValue == value;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="float"/>.</summary>
        public bool TryGetSingle(out float value)
        {
            if (_bigData is null)
            {
                value = (float)_smallValue;
                return true;
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="double"/>.</summary>
        public bool TryGetDouble(out double value)
        {
            if (_bigData is null)
            {
                value = (double)_smallValue;
                return true;
            }

            return TryGetViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="decimal"/>.</summary>
        public bool TryGetDecimal(out decimal value)
        {
            if (_bigData is null)
            {
                value = _smallValue;
                return true;
            }

            return TryGetViaFormatting(out value);
        }

#if NET
        /// <summary>Attempts to represent the value as a <see cref="Half"/>.</summary>
        public bool TryGetHalf(out Half value)
        {
            if (TryGetDouble(out double d))
            {
                value = (Half)d;
                return Half.IsFinite(value);
            }

            value = default;
            return false;
        }

        /// <summary>Attempts to represent the value as an <see cref="Int128"/>.</summary>
        public bool TryGetInt128(out Int128 value)
        {
            if (_bigData is null)
            {
                if (_smallValue != decimal.Truncate(_smallValue))
                {
                    value = default;
                    return false;
                }

                try
                {
                    value = (Int128)_smallValue;
                    return true;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetInt128ViaFormatting(out value);
        }

        /// <summary>Attempts to represent the value as a <see cref="UInt128"/>.</summary>
        [CLSCompliant(false)]
        public bool TryGetUInt128(out UInt128 value)
        {
            if (_bigData is null)
            {
                if (_smallValue != decimal.Truncate(_smallValue) || _smallValue < 0m)
                {
                    value = default;
                    return false;
                }

                try
                {
                    value = (UInt128)_smallValue;
                    return true;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            return TryGetUInt128ViaFormatting(out value);
        }

        private bool TryGetInt128ViaFormatting(out Int128 value)
        {
            if (!IsInteger)
            {
                value = default;
                return false;
            }

            // Big numbers format in scientific notation (e.g., "1.7E+38"),
            // so use NumberStyles.Float to accept the exponent.
            string s = ToString();
            if (Int128.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private bool TryGetUInt128ViaFormatting(out UInt128 value)
        {
            if (!IsInteger || IsNegative)
            {
                value = default;
                return false;
            }

            string s = ToString();
            if (UInt128.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = default;
            return false;
        }
#endif

        /// <summary>
        /// Generic fallback: format to UTF-8, then parse as the target type.
        /// Used for big numbers where direct conversion isn't available.
        /// </summary>
        private bool TryGetViaFormatting<T>(out T value) where T : struct
        {
            int maxLen = GetMaxUtf8Length();
            byte[]? rented = null;
            Span<byte> buffer = maxLen <= 256
                ? stackalloc byte[256]
                : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxLen));

            try
            {
                int written = WriteUtf8(buffer);
                ReadOnlySpan<byte> utf8 = buffer.Slice(0, written);

                if (typeof(T) == typeof(byte))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out byte v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(sbyte))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out sbyte v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(short))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out short v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(ushort))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out ushort v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(int))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out int v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(uint))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out uint v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(long))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out long v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(ulong))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out ulong v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(float))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out float v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(double))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out double v, out int consumed) && consumed == written;
                    value = (T)(object)v;
                    return ok;
                }

                if (typeof(T) == typeof(decimal))
                {
                    bool ok = Utf8Parser.TryParse(utf8, out decimal v, out int consumed) && consumed == written;
                    if (ok)
                    {
                        // Verify faithfulness: Utf8Parser can silently underflow or lose precision.
                        ok = DecimalFastPathIsFaithful(utf8, v);
                    }

                    value = (T)(object)v;
                    return ok;
                }

                value = default;
                return false;
            }
            finally
            {
                if (rented is not null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }
    }
}
