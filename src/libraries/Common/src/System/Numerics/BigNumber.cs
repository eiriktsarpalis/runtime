// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Numerics
{
    /// <summary>Represents an arbitrary-precision base-10 number.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="BigNumber"/> is an exchange type for numeric values that cannot be represented
    /// without loss by the primitive numeric types. It supports parsing, formatting, conversion,
    /// equality, and ordering, but it does not provide arithmetic operations.
    /// </para>
    /// <para>
    /// Values that fit in <see cref="decimal"/> are stored without allocating. Other values are
    /// stored as an arbitrary-precision significand and a base-10 exponent.
    /// </para>
    /// </remarks>
#if NET
    public readonly partial struct BigNumber :
        IComparable,
        IComparable<BigNumber>,
        IEquatable<BigNumber>,
        IFormattable,
        ISpanFormattable,
        IParsable<BigNumber>,
        ISpanParsable<BigNumber>,
        IUtf8SpanFormattable,
        IUtf8SpanParsable<BigNumber>,
        IComparisonOperators<BigNumber, BigNumber, bool>,
        IEqualityOperators<BigNumber, BigNumber, bool>
#else
    public readonly partial struct BigNumber :
        IComparable,
        IComparable<BigNumber>,
        IEquatable<BigNumber>,
        IFormattable
#endif
    {
        // When _significand is non-null, _value encodes the exponent and sign metadata.
        private readonly decimal _value;
        private readonly uint[]? _significand;

        /// <summary>Gets a <see cref="BigNumber"/> value representing zero.</summary>
        public static BigNumber Zero => default;

        /// <summary>Gets a value indicating whether this number is zero.</summary>
        public bool IsZero => _significand is null && _value == 0m;

        /// <summary>Gets a value indicating whether this number has a negative sign.</summary>
        /// <remarks>This property returns <see langword="true"/> for negative zero.</remarks>
        public bool IsNegative => _significand is null ? IsNegativeDecimal(_value) : GetLargeIsNegative(_value);

        /// <summary>Gets a value indicating whether this number has no fractional component.</summary>
        public bool IsInteger
        {
            get
            {
                if (_significand is null)
                {
                    return _value == decimal.Truncate(_value);
                }

                return HasIntegerValue(this);
            }
        }

        internal BigNumber(decimal value)
        {
            _value = value;
            _significand = null;
        }

        internal BigNumber(uint[] significand, decimal exponent, bool isNegative)
        {
            Debug.Assert(significand is not null);
            Debug.Assert(significand.Length > 0);
            Debug.Assert(!BigArithmetic.IsZero(significand));
            Debug.Assert(exponent == decimal.Truncate(exponent));

            _value = EncodeLargeMetadata(exponent, isNegative);
            _significand = significand;
        }

        internal bool IsLarge => _significand is not null;

        internal uint[] Significand
        {
            get
            {
                Debug.Assert(_significand is not null);
                return _significand;
            }
        }

        internal decimal LargeExponent
        {
            get
            {
                Debug.Assert(_significand is not null);
                return GetLargeExponent(_value);
            }
        }

        private static decimal EncodeLargeMetadata(decimal exponent, bool isNegative)
        {
            bool isNegativeExponent = exponent < 0m;
            decimal magnitude = Math.Abs(exponent);
            GetDecimalBits(magnitude, out int lo, out int mid, out int hi, out _);
            return new decimal(lo, mid, hi, isNegative, isNegativeExponent ? (byte)1 : (byte)0);
        }

        private static decimal GetLargeExponent(decimal metadata)
        {
            GetDecimalBits(metadata, out int lo, out int mid, out int hi, out int flags);
            decimal magnitude = new decimal(lo, mid, hi, isNegative: false, scale: 0);
            return ((flags >> 16) & 0xFF) == 0 ? magnitude : -magnitude;
        }

        private static bool GetLargeIsNegative(decimal metadata)
        {
            GetDecimalBits(metadata, out _, out _, out _, out int flags);
            return flags < 0;
        }

        private static bool IsNegativeDecimal(decimal value)
        {
            GetDecimalBits(value, out _, out _, out _, out int flags);
            return flags < 0;
        }

        private static decimal WithNegativeSign(decimal value)
        {
            GetDecimalBits(value, out int lo, out int mid, out int hi, out int flags);
            return new decimal(lo, mid, hi, isNegative: true, scale: (byte)((flags >> 16) & 0xFF));
        }

        private static void GetDecimalBits(decimal value, out int lo, out int mid, out int hi, out int flags)
        {
#if NET
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(value, bits);
            lo = bits[0];
            mid = bits[1];
            hi = bits[2];
            flags = bits[3];
#else
            int[] bits = decimal.GetBits(value);
            lo = bits[0];
            mid = bits[1];
            hi = bits[2];
            flags = bits[3];
#endif
        }

        [DoesNotReturn]
        private static void ThrowFormatException() => throw new FormatException(SR.Format_BigNumber);

        [DoesNotReturn]
        private static void ThrowExponentOverflow() => throw new OverflowException(SR.Overflow_BigNumberExponent);

        [DoesNotReturn]
        private static void ThrowNonFiniteNumber() => throw new OverflowException(SR.Overflow_BigNumberNonFinite);
    }
}
