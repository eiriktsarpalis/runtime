// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.SourceGeneration.Tests
{
    public sealed partial class PolymorphicTests_SourceGen_Metadata
    {
        [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
        [JsonSerializable(typeof(DiscriminatorBindingBase))]
        [JsonSerializable(typeof(DiscriminatorBindingDerived))]
        [JsonSerializable(typeof(FallbackBase))]
        [JsonSerializable(typeof(FallbackKnown))]
        [JsonSerializable(typeof(FallbackUnknown))]
        [JsonSerializable(typeof(IgnoreUnrecognizedWithBindingBase))]
        [JsonSerializable(typeof(IgnoreUnrecognizedWithBindingDerived))]
        internal sealed partial class PolymorphicTestsContext_Metadata : JsonSerializerContext
        {
        }

        [Fact]
        public void TypeDiscriminatorBinding_RecognizedType_BindsDiscriminator()
        {
            string json = """{"$type":"derived","Name":"test"}""";
            var result = JsonSerializer.Deserialize(json, PolymorphicTestsContext_Metadata.Default.DiscriminatorBindingBase);

            Assert.IsType<DiscriminatorBindingDerived>(result);
            var derived = (DiscriminatorBindingDerived)result;
            Assert.Equal("derived", derived.Kind);
            Assert.Equal("test", derived.Name);
        }

        [Fact]
        public void TypeDiscriminatorBinding_Serialization_SkipsProperty()
        {
            var value = new DiscriminatorBindingDerived { Kind = "should_be_ignored", Name = "test" };
            string json = JsonSerializer.Serialize<DiscriminatorBindingBase>(value, PolymorphicTestsContext_Metadata.Default.DiscriminatorBindingBase);

            Assert.DoesNotContain("\"Kind\"", json);
            Assert.Contains("\"$type\"", json);
            Assert.Contains("\"Name\"", json);
        }

        [Fact]
        public void FallbackType_UnrecognizedDiscriminator_UsesFallbackType()
        {
            string json = """{"$type":"unknown_value","Data":"hello"}""";
            var result = JsonSerializer.Deserialize(json, PolymorphicTestsContext_Metadata.Default.FallbackBase);

            Assert.IsType<FallbackUnknown>(result);
            var unknown = (FallbackUnknown)result;
            Assert.Equal("unknown_value", unknown.TypeId);
            Assert.Equal("hello", unknown.Data);
        }

        [Fact]
        public void FallbackType_RecognizedDiscriminator_UsesCorrectType()
        {
            string json = """{"$type":"known","Data":"hello"}""";
            var result = JsonSerializer.Deserialize(json, PolymorphicTestsContext_Metadata.Default.FallbackBase);

            Assert.IsType<FallbackKnown>(result);
        }

        [Fact]
        public void IgnoreUnrecognized_BaseTypeGetsDiscriminator()
        {
            string json = """{"$type":"not_declared","Data":"hello"}""";
            var result = JsonSerializer.Deserialize(json, PolymorphicTestsContext_Metadata.Default.IgnoreUnrecognizedWithBindingBase);

            Assert.IsType<IgnoreUnrecognizedWithBindingBase>(result);
            Assert.Equal("not_declared", result!.DiscriminatorValue);
            Assert.Equal("hello", result.Data);
        }
    }

    public sealed partial class PolymorphicTests_SourceGen_Default
    {
        [JsonSerializable(typeof(DiscriminatorBindingBase))]
        [JsonSerializable(typeof(DiscriminatorBindingDerived))]
        [JsonSerializable(typeof(FallbackBase))]
        [JsonSerializable(typeof(FallbackKnown))]
        [JsonSerializable(typeof(FallbackUnknown))]
        [JsonSerializable(typeof(IgnoreUnrecognizedWithBindingBase))]
        [JsonSerializable(typeof(IgnoreUnrecognizedWithBindingDerived))]
        internal sealed partial class PolymorphicTestsContext_Default : JsonSerializerContext
        {
        }

        [Fact]
        public void TypeDiscriminatorBinding_RecognizedType_BindsDiscriminator()
        {
            string json = """{"$type":"derived","Name":"test"}""";
            var result = JsonSerializer.Deserialize(json, PolymorphicTestsContext_Default.Default.DiscriminatorBindingBase);

            Assert.IsType<DiscriminatorBindingDerived>(result);
            var derived = (DiscriminatorBindingDerived)result;
            Assert.Equal("derived", derived.Kind);
            Assert.Equal("test", derived.Name);
        }

        [Fact]
        public void FallbackType_UnrecognizedDiscriminator_UsesFallbackType()
        {
            string json = """{"$type":"unknown_value","Data":"hello"}""";
            var result = JsonSerializer.Deserialize(json, PolymorphicTestsContext_Default.Default.FallbackBase);

            Assert.IsType<FallbackUnknown>(result);
            var unknown = (FallbackUnknown)result;
            Assert.Equal("unknown_value", unknown.TypeId);
            Assert.Equal("hello", unknown.Data);
        }
    }

    // Shared test type hierarchies for source gen polymorphic tests
    [JsonPolymorphic]
    [JsonDerivedType(typeof(DiscriminatorBindingDerived), "derived")]
    public class DiscriminatorBindingBase
    {
        public string? Data { get; set; }
    }

    public class DiscriminatorBindingDerived : DiscriminatorBindingBase
    {
        [JsonTypeDiscriminator]
        public string? Kind { get; set; }

        public string? Name { get; set; }
    }

    [JsonPolymorphic(FallbackType = typeof(FallbackUnknown), UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
    [JsonDerivedType(typeof(FallbackKnown), "known")]
    public class FallbackBase
    {
        public string? Data { get; set; }
    }

    public class FallbackKnown : FallbackBase { }

    public class FallbackUnknown : FallbackBase
    {
        [JsonTypeDiscriminator]
        public string? TypeId { get; set; }
    }

    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true)]
    [JsonDerivedType(typeof(IgnoreUnrecognizedWithBindingDerived), "known")]
    public class IgnoreUnrecognizedWithBindingBase
    {
        [JsonTypeDiscriminator]
        public string? DiscriminatorValue { get; set; }

        public string? Data { get; set; }
    }

    public class IgnoreUnrecognizedWithBindingDerived : IgnoreUnrecognizedWithBindingBase { }
}
