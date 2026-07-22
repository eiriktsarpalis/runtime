// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;

namespace System.Numerics
{
    public readonly partial struct BigNumber
    {
        /// <summary>
        /// Determines whether this instance and another <see cref="BigNumber"/> represent the same value.
        /// </summary>
        /// <param name="other">The number to compare with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if this instance and <paramref name="other"/> represent the same value;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals(BigNumber other)
        {
            if (_significand is null && other._significand is null)
            {
                return _value == other._value;
            }

            return CompareTo(other) == 0;
        }

        /// <summary>Determines whether this instance and a specified object represent the same value.</summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="obj"/> is a <see cref="BigNumber"/> that represents
        /// the same value as this instance; otherwise, <see langword="false"/>.
        /// </returns>
        public override bool Equals(object? obj) => obj is BigNumber other && Equals(other);

        /// <summary>Returns the hash code for this number.</summary>
        /// <returns>The hash code for this number.</returns>
        public override int GetHashCode()
        {
            if (IsZero)
            {
                return 0;
            }

            int capacity = GetSignificandDigitCount(this);
            char[]? rented = null;
            Span<char> digits = capacity <= 256
                ? stackalloc char[256]
                : (rented = ArrayPool<char>.Shared.Rent(capacity));

            try
            {
                int length = WriteNormalizedDigits(this, digits, out decimal exponent);
                int hash = IsNegative ? -1 : 1;
                hash = (hash * 397) ^ exponent.GetHashCode();
                for (int i = 0; i < length; i++)
                {
                    hash = (hash * 397) ^ digits[i];
                }

                return hash;
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
        }

        /// <summary>Compares this instance with a specified object.</summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>An integer that indicates the relative order of this instance and <paramref name="obj"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="obj"/> is not a <see cref="BigNumber"/>.</exception>
        public int CompareTo(object? obj)
        {
            if (obj is null)
            {
                return 1;
            }

            if (obj is not BigNumber other)
            {
                throw new ArgumentException(SR.Argument_MustBeBigNumber, nameof(obj));
            }

            return CompareTo(other);
        }

        /// <summary>Compares this instance with another <see cref="BigNumber"/>.</summary>
        /// <param name="other">The number to compare with this instance.</param>
        /// <returns>An integer that indicates the relative order of this instance and <paramref name="other"/>.</returns>
        public int CompareTo(BigNumber other)
        {
            if (_significand is null && other._significand is null)
            {
                return _value.CompareTo(other._value);
            }

            bool leftIsZero = IsZero;
            bool rightIsZero = other.IsZero;
            if (leftIsZero || rightIsZero)
            {
                if (leftIsZero && rightIsZero)
                {
                    return 0;
                }

                return leftIsZero ? (other.IsNegative ? 1 : -1) : (IsNegative ? -1 : 1);
            }

            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1;
            }

            int comparison = CompareMagnitude(this, other);

            return IsNegative ? -comparison : comparison;
        }

        /// <summary>Determines whether two specified numbers represent the same value.</summary>
        public static bool operator ==(BigNumber left, BigNumber right) => left.Equals(right);

        /// <summary>Determines whether two specified numbers represent different values.</summary>
        public static bool operator !=(BigNumber left, BigNumber right) => !left.Equals(right);

        /// <summary>Determines whether one specified number is less than another specified number.</summary>
        public static bool operator <(BigNumber left, BigNumber right) => left.CompareTo(right) < 0;

        /// <summary>Determines whether one specified number is less than or equal to another specified number.</summary>
        public static bool operator <=(BigNumber left, BigNumber right) => left.CompareTo(right) <= 0;

        /// <summary>Determines whether one specified number is greater than another specified number.</summary>
        public static bool operator >(BigNumber left, BigNumber right) => left.CompareTo(right) > 0;

        /// <summary>Determines whether one specified number is greater than or equal to another specified number.</summary>
        public static bool operator >=(BigNumber left, BigNumber right) => left.CompareTo(right) >= 0;

        private static bool HasIntegerValue(BigNumber value)
        {
            decimal exponent = value.LargeExponent;
            if (exponent >= 0m)
            {
                return true;
            }

            int capacity = GetSignificandDigitCount(value);
            if (-exponent >= capacity)
            {
                return false;
            }

            char[]? rented = null;
            Span<char> digits = capacity <= 256
                ? stackalloc char[256]
                : (rented = ArrayPool<char>.Shared.Rent(capacity));

            try
            {
                WriteNormalizedDigits(value, digits, out exponent);
                return exponent >= 0m;
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
        }

        private static int CompareMagnitude(BigNumber left, BigNumber right)
        {
            if (left._significand is not null &&
                right._significand is not null &&
                left.LargeExponent == right.LargeExponent)
            {
                return BigArithmetic.Compare(left._significand, right._significand);
            }

            int leftCapacity = GetSignificandDigitCount(left);
            int rightCapacity = GetSignificandDigitCount(right);
            char[]? rentedLeft = null;
            char[]? rentedRight = null;
            Span<char> leftDigits = leftCapacity <= 256
                ? stackalloc char[256]
                : (rentedLeft = ArrayPool<char>.Shared.Rent(leftCapacity));
            Span<char> rightDigits = rightCapacity <= 256
                ? stackalloc char[256]
                : (rentedRight = ArrayPool<char>.Shared.Rent(rightCapacity));

            try
            {
                int leftLength = WriteNormalizedDigits(left, leftDigits, out decimal leftExponent);
                int rightLength = WriteNormalizedDigits(right, rightDigits, out decimal rightExponent);
                int orderComparison;

                try
                {
                    decimal exponentDifference = leftExponent - rightExponent;
                    orderComparison = exponentDifference.CompareTo(rightLength - leftLength);
                }
                catch (OverflowException)
                {
                    orderComparison = leftExponent.CompareTo(rightExponent);
                }

                if (orderComparison != 0)
                {
                    return orderComparison;
                }

                int maximumLength = Math.Max(leftLength, rightLength);
                for (int i = 0; i < maximumLength; i++)
                {
                    char leftDigit = i < leftLength ? leftDigits[i] : '0';
                    char rightDigit = i < rightLength ? rightDigits[i] : '0';
                    if (leftDigit != rightDigit)
                    {
                        return leftDigit.CompareTo(rightDigit);
                    }
                }

                return 0;
            }
            finally
            {
                if (rentedLeft is not null)
                {
                    ArrayPool<char>.Shared.Return(rentedLeft);
                }

                if (rentedRight is not null)
                {
                    ArrayPool<char>.Shared.Return(rentedRight);
                }
            }
        }

        private static int GetSignificandDigitCount(BigNumber value)
        {
            if (value._significand is not null)
            {
                return BigArithmetic.GetDecimalDigitCount(value._significand);
            }

            GetDecimalBits(value._value, out int lo, out int mid, out int hi, out _);
            Span<uint> magnitude = stackalloc uint[3];
            magnitude[0] = (uint)lo;
            magnitude[1] = (uint)mid;
            magnitude[2] = (uint)hi;
            int length = hi != 0 ? 3 : mid != 0 ? 2 : 1;
            return BigArithmetic.GetDecimalDigitCountFromBinary(magnitude.Slice(0, length));
        }

        private static int WriteNormalizedDigits(
            BigNumber value,
            Span<char> destination,
            out decimal exponent)
        {
            int length;
            if (value._significand is not null)
            {
                exponent = value.LargeExponent;
                length = BigArithmetic.ToDecimalDigits(value._significand, destination);
            }
            else
            {
                GetDecimalBits(value._value, out int lo, out int mid, out int hi, out int flags);
                Span<uint> magnitude = stackalloc uint[3];
                magnitude[0] = (uint)lo;
                magnitude[1] = (uint)mid;
                magnitude[2] = (uint)hi;
                int magnitudeLength = hi != 0 ? 3 : mid != 0 ? 2 : 1;
                exponent = -((flags >> 16) & 0xFF);
                length = BigArithmetic.ToDecimalDigitsFromBinary(magnitude.Slice(0, magnitudeLength), destination);
            }

            while (length > 1 && destination[length - 1] == '0' && exponent < decimal.MaxValue)
            {
                length--;
                exponent++;
            }

            return length;
        }
    }
}
