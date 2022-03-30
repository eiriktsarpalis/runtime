﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public class PolymorphicTypeDiscriminatorTests_String : PolymorphicTypeDiscriminatorTests
    {
        public PolymorphicTypeDiscriminatorTests_String() : base(JsonSerializerWrapper.StringSerializer)
        {
        }
    }

    public class PolymorphicTypeDiscriminatorTests_Async : PolymorphicTypeDiscriminatorTests
    {
        public PolymorphicTypeDiscriminatorTests_Async() : base(JsonSerializerWrapper.AsyncStreamSerializer)
        {
        }
    }

    public class PolymorphicTypeDiscriminatorTests_AsyncWithSmallBuffer : PolymorphicTypeDiscriminatorTests
    {
        public PolymorphicTypeDiscriminatorTests_AsyncWithSmallBuffer() : base(JsonSerializerWrapper.AsyncStreamSerializerWithSmallBuffer)
        {
        }
    }

    public abstract class PolymorphicTypeDiscriminatorTests : SerializerTests
    {
        public PolymorphicTypeDiscriminatorTests(JsonSerializerWrapper serializerUnderTest)
            : base(serializerUnderTest)
        {
        }

        private readonly static JsonSerializerOptions s_jsonSerializerOptionsPreserveRefs = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve
        };

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson))]
        public async Task HappyPath_Serialization_AsRootValue(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(value);
            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson))]
        public async Task HappyPath_Serialization_AsPoco(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new { Value = value });
            JsonTestHelper.AssertJsonEqual($@"{{""Value"":{expectedJson}}}", actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson))]
        public async Task HappyPath_Serialization_AsListElement(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new List<HappyPath_BaseClass> { value });
            JsonTestHelper.AssertJsonEqual($"[{expectedJson}]", actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson))]
        public async Task HappyPath_Serialization_AsDictionaryValue(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new Dictionary<string, HappyPath_BaseClass> { ["key"] = value });
            JsonTestHelper.AssertJsonEqual(@$"{{""key"":{expectedJson}}}", actualJson);
        }

        [Fact]
        public async Task HappyPath_Serialization_MultiValueArray()
        {
            // construct input array and expected json from list of inputs
            var values = GetHappyPathValuesAndExpectedJson()
                .Select(array => (value: (HappyPath_BaseClass)array[0], json: (string)array[1]))
                .ToArray();

            List<HappyPath_BaseClass> inputList = values.Select(x => x.value).ToList();
            string expectedJson = $"[{string.Join(",", values.Select(x => x.json))}]";

            string actualJson = await Serializer.SerializeWrapper(inputList);

            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        public static IEnumerable<object[]> GetHappyPathValuesAndExpectedJson()
        {
            yield return WrapArgs(new HappyPath_BaseClass { Number = 42 }, @"{""Number"":42}");

            yield return WrapArgs(
                new HappyPath_DerivedClass1 { Number = 42, Boolean1 = true },
                @"{""$type"":""derived1"",""Number"":42,""Boolean1"":true}");

            yield return WrapArgs(
                new HappyPath_DerivedClass2 { Number = 42, Boolean2 = true },
                @"{""Number"":42}");

            yield return WrapArgs(
                new HappyPath_DerivedEnumerable(),
                @"{""$type"":""enumerable"",""$values"":[0,1,2,3,4]}");

            yield return WrapArgs(
                new HappyPath_DerivedWithCustomConverter(),
                "{}");

            yield return WrapArgs(
                new HappyPath_DerivedDerivedClass1 { Number = 42, Boolean1 = true, String1 = "str" },
                @"{""$type"":""derived1"",""Number"":42,""Boolean1"":true}");

            yield return WrapArgs(
                new HappyPath_DerivedDerivedClass2 { Number = 42, Boolean2 = true, String2 = "str" },
                @"{""$type"":""derived2"",""Number"":42,""Boolean2"":true,""String2"":""str""}");

            static object[] WrapArgs(HappyPath_BaseClass value, string expectedJson)
                => new object[] { value, expectedJson };
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Deserialization_AsRootValue(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<HappyPath_BaseClass>(json));
                return;
            }

            var result = await Serializer.DeserializeWrapper<HappyPath_BaseClass>(json);

            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Deserialization_AsPoco(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            string input = @$"{{""Value"":{json}}}";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<GenericPoco<HappyPath_BaseClass>>(input));
                return;
            }

            var result = await Serializer.DeserializeWrapper<GenericPoco<HappyPath_BaseClass>>(input);

            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result.Value);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Deserialization_AsListElement(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            string input = $"[{json}]";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<List<HappyPath_BaseClass>>(input));
                return;
            }

            var result = await Serializer.DeserializeWrapper<List<HappyPath_BaseClass>>(input);

            Assert.Equal(1, result.Count);
            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result[0]);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Deserialization_AsDictionaryValue(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            string input = @$"{{""key"":{json}}}";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<Dictionary<string, HappyPath_BaseClass>>(input));
                return;
            }

            var result = await Serializer.DeserializeWrapper<Dictionary<string, HappyPath_BaseClass>>(input);

            Assert.Equal(1, result.Count);
            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result["key"]);
        }

        [Fact]
        public async Task HappyPath_Deserialization_MultiValueArray()
        {
            // construct input array and expected json from list of inputs
            var values = GetHappyPathJsonAndExpectedDeserializationType()
                .Select(array => (json: (string)array[0], expectedValue: (HappyPath_BaseClass)array[1]))
                .Where(x => x.expectedValue is not null) // null denotes non-deserializable values
                .ToArray();

            List<HappyPath_BaseClass> expectedValues = values.Select(x => x.expectedValue).ToList();
            string json = $"[{string.Join(",", values.Select(x => x.json))}]";

            List<HappyPath_BaseClass> actualValues = await Serializer.DeserializeWrapper<List<HappyPath_BaseClass>>(json);

            Assert.Equal(expectedValues.Count, actualValues.Count);
            for (int i = 0; i < expectedValues.Count; i++)
            {
                HappyPath_BaseClass.AssertEqual(expectedValues[i], actualValues[i]);
            }
        }

        public static IEnumerable<object[]> GetHappyPathJsonAndExpectedDeserializationType()
        {
            yield return WrapArgs(@"{""Number"":42}", new HappyPath_BaseClass { Number = 42 });

            yield return WrapArgs(
                @"{""$type"":""derived1"",""Number"":42,""Boolean1"":true, ""SomeOtherPropertyThatWeIgnore"" : ""longvalue aasdfasdfasdasd  asdasd asd as""}",
                new HappyPath_DerivedClass1 { Number = 42, Boolean1 = true }
                );

            yield return WrapArgs(
                @"{""$type"":""enumerable"",""$values"":[0,1,2,3,4]}",
                null // deserialization throws NotSupportedException due to derived type not being deserializable
                );

            yield return WrapArgs(
                @"{""$type"":""derived2"",""Number"":42,""Boolean2"":true,""String2"":""str""}",
                new HappyPath_DerivedDerivedClass2 { Number = 42, Boolean2 = true, String2 = "str" }
                );

            yield return WrapArgs(
                @"{""$type"":""custom"",""Number"":42}",
                null // deserialization throws NotSupportedException due to derived type not supporting metadata reads
            );

            static object[] WrapArgs(string json, HappyPath_BaseClass expectedDeserializedValue)
                => new object[] { json, expectedDeserializedValue };
        }

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson_CustomConfiguration))]
        public async Task HappyPath_Interface_SerializationWithCustomConfig_AsRootValue(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(value, s_optionsWithCustomKnownTypeConfiguration);
            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson_CustomConfiguration))]
        public async Task HappyPath_Interface_SerializationWithCustomConfig_AsPoco(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new { Value = value }, s_optionsWithCustomKnownTypeConfiguration);
            JsonTestHelper.AssertJsonEqual($@"{{""Value"":{expectedJson}}}", actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson_CustomConfiguration))]
        public async Task HappyPath_Interface_SerializationWithCustomConfig_AsListElement(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new List<HappyPath_BaseClass> { value }, s_optionsWithCustomKnownTypeConfiguration);
            JsonTestHelper.AssertJsonEqual($"[{expectedJson}]", actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathValuesAndExpectedJson_CustomConfiguration))]
        public async Task HappyPath_Interface_SerializationWithCustomConfig_AsDictionaryValue(HappyPath_BaseClass value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new Dictionary<string, HappyPath_BaseClass> { ["key"] = value }, s_optionsWithCustomKnownTypeConfiguration);
            JsonTestHelper.AssertJsonEqual(@$"{{""key"":{expectedJson}}}", actualJson);
        }

        public static IEnumerable<object[]> GetHappyPathValuesAndExpectedJson_CustomConfiguration()
        {
            yield return WrapArgs(new HappyPath_BaseClass { Number = 42 }, @"{""Number"":42}");

            yield return WrapArgs(
                new HappyPath_DerivedClass1 { Number = 42, Boolean1 = true },
                @"{""Number"":42}");

            yield return WrapArgs(
                new HappyPath_DerivedClass2 { Number = 42, Boolean2 = true },
                @"{""$type"":""derived_2"",""Number"":42,""Boolean2"":true}");

            yield return WrapArgs(
                new HappyPath_DerivedEnumerable(),
                @"{""$type"":""enum"",""$values"":[0,1,2,3,4]}");

            yield return WrapArgs(
                new HappyPath_DerivedWithCustomConverter(),
                @"{""Number"":0}");

            yield return WrapArgs(
                new HappyPath_DerivedDerivedClass1 { Number = 42, Boolean1 = true, String1 = "str" },
                @"{""$type"":""derived_1"",""Number"":42,""Boolean1"":true,""String1"":""str""}");

            yield return WrapArgs(
                new HappyPath_DerivedDerivedClass2 { Number = 42, Boolean2 = true, String2 = "str" },
                @"{""$type"":""derived_2"",""Number"":42,""Boolean2"":true}");

            static object[] WrapArgs(HappyPath_BaseClass value, string expectedJson) => new object[] { value, expectedJson };
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType_CustomConfiguration))]
        public async Task HappyPath_Interface_DeserializationWithCustomConfig_AsRootValue(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<HappyPath_BaseClass>(json, s_optionsWithCustomKnownTypeConfiguration));
                return;
            }

            var result = await Serializer.DeserializeWrapper<HappyPath_BaseClass>(json, s_optionsWithCustomKnownTypeConfiguration);

            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType_CustomConfiguration))]
        public async Task HappyPath_Interface_DeserializationWithCustomConfig_AsPoco(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            string input = @$"{{""Value"":{json}}}";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<GenericPoco<HappyPath_BaseClass>>(input, s_optionsWithCustomKnownTypeConfiguration));
                return;
            }

            var result = await Serializer.DeserializeWrapper<GenericPoco<HappyPath_BaseClass>>(input, s_optionsWithCustomKnownTypeConfiguration);

            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result.Value);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType_CustomConfiguration))]
        public async Task HappyPath_Interface_DeserializationWithCustomConfig_AsListElement(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            string input = $"[{json}]";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<List<HappyPath_BaseClass>>(input, s_optionsWithCustomKnownTypeConfiguration));
                return;
            }

            var result = await Serializer.DeserializeWrapper<List<HappyPath_BaseClass>>(input, s_optionsWithCustomKnownTypeConfiguration);

            Assert.Equal(1, result.Count);
            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result[0]);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathJsonAndExpectedDeserializationType_CustomConfiguration))]
        public async Task HappyPath_Interface_DeserializationWithCustomConfig_AsDictionaryValue(string json, HappyPath_BaseClass? expectedDeserializedValue)
        {
            string input = @$"{{""key"":{json}}}";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<Dictionary<string, HappyPath_BaseClass>>(input, s_optionsWithCustomKnownTypeConfiguration));
                return;
            }

            var result = await Serializer.DeserializeWrapper<Dictionary<string, HappyPath_BaseClass>>(input, s_optionsWithCustomKnownTypeConfiguration);

            Assert.Equal(1, result.Count);
            HappyPath_BaseClass.AssertEqual(expectedDeserializedValue, result["key"]);
        }

        public static IEnumerable<object[]> GetHappyPathJsonAndExpectedDeserializationType_CustomConfiguration()
        {
            yield return WrapArgs(@"{""Number"":42}", new HappyPath_BaseClass { Number = 42 });

            yield return WrapArgs(
                @"{""$type"":""derived_2"",""Number"":42,""Boolean2"":true}",
                new HappyPath_DerivedClass2 { Number = 42, Boolean2 = true }
            );

            yield return WrapArgs(
                @"{""$type"":""derived_1"",""Number"":42,""Boolean1"":true,""String1"":""str""}",
                new HappyPath_DerivedDerivedClass1 { Number = 42, Boolean1 = true, String1 = "str" }
            );

            yield return WrapArgs(
                @"{""$type"":""enum"",""$values"":[0,1,2,3,4]}",
                null // deserialization throws NotSupportedException due to derived type not being deserializable
            );

            static object[] WrapArgs(string json, HappyPath_BaseClass expectedDeserializedValue) => new object[] { json, expectedDeserializedValue };
        }

        private static JsonSerializerOptions s_optionsWithCustomKnownTypeConfiguration = new JsonSerializerOptions
        {
            PolymorphicTypeConfigurations =
            {
                new PolymorphicTypeConfiguration<HappyPath_BaseClass>("$type")
                    .WithKnownType<HappyPath_DerivedClass2>("derived_2")
                    .WithKnownType<HappyPath_DerivedDerivedClass1>("derived_1")
                    .WithKnownType<HappyPath_DerivedEnumerable>("enum")
            }
        };

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceValuesAndExpectedJson))]
        public async Task HappyPath_Interface_Serialization_AsRootValue(HappyPath_Interface_BaseInterface value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(value);
            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceValuesAndExpectedJson))]
        public async Task HappyPath_Interface_Serialization_AsPoco(HappyPath_Interface_BaseInterface value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new { Value = value });
            JsonTestHelper.AssertJsonEqual($@"{{""Value"":{expectedJson}}}", actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceValuesAndExpectedJson))]
        public async Task HappyPath_Interface_Serialization_AsListElement(HappyPath_Interface_BaseInterface value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new List<HappyPath_Interface_BaseInterface> { value });
            JsonTestHelper.AssertJsonEqual($"[{expectedJson}]", actualJson);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceValuesAndExpectedJson))]
        public async Task HappyPath_Interface_Serialization_AsDictionaryValue(HappyPath_Interface_BaseInterface value, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(new Dictionary<string, HappyPath_Interface_BaseInterface> { ["key"] = value });
            JsonTestHelper.AssertJsonEqual(@$"{{""key"":{expectedJson}}}", actualJson);
        }

        public static IEnumerable<object[]> GetHappyPathInterfaceValuesAndExpectedJson()
        {
            yield return WrapArgs(new HappyPath_Interface_DerivedClass1 { Number = 42 }, @"{""$type"":""derived1"",""Number"":42}");
            yield return WrapArgs(new HappyPath_Interface_DerivedClass2 { String = "str" }, "{}");
            yield return WrapArgs(
                new HappyPath_Interface_DerivedClassFromDerivedInterface { Boolean = true, Number = 42 },
                @"{""$type"":""derived_interface"",""Boolean"":true}");
            yield return WrapArgs(
                new HappyPath_Interface_DerivedStruct { Number = 42 },
                @"{""$type"":""derived_struct"",""Number"":42}");

            static object[] WrapArgs(HappyPath_Interface_BaseInterface value, string expectedJson) => new object[] { value, expectedJson };
        }

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Interface_Deserialization_AsRootValue(string json, HappyPath_Interface_BaseInterface? expectedDeserializedValue)
        {
            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<HappyPath_Interface_BaseInterface>(json));
                return;
            }

            var result = await Serializer.DeserializeWrapper<HappyPath_Interface_BaseInterface>(json);
            HappyPath_Interface_BaseInterfaceHelpers.AssertEqual(expectedDeserializedValue, result);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Interface_Deserialization_AsPoco(string json, HappyPath_Interface_BaseInterface? expectedDeserializedValue)
        {
            string input = @$"{{""Value"":{json}}}";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<GenericPoco<HappyPath_Interface_BaseInterface>>(input));
                return;
            }

            var result = await Serializer.DeserializeWrapper<GenericPoco<HappyPath_Interface_BaseInterface>>(input);
            HappyPath_Interface_BaseInterfaceHelpers.AssertEqual(expectedDeserializedValue, result.Value);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Interface_Deserialization_AsListElement(string json, HappyPath_Interface_BaseInterface? expectedDeserializedValue)
        {
            string input = $"[{json}]";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<List<HappyPath_Interface_BaseInterface>>(input));
                return;
            }

            var result = await Serializer.DeserializeWrapper<List<HappyPath_Interface_BaseInterface>>(input);

            Assert.Equal(1, result.Count);
            HappyPath_Interface_BaseInterfaceHelpers.AssertEqual(expectedDeserializedValue, result[0]);
        }

        [Theory]
        [MemberData(nameof(GetHappyPathInterfaceJsonAndExpectedDeserializationType))]
        public async Task HappyPath_Interface_Deserialization_AsDictionaryValue(string json, HappyPath_Interface_BaseInterface? expectedDeserializedValue)
        {
            string input = @$"{{""key"":{json}}}";

            if (expectedDeserializedValue is null)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.DeserializeWrapper<Dictionary<string, HappyPath_Interface_BaseInterface>>(input));
                return;
            }

            var result = await Serializer.DeserializeWrapper<Dictionary<string, HappyPath_Interface_BaseInterface>>(input);

            Assert.Equal(1, result.Count);
            HappyPath_Interface_BaseInterfaceHelpers.AssertEqual(expectedDeserializedValue, result["key"]);
        }

        public static IEnumerable<object[]> GetHappyPathInterfaceJsonAndExpectedDeserializationType()
        {
            yield return WrapArgs(@"{""$type"":""derived1"",""Number"":42}", new HappyPath_Interface_DerivedClass1 { Number = 42 });
            yield return WrapArgs(
                @"{""$type"":""derived_interface"",""Boolean"":true}",
                null // deserialization throws NotSupportedException due to derived type not being deserializable
            );
            yield return WrapArgs(
                @"{""$type"":""derived_struct"",""Number"":42}",
                new HappyPath_Interface_DerivedStruct { Number = 42 }
            );

            static object[] WrapArgs(string json, HappyPath_Interface_BaseInterface expectedDeserializedValue)
                => new object[] { json, expectedDeserializedValue };
        }

        [Theory]
        [InlineData(0, @"{""$type"":""zero""}")]
        [InlineData(1, @"{""$type"":""succ"", ""value"":{""$type"":""zero""}}")]
        [InlineData(3, @"{""$type"":""succ"", ""value"":{""$type"":""succ"",""value"":{""$type"":""succ"",""value"":{""$type"":""zero""}}}}")]
        public async Task SimpleRecursiveTypeSerialization(int size, string expectedJson)
        {
            Peano peano = Peano.FromInteger(size);
            string actualJson = await Serializer.SerializeWrapper(peano);
            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Theory]
        [InlineData(0, @"{""$type"":""zero""}")]
        [InlineData(1, @"{""$type"":""succ"", ""value"":{""$type"":""zero""}}")]
        [InlineData(3, @"{""$type"":""succ"", ""value"":{""$type"":""succ"",""value"":{""$type"":""succ"",""value"":{""$type"":""zero""}}}}")]
        public async Task SimpleRecursiveTypeDeserialization(int expectedSize, string json)
        {
            Peano expected = Peano.FromInteger(expectedSize);
            Peano actual = await Serializer.DeserializeWrapper<Peano>(json);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetGenericRecursiveTypeValues))]
        public async Task GenericRecursiveTypeSerialization<T>(BinTree<T> tree, string expectedJson)
        {
            string actualJson = await Serializer.SerializeWrapper(tree, BinTree<T>.Options);
            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Theory]
        [MemberData(nameof(GetGenericRecursiveTypeValues))]
        public async Task GenericRecursiveTypeDeserialization<T>(BinTree<T> expected, string json)
        {
            BinTree<T> actual = await Serializer.DeserializeWrapper<BinTree<T>>(json, BinTree<T>.Options);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task ReferencePreservationSerialization_SingleValue()
        {
            HappyPath_BaseClass value = new HappyPath_DerivedClass1 { Number = 42, Boolean1 = true };
            string expectedJson = @"{""$id"":""1"",""$type"":""derived1"",""Number"":42,""Boolean1"":true}";

            string actualJson = await Serializer.SerializeWrapper(value, s_jsonSerializerOptionsPreserveRefs);

            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Fact]
        public async Task ReferencePreservationDeserialization_SingleValue()
        {
            string json = @"{""$id"":""1"",""$type"":""derived1"",""Number"":42,""Boolean1"":true}";
            var expectedValue = new HappyPath_DerivedClass1 { Number = 42, Boolean1 = true };

            var actualValue = await Serializer.DeserializeWrapper<HappyPath_BaseClass>(json, s_jsonSerializerOptionsPreserveRefs);

            HappyPath_BaseClass.AssertEqual(expectedValue, actualValue);
        }

        [Fact]
        public async Task ReferencePreservationSerialization_RepeatingValue()
        {
            HappyPath_BaseClass value = new HappyPath_DerivedClass1 { Number = 42, Boolean1 = true };
            List<HappyPath_BaseClass> input = new() { value, value };
            string expectedJson =
                @"{""$id"":""1"", ""$values"":[
                  {""$id"":""2"",""$type"":""derived1"",""Number"":42,""Boolean1"":true},
                  {""$ref"":""2""}]
                }";

            string actualJson = await Serializer.SerializeWrapper(input, s_jsonSerializerOptionsPreserveRefs);

            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Fact]
        public async Task ReferencePreservationDeserialization_RepeatingValue()
        {
            string json =
                @"{""$id"":""1"", ""$values"":[
                  {""$id"":""2"",""$type"":""derived1"",""Number"":42,""Boolean1"":true}, 
                  {""$ref"":""2""}]
                }";

            HappyPath_BaseClass expectedValue = new HappyPath_DerivedClass1 { Number = 42, Boolean1 = true };

            var result = await Serializer.DeserializeWrapper<List<HappyPath_BaseClass>>(json, s_jsonSerializerOptionsPreserveRefs);

            Assert.Equal(2, result.Count);
            HappyPath_BaseClass.AssertEqual(expectedValue, result[0]);
            Assert.Same(result[0], result[1]);
        }

        public static IEnumerable<object[]> GetGenericRecursiveTypeValues()
        {
            yield return WrapArgs(new BinTree<int>.Leaf(), @"{""$type"":""leaf""}");
            yield return WrapArgs(
                new BinTree<bool>.Node(false,
                    new BinTree<bool>.Leaf(),
                    new BinTree<bool>.Leaf()),
                @"{""$type"":""node"",""value"":false,""left"":{""$type"":""leaf""},""right"":{""$type"":""leaf""}}");

            yield return WrapArgs(
                new BinTree<string>.Node("A",
                    new BinTree<string>.Leaf(),
                    new BinTree<string>.Node("B",
                        new BinTree<string>.Leaf(),
                        new BinTree<string>.Leaf())),
                @"{""$type"":""node"", ""value"":""A"",
                        ""left"":{""$type"":""leaf""},
                        ""right"":{""$type"":""node"", ""value"":""B"",
                                  ""left"":{""$type"":""leaf""},
                                  ""right"":{""$type"":""leaf""}}}");

            static object[] WrapArgs<T>(BinTree<T> value, string expectedJson) => new object[] { value, expectedJson };
        }

        [Theory]
        [InlineData("null")]
        [InlineData("0")]
        [InlineData("{}")]
        [InlineData("[]")]
        //[InlineData("\"invalid tag\"")] // invalid tags currently ignored. TODO decide on semantics
        //[InlineData("\"System.Diagnostics.Process\"")]
        public async Task InvalidTypeDiscriminator_ShouldBeIgnored(string invalidTypeId)
        {
            string json = @$"{{""$type"":{invalidTypeId},""Number"":42}}";
            await Assert.ThrowsAsync<JsonException>(async () => await Serializer.DeserializeWrapper<HappyPath_BaseClass>(json));
        }

        [Fact]
        public async Task ApplyTaggedPolymorphismToEnumerableTypes_Serialization()
        {
            var source = new int[] { 1, 2, 3 };
            var values = new IEnumerable<int>[] { source, new List<int>(source), new Queue<int>(source), ImmutableArray.Create(source) };

            string expectedJson =
                @"[ [1,2,3],
                    { ""$type"":""list"", ""$values"":[1,2,3]  },
                    { ""$type"":""set"", ""$values"":[1,2,3]   },
                    [1,2,3] ]";

            string actualJson = await Serializer.SerializeWrapper(values, s_optionsWithCollectionKnownTypes);

            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Fact]
        public async Task ApplyTaggedPolymorphismToEnumerableTypes_Deserialization()
        {
            var source = new int[] { 1, 2, 3 };
            var expectedValues = new IEnumerable<int>[] { /*source, */ new List<int>(source), new Queue<int>(source), new List<int>(source) };

            string json =
                // TODO negative test for array polymorphic serialization (array does not support metadata)
                //@"[ { ""$type"":""array"", ""$values"":[1,2,3] },
                @"[ { ""$type"":""list"", ""$values"":[1,2,3]  },
                    { ""$type"":""set"", ""$values"":[1,2,3]   },
                    [1,2,3] ]";

            var actualValues = await Serializer.DeserializeWrapper<IEnumerable<int>[]>(json, s_optionsWithCollectionKnownTypes);

            Assert.Equal(expectedValues.Length, actualValues.Length);
            for (int i = 0; i < expectedValues.Length; i++)
            {
                Assert.Equal(expectedValues[i], actualValues[i]);
                Assert.IsType(expectedValues[i].GetType(), actualValues[i]);
            }
        }

        [Fact]
        public async Task ApplyTaggedPolymorphismToDictionaryTypes_Serialization()
        {
            var values = new IEnumerable<KeyValuePair<int, object>>[]
            {
                new KeyValuePair<int, object>[] { new KeyValuePair<int, object>(0, 0) },
                new Dictionary<int, object> { [42] = false },
                ImmutableDictionary.Create<int, object>(),
                new SortedDictionary<int, object> { [0] = 1, [1] = 42 }
            };

            string expectedJson =
                @"[ [ { ""Key"":0, ""Value"":0 } ],
                    { ""$type"" : ""dictionary"", ""42"" : false },
                    { },
                    { ""$type"" : ""readOnlyDictionary"", ""0"" : 1, ""1"" : 42 } ]";

            string actualJson = await Serializer.SerializeWrapper(values, s_optionsWithCollectionKnownTypes);

            JsonTestHelper.AssertJsonEqual(expectedJson, actualJson);
        }

        [Fact]
        public async Task ApplyTaggedPolymorphismToDictionaryTypes_Deserialization()
        {
            string json =
                @"[ [ { ""Key"":0, ""Value"":0 } ],
                    { ""$type"" : ""dictionary"", ""42"" : false },
                    { ""$type"" : ""readOnlyDictionary"", ""0"" : 1, ""1"" : 42 } ]";
                    //{ ""$type"" : ""immutableDictionary"" }, // TODO negative test for ImmutableDictionary which doesn't allow metadata

            var expectedValues = new IEnumerable<KeyValuePair<int, object>>[]
            {
                new List<KeyValuePair<int, object>> { new KeyValuePair<int, object>(0, 0) },
                new Dictionary<int, object> { [42] = false },
                //ImmutableDictionary.Create<int, object>(),
                new Dictionary<int, object> { [0] = 1, [1] = 42 }
            };

            var actualValues = await Serializer.DeserializeWrapper<IEnumerable<KeyValuePair<int, object>>[]>(json, s_optionsWithCollectionKnownTypes);

            Assert.Equal(expectedValues.Length, actualValues.Length);
            for (int i = 0; i < expectedValues.Length; i++)
            {
                Assert.Equal(expectedValues[i].Select(x => x.Key), actualValues[i].Select(x => x.Key));
                Assert.Equal(expectedValues[i].Select(x => x.Value.ToString()), actualValues[i].Select(x => x.Value.ToString()));
                Assert.IsType(expectedValues[i].GetType(), actualValues[i]);
            }
        }

        private readonly static JsonSerializerOptions s_optionsWithCollectionKnownTypes = new JsonSerializerOptions
        {
            PolymorphicTypeConfigurations =
                {
                    new PolymorphicTypeConfiguration<IEnumerable<int>>("$type")
                        .WithKnownType<int[]>("array")
                        .WithKnownType<List<int>>("list")
                        .WithKnownType<Queue<int>>("set")
                    ,

                    new PolymorphicTypeConfiguration<IEnumerable<KeyValuePair<int, object>>>("$type")
                        .WithKnownType<Dictionary<int, object>>("dictionary")
                        .WithKnownType<ImmutableDictionary<int, object>>("immutableDictionary")
                        .WithKnownType<IReadOnlyDictionary<int, object>>("readOnlyDictionary")
                }
        };

        [Theory]
        [InlineData(typeof(InvalidConfig_KnownTypeIsInteger))]
        [InlineData(typeof(InvalidConfig_KnownTypeIsString))]
        [InlineData(typeof(InvalidConfig_KnownTypeNotASubclass))]
        public async Task KnownTypeWithInvalidTypeParameter_ShouldThrowArgumentException(Type type)
        {
            object value = Activator.CreateInstance(type);
            await Assert.ThrowsAsync<ArgumentException>(() => Serializer.SerializeWrapper(value));
        }

        [Fact]
        public async Task KnownTypeWithDuplicateType_ShouldThrowArgumentException()
        {
            var value = new InvalidConfig_DuplicateTypes();
            await Assert.ThrowsAsync<ArgumentException>(() => Serializer.SerializeWrapper(value));
        }

        [Fact]
        public async Task KnownTypeWithDuplicateTypeIds_ShouldThrowArgumentException()
        {
            var value = new InvalidConfig_DuplicateTypeIds();
            await Assert.ThrowsAsync<ArgumentException>(() => Serializer.SerializeWrapper(value));
        }

        [Fact]
        public void PolymorphicTypeDiscriminatorConfiguration_AddingKnownTypesAfterAssignmentToOptions_ShouldThrowInvalidOperationException()
        {
            var config = new PolymorphicTypeConfiguration(typeof(HappyPath_BaseClass), "$type");
            config.WithKnownType(typeof(HappyPath_DerivedClass1), "derived1");
            _ = new JsonSerializerOptions { PolymorphicTypeConfigurations = { config } };
            Assert.Throws<InvalidOperationException>(() => config.WithKnownType(typeof(HappyPath_DerivedClass2), "derived2"));
        }

        // TODO add test coverage for polymorphism without discriminators

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(object))]
        [InlineData(typeof(BinTree<>))]
        public void KnownTypeConfiguration_InvalidBaseType_ShouldThrowArgumentException(Type baseType)
        {
            Assert.Throws<ArgumentException>(() => new PolymorphicTypeConfiguration(baseType, "$type"));
        }

        [Theory]
        [InlineData(typeof(HappyPath_BaseClass), typeof(int))]
        [InlineData(typeof(HappyPath_BaseClass), typeof(string))]
        [InlineData(typeof(HappyPath_BaseClass), typeof(Guid))]
        [InlineData(typeof(HappyPath_BaseClass), typeof(object))]
        [InlineData(typeof(HappyPath_BaseClass), typeof(BinTree<>))]
        [InlineData(typeof(HappyPath_BaseClass), typeof(HappyPath_BaseClass))]
        [InlineData(typeof(HappyPath_DerivedClass1), typeof(HappyPath_BaseClass))]
        public void KnownTypeConfiguration_InvalidKnownType_ShouldThrowArgumentException(Type baseType, Type knownType)
        {
            var config = new PolymorphicTypeConfiguration(baseType, "$type");
            Assert.Throws<ArgumentException>(() => config.WithKnownType(knownType, "knownTypeId"));
        }

        [Fact]
        public void KnownTypeConfiguration_DuplicateTypeId_ShouldThrowArgumentException()
        {
            var config = new PolymorphicTypeConfiguration(typeof(HappyPath_BaseClass), "$type");
            config.WithKnownType(typeof(HappyPath_DerivedClass1), "id1");
            Assert.Throws<ArgumentException>(() => config.WithKnownType(typeof(HappyPath_DerivedClass2), "id1"));
        }

        [Fact]
        public async Task KnownTypeConfiguration_ConflictingDiscriminators_ShouldThrowNotSupportedExceptions()
        {
            var options = new JsonSerializerOptions
            {
                PolymorphicTypeConfigurations =
                {
                    new PolymorphicTypeConfiguration<IEnumerable<int>>("$type")
                        .WithKnownType<ICollection<int>>("collection")
                        .WithKnownType<IReadOnlyCollection<int>>("readonlycollection")
                }
            };

            IEnumerable<int> value = new List<int>(); // implements both ICollection<int> and IReadOnlyCollection<int>

            await Assert.ThrowsAsync<NotSupportedException>(() => Serializer.SerializeWrapper(value, options));
        }

        //----------

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(HappyPath_DerivedClass1), "derived1")]
        [JsonKnownType(typeof(HappyPath_DerivedDerivedClass2), "derived2")]
        [JsonKnownType(typeof(HappyPath_DerivedEnumerable), "enumerable")]
        [JsonKnownType(typeof(HappyPath_DerivedWithCustomConverter), "custom")]
        public class HappyPath_BaseClass
        {
            public int Number { get; set; }

            public static void AssertEqual(HappyPath_BaseClass expected, HappyPath_BaseClass actual)
            {
                Type expectedType = expected.GetType();
                Assert.IsType(expectedType, actual);

                // check for property equality
                foreach (var propInfo in expectedType.GetProperties())
                {
                    Assert.Equal(propInfo.GetValue(expected), propInfo.GetValue(actual));
                }
            }
        }

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(HappyPath_DerivedDerivedClass1), "derived_derived1")]
        public class HappyPath_DerivedClass1 : HappyPath_BaseClass
        {
            public bool Boolean1 { get; set; }
        }

        public class HappyPath_DerivedClass2 : HappyPath_BaseClass
        {
            public bool Boolean2 { get; set; }
        }

        public class HappyPath_DerivedEnumerable : HappyPath_BaseClass, IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator()
            {
                for (int i = 0; i < 5; i++) yield return i;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [JsonConverter(typeof(Converter))]
        public class HappyPath_DerivedWithCustomConverter : HappyPath_BaseClass
        {
            public class Converter : JsonConverter<HappyPath_DerivedWithCustomConverter>
            {
                public override HappyPath_DerivedWithCustomConverter? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    reader.Skip();
                    return new HappyPath_DerivedWithCustomConverter();
                }

                public override void Write(Utf8JsonWriter writer, HappyPath_DerivedWithCustomConverter value, JsonSerializerOptions options)
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }
            }
        }

        public class HappyPath_DerivedDerivedClass1 : HappyPath_DerivedClass1
        {
            public string String1 { get; set; }
        }

        public class HappyPath_DerivedDerivedClass2 : HappyPath_DerivedClass2
        {
            public string String2 { get; set; }

            [JsonExtensionData]
            public Dictionary<string, object>? ExtensionData { get; set; }
        }

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(HappyPath_Interface_DerivedClass1), "derived1")]
        [JsonKnownType(typeof(HappyPath_Interface_DerivedInterface), "derived_interface")]
        [JsonKnownType(typeof(HappyPath_Interface_DerivedStruct), "derived_struct")]
        public interface HappyPath_Interface_BaseInterface { }

        public static class HappyPath_Interface_BaseInterfaceHelpers
        {
            public static void AssertEqual(HappyPath_Interface_BaseInterface expected, HappyPath_Interface_BaseInterface actual)
            {
                if (expected is null)
                {
                    Assert.Equal(expected, actual);
                    return;
                }

                Type expectedType = expected.GetType();
                Assert.IsType(expectedType, actual);

                // check for property equality
                foreach (var propInfo in expectedType.GetProperties())
                {
                    Assert.Equal(propInfo.GetValue(expected), propInfo.GetValue(actual));
                }
            }
        }


        public class HappyPath_Interface_DerivedClass1 : HappyPath_Interface_BaseInterface
        {
            public int Number { get; set; }
        }

        public class HappyPath_Interface_DerivedClass2 : HappyPath_Interface_BaseInterface
        {
            public string String { get; set; }
        }

        public interface HappyPath_Interface_DerivedInterface : HappyPath_Interface_BaseInterface
        {
            public bool Boolean { get; set; }
        }

        public class HappyPath_Interface_DerivedClassFromDerivedInterface : HappyPath_Interface_DerivedInterface
        {
            public bool Boolean { get; set; }
            public int Number { get; set; }
        }

        public struct HappyPath_Interface_DerivedStruct : HappyPath_Interface_BaseInterface
        {
            public int Number { get; set; }
        }

        /// <summary>A Peano arithmetic encoding.</summary>
        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(Zero), "zero")]
        [JsonKnownType(typeof(Succ), "succ")]
        public abstract record Peano
        {
            public static Peano FromInteger(int value) => value == 0 ? new Zero() : new Succ(FromInteger(value - 1));

            public record Zero : Peano;
            public record Succ(Peano value) : Peano;
        }

        public abstract record BinTree<T>
        {
            public record Leaf : BinTree<T>;
            public record Node(T value, BinTree<T> left, BinTree<T> right) : BinTree<T>;

            public static JsonSerializerOptions Options { get; } =
                new JsonSerializerOptions
                {
                    PolymorphicTypeConfigurations =
                    {
                        new PolymorphicTypeConfiguration<BinTree<T>>("TODO: fix passing custom names")
                            .WithKnownType<Leaf>("leaf")
                            .WithKnownType<Node>("node")
                    }
                };
        }

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(int), "integer")]
        public class InvalidConfig_KnownTypeIsInteger
        {
        }

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(int), "string")]
        public class InvalidConfig_KnownTypeIsString
        {
        }

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(HappyPath_BaseClass), "baseclass")]
        public class InvalidConfig_KnownTypeNotASubclass
        {
        }

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(Subtype), "A")]
        [JsonKnownType(typeof(Subtype), "B")]
        public class InvalidConfig_DuplicateTypes
        {
            public class Subtype : InvalidConfig_DuplicateTypes { }
        }

        [JsonPolymorphicType("$type")]
        [JsonKnownType(typeof(A), "duplicateId")]
        [JsonKnownType(typeof(B), "duplicateId")]
        public class InvalidConfig_DuplicateTypeIds
        {
            public class A : InvalidConfig_DuplicateTypes { }
            public class B : InvalidConfig_DuplicateTypes { }
        }

        public class GenericPoco<T>
        {
            public T Value { get; set; }
        }
    }
}
