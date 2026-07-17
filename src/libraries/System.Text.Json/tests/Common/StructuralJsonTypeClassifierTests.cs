// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public abstract class StructuralJsonTypeClassifierTests(JsonSerializerWrapper serializerUnderTest) : SerializerTests(serializerUnderTest)
    {
        private readonly JsonSerializerOptions _options = new(serializerUnderTest.DefaultOptions);
        private readonly JsonSerializerOptions _caseInsensitiveOptions = new(serializerUnderTest.DefaultOptions)
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly JsonSerializerOptions _disallowUnmappedOptions = new(serializerUnderTest.DefaultOptions)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        private readonly JsonSerializerOptions _numberFromStringOptions = new(serializerUnderTest.DefaultOptions)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        [Fact]
        public async Task StructuralClassifier_DistinguishesObjectProperties()
        {
            PetUnion? dog = await Serializer.DeserializeWrapper<PetUnion>("""{"Name":"Rex","Breed":"Labrador"}""", _options);
            Assert.NotNull(dog);
            Dog dogValue = Assert.IsType<Dog>(GetUnionValue(dog));
            Assert.Equal("Rex", dogValue.Name);
            Assert.Equal("Labrador", dogValue.Breed);

            PetUnion? cat = await Serializer.DeserializeWrapper<PetUnion>("""{"Name":"Misty","Lives":9}""", _options);
            Assert.NotNull(cat);
            Cat catValue = Assert.IsType<Cat>(GetUnionValue(cat));
            Assert.Equal("Misty", catValue.Name);
            Assert.Equal(9, catValue.Lives);
        }

        [Theory]
        [InlineData("42", typeof(int))]
        [InlineData("\"text\"", typeof(string))]
        [InlineData("true", typeof(bool))]
        [InlineData("[1,2,3]", typeof(List<int>))]
        [InlineData("""{"Name":"Rex","Breed":"Labrador"}""", typeof(Dog))]
        public async Task StructuralClassifier_DistinguishesJsonValueTypes(string json, Type expectedType)
        {
            UniqueShapeUnion? result = await Serializer.DeserializeWrapper<UniqueShapeUnion>(json, _options);
            Assert.NotNull(result);
            Assert.IsType(expectedType, GetUnionValue(result));
        }

        [Fact]
        public async Task StructuralClassifier_DistinguishesArrayFromDictionary()
        {
            ArrayOrDictionaryUnion? array = await Serializer.DeserializeWrapper<ArrayOrDictionaryUnion>("[1,2,3]", _options);
            Assert.NotNull(array);
            Assert.Equal([1, 2, 3], Assert.IsType<List<int>>(GetUnionValue(array)));

            ArrayOrDictionaryUnion? dictionary = await Serializer.DeserializeWrapper<ArrayOrDictionaryUnion>("""{"one":1,"two":2}""", _options);
            Assert.NotNull(dictionary);
            Assert.Equal(2, Assert.IsType<Dictionary<string, int>>(GetUnionValue(dictionary))["two"]);
        }

        [Fact]
        public async Task StructuralClassifier_PrefersObjectShapeOverDictionaryWhenPropertiesMatch()
        {
            ObjectOrDictionaryUnion? result = await Serializer.DeserializeWrapper<ObjectOrDictionaryUnion>("""{"X":1,"Y":2}""", _options);
            Assert.NotNull(result);
            Point point = Assert.IsType<Point>(GetUnionValue(result));
            Assert.Equal(1, point.X);
            Assert.Equal(2, point.Y);
        }

        [Fact]
        public async Task StructuralClassifier_UsesJsonTypeInfoPropertyNames()
        {
            RenamedPropertyUnion? result = await Serializer.DeserializeWrapper<RenamedPropertyUnion>("""{"kind":"special"}""", _options);
            Assert.NotNull(result);
            RenamedPropertyCase value = Assert.IsType<RenamedPropertyCase>(GetUnionValue(result));
            Assert.Equal("special", value.Kind);
        }

        [Fact]
        public async Task StructuralClassifier_UsesCaseInsensitivePropertyNames()
        {
            PetUnion? dog = await Serializer.DeserializeWrapper<PetUnion>("""{"\u006eAME":"Rex","breed":"Labrador"}""", _caseInsensitiveOptions);
            Assert.NotNull(dog);
            Dog dogValue = Assert.IsType<Dog>(GetUnionValue(dog));
            Assert.Equal("Rex", dogValue.Name);
            Assert.Equal("Labrador", dogValue.Breed);

            UnicodePropertyUnion? unicode = await Serializer.DeserializeWrapper<UnicodePropertyUnion>("""{"\u00E5ngstr\u00F6m":1}""", _caseInsensitiveOptions);
            Assert.NotNull(unicode);
            Assert.Equal(1, Assert.IsType<UnicodePropertyCase>(GetUnionValue(unicode)).Value);
        }

        [Fact]
        public async Task StructuralClassifier_UsesNumberHandlingMetadataWithoutInspectingStringContent()
        {
            NumberHandlingUnion? result = await Serializer.DeserializeWrapper<NumberHandlingUnion>("\"42\"", _numberFromStringOptions);
            Assert.NotNull(result);
            Assert.Equal(42, Assert.IsType<int>(GetUnionValue(result)));

            await Assert.ThrowsAsync<JsonException>(
                () => Serializer.DeserializeWrapper<NumberHandlingUnion>("\"not-a-number\"", _numberFromStringOptions));
        }

        [Fact]
        public async Task StructuralClassifier_RequiresPolymorphicTypeDiscriminator()
        {
            PolymorphicOrStringUnion? cat = await Serializer.DeserializeWrapper<PolymorphicOrStringUnion>("""{"$type":"cat","Name":"Misty"}""", _disallowUnmappedOptions);
            Assert.NotNull(cat);
            Assert.Equal("Misty", Assert.IsType<PolyCat>(GetUnionValue(cat)).Name);

            await Assert.ThrowsAsync<JsonException>(
                () => Serializer.DeserializeWrapper<PolymorphicOrStringUnion>("""{"Name":"Misty"}""", _options));

            await Assert.ThrowsAsync<JsonException>(
                () => Serializer.DeserializeWrapper<PolymorphicOrStringUnion>("""{"$TYPE":"cat","Name":"Misty"}""", _caseInsensitiveOptions));
        }

        [Fact]
        public async Task StructuralClassifier_ExtensionDataOverridesGlobalDisallowUnmappedMembers()
        {
            ExtensionDataUnion? result = await Serializer.DeserializeWrapper<ExtensionDataUnion>(
                """{"Id":42,"Additional":"value"}""",
                _disallowUnmappedOptions);

            Assert.NotNull(result);
            ExtensionDataCase extensionDataCase = Assert.IsType<ExtensionDataCase>(GetUnionValue(result));
            Assert.Equal(42, extensionDataCase.Id);
            Assert.Equal("value", extensionDataCase.ExtensionData["Additional"].GetString());
        }

        [Fact]
        public async Task StructuralClassifier_DistinguishesCaseSensitiveDiscriminatorNames()
        {
            CaseSensitiveDiscriminatorUnion? lower = await Serializer.DeserializeWrapper<CaseSensitiveDiscriminatorUnion>(
                """{"$type":"lower"}""",
                _caseInsensitiveOptions);
            Assert.NotNull(lower);
            Assert.IsType<LowercaseDiscriminatorDerived>(GetUnionValue(lower));

            CaseSensitiveDiscriminatorUnion? upper = await Serializer.DeserializeWrapper<CaseSensitiveDiscriminatorUnion>(
                """{"$TYPE":"upper"}""",
                _caseInsensitiveOptions);
            Assert.NotNull(upper);
            Assert.IsType<UppercaseDiscriminatorDerived>(GetUnionValue(upper));
        }

        [Theory]
        [InlineData("""{"Name":"Shared"}""")]
        [InlineData("{}")]
        [InlineData("true")]
        public async Task StructuralClassifier_AmbiguousOrUnsupportedPayloadThrows(string json)
        {
            await Assert.ThrowsAsync<JsonException>(
                () => Serializer.DeserializeWrapper<PetUnion>(json, _options));
        }

        [Theory]
        [InlineData("\"text\"", typeof(StringUnion))]
        [InlineData("42", typeof(NumericUnion))]
        [InlineData("true", typeof(BooleanUnion))]
        [InlineData("[]", typeof(ListUnion))]
        public async Task StructuralClassifier_RejectsAmbiguousNonObjectValueTypes(string json, Type unionType)
        {
            await Assert.ThrowsAsync<NotSupportedException>(
                () => Serializer.DeserializeWrapper(json, unionType, _options));
        }

        [Fact]
        public async Task StructuralClassifier_NumberHandlingCanIntroduceStringAmbiguity()
        {
            await Assert.ThrowsAsync<NotSupportedException>(
                () => Serializer.DeserializeWrapper<NumericStringUnion>("\"42\"", _numberFromStringOptions));
        }

        [Fact]
        public async Task StructuralClassifier_DoesNotRecursivelyClassifyNestedUnions()
        {
            await Assert.ThrowsAsync<NotSupportedException>(
                () => Serializer.DeserializeWrapper<OuterNestedUnion>("42", _options));
        }

        [Theory]
        [InlineData("""{"one":1}""", typeof(DictionaryUnion))]
        [InlineData("""{"Source":"sensor","Items":[{"Celsius":21.5}]}""", typeof(BatchUnion))]
        [InlineData("""{"Name":"Misty","Age":5}""", typeof(IdenticalPetUnion))]
        public async Task StructuralClassifier_RejectsIndistinguishableObjectPropertySets(string json, Type unionType)
        {
            await Assert.ThrowsAsync<NotSupportedException>(
                () => Serializer.DeserializeWrapper(json, unionType, _options));
        }

        [Fact]
        public async Task StructuralClassifier_RequiredPropertiesDisqualifyCandidate()
        {
            RequiredPropertyUnion? withRequired = await Serializer.DeserializeWrapper<RequiredPropertyUnion>("""{"Sku":"A123","Quantity":2}""", _options);
            Assert.NotNull(withRequired);
            Assert.IsType<Order>(GetUnionValue(withRequired));

            RequiredPropertyUnion? withoutRequired = await Serializer.DeserializeWrapper<RequiredPropertyUnion>("""{"Quantity":2}""", _options);
            Assert.NotNull(withoutRequired);
            Assert.IsType<Quote>(GetUnionValue(withoutRequired));
        }

        [Fact]
        public async Task StructuralClassifier_UnmappedMemberHandlingDisallowDisqualifiesCandidate()
        {
            UnmappedMemberUnion? loose = await Serializer.DeserializeWrapper<UnmappedMemberUnion>("""{"Id":1,"Extra":"x"}""", _options);
            Assert.NotNull(loose);
            Assert.IsType<Loose>(GetUnionValue(loose));
        }

        [Fact]
        public async Task StructuralClassifier_CountsPropertyNamePresenceOnlyOnce()
        {
            DuplicatePropertyNameUnion? result = await Serializer.DeserializeWrapper<DuplicatePropertyNameUnion>(
                """{"AOnly":1,"AOnly":2,"AOnly":3,"B1":4,"B2":5}""",
                _options);

            Assert.NotNull(result);
            Assert.IsType<DuplicatePropertyB>(GetUnionValue(result));
        }

        [Fact]
        public async Task StructuralClassifier_TracksMoreThan64PropertyNames()
        {
            LargePropertyUnion? result = await Serializer.DeserializeWrapper<LargePropertyUnion>(
                """{"P64":1}""",
                _options);

            Assert.NotNull(result);
            Assert.Equal(1, Assert.IsType<LargePropertyCase>(GetUnionValue(result)).P64);

            await Assert.ThrowsAsync<JsonException>(
                () => Serializer.DeserializeWrapper<LargePropertyUnion>(
                    """{"P64":1,"P64":2,"Q":3}""",
                    _options));
        }

        [Fact]
        public async Task StructuralClassifier_SupportsSelfReferentialCaseTypes()
        {
            TreeUnion? tree = await Serializer.DeserializeWrapper<TreeUnion>("""{"Value":1,"Left":{"Value":2,"Left":null,"Right":null},"Right":null}""", _options);
            Assert.NotNull(tree);
            Assert.IsType<TreeNode>(GetUnionValue(tree));
        }

        private static object? GetUnionValue<TUnion>(TUnion? union)
            where TUnion : struct, IUnion
        {
            Assert.True(union.HasValue);

            return union.GetValueOrDefault().Value;
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union PetUnion(Dog, Cat);

        public sealed class Dog
        {
            public string? Name { get; set; }
            public string? Breed { get; set; }
        }

        public sealed class Cat
        {
            public string? Name { get; set; }
            public int Lives { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union UniqueShapeUnion(int, string, bool, List<int>, Dog);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union ArrayOrDictionaryUnion(List<int>, Dictionary<string, int>);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union ObjectOrDictionaryUnion(Point, Dictionary<string, int>);

        public sealed class Point
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union UnicodePropertyUnion(UnicodePropertyCase, UnicodeOtherPropertyCase);

        public sealed class UnicodePropertyCase
        {
            [JsonPropertyName("\u00C5ngstr\u00F6m")]
            public int Value { get; set; }
        }

        public sealed class UnicodeOtherPropertyCase
        {
            public int Other { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union RenamedPropertyUnion(RenamedPropertyCase, OtherRenamedPropertyCase);

        public sealed class RenamedPropertyCase
        {
            [JsonPropertyName("kind")]
            public string? Kind { get; set; }
        }

        public sealed class OtherRenamedPropertyCase
        {
            public int Code { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union NumberHandlingUnion(int, bool);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union NumericStringUnion(int, string);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union StringUnion(Guid, string);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union NumericUnion(int, long);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union BooleanUnion(bool, bool?);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union ListUnion(List<int>, List<string>);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union DictionaryUnion(Dictionary<string, int>, Dictionary<string, string>);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union InnerScalarUnion(int, string);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union OuterNestedUnion(InnerScalarUnion, bool);

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union BatchUnion(Batch<TemperatureReading>, Batch<StatusReading>);

        public sealed class Batch<T>
        {
            public string? Source { get; set; }
            public List<T>? Items { get; set; }
        }

        public sealed class TemperatureReading
        {
            public double Celsius { get; set; }
        }

        public sealed class StatusReading
        {
            public bool IsOnline { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union PolymorphicOrStringUnion(PolyAnimal, string);

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(PolyCat), "cat")]
        [JsonDerivedType(typeof(PolyDog), "dog")]
        public record PolyAnimal;

        public sealed record PolyCat(string Name) : PolyAnimal;

        public sealed record PolyDog(string Breed) : PolyAnimal;

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union ExtensionDataUnion(ExtensionDataCase, ExtensionDataFallback);

        public sealed class ExtensionDataCase
        {
            public int Id { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
        }

        public sealed class ExtensionDataFallback
        {
            public int Id { get; set; }
            public string? Known { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union CaseSensitiveDiscriminatorUnion(LowercaseDiscriminatorBase, UppercaseDiscriminatorBase);

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(LowercaseDiscriminatorDerived), "lower")]
        public abstract record LowercaseDiscriminatorBase;

        public sealed record LowercaseDiscriminatorDerived : LowercaseDiscriminatorBase;

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$TYPE")]
        [JsonDerivedType(typeof(UppercaseDiscriminatorDerived), "upper")]
        public abstract record UppercaseDiscriminatorBase;

        public sealed record UppercaseDiscriminatorDerived : UppercaseDiscriminatorBase;

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union IdenticalPetUnion(IdenticalDog, IdenticalCat);

        public sealed class IdenticalDog
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public sealed class IdenticalCat
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union RequiredPropertyUnion(Order, Quote);

        public sealed class Order
        {
            public required string Sku { get; set; }
            public int Quantity { get; set; }
        }

        public sealed class Quote
        {
            public int Quantity { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union UnmappedMemberUnion(Strict, Loose);

        [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
        public sealed class Strict
        {
            public int Id { get; set; }
        }

        public sealed class Loose
        {
            public int Id { get; set; }
            public int? Note { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union DuplicatePropertyNameUnion(DuplicatePropertyA, DuplicatePropertyB);

        public sealed class DuplicatePropertyA
        {
            public int AOnly { get; set; }
        }

        public sealed class DuplicatePropertyB
        {
            public int B1 { get; set; }
            public int B2 { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union LargePropertyUnion(LargePropertyCase, SinglePropertyCase);

        public sealed class LargePropertyCase
        {
            public int P00 { get; set; }
            public int P01 { get; set; }
            public int P02 { get; set; }
            public int P03 { get; set; }
            public int P04 { get; set; }
            public int P05 { get; set; }
            public int P06 { get; set; }
            public int P07 { get; set; }
            public int P08 { get; set; }
            public int P09 { get; set; }
            public int P10 { get; set; }
            public int P11 { get; set; }
            public int P12 { get; set; }
            public int P13 { get; set; }
            public int P14 { get; set; }
            public int P15 { get; set; }
            public int P16 { get; set; }
            public int P17 { get; set; }
            public int P18 { get; set; }
            public int P19 { get; set; }
            public int P20 { get; set; }
            public int P21 { get; set; }
            public int P22 { get; set; }
            public int P23 { get; set; }
            public int P24 { get; set; }
            public int P25 { get; set; }
            public int P26 { get; set; }
            public int P27 { get; set; }
            public int P28 { get; set; }
            public int P29 { get; set; }
            public int P30 { get; set; }
            public int P31 { get; set; }
            public int P32 { get; set; }
            public int P33 { get; set; }
            public int P34 { get; set; }
            public int P35 { get; set; }
            public int P36 { get; set; }
            public int P37 { get; set; }
            public int P38 { get; set; }
            public int P39 { get; set; }
            public int P40 { get; set; }
            public int P41 { get; set; }
            public int P42 { get; set; }
            public int P43 { get; set; }
            public int P44 { get; set; }
            public int P45 { get; set; }
            public int P46 { get; set; }
            public int P47 { get; set; }
            public int P48 { get; set; }
            public int P49 { get; set; }
            public int P50 { get; set; }
            public int P51 { get; set; }
            public int P52 { get; set; }
            public int P53 { get; set; }
            public int P54 { get; set; }
            public int P55 { get; set; }
            public int P56 { get; set; }
            public int P57 { get; set; }
            public int P58 { get; set; }
            public int P59 { get; set; }
            public int P60 { get; set; }
            public int P61 { get; set; }
            public int P62 { get; set; }
            public int P63 { get; set; }
            public int P64 { get; set; }
        }

        public sealed class SinglePropertyCase
        {
            public int Q { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
        public union TreeUnion(TreeNode, Leaf);

        public sealed class TreeNode
        {
            public int Value { get; set; }
            public TreeNode? Left { get; set; }
            public TreeNode? Right { get; set; }
        }

        public sealed class Leaf
        {
            public int Value { get; set; }
        }
    }
}
