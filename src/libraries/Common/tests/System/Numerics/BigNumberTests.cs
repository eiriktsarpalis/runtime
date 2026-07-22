// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
#if NET
using System.Runtime.CompilerServices;
using System.Text;
#endif
using Xunit;

namespace System.Numerics.Tests
{
    public class BigNumberTests
    {
        [Theory]
        [InlineData("0", "0")]
        [InlineData("-0", "-0")]
        [InlineData("+001.50", "1.50")]
        [InlineData("0.1234567890123456789012345678901234567890", "0.1234567890123456789012345678901234567890")]
        [InlineData("1234567890123456789012345678901", "1234567890123456789012345678901")]
        [InlineData("123456789012345678901234567890", "123456789012345678901234567890")]
        [InlineData("-123456789012345678901234567890.00", "-123456789012345678901234567890.00")]
        [InlineData("1234567890123456789012345678901234567890e100", "1.234567890123456789012345678901234567890E+139")]
        [InlineData("1234567890123456789012345678901234567890e-100", "1.234567890123456789012345678901234567890E-61")]
        [InlineData("1.234567890123456789012345678e-5", "0.00001234567890123456789012345678")]
        [InlineData("1e1000", "1E+1000")]
        [InlineData("1e-1000", "1E-1000")]
        [InlineData("1e79228162514264337593543950335", "1E+79228162514264337593543950335")]
        [InlineData("12e79228162514264337593543950335", "12E+79228162514264337593543950335")]
        [InlineData("1e-79228162514264337593543950335", "1E-79228162514264337593543950335")]
        public void Parse_ToString_RoundTrips(string input, string expected)
        {
            BigNumber value = BigNumber.Parse(input);

            Assert.Equal(expected, value.ToString());
            Assert.Equal(value, BigNumber.Parse(value.ToString()));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(".")]
        [InlineData(".1")]
        [InlineData("1.")]
        [InlineData("e1")]
        [InlineData("--1")]
        [InlineData("1e")]
        [InlineData("1e+")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        public void TryParse_InvalidInput_ReturnsFalse(string input)
        {
            Assert.False(BigNumber.TryParse(input, out BigNumber result));
            Assert.Equal(BigNumber.Zero, result);
            Assert.Throws<FormatException>(() => BigNumber.Parse(input));
        }

        [Theory]
        [InlineData("1e79228162514264337593543950336")]
        [InlineData("1.0e-79228162514264337593543950335")]
        [InlineData("1e1000000000000000000000000000000")]
        public void Parse_ExponentOutsideRange_ThrowsOverflowException(string input)
        {
            Assert.False(BigNumber.TryParse(input, out BigNumber result));
            Assert.Equal(BigNumber.Zero, result);
            Assert.Throws<OverflowException>(() => BigNumber.Parse(input));
        }

        [Fact]
        public void Parse_NullString_Throws()
        {
            Assert.False(BigNumber.TryParse((string?)null, out BigNumber result));
            Assert.Equal(BigNumber.Zero, result);
            AssertExtensions.Throws<ArgumentNullException>("text", () => BigNumber.Parse((string)null!));
        }

        [Fact]
        public void Parse_VeryLargeSignificand_RoundTrips()
        {
            string text = "1" + new string('2', 99_999);

            BigNumber value = BigNumber.Parse(text);

            Assert.Equal(text, value.ToString());
            Assert.Equal(value, BigNumber.Parse(value.ToString()));
        }

        [Theory]
        [InlineData("1", "1.0")]
        [InlineData("1", "10e-1")]
        [InlineData("0", "-0")]
        [InlineData("1e1000", "10e999")]
        [InlineData("123456789012345678901234567890.00", "123456789012345678901234567890")]
        [InlineData("79228162514264337593543950335", "79228162514264337593543950335.0")]
        [InlineData("10e79228162514264337593543950334", "1e79228162514264337593543950335")]
        [InlineData("7222736612338360310555537912e-33", "72227366123383603105555379120e-34")]
        public void Equality_IsNumeric(string leftText, string rightText)
        {
            BigNumber left = BigNumber.Parse(leftText);
            BigNumber right = BigNumber.Parse(rightText);

            Assert.Equal(left, right);
            Assert.True(left == right);
            Assert.False(left != right);
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Theory]
        [InlineData("-1e1000", "-1e999", -1)]
        [InlineData("-1", "0", -1)]
        [InlineData("-0", "0", 0)]
        [InlineData("1", "1.0", 0)]
        [InlineData("99999999999999999999999999999", "100000000000000000000000000000", -1)]
        [InlineData("1e999", "1e1000", -1)]
        [InlineData("1e-1000", "1e-999", -1)]
        [InlineData("1e79228162514264337593543950335", "1e-79228162514264337593543950335", 1)]
        public void Comparison_OrdersNumericValues(string leftText, string rightText, int expected)
        {
            BigNumber left = BigNumber.Parse(leftText);
            BigNumber right = BigNumber.Parse(rightText);

            Assert.Equal(expected, Math.Sign(left.CompareTo(right)));
            Assert.Equal(-expected, Math.Sign(right.CompareTo(left)));
            Assert.Equal(expected < 0, left < right);
            Assert.Equal(expected <= 0, left <= right);
            Assert.Equal(expected > 0, left > right);
            Assert.Equal(expected >= 0, left >= right);
        }

        [Fact]
        public void Comparison_RandomizedMatchesBigInteger()
        {
            var random = new Random(42);

            for (int i = 0; i < 500; i++)
            {
                (string leftText, BigInteger leftSignificand, int leftExponent) = CreateRandomNumber(random);
                (string rightText, BigInteger rightSignificand, int rightExponent) = CreateRandomNumber(random);
                int commonExponent = Math.Min(leftExponent, rightExponent);
                BigInteger left = leftSignificand * BigInteger.Pow(10, leftExponent - commonExponent);
                BigInteger right = rightSignificand * BigInteger.Pow(10, rightExponent - commonExponent);
                int expected = Math.Sign(left.CompareTo(right));

                BigNumber leftNumber = BigNumber.Parse(leftText);
                BigNumber rightNumber = BigNumber.Parse(rightText);
                Assert.Equal(expected, Math.Sign(leftNumber.CompareTo(rightNumber)));

                string equivalentLeftText = $"{leftSignificand * 10}e{leftExponent - 1}";
                BigNumber equivalentLeft = BigNumber.Parse(equivalentLeftText);
                Assert.Equal(leftNumber, equivalentLeft);
                Assert.Equal(leftNumber.GetHashCode(), equivalentLeft.GetHashCode());
            }
        }

        [Theory]
        [InlineData("0", true, false, true)]
        [InlineData("-0", true, true, true)]
        [InlineData("1.0", false, false, true)]
        [InlineData("1.5", false, false, false)]
        [InlineData("1e1000", false, false, true)]
        [InlineData("1e-1000", false, false, false)]
        [InlineData("123456789012345678901234567890.0", false, false, true)]
        [InlineData("-123456789012345678901234567890.5", false, true, false)]
        public void Properties_ReportValueCharacteristics(
            string text,
            bool isZero,
            bool isNegative,
            bool isInteger)
        {
            BigNumber value = BigNumber.Parse(text);

            Assert.Equal(isZero, value.IsZero);
            Assert.Equal(isNegative, value.IsNegative);
            Assert.Equal(isInteger, value.IsInteger);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        public void Int32_Conversion_RoundTrips(int expected)
        {
            BigNumber value = expected;

            Assert.True(value.TryGetInt32(out int actual));
            Assert.Equal(expected, actual);
            Assert.Equal(expected, (int)value);
        }

        [Theory]
        [InlineData("1.5")]
        [InlineData("2147483648")]
        [InlineData("-2147483649")]
        [InlineData("1e1000")]
        public void TryGetInt32_UnrepresentableValue_ReturnsFalse(string text)
        {
            BigNumber value = BigNumber.Parse(text);

            Assert.False(value.TryGetInt32(out int result));
            Assert.Equal(0, result);
            Assert.Throws<OverflowException>(() => (int)value);
        }

        [Fact]
        public void Decimal_Conversion_RequiresExactRepresentation()
        {
            BigNumber decimalMaximum = BigNumber.Parse("79228162514264337593543950335");
            Assert.True(decimalMaximum.TryGetDecimal(out decimal maximum));
            Assert.Equal(decimal.MaxValue, maximum);

            BigNumber equivalentToOne = BigNumber.Parse("10000000000000000000000000000e-28");
            Assert.True(equivalentToOne.TryGetDecimal(out decimal one));
            Assert.Equal(1m, one);

            BigNumber unrepresentable = BigNumber.Parse("1.00000000000000000000000000001");
            Assert.False(unrepresentable.TryGetDecimal(out _));
        }

        [Theory]
        [InlineData(0.1f)]
        [InlineData(float.Epsilon)]
        [InlineData(float.MaxValue)]
        [InlineData(float.MinValue)]
        [InlineData(1.2345678e-20f)]
        public void Single_Conversion_RoundTrips(float expected)
        {
            BigNumber value = (BigNumber)expected;

            Assert.True(value.TryGetSingle(out float actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0.1)]
        [InlineData(double.Epsilon)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(1.2345678901234567e-20)]
        public void Double_Conversion_RoundTrips(double expected)
        {
            BigNumber value = (BigNumber)expected;

            Assert.True(value.TryGetDouble(out double actual));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FloatingPointConversion_NonFiniteValue_Throws()
        {
            Assert.Throws<OverflowException>(() => (BigNumber)float.NaN);
            Assert.Throws<OverflowException>(() => (BigNumber)float.PositiveInfinity);
            Assert.Throws<OverflowException>(() => (BigNumber)double.NegativeInfinity);
        }

        [Fact]
        public void FloatingPointConversion_PreservesNegativeZero()
        {
            Assert.True(((BigNumber)(-0.0f)).IsNegative);
            Assert.True(((BigNumber)(-0.0d)).IsNegative);
        }

        [Fact]
        public void IComparable_InvalidObject_Throws()
        {
            IComparable value = BigNumber.Zero;

            Assert.Equal(1, value.CompareTo(null));
            AssertExtensions.Throws<ArgumentException>("obj", () => value.CompareTo(0));
        }

        [Fact]
        public void Type_IsProvidedByExpectedAssembly()
        {
#if NET
            Assert.Equal("System.Runtime.Numerics", typeof(BigNumber).Assembly.GetName().Name);
#else
            Assert.Equal("Microsoft.Bcl.Numerics", typeof(BigNumber).Assembly.GetName().Name);
#endif
        }

#if NET
        [Fact]
        public void Int128_Conversions_RoundTrip()
        {
            BigNumber signed = Int128.MinValue;
            Assert.True(signed.TryGetInt128(out Int128 signedResult));
            Assert.Equal(Int128.MinValue, signedResult);

            BigNumber unsigned = UInt128.MaxValue;
            Assert.True(unsigned.TryGetUInt128(out UInt128 unsignedResult));
            Assert.Equal(UInt128.MaxValue, unsignedResult);

            BigNumber negativeZero = BigNumber.Parse("-0");
            Assert.True(negativeZero.TryGetUInt128(out UInt128 zero));
            Assert.Equal(UInt128.Zero, zero);
        }

        [Fact]
        public void Int128_TryGetDoesNotAllocate()
        {
            BigNumber signed = Int128.MinValue;
            BigNumber unsigned = UInt128.MaxValue;

            Assert.True(TryGetInt128Values(signed, unsigned));
            long before = GC.GetAllocatedBytesForCurrentThread();

            bool result = TryGetInt128Values(signed, unsigned);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(result);
            Assert.Equal(0, allocated);
        }

        [Fact]
        public void TryFormat_FormatsCharactersAndUtf8()
        {
            BigNumber value = BigNumber.Parse("-123456789012345678901234567890.00");
            const string Expected = "-123456789012345678901234567890.00";

            Span<char> characters = stackalloc char[Expected.Length];
            Assert.True(value.TryFormat(characters, out int charsWritten));
            Assert.Equal(Expected.Length, charsWritten);
            Assert.Equal(Expected, characters.ToString());

            Span<byte> utf8 = stackalloc byte[Expected.Length];
            Assert.True(value.TryFormat(utf8, out int bytesWritten));
            Assert.Equal(Expected.Length, bytesWritten);
            Assert.Equal(Expected, Encoding.UTF8.GetString(utf8));
        }

        [Theory]
        [InlineData("")]
        [InlineData("G")]
        [InlineData("g")]
        [InlineData("R")]
        [InlineData("r")]
        public void Formatting_SupportedFormatsProduceInvariantValue(string format)
        {
            BigNumber value = BigNumber.Parse("-123456789012345678901234567890.00");
            string expected = value.ToString();

            Assert.Equal(expected, value.ToString(format, CultureInfo.GetCultureInfo("fr-FR")));

            char[] characters = new char[expected.Length];
            Assert.True(value.TryFormat(
                characters,
                out int charsWritten,
                format,
                CultureInfo.GetCultureInfo("fr-FR")));
            Assert.Equal(expected, characters.AsSpan(0, charsWritten).ToString());
        }

        [Theory]
        [InlineData("C")]
        [InlineData("E")]
        [InlineData("G1")]
        [InlineData("N")]
        [InlineData("X")]
        public void Formatting_UnsupportedFormat_Throws(string format)
        {
            BigNumber value = BigNumber.Parse("123456789012345678901234567890");
            char[] characters = new char[64];

            Assert.Throws<FormatException>(() => value.ToString(format, null));
            Assert.Throws<FormatException>(() => value.TryFormat(characters, out _, format));
        }

        [Fact]
        public void DecimalConversion_DoesNotAllocate()
        {
            Assert.False(CreateSmallValue(1m));
            long before = GC.GetAllocatedBytesForCurrentThread();

            int zeroCount = 0;
            for (int i = 1; i <= 1_000; i++)
            {
                zeroCount += CreateSmallValue(i) ? 1 : 0;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, zeroCount);
            Assert.Equal(0, allocated);
        }

        [Fact]
        public void LargeComparisonHashingAndFormatting_DoNotAllocate()
        {
            BigNumber value = BigNumber.Parse("1234567890123456789012345678900e-1");
            BigNumber equivalent = BigNumber.Parse("123456789012345678901234567890");
            BigNumber greater = BigNumber.Parse("123456789012345678901234567891");
            char[] characters = new char[64];
            byte[] utf8 = new byte[64];

            _ = ExerciseLargeValueOperations(value, equivalent, greater, characters, utf8);
            long before = GC.GetAllocatedBytesForCurrentThread();

            bool result = ExerciseLargeValueOperations(value, equivalent, greater, characters, utf8);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(result);
            Assert.Equal(0, allocated);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CreateSmallValue(decimal value)
        {
            BigNumber number = value;
            return number.IsZero;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetInt128Values(BigNumber signed, BigNumber unsigned) =>
            signed.TryGetInt128(out Int128 signedResult) &&
            signedResult == Int128.MinValue &&
            unsigned.TryGetUInt128(out UInt128 unsignedResult) &&
            unsignedResult == UInt128.MaxValue;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ExerciseLargeValueOperations(
            BigNumber value,
            BigNumber equivalent,
            BigNumber greater,
            char[] characters,
            byte[] utf8)
        {
            return value == equivalent &&
                value.GetHashCode() == equivalent.GetHashCode() &&
                value.CompareTo(greater) < 0 &&
                value.IsInteger &&
                value.TryFormat(characters, out int charsWritten) &&
                charsWritten > 0 &&
                value.TryFormat(utf8, out int bytesWritten) &&
                bytesWritten == charsWritten;
        }
#endif

        private static (string Text, BigInteger Significand, int Exponent) CreateRandomNumber(Random random)
        {
            int digitCount = random.Next(1, 81);
            char[] digits = new char[digitCount];
            digits[0] = (char)('1' + random.Next(9));
            for (int i = 1; i < digits.Length; i++)
            {
                digits[i] = (char)('0' + random.Next(10));
            }

            BigInteger significand = BigInteger.Parse(new string(digits), CultureInfo.InvariantCulture);
            if (random.Next(2) == 0)
            {
                significand = -significand;
            }

            int exponent = random.Next(-50, 51);
            return ($"{significand}e{exponent}", significand, exponent);
        }
    }
}
