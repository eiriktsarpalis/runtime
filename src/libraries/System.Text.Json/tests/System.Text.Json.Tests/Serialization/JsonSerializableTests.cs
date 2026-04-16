// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    /// <summary>
    /// A POCO annotated directly with [JsonSerializable] (parameterless).
    /// The source generator emits the IJsonSerializable&lt;T&gt; implementation automatically.
    /// </summary>
    [JsonSerializable]
    internal partial class DirectAnnotatedPoco
    {
        public string? Value { get; set; }
    }

    public partial class JsonSerializableTests
    {
        private class SimplePoco
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        [JsonSerializable(typeof(SimplePoco))]
        private partial class TestContext : JsonSerializerContext
        {
        }

        // ─── Serialize Tests ─────────────────────────────────────────────

        [Fact]
        public void Serialize_ContextPattern_ReturnsValidJson()
        {
            var poco = new SimplePoco { Name = "Alice", Age = 30 };
            string json = JsonSerializer.Serialize<TestContext, SimplePoco>(poco);
            Assert.Contains("\"Name\":\"Alice\"", json);
            Assert.Contains("\"Age\":30", json);
        }

        [Fact]
        public void Serialize_ToStream_ContextPattern()
        {
            var poco = new SimplePoco { Name = "Bob", Age = 25 };
            using var ms = new MemoryStream();
            JsonSerializer.Serialize<TestContext, SimplePoco>(ms, poco);
            ms.Position = 0;
            string json = new StreamReader(ms).ReadToEnd();
            Assert.Contains("\"Name\":\"Bob\"", json);
        }

        [Fact]
        public void Serialize_ToWriter_ContextPattern()
        {
            var poco = new SimplePoco { Name = "Carol", Age = 40 };
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            JsonSerializer.Serialize<TestContext, SimplePoco>(writer, poco);
            writer.Flush();
            ms.Position = 0;
            string json = new StreamReader(ms).ReadToEnd();
            Assert.Contains("\"Name\":\"Carol\"", json);
        }

        [Fact]
        public async Task SerializeAsync_ContextPattern()
        {
            var poco = new SimplePoco { Name = "Dave", Age = 50 };
            using var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync<TestContext, SimplePoco>(ms, poco);
            ms.Position = 0;
            string json = new StreamReader(ms).ReadToEnd();
            Assert.Contains("\"Name\":\"Dave\"", json);
        }

        [Fact]
        public void SerializeToUtf8Bytes_ContextPattern()
        {
            var poco = new SimplePoco { Name = "Eve", Age = 35 };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes<TestContext, SimplePoco>(poco);
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Contains("\"Name\":\"Eve\"", json);
        }

        // ─── Deserialize Tests ───────────────────────────────────────────

        [Fact]
        public void Deserialize_String_ContextPattern()
        {
            string json = """{"Name":"Frank","Age":60}""";
            SimplePoco? result = JsonSerializer.Deserialize<TestContext, SimplePoco>(json);
            Assert.NotNull(result);
            Assert.Equal("Frank", result.Name);
            Assert.Equal(60, result.Age);
        }

        [Fact]
        public void Deserialize_ReadOnlySpanChar_ContextPattern()
        {
            ReadOnlySpan<char> json = """{"Name":"Grace","Age":45}""".AsSpan();
            SimplePoco? result = JsonSerializer.Deserialize<TestContext, SimplePoco>(json);
            Assert.NotNull(result);
            Assert.Equal("Grace", result.Name);
        }

        [Fact]
        public void Deserialize_ReadOnlySpanByte_ContextPattern()
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("""{"Name":"Heidi","Age":55}""");
            SimplePoco? result = JsonSerializer.Deserialize<TestContext, SimplePoco>(utf8.AsSpan());
            Assert.NotNull(result);
            Assert.Equal("Heidi", result.Name);
        }

        [Fact]
        public void Deserialize_Stream_ContextPattern()
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("""{"Name":"Ivan","Age":70}""");
            using var ms = new MemoryStream(utf8);
            SimplePoco? result = JsonSerializer.Deserialize<TestContext, SimplePoco>(ms);
            Assert.NotNull(result);
            Assert.Equal("Ivan", result.Name);
        }

        [Fact]
        public async Task DeserializeAsync_ContextPattern()
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("""{"Name":"Judy","Age":80}""");
            using var ms = new MemoryStream(utf8);
            SimplePoco? result = await JsonSerializer.DeserializeAsync<TestContext, SimplePoco>(ms);
            Assert.NotNull(result);
            Assert.Equal("Judy", result.Name);
        }

        [Fact]
        public void Deserialize_Utf8JsonReader_ContextPattern()
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("""{"Name":"Karl","Age":90}""");
            var reader = new Utf8JsonReader(utf8);
            SimplePoco? result = JsonSerializer.Deserialize<TestContext, SimplePoco>(ref reader);
            Assert.NotNull(result);
            Assert.Equal("Karl", result.Name);
        }

        // ─── TypeInfoResolver Tests ──────────────────────────────────────

        [Fact]
        public void GetTypeInfo_ReturnsCachedInstance()
        {
            JsonTypeInfo<SimplePoco> info1 = GetTypeInfo<TestContext, SimplePoco>();
            JsonTypeInfo<SimplePoco> info2 = GetTypeInfo<TestContext, SimplePoco>();
            Assert.Same(info1, info2);

            static JsonTypeInfo<T> GetTypeInfo<TContext, T>() where TContext : IJsonSerializable<T>
                => TContext.GetTypeInfo();
        }

        [Fact]
        public void GetTypeInfo_OptionsContainResolver()
        {
            JsonTypeInfo<SimplePoco> typeInfo = GetTypeInfo<TestContext, SimplePoco>();
            Assert.NotNull(typeInfo.Options);
            JsonTypeInfo? resolved = typeInfo.Options.TypeInfoResolver?.GetTypeInfo(typeof(SimplePoco), typeInfo.Options);
            Assert.NotNull(resolved);
            Assert.Equal(typeof(SimplePoco), resolved.Type);

            static JsonTypeInfo<T> GetTypeInfo<TContext, T>() where TContext : IJsonSerializable<T>
                => TContext.GetTypeInfo();
        }

        // ─── Null/Empty Edge Cases ───────────────────────────────────────

        [Fact]
        public void Serialize_Null_ReturnsNullLiteral()
        {
            string json = JsonSerializer.Serialize<TestContext, SimplePoco>(null!);
            Assert.Equal("null", json);
        }

        [Fact]
        public void Deserialize_NullJson_ReturnsNull()
        {
            SimplePoco? result = JsonSerializer.Deserialize<TestContext, SimplePoco>("null");
            Assert.Null(result);
        }

        // ─── RoundTrip ──────────────────────────────────────────────────

        [Fact]
        public void RoundTrip_ContextPattern()
        {
            var original = new SimplePoco { Name = "RoundTrip", Age = 42 };
            string json = JsonSerializer.Serialize<TestContext, SimplePoco>(original);
            SimplePoco? deserialized = JsonSerializer.Deserialize<TestContext, SimplePoco>(json);
            Assert.NotNull(deserialized);
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.Age, deserialized.Age);
        }

        // ─── Pattern A Tests (T : IJsonSerializable<T>) ────────────────
        // DirectAnnotatedPoco is annotated with [JsonSerializable] directly.
        // The source generator emits the IJsonSerializable<T> implementation.

        [Fact]
        public void Serialize_PatternA_ReturnsValidJson()
        {
            var poco = new DirectAnnotatedPoco { Value = "hello" };
            string json = JsonSerializer.Serialize(poco);
            Assert.Contains("\"Value\":\"hello\"", json);
        }

        [Fact]
        public void Deserialize_PatternA_ReturnsValidObject()
        {
            string json = """{"Value":"world"}""";
            DirectAnnotatedPoco? result = JsonSerializer.Deserialize<DirectAnnotatedPoco>(json);
            Assert.NotNull(result);
            Assert.Equal("world", result.Value);
        }

        [Fact]
        public void RoundTrip_PatternA()
        {
            var original = new DirectAnnotatedPoco { Value = "roundtrip" };
            string json = JsonSerializer.Serialize(original);
            DirectAnnotatedPoco? deserialized = JsonSerializer.Deserialize<DirectAnnotatedPoco>(json);
            Assert.NotNull(deserialized);
            Assert.Equal(original.Value, deserialized.Value);
        }

        [Fact]
        public void SerializeToStream_PatternA()
        {
            var poco = new DirectAnnotatedPoco { Value = "stream" };
            using var ms = new MemoryStream();
            JsonSerializer.Serialize(ms, poco);
            ms.Position = 0;
            string json = new StreamReader(ms).ReadToEnd();
            Assert.Contains("\"Value\":\"stream\"", json);
        }

        [Fact]
        public void SerializeToUtf8Bytes_PatternA()
        {
            var poco = new DirectAnnotatedPoco { Value = "bytes" };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(poco);
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Contains("\"Value\":\"bytes\"", json);
        }

        [Fact]
        public void Deserialize_ReadOnlySpanByte_PatternA()
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("""{"Value":"span"}""");
            DirectAnnotatedPoco? result = JsonSerializer.Deserialize<DirectAnnotatedPoco>(utf8.AsSpan());
            Assert.NotNull(result);
            Assert.Equal("span", result.Value);
        }
    }
}

#endif
