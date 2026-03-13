// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace System.Text.Json.Tests
{
    public static class JsonNumberTests
    {
        // ===================== Construction & Properties =====================

        [Fact]
        public static void DefaultValue_IsZero()
        {
            JsonNumber n = default;
            Assert.True(n.IsZero);
            Assert.False(n.IsNegative);
            Assert.True(n.IsInteger);
            Assert.Equal("0", n.ToString());
        }

        [Fact]
        public static void StaticZero()
        {
            Assert.True(JsonNumber.Zero.IsZero);
            Assert.Equal(default(JsonNumber), JsonNumber.Zero);
        }

        [Theory]
        [InlineData("0", true, false, true)]
        [InlineData("1", false, false, true)]
        [InlineData("-1", false, true, true)]
        [InlineData("1.5", false, false, false)]
        [InlineData("-0.001", false, true, false)]
        [InlineData("1e10", false, false, true)]
        [InlineData("1.5e2", false, false, true)]
        [InlineData("1.23e-4", false, false, false)]
        public static void Properties(string text, bool isZero, bool isNegative, bool isInteger)
        {
            JsonNumber n = JsonNumber.Parse(text);
            Assert.Equal(isZero, n.IsZero);
            Assert.Equal(isNegative, n.IsNegative);
            Assert.Equal(isInteger, n.IsInteger);
        }

        // ===================== Implicit Conversions =====================

        [Theory]
        [InlineData(0)]
        [InlineData(42)]
        [InlineData(-100)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public static void ImplicitFromInt32(int value)
        {
            JsonNumber n = value;
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), n.ToString());
            Assert.True(n.TryGetInt32(out int result));
            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public static void ImplicitFromInt64(long value)
        {
            JsonNumber n = value;
            Assert.True(n.TryGetInt64(out long result));
            Assert.Equal(value, result);
        }

        [Fact]
        public static void ImplicitFromDecimal()
        {
            JsonNumber n = 3.14m;
            Assert.True(n.TryGetDecimal(out decimal result));
            Assert.Equal(3.14m, result);
        }

        [Fact]
        public static void ExplicitFromDouble()
        {
            JsonNumber n = (JsonNumber)1.5;
            Assert.True(n.TryGetDouble(out double result));
            Assert.Equal(1.5, result);
        }

        // ===================== Parsing =====================

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("-1")]
        [InlineData("123456789")]
        [InlineData("1.5")]
        [InlineData("-0.001")]
        [InlineData("1e10")]
        [InlineData("1E+10")]
        [InlineData("1.5e-3")]
        [InlineData("123456789.123456789")]
        public static void Parse_ValidNumbers(string text)
        {
            JsonNumber n = JsonNumber.Parse(text);
            Assert.NotEqual(default, n.ToString());

            Assert.True(JsonNumber.TryParse(text, out JsonNumber n2));
            Assert.Equal(n, n2);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        public static void Parse_InvalidNumbers(string text)
        {
            Assert.Throws<FormatException>(() => JsonNumber.Parse(text));
            Assert.False(JsonNumber.TryParse(text, out _));
        }

        [Theory]
        [InlineData("+1")]
        [InlineData(".5")]
        [InlineData("1.")]
        [InlineData("01")]
        public static void Parse_NonStrictJsonNumbers_Rejected(string text)
        {
            // These are not valid JSON numbers per the spec. The parser validates
            // strict JSON grammar before attempting decimal parsing.
            Assert.False(JsonNumber.TryParse(text, out _));
            Assert.Throws<FormatException>(() => JsonNumber.Parse(text));
        }

        [Fact]
        public static void Parse_LargeNumber()
        {
            string large = "123456789012345678901234567890";
            JsonNumber n = JsonNumber.Parse(large);
            Assert.False(n.IsZero);
            Assert.False(n.IsNegative);
            Assert.True(n.IsInteger);

            string roundTripped = n.ToString();
            Assert.Contains("1234567890123456789", roundTripped);
        }

        [Fact]
        public static void Parse_Utf8()
        {
            ReadOnlySpan<byte> utf8 = "42"u8;
            JsonNumber n = JsonNumber.Parse(utf8);
            Assert.True(n.TryGetInt32(out int result));
            Assert.Equal(42, result);
        }

        [Fact]
        public static void Parse_LargeExponent()
        {
            string text = "1e100";
            JsonNumber n = JsonNumber.Parse(text);
            Assert.False(n.IsZero);
            Assert.True(n.IsInteger);
        }

        // ===================== Equality =====================

        [Theory]
        [InlineData("1", "1.0")]
        [InlineData("1", "1.00")]
        [InlineData("1", "10e-1")]
        [InlineData("100", "1e2")]
        [InlineData("0", "-0")]
        [InlineData("0.0", "0")]
        [InlineData("1.5", "15e-1")]
        [InlineData("1.50", "1.5")]
        public static void Equality_SameValue(string left, string right)
        {
            JsonNumber l = JsonNumber.Parse(left);
            JsonNumber r = JsonNumber.Parse(right);
            Assert.Equal(l, r);
            Assert.True(l == r);
            Assert.False(l != r);
            Assert.Equal(l.GetHashCode(), r.GetHashCode());
        }

        [Theory]
        [InlineData("1", "2")]
        [InlineData("1", "-1")]
        [InlineData("1.0", "1.1")]
        [InlineData("0", "1")]
        public static void Equality_DifferentValue(string left, string right)
        {
            JsonNumber l = JsonNumber.Parse(left);
            JsonNumber r = JsonNumber.Parse(right);
            Assert.NotEqual(l, r);
            Assert.False(l == r);
            Assert.True(l != r);
        }

        // ===================== Comparison =====================

        [Theory]
        [InlineData("1", "2", -1)]
        [InlineData("2", "1", 1)]
        [InlineData("1", "1", 0)]
        [InlineData("-1", "1", -1)]
        [InlineData("0", "-0", 0)]
        [InlineData("1.5", "2.5", -1)]
        [InlineData("10", "2", 1)]
        public static void Comparison(string left, string right, int expected)
        {
            JsonNumber l = JsonNumber.Parse(left);
            JsonNumber r = JsonNumber.Parse(right);
            Assert.Equal(expected, Math.Sign(l.CompareTo(r)));
        }

        // ===================== TryGet Methods =====================

        [Fact]
        public static void TryGetByte_InRange()
        {
            JsonNumber n = 200;
            Assert.True(n.TryGetByte(out byte result));
            Assert.Equal(200, result);
        }

        [Fact]
        public static void TryGetByte_OutOfRange()
        {
            JsonNumber n = 300;
            Assert.False(n.TryGetByte(out _));
        }

        [Fact]
        public static void TryGetInt32_FromLargeNumber()
        {
            JsonNumber n = long.MaxValue;
            Assert.False(n.TryGetInt32(out _));
        }

        [Fact]
        public static void TryGetDecimal_FromSmallNumber()
        {
            JsonNumber n = 42;
            Assert.True(n.TryGetDecimal(out decimal result));
            Assert.Equal(42m, result);
        }

        [Fact]
        public static void TryGetDecimal_FromLargeNumber()
        {
            JsonNumber n = JsonNumber.Parse("123456789012345678901234567890");
            Assert.False(n.TryGetDecimal(out _));
        }

        [Fact]
        public static void TryGetDouble_FromSmallNumber()
        {
            JsonNumber n = 1.5m;
            Assert.True(n.TryGetDouble(out double result));
            Assert.Equal(1.5, result);
        }

        // ===================== Formatting =====================

        [Theory]
        [InlineData("0", "0")]
        [InlineData("1", "1")]
        [InlineData("-1", "-1")]
        [InlineData("42", "42")]
        [InlineData("1.5", "1.5")]
        [InlineData("-0.001", "-0.001")]
        public static void ToString_SmallNumbers(string input, string expected)
        {
            JsonNumber n = JsonNumber.Parse(input);
            Assert.Equal(expected, n.ToString());
        }

        // ===================== Reader Integration =====================

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("-1")]
        [InlineData("3.14")]
        [InlineData("1e10")]
        [InlineData("1.5e-3")]
        [InlineData("123456789")]
        public static void Reader_GetJsonNumber(string jsonNumber)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(jsonNumber);
            var reader = new Utf8JsonReader(data, isFinalBlock: true, state: default);
            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.Number, reader.TokenType);

            JsonNumber n = reader.GetJsonNumber();
            Assert.NotEqual(default, n.ToString());
        }

        [Theory]
        [InlineData("0")]
        [InlineData("42")]
        [InlineData("-123.456")]
        public static void Reader_TryGetJsonNumber(string jsonNumber)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(jsonNumber);
            var reader = new Utf8JsonReader(data, isFinalBlock: true, state: default);
            Assert.True(reader.Read());
            Assert.True(reader.TryGetJsonNumber(out JsonNumber n));
        }

        [Fact]
        public static void Reader_GetJsonNumber_WrongToken_Throws()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("\"hello\"");
            var reader = new Utf8JsonReader(data, isFinalBlock: true, state: default);
            Assert.True(reader.Read());
            Assert.Throws<InvalidOperationException>(() =>
            {
                var r = new Utf8JsonReader(data, isFinalBlock: true, state: default);
                r.Read();
                r.GetJsonNumber();
            });
        }

        // ===================== Writer Integration =====================

        [Theory]
        [InlineData(42)]
        [InlineData(0)]
        [InlineData(-100)]
        public static void Writer_WriteNumberValue(int value)
        {
            JsonNumber n = value;
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteNumberValue(n);
            writer.Flush();
            string json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), json);
        }

        [Fact]
        public static void Writer_WriteNumber_WithProperty()
        {
            JsonNumber n = 42;
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            writer.WriteNumber("value", n);
            writer.WriteEndObject();
            writer.Flush();
            string json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("{\"value\":42}", json);
        }

        // ===================== Round-trip =====================

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("-1")]
        [InlineData("3.14")]
        [InlineData("1e10")]
        [InlineData("1.5e-3")]
        [InlineData("123456789.987654321")]
        public static void RoundTrip_ReaderWriter(string jsonNumber)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(jsonNumber);
            var reader = new Utf8JsonReader(data, isFinalBlock: true, state: default);
            Assert.True(reader.Read());
            JsonNumber n = reader.GetJsonNumber();

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteNumberValue(n);
            writer.Flush();

            string result = System.Text.Encoding.UTF8.GetString(stream.ToArray());

            // The round-tripped value should represent the same number
            JsonNumber n2 = JsonNumber.Parse(result);
            Assert.Equal(n, n2);
        }

        // ===================== Serialization =====================

        [Fact]
        public static void Serialize_JsonNumber()
        {
            JsonNumber n = 42;
            string json = JsonSerializer.Serialize(n);
            Assert.Equal("42", json);
        }

        [Fact]
        public static void Deserialize_JsonNumber()
        {
            JsonNumber n = JsonSerializer.Deserialize<JsonNumber>("42");
            Assert.Equal((JsonNumber)42, n);
        }

        [Fact]
        public static void Serialize_Decimal_RoundTrip()
        {
            JsonNumber n = 3.14m;
            string json = JsonSerializer.Serialize(n);
            JsonNumber n2 = JsonSerializer.Deserialize<JsonNumber>(json);
            Assert.Equal(n, n2);
        }

        [Fact]
        public static void Serialize_Nullable_Null()
        {
            JsonNumber? n = null;
            string json = JsonSerializer.Serialize(n);
            Assert.Equal("null", json);
        }

        [Fact]
        public static void Serialize_Nullable_WithValue()
        {
            JsonNumber? n = (JsonNumber)42;
            string json = JsonSerializer.Serialize(n);
            Assert.Equal("42", json);
        }

        [Fact]
        public static void Deserialize_Nullable_Null()
        {
            JsonNumber? n = JsonSerializer.Deserialize<JsonNumber?>("null");
            Assert.Null(n);
        }

        [Fact]
        public static void Serialize_InObject()
        {
            var obj = new { Value = (JsonNumber)42 };
            string json = JsonSerializer.Serialize(obj);
            Assert.Equal("{\"Value\":42}", json);
        }

        [Fact]
        public static void Deserialize_InObject()
        {
            string json = "{\"Value\":42}";
            var result = JsonSerializer.Deserialize<JsonNumberWrapper>(json);
            Assert.Equal((JsonNumber)42, result.Value);
        }

        [Fact]
        public static void NumberHandling_ReadFromString()
        {
            var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString };
            JsonNumber n = JsonSerializer.Deserialize<JsonNumber>("\"42\"", options);
            Assert.Equal((JsonNumber)42, n);
        }

        [Fact]
        public static void NumberHandling_WriteAsString()
        {
            var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.WriteAsString };
            string json = JsonSerializer.Serialize((JsonNumber)42, options);
            Assert.Equal("\"42\"", json);
        }

        // ===================== JsonNode Integration =====================

        [Fact]
        public static void JsonValue_Create_FromJsonNumber()
        {
            JsonNumber n = 42;
            JsonValue value = JsonValue.Create(n);
            Assert.Equal(JsonValueKind.Number, value.GetValueKind());
            Assert.Equal(n, value.GetValue<JsonNumber>());
        }

        [Fact]
        public static void JsonValue_Create_Nullable_Null()
        {
            JsonNumber? n = null;
            JsonValue? value = JsonValue.Create(n);
            Assert.Null(value);
        }

        [Fact]
        public static void JsonNode_ImplicitOperator()
        {
            JsonNumber n = 42;
            JsonNode node = n;
            JsonNumber result = node.GetValue<JsonNumber>();
            Assert.Equal(n, result);
        }

        [Fact]
        public static void JsonNode_ExplicitOperator()
        {
            JsonNode node = JsonValue.Create((JsonNumber)42);
            JsonNumber n = (JsonNumber)node;
            Assert.Equal((JsonNumber)42, n);
        }

        [Fact]
        public static void JsonObject_WithJsonNumber()
        {
            var obj = new JsonObject();
            obj["value"] = (JsonNumber)42;
            string json = obj.ToJsonString();
            Assert.Equal("{\"value\":42}", json);
        }

        // ===================== Helper Types =====================

        private class JsonNumberWrapper
        {
            public JsonNumber Value { get; set; }
        }

        // ===================== Edge Cases =====================

        [Theory]
        [InlineData("1e2000000000", "1e1000000000", 1)]
        [InlineData("1e1000000000", "1e2000000000", -1)]
        [InlineData("1e100", "2e100", -1)]
        [InlineData("2e100", "1e100", 1)]
        [InlineData("1e100", "1e100", 0)]
        public static void Comparison_LargeExponents(string left, string right, int expected)
        {
            JsonNumber l = JsonNumber.Parse(left);
            JsonNumber r = JsonNumber.Parse(right);
            Assert.Equal(expected, Math.Sign(l.CompareTo(r)));
        }

        [Theory]
        [InlineData("1e2147483647")]
        [InlineData("-1e2147483647")]
        [InlineData("1.23e100")]
        public static void Parse_ExtremeExponent_Succeeds(string text)
        {
            Assert.True(JsonNumber.TryParse(text, out JsonNumber n));
            Assert.False(n.IsZero);
        }

        [Fact]
        public static void NegativeZero_IsNotNegative()
        {
            JsonNumber n = JsonNumber.Parse("-0");
            Assert.True(n.IsZero);
            Assert.False(n.IsNegative);
        }

        [Fact]
        public static void Equality_SmallVsBig_SameValue()
        {
            // 79228162514264337593543950335 is decimal.MaxValue
            JsonNumber fromDecimal = decimal.MaxValue;
            JsonNumber fromParse = JsonNumber.Parse("79228162514264337593543950335");
            Assert.Equal(fromDecimal, fromParse);
            Assert.Equal(fromDecimal.GetHashCode(), fromParse.GetHashCode());
        }

        // ===================== JsonNode Normalization =====================

        [Theory]
        [InlineData("42", 42)]
        [InlineData("0", 0)]
        [InlineData("-1", -1)]
        [InlineData("2147483647", int.MaxValue)]
        [InlineData("-2147483648", int.MinValue)]
        public static void ParsedJsonNode_GetValueInt32(string json, int expected)
        {
            JsonNode node = JsonNode.Parse(json);
            Assert.Equal(expected, node.GetValue<int>());
        }

        [Theory]
        [InlineData("42", 42L)]
        [InlineData("9007199254740993", 9007199254740993L)]
        [InlineData("9223372036854775807", long.MaxValue)]
        public static void ParsedJsonNode_GetValueInt64(string json, long expected)
        {
            JsonNode node = JsonNode.Parse(json);
            Assert.Equal(expected, node.GetValue<long>());
        }

        [Theory]
        [InlineData("1.5", 1.5)]
        [InlineData("3.14", 3.14)]
        [InlineData("1e10", 1e10)]
        public static void ParsedJsonNode_GetValueDouble(string json, double expected)
        {
            JsonNode node = JsonNode.Parse(json);
            Assert.Equal(expected, node.GetValue<double>());
        }

        [Theory]
        [InlineData("1.5")]
        [InlineData("42")]
        [InlineData("9999999999999999999999999999")]
        [InlineData("1e100")]
        public static void ParsedJsonNode_GetValueJsonNumber(string json)
        {
            JsonNode node = JsonNode.Parse(json);
            JsonNumber jn = node.GetValue<JsonNumber>();
            Assert.Equal(JsonNumber.Parse(json), jn);
        }

        [Theory]
        [InlineData("42")]
        [InlineData("3.14")]
        [InlineData("1e100")]
        public static void DeserializedJsonNode_GetValueJsonNumber(string json)
        {
            JsonNode node = JsonSerializer.Deserialize<JsonNode>(json);
            JsonNumber jn = node.GetValue<JsonNumber>();
            Assert.Equal(JsonNumber.Parse(json), jn);
        }

        [Fact]
        public static void ProgrammaticJsonValue_GetValueJsonNumber()
        {
            JsonValue node = JsonValue.Create(42);
            JsonNumber jn = node.GetValue<JsonNumber>();
            Assert.Equal((JsonNumber)42, jn);
        }

        [Theory]
        [InlineData("42", typeof(int))]
        [InlineData("2147483648", typeof(long))]
        [InlineData("9223372036854775808", typeof(ulong))]
        [InlineData("1.5", typeof(decimal))]
        public static void ParsedJsonNode_GetValueObject_ReturnsNarrowestClrType(string json, Type expectedType)
        {
            JsonNode node = JsonNode.Parse(json);
            object obj = node.GetValue<object>();
            Assert.IsType(expectedType, obj);
        }

        [Fact]
        public static void ParsedJsonNode_DeepEquals_AcrossRepresentations()
        {
            JsonNode parsed = JsonNode.Parse("42");
            JsonNode created = JsonValue.Create(42);
            Assert.True(JsonNode.DeepEquals(parsed, created));
        }

        [Fact]
        public static void ParsedJsonNode_DeepEquals_IntegerAndDecimal()
        {
            JsonNode a = JsonNode.Parse("4");
            JsonNode b = JsonNode.Parse("4.0");
            Assert.True(JsonNode.DeepEquals(a, b));
        }

        [Fact]
        public static void ParsedJsonNode_DeepEquals_DifferentValues()
        {
            JsonNode a = JsonNode.Parse("42");
            JsonNode b = JsonNode.Parse("43");
            Assert.False(JsonNode.DeepEquals(a, b));
        }

        [Fact]
        public static void ParsedJsonNode_CrossType_ByteFromLargerInteger()
        {
            JsonNode node = JsonNode.Parse("255");
            Assert.Equal((byte)255, node.GetValue<byte>());
        }

        [Theory]
        [InlineData("0.1")]
        [InlineData("1e-1024", "1E-1024")]
        [InlineData("1.23456789012345678901234567890", "1.2345678901234567890123456789")]
        public static void ParsedJsonNode_HighPrecision_PreservedAsJsonNumber(string json, string expectedString = null)
        {
            JsonNode node = JsonNode.Parse(json);
            JsonNumber jn = node.GetValue<JsonNumber>();
            // JsonNumber normalizes to uppercase E in scientific notation
            Assert.Equal(expectedString ?? json, jn.ToString());
        }

        [Fact]
        public static void DecimalFaithfulness_SmallExponent_UsesDecimalPath()
        {
            // 0.1 should round-trip faithfully through decimal
            JsonNumber n = JsonNumber.Parse("0.1");
            Assert.True(n.TryGetDecimal(out decimal d));
            Assert.Equal(0.1m, d);
        }

        [Fact]
        public static void DecimalFaithfulness_Underflow_UsesBigPath()
        {
            // 1e-1024 underflows to 0m via decimal fast path, so it must use big path
            JsonNumber n = JsonNumber.Parse("1e-1024");
            Assert.False(n.IsZero);
            Assert.Equal("1E-1024", n.ToString());
        }

        [Fact]
        public static void DecimalFaithfulness_HighPrecision_UsesBigPath()
        {
            // 30+ digit number exceeds decimal's 28-29 digit precision
            string thirtyDigits = "1234567890123456789012345678901";
            JsonNumber n = JsonNumber.Parse(thirtyDigits);
            // Big path formats in scientific notation
            Assert.Equal("1.234567890123456789012345678901E+30", n.ToString());
        }

        [Fact]
        public static void ParsedJsonNode_RoundTrip_Serialize()
        {
            string json = """{"value":9007199254740993}""";
            JsonNode node = JsonNode.Parse(json);
            string output = node.ToJsonString();
            Assert.Equal(json, output);
        }

        [Fact]
        public static void DeserializedJsonNode_RoundTrip_LargeInteger()
        {
            string json = "9007199254740993";
            JsonNode node = JsonSerializer.Deserialize<JsonNode>(json);
            long value = node.GetValue<long>();
            Assert.Equal(9007199254740993L, value);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public static void ProgrammaticJsonValue_NaN_Infinity_TryGetJsonNumber_ReturnsFalse(double nonFinite)
        {
            JsonValue node = JsonValue.Create(nonFinite);
            Assert.False(node.TryGetValue(out JsonNumber _));
        }

        [Theory]
        [InlineData("42", typeof(int))]
        [InlineData("1.5", typeof(decimal))]
        public static void DeserializedJsonNode_GetValueObject_ReturnsNarrowestClrType(string json, Type expectedType)
        {
            JsonNode node = JsonSerializer.Deserialize<JsonNode>(json);
            object obj = node.GetValue<object>();
            Assert.IsType(expectedType, obj);
        }

        // ===================== Follow-up: Half, Int128, UInt128, string =====================

        [Fact]
        public static void TryGetHalf_SmallValue()
        {
            JsonNumber n = JsonNumber.Parse("1.5");
            Assert.True(n.TryGetHalf(out Half h));
            Assert.Equal((Half)1.5f, h);
        }

        [Fact]
        public static void TryGetHalf_TooLarge_ReturnsFalse()
        {
            // Half.MaxValue is 65504; values beyond that overflow to Infinity
            JsonNumber n = JsonNumber.Parse("100000");
            Assert.False(n.TryGetHalf(out _));
        }

        [Fact]
        public static void ExplicitHalfOperator_RoundTrip()
        {
            Half original = (Half)3.14f;
            JsonNumber jn = (JsonNumber)original;
            Half result = (Half)jn;
            Assert.Equal(original, result);
        }

        [Theory]
        [InlineData("0", 0)]
        [InlineData("170141183460469231731687303715884105727", 0)] // Int128.MaxValue
        [InlineData("-170141183460469231731687303715884105728", 0)] // Int128.MinValue
        public static void TryGetInt128_ValidValues(string text, int _)
        {
            JsonNumber n = JsonNumber.Parse(text);
            Assert.True(n.TryGetInt128(out Int128 result));
            Assert.Equal(Int128.Parse(text), result);
        }

        [Fact]
        public static void TryGetInt128_NonInteger_ReturnsFalse()
        {
            JsonNumber n = JsonNumber.Parse("1.5");
            Assert.False(n.TryGetInt128(out _));
        }

        [Fact]
        public static void TryGetInt128_TooLarge_ReturnsFalse()
        {
            // Larger than Int128.MaxValue
            JsonNumber n = JsonNumber.Parse("999999999999999999999999999999999999999");
            Assert.False(n.TryGetInt128(out _));
        }

        [Theory]
        [InlineData("0", 0)]
        [InlineData("340282366920938463463374607431768211455", 0)] // UInt128.MaxValue
        public static void TryGetUInt128_ValidValues(string text, int _)
        {
            JsonNumber n = JsonNumber.Parse(text);
            Assert.True(n.TryGetUInt128(out UInt128 result));
            Assert.Equal(UInt128.Parse(text), result);
        }

        [Fact]
        public static void TryGetUInt128_Negative_ReturnsFalse()
        {
            JsonNumber n = JsonNumber.Parse("-1");
            Assert.False(n.TryGetUInt128(out _));
        }

        [Theory]
        [InlineData("42")]
        [InlineData("3.14")]
        [InlineData("1e100")]
        public static void ParsedJsonNode_GetValueString_Throws(string json)
        {
            JsonNode node = JsonNode.Parse(json);
            Assert.Throws<InvalidOperationException>(() => node.GetValue<string>());
        }

        [Fact]
        public static void ParsedJsonNode_GetValueHalf()
        {
            JsonNode node = JsonNode.Parse("1.5");
            Half h = node.GetValue<Half>();
            Assert.Equal((Half)1.5f, h);
        }

        [Fact]
        public static void ParsedJsonNode_GetValueInt128()
        {
            string json = "170141183460469231731687303715884105727";
            JsonNode node = JsonNode.Parse(json);
            Int128 value = node.GetValue<Int128>();
            Assert.Equal(Int128.MaxValue, value);
        }

        [Fact]
        public static void ParsedJsonNode_GetValueUInt128()
        {
            string json = "340282366920938463463374607431768211455";
            JsonNode node = JsonNode.Parse(json);
            UInt128 value = node.GetValue<UInt128>();
            Assert.Equal(UInt128.MaxValue, value);
        }

        [Fact]
        public static void ProgrammaticInt128_GetValueJsonNumber()
        {
            Int128 original = Int128.MaxValue;
            JsonValue node = JsonValue.Create(original);
            JsonNumber jn = node.GetValue<JsonNumber>();
            Assert.True(jn.TryGetInt128(out Int128 result));
            Assert.Equal(original, result);
        }

        [Fact]
        public static void DecimalFaithfulness_LeadingZeros_UsesDecimalPath()
        {
            // Many leading zeros but only 1 significant digit — should stay on decimal fast path
            JsonNumber n = JsonNumber.Parse("0.0000000000000000000000000001");
            Assert.True(n.TryGetDecimal(out decimal d));
            Assert.Equal(0.0000000000000000000000000001m, d);
        }

        [Fact]
        public static void ImplicitConversion_Int128ToJsonNumber()
        {
            Int128 value = Int128.MaxValue;
            JsonNumber jn = value;
            Assert.True(jn.TryGetInt128(out Int128 result));
            Assert.Equal(value, result);
        }

        [Fact]
        public static void ImplicitConversion_UInt128ToJsonNumber()
        {
            UInt128 value = UInt128.MaxValue;
            JsonNumber jn = value;
            Assert.True(jn.TryGetUInt128(out UInt128 result));
            Assert.Equal(value, result);
        }

        // ===================== Code review fixes =====================

        [Fact]
        public static void FloatMaxValue_ExplicitConversion_DoesNotThrow()
        {
            JsonNumber jn = (JsonNumber)float.MaxValue;
            Assert.True(jn.TryGetSingle(out float result));
            Assert.Equal(float.MaxValue, result);
        }

        [Fact]
        public static void FloatMinValue_ExplicitConversion_DoesNotThrow()
        {
            JsonNumber jn = (JsonNumber)float.MinValue;
            Assert.True(jn.TryGetSingle(out float result));
            Assert.Equal(float.MinValue, result);
        }

        [Fact]
        public static void TryGetUInt128_DecimalAboveUlongMax_Succeeds()
        {
            // 10^20 is above ulong.MaxValue (~1.8e19) but fits in decimal and UInt128
            JsonNumber jn = JsonNumber.Parse("100000000000000000000");
            Assert.True(jn.TryGetUInt128(out UInt128 result));
            Assert.Equal((UInt128)100000000000000000000m, result);
        }

        [Theory]
        [InlineData("100.0")]
        [InlineData("12345678901234567890.00")]
        [InlineData("1.000000000000000000000000000000")]
        public static void IsInteger_BigPath_TrailingFractionalZeros(string input)
        {
            JsonNumber jn = JsonNumber.Parse(input);
            Assert.True(jn.IsInteger);
        }

        [Fact]
        public static void TryGetDecimal_BigPath_UnderflowReturnsFalse()
        {
            // This number has 30+ significant digits, so it goes through big path.
            // TryGetDecimal should return false since 30 sig digits exceeds decimal precision.
            JsonNumber jn = JsonNumber.Parse("1.23456789012345678901234567890");
            Assert.False(jn.TryGetDecimal(out _));
        }
    }
}
