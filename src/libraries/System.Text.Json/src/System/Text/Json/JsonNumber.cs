// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Json
{
    /// <summary>
    /// Represents an arbitrary-precision JSON number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="JsonNumber"/> can hold any valid JSON number without loss of precision.
    /// Small numbers that fit in <see cref="decimal"/> are stored inline with no heap allocation.
    /// Larger numbers use a compact internal representation consisting of an arbitrary-precision
    /// significand and a base-10 exponent.
    /// </para>
    /// <para>
    /// Two <see cref="JsonNumber"/> values are considered equal if they represent the same
    /// mathematical value, regardless of their textual representation. For example,
    /// <c>1</c>, <c>1.0</c>, and <c>10e-1</c> are all equal.
    /// </para>
    /// </remarks>
    public readonly partial struct JsonNumber : IEquatable<JsonNumber>, IComparable<JsonNumber>,
#if NET
        ISpanFormattable, ISpanParsable<JsonNumber>, IUtf8SpanFormattable, IUtf8SpanParsable<JsonNumber>
#else
        IFormattable
#endif
    {
        // Small representation — used when _bigData is null.
        // Covers numbers with up to 28-29 significant digits and exponent in [-28, 0].
        private readonly decimal _smallValue;

        // Big representation — null signals small mode.
        // When non-null, encapsulates significand (uint[]), exponent (int), and sign.
        private readonly BigDecimalData? _bigData;

        /// <summary>
        /// Gets a <see cref="JsonNumber"/> value representing zero.
        /// </summary>
        public static JsonNumber Zero => default;

        /// <summary>
        /// Gets a value indicating whether this number is zero.
        /// </summary>
        public bool IsZero
        {
            get
            {
                if (_bigData is not null)
                {
                    return BigArithmetic.IsZero(_bigData.Significand);
                }

                return _smallValue == 0m;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this number is negative.
        /// </summary>
        public bool IsNegative
        {
            get
            {
                if (_bigData is not null)
                {
                    return _bigData.IsNegative && !BigArithmetic.IsZero(_bigData.Significand);
                }

                return _smallValue < 0m;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this number is an integer (has no fractional component).
        /// </summary>
        public bool IsInteger
        {
            get
            {
                if (_bigData is not null)
                {
                    // An integer has exponent >= 0 in normalized form.
                    return _bigData.Exponent >= 0;
                }

                return _smallValue == decimal.Truncate(_smallValue);
            }
        }

        internal JsonNumber(decimal value)
        {
            _smallValue = value;
            _bigData = null;
        }

        internal JsonNumber(BigDecimalData bigData)
        {
            _smallValue = default;
            _bigData = bigData;
        }

        /// <summary>
        /// Internal container for big-number state.
        /// Stores the value as: sign × significand × 10^exponent.
        /// </summary>
        internal sealed class BigDecimalData
        {
            /// <summary>
            /// The magnitude of the significand in base 2^32, little-endian.
            /// Must not have leading zero elements (i.e., the last element is non-zero, or the array is empty for zero).
            /// </summary>
            internal readonly uint[] Significand;

            /// <summary>
            /// The power-of-10 exponent. The full value is significand × 10^Exponent.
            /// </summary>
            internal readonly int Exponent;

            /// <summary>
            /// Whether the value is negative.
            /// </summary>
            internal readonly bool IsNegative;

            internal BigDecimalData(uint[] significand, int exponent, bool isNegative)
            {
                Debug.Assert(significand is not null);
                Significand = significand;
                Exponent = exponent;
                IsNegative = isNegative;
            }
        }
    }
}
