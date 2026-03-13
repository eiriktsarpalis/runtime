// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.Json
{
    public readonly partial struct JsonNumber
    {
        /// <summary>
        /// Determines whether this instance and another <see cref="JsonNumber"/> represent the same value.
        /// </summary>
        /// <remarks>
        /// Equality is semantic: two numbers are equal if and only if they represent the
        /// same mathematical value, regardless of their textual representation.
        /// For example, <c>1</c>, <c>1.0</c>, and <c>10e-1</c> are all equal.
        /// </remarks>
        public bool Equals(JsonNumber other)
        {
            // Fast path: both small.
            if (_bigData is null && other._bigData is null)
            {
                return _smallValue == other._smallValue;
            }

            // Normalize both to (sign, significandDigits, exponent) form and compare.
            Normalize(this, out bool leftNeg, out uint[] leftSig, out int leftExp);
            Normalize(other, out bool rightNeg, out uint[] rightSig, out int rightExp);

            bool leftIsZero = BigArithmetic.IsZero(leftSig);
            bool rightIsZero = BigArithmetic.IsZero(rightSig);

            if (leftIsZero && rightIsZero)
            {
                return true;
            }

            if (leftIsZero != rightIsZero)
            {
                return false;
            }

            if (leftNeg != rightNeg)
            {
                return false;
            }

            if (leftExp != rightExp)
            {
                return false;
            }

            return BigArithmetic.Compare(leftSig, rightSig) == 0;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is JsonNumber other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            Normalize(this, out bool isNeg, out uint[] sig, out int exp);

            if (BigArithmetic.IsZero(sig))
            {
                return 0;
            }

            int hash = isNeg ? -1 : 1;
            hash = (hash * 397) ^ exp;
            for (int i = 0; i < sig.Length; i++)
            {
                hash = (hash * 397) ^ (int)sig[i];
            }

            return hash;
        }

        /// <summary>
        /// Compares this instance with another <see cref="JsonNumber"/> and returns an indication of their relative values.
        /// </summary>
        public int CompareTo(JsonNumber other)
        {
            // Fast path: both small.
            if (_bigData is null && other._bigData is null)
            {
                return _smallValue.CompareTo(other._smallValue);
            }

            Normalize(this, out bool leftNeg, out uint[] leftSig, out int leftExp);
            Normalize(other, out bool rightNeg, out uint[] rightSig, out int rightExp);

            bool leftIsZero = BigArithmetic.IsZero(leftSig);
            bool rightIsZero = BigArithmetic.IsZero(rightSig);

            if (leftIsZero && rightIsZero)
            {
                return 0;
            }

            if (leftIsZero)
            {
                return rightNeg ? 1 : -1;
            }

            if (rightIsZero)
            {
                return leftNeg ? -1 : 1;
            }

            // Different signs.
            if (leftNeg != rightNeg)
            {
                return leftNeg ? -1 : 1;
            }

            int signMultiplier = leftNeg ? -1 : 1;

            // Align exponents and compare significands.
            int cmp = CompareAligned(leftSig, leftExp, rightSig, rightExp);

            return cmp * signMultiplier;
        }

        /// <summary>
        /// Determines whether two <see cref="JsonNumber"/> values are equal.
        /// </summary>
        public static bool operator ==(JsonNumber left, JsonNumber right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="JsonNumber"/> values are not equal.
        /// </summary>
        public static bool operator !=(JsonNumber left, JsonNumber right) => !left.Equals(right);

        /// <summary>
        /// Determines whether one <see cref="JsonNumber"/> is less than another.
        /// </summary>
        public static bool operator <(JsonNumber left, JsonNumber right) => left.CompareTo(right) < 0;

        /// <summary>
        /// Determines whether one <see cref="JsonNumber"/> is less than or equal to another.
        /// </summary>
        public static bool operator <=(JsonNumber left, JsonNumber right) => left.CompareTo(right) <= 0;

        /// <summary>
        /// Determines whether one <see cref="JsonNumber"/> is greater than another.
        /// </summary>
        public static bool operator >(JsonNumber left, JsonNumber right) => left.CompareTo(right) > 0;

        /// <summary>
        /// Determines whether one <see cref="JsonNumber"/> is greater than or equal to another.
        /// </summary>
        public static bool operator >=(JsonNumber left, JsonNumber right) => left.CompareTo(right) >= 0;

        /// <summary>
        /// Normalizes a JsonNumber into its canonical (sign, significand, exponent) form.
        /// The significand is a uint[] with trailing zeros removed, and the exponent is adjusted.
        /// Zero values always normalize to (false, [], 0).
        /// </summary>
        private static void Normalize(JsonNumber value, out bool isNegative, out uint[] significand, out int exponent)
        {
            if (value._bigData is not null)
            {
                NormalizeBig(value._bigData, out isNegative, out significand, out exponent);
                return;
            }

            NormalizeDecimal(value._smallValue, out isNegative, out significand, out exponent);
        }

        private static void NormalizeBig(BigDecimalData data, out bool isNegative, out uint[] significand, out int exponent)
        {
            significand = BigArithmetic.TrimLeadingZeros(data.Significand);
            exponent = data.Exponent;

            if (BigArithmetic.IsZero(significand))
            {
                isNegative = false;
                exponent = 0;
                return;
            }

            isNegative = data.IsNegative;

            // Remove trailing zeros from significand (in decimal representation)
            // by dividing by 10 and incrementing exponent.
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

        private static void NormalizeDecimal(decimal value, out bool isNegative, out uint[] significand, out int exponent)
        {
            if (value == 0m)
            {
                isNegative = false;
                significand = [];
                exponent = 0;
                return;
            }

            isNegative = value < 0m;
            decimal abs = isNegative ? -value : value;

            // Extract decimal components.
            Span<int> bits = stackalloc int[4];
#if NET
            decimal.GetBits(abs, bits);
#else
            int[] bitsArr = decimal.GetBits(abs);
            bitsArr.AsSpan().CopyTo(bits);
#endif

            // bits[0..2] are the 96-bit mantissa (lo, mid, hi).
            // bits[3] contains the scale (bits 16-23).
            uint lo = (uint)bits[0];
            uint mid = (uint)bits[1];
            uint hi = (uint)bits[2];
            int scale = (bits[3] >> 16) & 0xFF;

            // Build the significand.
            if (hi != 0)
            {
                significand = [lo, mid, hi];
            }
            else if (mid != 0)
            {
                significand = [lo, mid];
            }
            else if (lo != 0)
            {
                significand = [lo];
            }
            else
            {
                significand = [];
                exponent = 0;
                isNegative = false;
                return;
            }

            exponent = -scale;

            // Remove trailing decimal zeros.
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

        /// <summary>
        /// Compares two significands with different exponents by aligning them.
        /// Uses magnitude pre-check to avoid unbounded memory allocation when
        /// exponents differ wildly.
        /// </summary>
        private static int CompareAligned(uint[] leftSig, int leftExp, uint[] rightSig, int rightExp)
        {
            if (leftExp == rightExp)
            {
                return BigArithmetic.Compare(leftSig, rightSig);
            }

            // Compare "order of magnitude" to avoid huge scaling operations.
            // GetDecimalDigitCount overestimates by at most 2, so if magnitudes
            // differ by more than 4, the comparison is definitive without scaling.
            int leftDigits = BigArithmetic.GetDecimalDigitCount(leftSig);
            int rightDigits = BigArithmetic.GetDecimalDigitCount(rightSig);
            long leftMag = (long)leftDigits + leftExp;
            long rightMag = (long)rightDigits + rightExp;

            if (leftMag - rightMag > 4)
            {
                return 1;
            }

            if (rightMag - leftMag > 4)
            {
                return -1;
            }

            // Magnitudes are close — need exact comparison via scaling.
            // The scaling power is bounded by the digit count difference plus a small
            // constant, so this won't allocate disproportionately large arrays.
            if (leftExp < rightExp)
            {
                uint[] scaledRight = BigArithmetic.MultiplyByPowerOf10(rightSig, rightExp - leftExp);
                return BigArithmetic.Compare(leftSig, scaledRight);
            }
            else
            {
                uint[] scaledLeft = BigArithmetic.MultiplyByPowerOf10(leftSig, leftExp - rightExp);
                return BigArithmetic.Compare(scaledLeft, rightSig);
            }
        }
    }
}
