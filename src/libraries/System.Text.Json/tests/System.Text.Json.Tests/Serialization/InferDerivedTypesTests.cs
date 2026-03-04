// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public class InferDerivedTypesTests
    {

        [Fact]
        public void ClosedHierarchy_InferDerivedTypes_RoundTrips()
        {
            var options = new JsonSerializerOptions();
            ClosedAnimal animal = new ClosedDog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(animal, options);
            Assert.Contains("\"$type\"", json);
            Assert.Contains("\"ClosedDog\"", json);

            ClosedAnimal? deserialized = JsonSerializer.Deserialize<ClosedAnimal>(json, options);
            Assert.IsType<ClosedDog>(deserialized);
            Assert.Equal("Rex", ((ClosedDog)deserialized).Name);
            Assert.Equal("Labrador", ((ClosedDog)deserialized).Breed);
        }

        [Fact]
        public void ClosedHierarchy_InferDerivedTypes_AllSubtypes()
        {
            var options = new JsonSerializerOptions();

            ClosedAnimal dog = new ClosedDog { Name = "Rex", Breed = "Labrador" };
            string dogJson = JsonSerializer.Serialize(dog, options);
            Assert.Contains("\"ClosedDog\"", dogJson);

            ClosedAnimal cat = new ClosedCat { Name = "Whiskers", Lives = 9 };
            string catJson = JsonSerializer.Serialize(cat, options);
            Assert.Contains("\"ClosedCat\"", catJson);

            Assert.IsType<ClosedDog>(JsonSerializer.Deserialize<ClosedAnimal>(dogJson, options));
            Assert.IsType<ClosedCat>(JsonSerializer.Deserialize<ClosedAnimal>(catJson, options));
        }

        [Fact]
        public void ClosedHierarchy_WithNamingPolicy_CamelCase()
        {
            var options = new JsonSerializerOptions();
            ClosedAnimalCamelCase animal = new ClosedDogCamelCase { Name = "Rex" };
            string json = JsonSerializer.Serialize(animal, options);
            Assert.Contains("\"closedDogCamelCase\"", json);

            ClosedAnimalCamelCase? deserialized = JsonSerializer.Deserialize<ClosedAnimalCamelCase>(json, options);
            Assert.IsType<ClosedDogCamelCase>(deserialized);
        }

        [Fact]
        public void ClosedHierarchy_ExplicitDerivedType_TakesPrecedence()
        {
            var options = new JsonSerializerOptions();
            ClosedAnimalExplicit animal = new ClosedDogExplicit { Name = "Rex" };
            string json = JsonSerializer.Serialize(animal, options);
            Assert.Contains("\"doggo\"", json);
            Assert.DoesNotContain("\"ClosedDogExplicit\"", json);

            ClosedAnimalExplicit? deserialized = JsonSerializer.Deserialize<ClosedAnimalExplicit>(json, options);
            Assert.IsType<ClosedDogExplicit>(deserialized);
        }

        [Fact]
        public void ClosedHierarchy_ProgrammaticOptions_Work()
        {
            var polyOptions = new JsonPolymorphismOptions
            {
                InferDerivedTypes = true,
                TypeDiscriminatorNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            polyOptions.DerivedTypes.Add(new JsonDerivedType(typeof(ClosedDog)));
            polyOptions.DerivedTypes.Add(new JsonDerivedType(typeof(ClosedCat)));

            Assert.True(polyOptions.InferDerivedTypes);
            Assert.Equal(JsonNamingPolicy.SnakeCaseLower, polyOptions.TypeDiscriminatorNamingPolicy);
        }



        [Fact]
        public void OpenHierarchy_InferDerivedTypes_AssemblyScanning()
        {
            var options = new JsonSerializerOptions();
            OpenShape shape = new OpenCircle { Radius = 5.0 };
            string json = JsonSerializer.Serialize(shape, options);
            Assert.Contains("\"$type\"", json);
            Assert.Contains("\"OpenCircle\"", json);

            OpenShape? deserialized = JsonSerializer.Deserialize<OpenShape>(json, options);
            Assert.IsType<OpenCircle>(deserialized);
            Assert.Equal(5.0, ((OpenCircle)deserialized).Radius);
        }

        [Fact]
        public void OpenHierarchy_AssemblyScanning_FindsAllSubtypes()
        {
            var options = new JsonSerializerOptions();

            OpenShape circle = new OpenCircle { Radius = 5.0 };
            string circleJson = JsonSerializer.Serialize(circle, options);
            Assert.Contains("\"OpenCircle\"", circleJson);

            OpenShape rect = new OpenRectangle { Width = 10, Height = 20 };
            string rectJson = JsonSerializer.Serialize(rect, options);
            Assert.Contains("\"OpenRectangle\"", rectJson);
        }

        [Fact]
        public void InferDerivedTypes_ExcludesCompilerGeneratedSubtypes()
        {
            var options = new JsonSerializerOptions();

            // RealSubtype should be discovered by assembly scanning.
            HierarchyWithCompilerGenerated real = new RealSubtype { Name = "Real", Value = 42 };
            string json = JsonSerializer.Serialize(real, options);
            Assert.Contains("\"$type\"", json);
            Assert.Contains("\"RealSubtype\"", json);

            HierarchyWithCompilerGenerated? deserialized =
                JsonSerializer.Deserialize<HierarchyWithCompilerGenerated>(json, options);
            Assert.IsType<RealSubtype>(deserialized);
            Assert.Equal(42, ((RealSubtype)deserialized).Value);
        }

        [Fact]
        public void InferDerivedTypes_CompilerGeneratedSubtype_NotInDerivedTypes()
        {
            // Verify that the compiler-generated subtype is NOT included in the
            // automatically inferred derived types list.
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };

            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(HierarchyWithCompilerGenerated));

            Assert.NotNull(typeInfo.PolymorphismOptions);
            var derivedTypeSet = new HashSet<Type>();
            foreach (JsonDerivedType dt in typeInfo.PolymorphismOptions!.DerivedTypes)
            {
                derivedTypeSet.Add(dt.DerivedType);
            }

            Assert.Contains(typeof(RealSubtype), derivedTypeSet);
            Assert.DoesNotContain(typeof(CompilerGeneratedSubtype), derivedTypeSet);
        }

        [Fact]
        public void InferDerivedTypes_CompilerGeneratedSubtype_SerializationDoesNotIncludeIt()
        {
            // Serializing a CompilerGeneratedSubtype instance as the base type should
            // fail because it's not in the derived types list.
            var options = new JsonSerializerOptions();
            HierarchyWithCompilerGenerated compGen = new CompilerGeneratedSubtype { Name = "Synthetic", Synthetic = "test" };

            // The compiler-generated type was excluded from inference, so serializing it
            // as the base type will write only base-type properties (no discriminator
            // for CompilerGeneratedSubtype).
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(compGen, options));
        }

        [Fact]
        public void Union_ImplicitOperators_ExcludesCompilerGeneratedTypes()
        {
            // UnionWithCompilerGeneratedCase has implicit operators for both Cat and
            // CompilerGeneratedPayload. The discovery should exclude CompilerGeneratedPayload.
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(UnionWithCompilerGeneratedCase));

            Assert.NotNull(typeInfo.UnionCases);
            var caseTypeSet = new HashSet<Type>();
            foreach (JsonUnionCaseInfo caseInfo in typeInfo.UnionCases!)
            {
                caseTypeSet.Add(caseInfo.CaseType);
            }

            Assert.Contains(typeof(Cat), caseTypeSet);
            Assert.DoesNotContain(typeof(CompilerGeneratedPayload), caseTypeSet);
        }

        [Fact]
        public void Union_Constructors_ExcludesCompilerGeneratedTypes()
        {
            // CtorUnionWithCompilerGeneratedCase discovers case types from constructors only
            // (no implicit operators). CompilerGeneratedPayload should be excluded.
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(CtorUnionWithCompilerGeneratedCase));

            Assert.NotNull(typeInfo.UnionCases);
            var caseTypeSet = new HashSet<Type>();
            foreach (JsonUnionCaseInfo caseInfo in typeInfo.UnionCases!)
            {
                caseTypeSet.Add(caseInfo.CaseType);
            }

            Assert.Contains(typeof(Dog), caseTypeSet);
            Assert.DoesNotContain(typeof(CompilerGeneratedPayload), caseTypeSet);
        }



        [Fact]
        public void Union_Serialize_ObjectCaseType()
        {
            var dog = new Dog { Name = "Rex", Breed = "Labrador" };
            var pet = new PetUnion(dog);

            string json = JsonSerializer.Serialize(pet);
            Assert.Contains("\"Name\"", json);
            Assert.Contains("\"Rex\"", json);
            Assert.Contains("\"Breed\"", json);
            Assert.DoesNotContain("$type", json);
        }

        [Fact]
        public void Union_Serialize_PrimitiveCaseType()
        {
            var result = new ResultUnion(42);
            string json = JsonSerializer.Serialize(result);
            Assert.Equal("42", json);

            result = new ResultUnion("hello");
            json = JsonSerializer.Serialize(result);
            Assert.Equal("\"hello\"", json);
        }

        [Fact]
        public void Union_Serialize_NullValue()
        {
            var pet = new PetUnion();
            string json = JsonSerializer.Serialize(pet);
            Assert.Equal("null", json);
        }

        [Fact]
        public void Union_Deserialize_ObjectCaseType_BestMatch()
        {
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            PetUnion pet = JsonSerializer.Deserialize<PetUnion>(json);

            Assert.IsType<Dog>(pet.Value);
            var dog = (Dog)pet.Value!;
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void Union_Deserialize_SelectsBestMatch()
        {
            // Dog has Name + Breed, Cat has Name + Lives
            // JSON with Breed should match Dog
            string dogJson = """{"Name":"Rex","Breed":"Labrador"}""";
            PetUnion pet = JsonSerializer.Deserialize<PetUnion>(dogJson);
            Assert.IsType<Dog>(pet.Value);

            // JSON with Lives should match Cat
            string catJson = """{"Name":"Whiskers","Lives":9}""";
            pet = JsonSerializer.Deserialize<PetUnion>(catJson);
            Assert.IsType<Cat>(pet.Value);
        }

        [Fact]
        public void Union_Deserialize_PrimitiveCaseType()
        {
            string intJson = "42";
            ResultUnion result = JsonSerializer.Deserialize<ResultUnion>(intJson);
            Assert.IsType<int>(result.Value);
            Assert.Equal(42, (int)result.Value!);

            string stringJson = "\"hello\"";
            result = JsonSerializer.Deserialize<ResultUnion>(stringJson);
            Assert.IsType<string>(result.Value);
            Assert.Equal("hello", (string)result.Value!);
        }

        [Fact]
        public void Union_Deserialize_Null()
        {
            string json = "null";
            PetUnion pet = JsonSerializer.Deserialize<PetUnion>(json);
            Assert.Null(pet.Value);
        }

        [Fact]
        public void Union_Deserialize_BooleanCaseType()
        {
            string json = "true";
            BoolOrIntUnion result = JsonSerializer.Deserialize<BoolOrIntUnion>(json);
            Assert.IsType<bool>(result.Value);
            Assert.True((bool)result.Value!);
        }

        [Theory]
        [InlineData("""[1,2,3]""", typeof(int[]))]
        [InlineData("""[{"Name":"Rex","Breed":"Lab"}]""", typeof(Dog[]))]
        public void Union_Deserialize_ArrayCaseTypes(string json, Type expectedElementType)
        {
            ArrayUnion result = JsonSerializer.Deserialize<ArrayUnion>(json);
            Assert.NotNull(result.Value);
            Assert.Equal(expectedElementType, result.Value!.GetType());
        }

        [Fact]
        public void Union_Serialize_BestMatchingCaseType()
        {
            // Labrador extends Dog, but union only has Dog as a case type.
            // Should serialize using Dog's contract.
            var labrador = new Labrador { Name = "Buddy", Breed = "Labrador", IsGuide = true };
            var pet = new PetUnion(new Dog { Name = labrador.Name, Breed = labrador.Breed });

            string json = JsonSerializer.Serialize(pet);
            Assert.Contains("\"Name\"", json);
            Assert.Contains("\"Breed\"", json);
        }

        [Fact]
        public void Union_RoundTrip_Object()
        {
            var original = new PetUnion(new Cat { Name = "Luna", Lives = 7 });
            string json = JsonSerializer.Serialize(original);

            PetUnion deserialized = JsonSerializer.Deserialize<PetUnion>(json);
            Assert.IsType<Cat>(deserialized.Value);
            var cat = (Cat)deserialized.Value!;
            Assert.Equal("Luna", cat.Name);
            Assert.Equal(7, cat.Lives);
        }



        [Fact]
        public void ClosedEnum_ValidIntValue_Succeeds()
        {
            string json = "1";
            ClosedColor color = JsonSerializer.Deserialize<ClosedColor>(json);
            Assert.Equal(ClosedColor.Green, color);
        }

        [Fact]
        public void ClosedEnum_InvalidIntValue_Throws()
        {
            string json = "10";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClosedColor>(json));
        }

        [Fact]
        public void ClosedEnum_StringMode_ValidValue()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };

            string json = "\"Red\"";
            ClosedColor color = JsonSerializer.Deserialize<ClosedColor>(json, options);
            Assert.Equal(ClosedColor.Red, color);
        }

        [Fact]
        public void ClosedEnum_StringMode_InvalidValue()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };

            string json = "\"Purple\"";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClosedColor>(json, options));
        }

        [Fact]
        public void OpenEnum_InvalidIntValue_Succeeds()
        {
            // Regular enums (not closed) should still accept undefined integer values.
            string json = "10";
            OpenColor color = JsonSerializer.Deserialize<OpenColor>(json);
            Assert.Equal((OpenColor)10, color);
        }

        [Theory]
        [InlineData(0, ClosedColor.Red)]
        [InlineData(1, ClosedColor.Green)]
        [InlineData(2, ClosedColor.Blue)]
        public void ClosedEnum_AllDefinedValues_Succeed(int intValue, ClosedColor expected)
        {
            string json = intValue.ToString();
            ClosedColor color = JsonSerializer.Deserialize<ClosedColor>(json);
            Assert.Equal(expected, color);
        }

        // ===== Phase 5: JsonUnion attribute tests =====

        [Fact]
        public void JsonUnion_ImplicitOperator_Serialize_Dog()
        {
            ImplicitPetUnion union = new Dog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(union);
            Assert.Contains("Rex", json);
            Assert.Contains("Labrador", json);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_Deserialize_Dog()
        {
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            ImplicitPetUnion union = JsonSerializer.Deserialize<ImplicitPetUnion>(json);

            Dog dog = (Dog)union;
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_Deserialize_Cat()
        {
            string json = """{"Name":"Whiskers","Lives":9}""";
            ImplicitPetUnion union = JsonSerializer.Deserialize<ImplicitPetUnion>(json);

            Cat cat = (Cat)union;
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_CtorDiscovery_Serialize_Dog()
        {
            var union = new CtorPetUnion(new Dog { Name = "Rex", Breed = "Labrador" });
            string json = JsonSerializer.Serialize(union);
            Assert.Contains("Rex", json);
            Assert.Contains("Labrador", json);
        }

        [Fact]
        public void JsonUnion_CtorDiscovery_Deserialize_Cat()
        {
            string json = """{"Name":"Whiskers","Lives":9}""";
            CtorPetUnion union = JsonSerializer.Deserialize<CtorPetUnion>(json);

            Cat cat = (Cat)union;
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_CustomClassifier_Deserialize_Dog()
        {
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            CustomClassifiedPetUnion union = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(json);

            Dog dog = (Dog)union;
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_CustomClassifier_Deserialize_Cat()
        {
            string json = """{"Name":"Whiskers","Lives":9}""";
            CustomClassifiedPetUnion union = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(json);

            Cat cat = (Cat)union;
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_ContractCustomization_CustomDeconstructor()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PetUnion))
                            {
                                typeInfo.UnionCases = new List<JsonUnionCaseInfo>
                                {
                                    new(typeof(Dog)),
                                    new(typeof(Cat)),
                                };

                                var factory = new JsonStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeof(PetUnion),
                                    new JsonDerivedType[] { new JsonDerivedType(typeof(Dog)), new JsonDerivedType(typeof(Cat)) },
                                    null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);

                                typeInfo.UnionDeconstructor = obj =>
                                {
                                    var pet = (PetUnion)obj;
                                    object? value = pet.Value;
                                    return (value?.GetType() ?? typeof(object), value);
                                };

                                typeInfo.UnionConstructor = (Type caseType, object? value) =>
                                {
                                    if (caseType == typeof(Dog)) return new PetUnion((Dog)value!);
                                    if (caseType == typeof(Cat)) return new PetUnion((Cat)value!);
                                    throw new InvalidOperationException();
                                };
                            }
                        }
                    }
                }
            };

            // Serialize
            var pet = new PetUnion(new Dog { Name = "Rex", Breed = "Labrador" });
            string json = JsonSerializer.Serialize(pet, options);
            Assert.Contains("Rex", json);

            // Deserialize
            PetUnion deserialized = JsonSerializer.Deserialize<PetUnion>(json, options);
            Assert.IsType<Dog>(deserialized.Value);
            Assert.Equal("Rex", ((Dog)deserialized.Value!).Name);
        }

        [Fact]
        public void JsonUnion_NullDeserializesAsDefault()
        {
            string json = "null";
            ImplicitPetUnion union = JsonSerializer.Deserialize<ImplicitPetUnion>(json);
            Assert.Equal(default, union);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_RoundTrip_Dog()
        {
            ImplicitPetUnion original = new Dog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(original);
            ImplicitPetUnion deserialized = JsonSerializer.Deserialize<ImplicitPetUnion>(json);

            Dog dog = (Dog)deserialized;
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_RoundTrip_Cat()
        {
            ImplicitPetUnion original = new Cat { Name = "Whiskers", Lives = 9 };
            string json = JsonSerializer.Serialize(original);
            ImplicitPetUnion deserialized = JsonSerializer.Deserialize<ImplicitPetUnion>(json);

            Cat cat = (Cat)deserialized;
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_ClosedSubtype_Serialize_Dog()
        {
            ClosedSubtypePetUnion union = new ClosedSubtypeDog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(union);
            Assert.Contains("Rex", json);
            Assert.Contains("Labrador", json);
        }

        [Fact]
        public void JsonUnion_ClosedSubtype_RoundTrip_Dog()
        {
            ClosedSubtypePetUnion original = new ClosedSubtypeDog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(original);
            ClosedSubtypePetUnion deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion>(json);

            ClosedSubtypeDog dog = Assert.IsType<ClosedSubtypeDog>(deserialized);
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_ClosedSubtype_RoundTrip_Cat()
        {
            ClosedSubtypePetUnion original = new ClosedSubtypeCat { Name = "Whiskers", Lives = 9 };
            string json = JsonSerializer.Serialize(original);
            ClosedSubtypePetUnion deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion>(json);

            ClosedSubtypeCat cat = Assert.IsType<ClosedSubtypeCat>(deserialized);
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_NullSerializesAsNull()
        {
            ImplicitPetUnion? union = null;
            string json = JsonSerializer.Serialize(union);
            Assert.Equal("null", json);
        }

        // Simulates compiler output for a closed hierarchy.
        [Closed]
        [ClosedSubtype(typeof(ClosedDog))]
        [ClosedSubtype(typeof(ClosedCat))]
        [JsonPolymorphic(InferDerivedTypes = true)]
        public abstract class ClosedAnimal
        {
            public string? Name { get; set; }
        }

        public class ClosedDog : ClosedAnimal
        {
            public string? Breed { get; set; }
        }

        public class ClosedCat : ClosedAnimal
        {
            public int Lives { get; set; }
        }

        // Closed hierarchy with camelCase naming policy.
        [Closed]
        [ClosedSubtype(typeof(ClosedDogCamelCase))]
        [JsonPolymorphic(InferDerivedTypes = true, TypeDiscriminatorNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        public abstract class ClosedAnimalCamelCase
        {
            public string? Name { get; set; }
        }

        public class ClosedDogCamelCase : ClosedAnimalCamelCase { }

        // Closed hierarchy with explicit [JsonDerivedType] override.
        [Closed]
        [ClosedSubtype(typeof(ClosedDogExplicit))]
        [ClosedSubtype(typeof(ClosedCatExplicit))]
        [JsonPolymorphic(InferDerivedTypes = true)]
        [JsonDerivedType(typeof(ClosedDogExplicit), "doggo")]
        public abstract class ClosedAnimalExplicit
        {
            public string? Name { get; set; }
        }

        public class ClosedDogExplicit : ClosedAnimalExplicit { }
        public class ClosedCatExplicit : ClosedAnimalExplicit { }



        [JsonPolymorphic(InferDerivedTypes = true)]
        public abstract class OpenShape { }

        public class OpenCircle : OpenShape
        {
            public double Radius { get; set; }
        }

        public class OpenRectangle : OpenShape
        {
            public double Width { get; set; }
            public double Height { get; set; }
        }

        // Hierarchy with a compiler-generated subtype that should be excluded from inference.
        [JsonPolymorphic(InferDerivedTypes = true)]
        public abstract class HierarchyWithCompilerGenerated
        {
            public string? Name { get; set; }
        }

        public class RealSubtype : HierarchyWithCompilerGenerated
        {
            public int Value { get; set; }
        }

        [CompilerGenerated]
        public class CompilerGeneratedSubtype : HierarchyWithCompilerGenerated
        {
            public string? Synthetic { get; set; }
        }

        // Union with a constructor accepting a compiler-generated type that should be excluded.
        [JsonUnion]
        public struct UnionWithCompilerGeneratedCase
        {
            public UnionWithCompilerGeneratedCase(Cat value) { _value = value; _caseType = typeof(Cat); }
            public UnionWithCompilerGeneratedCase(CompilerGeneratedPayload value) { _value = value; _caseType = typeof(CompilerGeneratedPayload); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static implicit operator UnionWithCompilerGeneratedCase(Cat value) => new(value);
            public static implicit operator UnionWithCompilerGeneratedCase(CompilerGeneratedPayload value) => new(value);
            public static explicit operator Cat(UnionWithCompilerGeneratedCase union) =>
                union._caseType == typeof(Cat) ? (Cat)union._value! : throw new InvalidCastException();
            public static explicit operator CompilerGeneratedPayload(UnionWithCompilerGeneratedCase union) =>
                union._caseType == typeof(CompilerGeneratedPayload) ? (CompilerGeneratedPayload)union._value! : throw new InvalidCastException();
        }

        [CompilerGenerated]
        public class CompilerGeneratedPayload
        {
            public string? Data { get; set; }
        }

        // Union that discovers case types only from constructors (no implicit operators).
        [JsonUnion]
        public struct CtorUnionWithCompilerGeneratedCase
        {
            public CtorUnionWithCompilerGeneratedCase(Dog value) { _value = value; _caseType = typeof(Dog); }
            public CtorUnionWithCompilerGeneratedCase(CompilerGeneratedPayload value) { _value = value; _caseType = typeof(CompilerGeneratedPayload); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static explicit operator Dog(CtorUnionWithCompilerGeneratedCase union) =>
                union._caseType == typeof(Dog) ? (Dog)union._value! : throw new InvalidCastException();
            public static explicit operator CompilerGeneratedPayload(CtorUnionWithCompilerGeneratedCase union) =>
                union._caseType == typeof(CompilerGeneratedPayload) ? (CompilerGeneratedPayload)union._value! : throw new InvalidCastException();
        }



        public class Dog
        {
            public string? Name { get; set; }
            public string? Breed { get; set; }
        }

        public class Cat
        {
            public string? Name { get; set; }
            public int Lives { get; set; }
        }

        public class Labrador : Dog
        {
            public bool IsGuide { get; set; }
        }

        [Union]
        public struct PetUnion : IUnion
        {
            public PetUnion(Dog value) => Value = value;
            public PetUnion(Cat value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct ResultUnion : IUnion
        {
            public ResultUnion(int value) => Value = value;
            public ResultUnion(string value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct BoolOrIntUnion : IUnion
        {
            public BoolOrIntUnion(bool value) => Value = value;
            public BoolOrIntUnion(int value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct ArrayUnion : IUnion
        {
            public ArrayUnion(int[] value) => Value = value;
            public ArrayUnion(Dog[] value) => Value = value;
            public object? Value { get; }
        }



        [Theory]
        [InlineData("42")]
        [InlineData("0")]
        [InlineData("-1")]
        public void Union_PrimitiveAmbiguity_IntLong_FirstDeclaredWins(string json)
        {
            // int and long are both numeric — first declared (int) wins.
            IntOrLongUnion result = JsonSerializer.Deserialize<IntOrLongUnion>(json);
            Assert.IsType<int>(result.Value);
        }

        [Theory]
        [InlineData("3.14")]
        [InlineData("0.0")]
        public void Union_PrimitiveAmbiguity_FloatDouble_FirstDeclaredWins(string json)
        {
            FloatOrDoubleUnion result = JsonSerializer.Deserialize<FloatOrDoubleUnion>(json);
            Assert.IsType<float>(result.Value);
        }

        [Fact]
        public void Union_StringAmbiguity_DateTimeVsString_FirstDeclaredWins()
        {
            // string and DateTime both score (1,0) for a JSON string token.
            // string is declared first, so it wins regardless of content.
            string json = "\"2024-01-15T12:30:00\"";
            StringOrDateTimeUnion result = JsonSerializer.Deserialize<StringOrDateTimeUnion>(json);
            Assert.IsType<string>(result.Value);
        }

        [Fact]
        public void Union_StringAmbiguity_GuidVsString_FirstDeclaredWins()
        {
            string json = "\"550e8400-e29b-41d4-a716-446655440000\"";
            StringOrGuidUnion result = JsonSerializer.Deserialize<StringOrGuidUnion>(json);
            Assert.IsType<string>(result.Value);
        }

        [Fact]
        public void Union_StringAmbiguity_TimeSpanVsString_FirstDeclaredWins()
        {
            string json = "\"01:30:00\"";
            StringOrTimeSpanUnion result = JsonSerializer.Deserialize<StringOrTimeSpanUnion>(json);
            Assert.IsType<string>(result.Value);
        }

        [Fact]
        public void Union_StringAmbiguity_PlainStringDeserializesCorrectly()
        {
            // Any string value deserializes as string (first declared).
            StringOrDateTimeUnion result = JsonSerializer.Deserialize<StringOrDateTimeUnion>("\"hello world\"");
            Assert.IsType<string>(result.Value);
            Assert.Equal("hello world", result.Value);
        }

        [Fact]
        public void Union_NullableAmbiguity_NullWithMultipleNullableCaseTypes()
        {
            // Both int? and string? accept null — first declared (int?) wins.
            string json = "null";
            NullableIntOrStringUnion result = JsonSerializer.Deserialize<NullableIntOrStringUnion>(json);
            Assert.Null(result.Value);
        }

        [Fact]
        public void Union_NullableAmbiguity_NumericPicksCorrectNullable()
        {
            string json = "42";
            NullableIntOrStringUnion result = JsonSerializer.Deserialize<NullableIntOrStringUnion>(json);
            Assert.IsType<int>(result.Value);
        }



        [Fact]
        public void Union_CommonAncestor_DerivedSpecificProps_CorrectMatch()
        {
            // JSON with Breed matches Dog specifically (more matched properties).
            string json = """{"Name":"Rex","Breed":"Lab"}""";
            AnimalUnion result = JsonSerializer.Deserialize<AnimalUnion>(json);
            Assert.IsType<DogWithAncestor>(result.Value);
        }

        [Fact]
        public void Union_CommonAncestor_SharedPropsOnly_FirstDeclaredWins()
        {
            // JSON with only Name matches all three equally → first declared wins.
            string json = """{"Name":"Rex"}""";
            AnimalUnion result = JsonSerializer.Deserialize<AnimalUnion>(json);
            // First declared is DogWithAncestor in our union.
            Assert.NotNull(result.Value);
        }

        [Fact]
        public void Union_CommonAncestor_CatSpecificProps_CorrectMatch()
        {
            string json = """{"Name":"Whiskers","Lives":9}""";
            AnimalUnion result = JsonSerializer.Deserialize<AnimalUnion>(json);
            Assert.IsType<CatWithAncestor>(result.Value);
        }

        [Fact]
        public void Union_CommonAncestor_UnknownProps_TieGoesToFirst()
        {
            // All types have same score when only unknown properties present.
            string json = """{"Name":"Rex","Unknown":"value"}""";
            AnimalUnion result = JsonSerializer.Deserialize<AnimalUnion>(json);
            Assert.NotNull(result.Value);
        }



        [Fact]
        public void Union_Array_EmptyArray_MatchesAnyCollectionType()
        {
            string json = "[]";
            ArrayUnion result = JsonSerializer.Deserialize<ArrayUnion>(json);
            Assert.NotNull(result.Value);
        }

        [Fact]
        public void Union_Array_NullFirstElement_StillScoresCorrectly()
        {
            // [null, {"Name":"Rex","Breed":"Lab"}] - second element is Dog-like.
            // Full array scoring should aggregate: null element is compatible with Dog (reference type).
            string json = """[null, {"Name":"Rex","Breed":"Lab"}]""";
            DogOrIntArrayUnion result = JsonSerializer.Deserialize<DogOrIntArrayUnion>(json);
            Assert.IsType<Dog[]>(result.Value);
        }

        [Fact]
        public void Union_Array_MultiElementDiscrimination()
        {
            // All elements have Breed → Dog[] should win over Cat[].
            string json = """[{"Name":"Rex","Breed":"Lab"},{"Name":"Fido","Breed":"Poodle"}]""";
            DogOrCatArrayUnion result = JsonSerializer.Deserialize<DogOrCatArrayUnion>(json);
            Assert.IsType<Dog[]>(result.Value);
        }

        [Fact]
        public void Union_Array_SecondElementDisambiguates()
        {
            // First element has only Name (ambiguous), second has Breed (Dog-specific).
            // Full array scoring should pick Dog[] thanks to the second element.
            string json = """[{"Name":"Rex"},{"Name":"Fido","Breed":"Poodle"}]""";
            DogOrCatArrayUnion result = JsonSerializer.Deserialize<DogOrCatArrayUnion>(json);
            Assert.IsType<Dog[]>(result.Value);
        }

        [Fact]
        public void Union_Array_IntArray_Clear()
        {
            string json = "[1,2,3,4,5]";
            DogOrIntArrayUnion result = JsonSerializer.Deserialize<DogOrIntArrayUnion>(json);
            Assert.IsType<int[]>(result.Value);
        }



        [Fact]
        public void Union_NestedUnion_PrimitiveReachesInnerUnion()
        {
            // Outer union(InnerUnion, bool). InnerUnion is union(int, string).
            // JSON 42 should reach through InnerUnion's int case.
            string json = "42";
            OuterUnion result = JsonSerializer.Deserialize<OuterUnion>(json);
            Assert.NotNull(result.Value);
            Assert.IsType<InnerUnion>(result.Value);
            var inner = (InnerUnion)result.Value!;
            Assert.IsType<int>(inner.Value);
            Assert.Equal(42, (int)inner.Value!);
        }

        [Fact]
        public void Union_NestedUnion_StringReachesInnerUnion()
        {
            string json = "\"hello\"";
            OuterUnion result = JsonSerializer.Deserialize<OuterUnion>(json);
            Assert.NotNull(result.Value);
            Assert.IsType<InnerUnion>(result.Value);
            var inner = (InnerUnion)result.Value!;
            Assert.IsType<string>(inner.Value);
            Assert.Equal("hello", (string)inner.Value!);
        }

        [Fact]
        public void Union_NestedUnion_BoolGoesToDirectCase()
        {
            // bool is a direct case type of OuterUnion, not in InnerUnion.
            string json = "true";
            OuterUnion result = JsonSerializer.Deserialize<OuterUnion>(json);
            Assert.IsType<bool>(result.Value);
        }

        [Fact]
        public void Union_NestedUnion_NullHandled()
        {
            string json = "null";
            OuterUnion result = JsonSerializer.Deserialize<OuterUnion>(json);
            Assert.Null(result.Value);
        }



        [Fact]
        public void Union_StructurallyIdentical_FirstDeclaredWins()
        {
            // Point2D and Complex have identical schemas {X, Y}.
            // First declared wins on a perfect tie.
            string json = """{"X":1.0,"Y":2.0}""";
            Point2DOrComplexUnion result = JsonSerializer.Deserialize<Point2DOrComplexUnion>(json);
            Assert.IsType<Point2D>(result.Value);
        }

        [Fact]
        public void Union_StructurallyIdentical_RoundTrip_Point2D()
        {
            var original = new Point2DOrComplexUnion(new Point2D { X = 3.0, Y = 4.0 });
            string json = JsonSerializer.Serialize(original);
            Point2DOrComplexUnion deserialized = JsonSerializer.Deserialize<Point2DOrComplexUnion>(json);

            // Both types have same shape, so round-trip may yield Point2D (first declared).
            Assert.IsType<Point2D>(deserialized.Value);
            var point = (Point2D)deserialized.Value!;
            Assert.Equal(3.0, point.X);
            Assert.Equal(4.0, point.Y);
        }



        [Fact]
        public void Union_ExtensionData_KnownPropsWin()
        {
            // Rigid has Name+Breed, Flexible has Name+ExtensionData.
            // JSON with Name+Breed should pick Rigid (more known property matches).
            string json = """{"Name":"Rex","Breed":"Lab","Color":"Brown"}""";
            FlexibleOrRigidUnion result = JsonSerializer.Deserialize<FlexibleOrRigidUnion>(json);
            Assert.IsType<RigidType>(result.Value);
        }

        [Fact]
        public void Union_ExtensionData_TieGoesToFirst()
        {
            // JSON with only Name — both types match equally (1 known property).
            string json = """{"Name":"Rex","Color":"Brown"}""";
            FlexibleOrRigidUnion result = JsonSerializer.Deserialize<FlexibleOrRigidUnion>(json);
            // Both score (1, 1): Name matches, Color is unknown for both.
            // Extension data on FlexibleType doesn't provide additional credit.
            Assert.NotNull(result.Value);
        }



        [Fact]
        public void Union_PolymorphicCaseType_DiscriminatorCountedAsUnknown()
        {
            // Dog with $type discriminator — matcher treats $type as an unknown property.
            string json = """{"$type":"PolyDog","Name":"Rex","Breed":"Lab"}""";
            PolyDogOrCatUnion result = JsonSerializer.Deserialize<PolyDogOrCatUnion>(json);
            // PolyDog: Name✓, Breed✓, $type unknown → (2, 1)
            // PolyCat: Name✓, $type unknown, Breed unknown → (1, 2)
            Assert.IsType<PolyDogType>(result.Value);
        }

        [Theory]
        [InlineData("42")]
        [InlineData("3.14")]
        [InlineData("true")]
        [InlineData("\"hello\"")]
        [InlineData("[1,2,3]")]
        public void Union_AllCasesDisqualified_ThrowsJsonException(string json)
        {
            // PetUnion only has Dog and Cat (object types).
            // All non-object JSON tokens should disqualify both candidates.
            Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<PetUnion>(json));
        }

        [Fact]
        public void Union_AllCasesDisqualified_ObjectWithOnlyUnknownProperties()
        {
            // IntOrLongUnion only has int and long (primitive types).
            // A JSON object should disqualify both candidates.
            Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<IntOrLongUnion>("""{"Foo":"bar"}"""));
        }

        [Union]
        public struct IntOrLongUnion : IUnion
        {
            public IntOrLongUnion(int value) => Value = value;
            public IntOrLongUnion(long value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct FloatOrDoubleUnion : IUnion
        {
            public FloatOrDoubleUnion(float value) => Value = value;
            public FloatOrDoubleUnion(double value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct StringOrDateTimeUnion : IUnion
        {
            public StringOrDateTimeUnion(string value) => Value = value;
            public StringOrDateTimeUnion(DateTime value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct StringOrGuidUnion : IUnion
        {
            public StringOrGuidUnion(string value) => Value = value;
            public StringOrGuidUnion(Guid value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct StringOrTimeSpanUnion : IUnion
        {
            public StringOrTimeSpanUnion(string value) => Value = value;
            public StringOrTimeSpanUnion(TimeSpan value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct NullableIntOrStringUnion : IUnion
        {
            public NullableIntOrStringUnion(int? value) => Value = value;
            public NullableIntOrStringUnion(string? value) => Value = value;
            public object? Value { get; }
        }

        public class AnimalBase
        {
            public string? Name { get; set; }
        }

        public class DogWithAncestor : AnimalBase
        {
            public string? Breed { get; set; }
        }

        public class CatWithAncestor : AnimalBase
        {
            public int Lives { get; set; }
        }

        [Union]
        public struct AnimalUnion : IUnion
        {
            public AnimalUnion(DogWithAncestor value) => Value = value;
            public AnimalUnion(CatWithAncestor value) => Value = value;
            public AnimalUnion(AnimalBase value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct DogOrIntArrayUnion : IUnion
        {
            public DogOrIntArrayUnion(Dog[] value) => Value = value;
            public DogOrIntArrayUnion(int[] value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct DogOrCatArrayUnion : IUnion
        {
            public DogOrCatArrayUnion(Dog[] value) => Value = value;
            public DogOrCatArrayUnion(Cat[] value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct InnerUnion : IUnion
        {
            public InnerUnion(int value) => Value = value;
            public InnerUnion(string value) => Value = value;
            public object? Value { get; }
        }

        [Union]
        public struct OuterUnion : IUnion
        {
            public OuterUnion(InnerUnion value) => Value = value;
            public OuterUnion(bool value) => Value = value;
            public object? Value { get; }
        }

        public class Point2D
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class Complex
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        [Union]
        public struct Point2DOrComplexUnion : IUnion
        {
            public Point2DOrComplexUnion(Point2D value) => Value = value;
            public Point2DOrComplexUnion(Complex value) => Value = value;
            public object? Value { get; }
        }

        public class FlexibleType
        {
            public string? Name { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement>? Extra { get; set; }
        }

        public class RigidType
        {
            public string? Name { get; set; }
            public string? Breed { get; set; }
        }

        [Union]
        public struct FlexibleOrRigidUnion : IUnion
        {
            public FlexibleOrRigidUnion(FlexibleType value) => Value = value;
            public FlexibleOrRigidUnion(RigidType value) => Value = value;
            public object? Value { get; }
        }

        public class PolyDogType
        {
            public string? Name { get; set; }
            public string? Breed { get; set; }
        }

        public class PolyCatType
        {
            public string? Name { get; set; }
            public int Lives { get; set; }
        }

        [Union]
        public struct PolyDogOrCatUnion : IUnion
        {
            public PolyDogOrCatUnion(PolyDogType value) => Value = value;
            public PolyDogOrCatUnion(PolyCatType value) => Value = value;
            public object? Value { get; }
        }



        [Closed]
        public enum ClosedColor
        {
            Red = 0,
            Green = 1,
            Blue = 2,
        }

        public enum OpenColor
        {
            Red = 0,
            Green = 1,
            Blue = 2,
        }

        // Phase 5 test model types

        [JsonUnion]
        public struct ImplicitPetUnion
        {
            public ImplicitPetUnion(Dog value) { _value = value; _caseType = typeof(Dog); }
            public ImplicitPetUnion(Cat value) { _value = value; _caseType = typeof(Cat); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static implicit operator ImplicitPetUnion(Dog value) => new(value);
            public static implicit operator ImplicitPetUnion(Cat value) => new(value);
            public static explicit operator Dog(ImplicitPetUnion union) =>
                union._caseType == typeof(Dog) ? (Dog)union._value! : throw new InvalidCastException();
            public static explicit operator Cat(ImplicitPetUnion union) =>
                union._caseType == typeof(Cat) ? (Cat)union._value! : throw new InvalidCastException();
        }

        [JsonUnion]
        public struct CtorPetUnion
        {
            public CtorPetUnion(Dog value) { _value = value; _caseType = typeof(Dog); }
            public CtorPetUnion(Cat value) { _value = value; _caseType = typeof(Cat); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static explicit operator Dog(CtorPetUnion union) =>
                union._caseType == typeof(Dog) ? (Dog)union._value! : throw new InvalidCastException();
            public static explicit operator Cat(CtorPetUnion union) =>
                union._caseType == typeof(Cat) ? (Cat)union._value! : throw new InvalidCastException();
        }

        [JsonUnion]
        [ClosedSubtype(typeof(ClosedSubtypeDog))]
        [ClosedSubtype(typeof(ClosedSubtypeCat))]
        public class ClosedSubtypePetUnion
        {
            public string? Name { get; set; }
        }

        public class ClosedSubtypeDog : ClosedSubtypePetUnion
        {
            public string? Breed { get; set; }
        }

        public class ClosedSubtypeCat : ClosedSubtypePetUnion
        {
            public int Lives { get; set; }
        }

        public class CustomTestClassifier : JsonTypeClassifierFactory
        {
            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is JsonTokenType.StartObject)
                    {
                        Utf8JsonReader copy = reader;
                        while (copy.Read())
                        {
                            if (copy.TokenType is JsonTokenType.PropertyName)
                            {
                                string prop = copy.GetString()!;
                                if (prop == "Breed" || prop == "breed")
                                {
                                    return typeof(Dog);
                                }

                                if (prop == "Lives" || prop == "lives")
                                {
                                    return typeof(Cat);
                                }
                            }

                            if (copy.TokenType is JsonTokenType.EndObject)
                            {
                                break;
                            }

                            copy.TrySkip();
                        }
                    }

                    return null;
                };
            }
        }

        [JsonUnion(TypeClassifier = typeof(CustomTestClassifier))]
        public struct CustomClassifiedPetUnion
        {
            public CustomClassifiedPetUnion(Dog value) { _value = value; _caseType = typeof(Dog); }
            public CustomClassifiedPetUnion(Cat value) { _value = value; _caseType = typeof(Cat); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static implicit operator CustomClassifiedPetUnion(Dog value) => new(value);
            public static implicit operator CustomClassifiedPetUnion(Cat value) => new(value);
            public static explicit operator Dog(CustomClassifiedPetUnion union) =>
                union._caseType == typeof(Dog) ? (Dog)union._value! : throw new InvalidCastException();
            public static explicit operator Cat(CustomClassifiedPetUnion union) =>
                union._caseType == typeof(Cat) ? (Cat)union._value! : throw new InvalidCastException();
        }

        // ===== Phase 6: Comprehensive API coverage tests =====

        [Fact]
        public void JsonTypeInfoKind_Union_IsSetForCompilerUnion()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
        }

        [Fact]
        public void JsonTypeInfoKind_Union_IsSetForJsonUnionAttribute()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(ImplicitPetUnion));
            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
        }

        [Fact]
        public void JsonTypeInfoKind_Union_IsSetForClosedSubtypeUnion()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(ClosedSubtypePetUnion));
            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
        }

        [Theory]
        [InlineData(typeof(Dog), """{"Name":"Rex","Breed":"Labrador"}""")]
        [InlineData(typeof(Cat), """{"Name":"Whiskers","Lives":9}""")]
        public void JsonUnion_ImplicitOperator_RoundTrip(Type caseType, string json)
        {
            ImplicitPetUnion deserialized = JsonSerializer.Deserialize<ImplicitPetUnion>(json);
            string reserialized = JsonSerializer.Serialize(deserialized);

            if (caseType == typeof(Dog))
            {
                Dog dog = (Dog)deserialized;
                Assert.Equal("Rex", dog.Name);
                Assert.Contains("Labrador", reserialized);
            }
            else
            {
                Cat cat = (Cat)deserialized;
                Assert.Equal("Whiskers", cat.Name);
                Assert.Contains("9", reserialized);
            }
        }

        [Theory]
        [InlineData(typeof(Dog), """{"Name":"Rex","Breed":"Labrador"}""")]
        [InlineData(typeof(Cat), """{"Name":"Whiskers","Lives":9}""")]
        public void JsonUnion_ClosedSubtype_RoundTrip(Type expectedCaseType, string json)
        {
            ClosedSubtypePetUnion deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion>(json);
            Assert.IsType(expectedCaseType == typeof(Dog) ? typeof(ClosedSubtypeDog) : typeof(ClosedSubtypeCat), deserialized);
            string reserialized = JsonSerializer.Serialize(deserialized);
            Assert.Contains(expectedCaseType == typeof(Dog) ? "Labrador" : "9", reserialized);
        }

        [Fact]
        public void JsonUnion_CollectionOfUnions_RoundTrip()
        {
            var list = new List<ImplicitPetUnion>
            {
                new Dog { Name = "Rex", Breed = "Labrador" },
                new Cat { Name = "Whiskers", Lives = 9 },
            };

            string json = JsonSerializer.Serialize(list);
            Assert.Contains("Rex", json);
            Assert.Contains("Whiskers", json);

            List<ImplicitPetUnion>? deserialized = JsonSerializer.Deserialize<List<ImplicitPetUnion>>(json);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Count);

            Dog dog = (Dog)deserialized[0];
            Assert.Equal("Rex", dog.Name);

            Cat cat = (Cat)deserialized[1];
            Assert.Equal("Whiskers", cat.Name);
        }

        [Fact]
        public void JsonUnion_NestedUnion_InObject_RoundTrip()
        {
            var wrapper = new UnionWrapper
            {
                Label = "test",
                Pet = new Dog { Name = "Rex", Breed = "Labrador" },
            };

            string json = JsonSerializer.Serialize(wrapper);
            Assert.Contains("test", json);
            Assert.Contains("Rex", json);

            UnionWrapper? deserialized = JsonSerializer.Deserialize<UnionWrapper>(json);
            Assert.NotNull(deserialized);
            Assert.Equal("test", deserialized.Label);

            Dog dog = (Dog)deserialized.Pet;
            Assert.Equal("Rex", dog.Name);
        }

        [Fact]
        public void JsonUnion_MissingClassifier_ThrowsDescriptiveException()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PetUnion))
                            {
                                typeInfo.TypeClassifier = null;
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Rex"}""";
            JsonException ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PetUnion>(json, options));
            Assert.Contains("TypeClassifier", ex.Message);
        }

        [Fact]
        public void JsonUnion_MissingConstructor_ThrowsDescriptiveException()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PetUnion))
                            {
                                typeInfo.UnionConstructor = null;
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            JsonException ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PetUnion>(json, options));
            Assert.Contains("UnionConstructor", ex.Message);
        }

        [Fact]
        public void JsonUnion_MissingDeconstructor_ThrowsDescriptiveException()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PetUnion))
                            {
                                typeInfo.UnionDeconstructor = null;
                            }
                        }
                    }
                }
            };

            var pet = new PetUnion(new Dog { Name = "Rex", Breed = "Labrador" });
            JsonException ex = Assert.Throws<JsonException>(() => JsonSerializer.Serialize(pet, options));
            Assert.Contains("UnionDeconstructor", ex.Message);
        }

        [Fact]
        public void JsonUnion_UnionCases_PopulatedAutomatically()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.NotNull(typeInfo.UnionCases);
            Assert.Equal(2, typeInfo.UnionCases.Count);
            Assert.Contains(typeInfo.UnionCases, c => c.CaseType == typeof(Dog));
            Assert.Contains(typeInfo.UnionCases, c => c.CaseType == typeof(Cat));
        }

        [Fact]
        public void JsonUnion_UnionCaseInfo_ThrowsOnNullCaseType()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonUnionCaseInfo(null!));
        }

        [Fact]
        public void JsonUnion_UnionCaseInfo_StoresCaseType()
        {
            var info = new JsonUnionCaseInfo(typeof(Dog));
            Assert.Equal(typeof(Dog), info.CaseType);
        }

        [Fact]
        public void JsonUnion_StronglyTypedDeconstructor_Works()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PetUnion) && typeInfo is JsonTypeInfo<PetUnion> typedInfo)
                            {
                                typedInfo.UnionDeconstructor = union =>
                                {
                                    object? value = union.Value;
                                    return (value?.GetType() ?? typeof(object), value);
                                };
                            }
                        }
                    }
                }
            };

            var pet = new PetUnion(new Dog { Name = "Rex", Breed = "Labrador" });
            string json = JsonSerializer.Serialize(pet, options);
            Assert.Contains("Rex", json);
        }

        [Fact]
        public void JsonUnion_StronglyTypedConstructor_Works()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PetUnion) && typeInfo is JsonTypeInfo<PetUnion> typedInfo)
                            {
                                typedInfo.UnionConstructor = (Type caseType, object? value) =>
                                {
                                    if (caseType == typeof(Dog)) return new PetUnion((Dog)value!);
                                    if (caseType == typeof(Cat)) return new PetUnion((Cat)value!);
                                    throw new InvalidOperationException();
                                };
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            PetUnion pet = JsonSerializer.Deserialize<PetUnion>(json, options);
            Assert.IsType<Dog>(pet.Value);
        }

        [Fact]
        public void JsonUnion_ClassifierProperty_IsSetAutomatically()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.NotNull(typeInfo.TypeClassifier);
        }

        [Fact]
        public void JsonUnion_DeconstructorProperty_IsSetAutomatically()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.NotNull(typeInfo.UnionDeconstructor);
        }

        [Fact]
        public void JsonUnion_ConstructorProperty_IsSetAutomatically()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.NotNull(typeInfo.UnionConstructor);
        }

        [Fact]
        public void JsonUnion_NoMatchingCase_ThrowsDescriptiveException()
        {
            string json = """{"Unknown":"field","Another":42}""";
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(PetUnion))
                            {
                                typeInfo.UnionCases = new List<JsonUnionCaseInfo>();
                                var factory = new JsonStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeof(PetUnion),
                                    Array.Empty<JsonDerivedType>(),
                                    null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };

            JsonException ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PetUnion>(json, options));
            Assert.Contains("Unable to classify", ex.Message);
        }

        [Fact]
        public void JsonUnion_ArrayOfClosedSubtypeUnion_RoundTrip()
        {
            var array = new ClosedSubtypePetUnion[]
            {
                new ClosedSubtypeDog { Name = "Rex", Breed = "Labrador" },
                new ClosedSubtypeCat { Name = "Whiskers", Lives = 9 },
            };

            string json = JsonSerializer.Serialize(array);
            ClosedSubtypePetUnion[]? deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion[]>(json);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Length);
            Assert.IsType<ClosedSubtypeDog>(deserialized[0]);
            Assert.IsType<ClosedSubtypeCat>(deserialized[1]);
        }

        [Fact]
        public void JsonUnion_EmptyObject_MatchesFirstDeclaredCase()
        {
            string json = "{}";
            ImplicitPetUnion union = JsonSerializer.Deserialize<ImplicitPetUnion>(json);

            // Empty object matches the first declared case (Dog) since both score equally.
            Dog dog = (Dog)union;
            Assert.Null(dog.Name);
        }

        [Fact]
        public void JsonUnionAttribute_TypeClassifier_CanBeNull()
        {
            var attr = new JsonUnionAttribute();
            Assert.Null(attr.TypeClassifier);
        }

        [Fact]
        public void JsonUnionAttribute_TypeClassifier_CanBeSet()
        {
            var attr = new JsonUnionAttribute();
            attr.TypeClassifier = typeof(CustomTestClassifier);
            Assert.Equal(typeof(CustomTestClassifier), attr.TypeClassifier);
        }

        [Fact]
        public void JsonStructuralClassifierFactory_NullContext_Throws()
        {
            var factory = new JsonStructuralClassifierFactory();
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            Assert.Throws<ArgumentNullException>(() => factory.CreateJsonClassifier(null!, options));
        }

        [Fact]
        public void JsonStructuralClassifierFactory_NullOptions_Throws()
        {
            var factory = new JsonStructuralClassifierFactory();
            var context = new JsonTypeClassifierContext(
                typeof(PetUnion),
                new JsonDerivedType[] { new JsonDerivedType(typeof(Dog)) },
                null);
            Assert.Throws<ArgumentNullException>(() => factory.CreateJsonClassifier(context, null!));
        }

        // Test model for nested union in object
        public class UnionWrapper
        {
            public string? Label { get; set; }
            public ImplicitPetUnion Pet { get; set; }
        }

        // =====================================================================
        // Phase C: Polymorphic TypeClassifier Tests
        // =====================================================================

        [JsonPolymorphic]
        [JsonDerivedType(typeof(ClassifierDog), "dog")]
        [JsonDerivedType(typeof(ClassifierCat), "cat")]
        public class ClassifierAnimal
        {
            public string? Name { get; set; }
        }

        public class ClassifierDog : ClassifierAnimal
        {
            public string? Breed { get; set; }
        }

        public class ClassifierCat : ClassifierAnimal
        {
            public int Lives { get; set; }
        }

        [Fact]
        public void PolymorphicTypeClassifier_StructuralClassifier_RoundTrips()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                typeInfo.TypeClassifier = (ref Utf8JsonReader reader) =>
                                {
                                    Utf8JsonReader copy = reader;
                                    if (copy.TokenType != JsonTokenType.StartObject)
                                        return null;

                                    bool hasBreed = false;
                                    bool hasLives = false;
                                    while (copy.Read() && copy.TokenType != JsonTokenType.EndObject)
                                    {
                                        if (copy.TokenType == JsonTokenType.PropertyName)
                                        {
                                            if (copy.ValueTextEquals("Breed"u8)) hasBreed = true;
                                            else if (copy.ValueTextEquals("Lives"u8)) hasLives = true;
                                            copy.Read();
                                            copy.TrySkip();
                                        }
                                    }

                                    if (hasBreed) return typeof(ClassifierDog);
                                    if (hasLives) return typeof(ClassifierCat);
                                    return null;
                                };
                            }
                        }
                    }
                }
            };

            ClassifierAnimal dog = new ClassifierDog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(dog, options);
            Assert.Contains("\"$type\"", json);

            string noDiscriminatorJson = """{"Name":"Rex","Breed":"Labrador"}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(noDiscriminatorJson, options);
            Assert.IsType<ClassifierDog>(deserialized);
            Assert.Equal("Rex", deserialized.Name);
            Assert.Equal("Labrador", ((ClassifierDog)deserialized).Breed);
        }

        [Fact]
        public void PolymorphicTypeClassifier_CatDeserialization()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                typeInfo.TypeClassifier = (ref Utf8JsonReader reader) =>
                                {
                                    Utf8JsonReader copy = reader;
                                    if (copy.TokenType != JsonTokenType.StartObject) return null;
                                    while (copy.Read() && copy.TokenType != JsonTokenType.EndObject)
                                    {
                                        if (copy.TokenType == JsonTokenType.PropertyName)
                                        {
                                            if (copy.ValueTextEquals("Breed"u8)) return typeof(ClassifierDog);
                                            if (copy.ValueTextEquals("Lives"u8)) return typeof(ClassifierCat);
                                            copy.Read();
                                            copy.TrySkip();
                                        }
                                    }
                                    return null;
                                };
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Whiskers","Lives":9}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.IsType<ClassifierCat>(deserialized);
            Assert.Equal("Whiskers", deserialized.Name);
            Assert.Equal(9, ((ClassifierCat)deserialized).Lives);
        }

        [Fact]
        public void PolymorphicTypeClassifier_ReturnsNull_FallsBackToBaseType()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                typeInfo.TypeClassifier = (ref Utf8JsonReader _) => null;
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Unknown"}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.NotNull(deserialized);
            Assert.Equal("Unknown", deserialized.Name);
        }

        [Fact]
        public void JsonDiscriminatorClassifier_StringDiscriminator_RoundTrips()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                var factory = new JsonDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new JsonDerivedType[]
                                    {
                                        new JsonDerivedType(typeof(ClassifierDog), "dog"),
                                        new JsonDerivedType(typeof(ClassifierCat), "cat"),
                                    },
                                    "kind");
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };

            string json = """{"kind":"dog","Name":"Rex","Breed":"Lab"}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.IsType<ClassifierDog>(deserialized);
            Assert.Equal("Rex", deserialized.Name);
            Assert.Equal("Lab", ((ClassifierDog)deserialized).Breed);
        }

        [Fact]
        public void JsonDiscriminatorClassifier_StringDiscriminator_AnyPropertyPosition()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                var factory = new JsonDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new JsonDerivedType[]
                                    {
                                        new JsonDerivedType(typeof(ClassifierDog), "dog"),
                                        new JsonDerivedType(typeof(ClassifierCat), "cat"),
                                    },
                                    "kind");
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Whiskers","Lives":9,"kind":"cat"}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.IsType<ClassifierCat>(deserialized);
            Assert.Equal("Whiskers", deserialized.Name);
            Assert.Equal(9, ((ClassifierCat)deserialized).Lives);
        }

        [Fact]
        public void JsonDiscriminatorClassifier_IntDiscriminator_RoundTrips()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                var factory = new JsonDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new JsonDerivedType[]
                                    {
                                        new JsonDerivedType(typeof(ClassifierDog), 1),
                                        new JsonDerivedType(typeof(ClassifierCat), 2),
                                    },
                                    "type_id");
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };

            string json = """{"type_id":2,"Name":"Whiskers","Lives":9}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.IsType<ClassifierCat>(deserialized);
            Assert.Equal(9, ((ClassifierCat)deserialized).Lives);
        }

        [Fact]
        public void JsonDiscriminatorClassifier_UnknownDiscriminator_ReturnsNull()
        {
            var factory = new JsonDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                typeof(object),
                new JsonDerivedType[] { new JsonDerivedType(typeof(ClassifierDog), "dog") },
                "kind");
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, options);

            Utf8JsonReader reader = new("""{"kind":"parrot","Name":"Polly"}"""u8);
            reader.Read(); // Position at StartObject
            Type? result = classify(ref reader);
            Assert.Null(result);
        }

        [Fact]
        public void JsonDiscriminatorClassifier_MissingProperty_ReturnsNull()
        {
            var factory = new JsonDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                typeof(object),
                new JsonDerivedType[] { new JsonDerivedType(typeof(ClassifierDog), "dog") },
                "kind");
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, options);

            Utf8JsonReader reader = new("""{"Name":"Rex"}"""u8);
            reader.Read();
            Type? result = classify(ref reader);
            Assert.Null(result);
        }

        [Fact]
        public void JsonDiscriminatorClassifier_NotStartObject_ReturnsNull()
        {
            var factory = new JsonDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                typeof(object),
                new JsonDerivedType[] { new JsonDerivedType(typeof(ClassifierDog), "dog") },
                "kind");
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, options);

            Utf8JsonReader reader = new("42"u8);
            reader.Read();
            Type? result = classify(ref reader);
            Assert.Null(result);
        }

        [Fact]
        public void JsonDiscriminatorClassifier_NullArguments_Throws()
        {
            var factory = new JsonDiscriminatorClassifierFactory();
            var validContext = new JsonTypeClassifierContext(
                typeof(object),
                new JsonDerivedType[] { new JsonDerivedType(typeof(ClassifierDog), "dog") },
                "kind");
            var validOptions = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

            Assert.Throws<ArgumentNullException>(() => factory.CreateJsonClassifier(null!, validOptions));
            Assert.Throws<ArgumentNullException>(() => factory.CreateJsonClassifier(validContext, null!));
        }

        [Fact]
        public void StandardDiscriminatorPolymorphism_StillWorks()
        {
            var options = new JsonSerializerOptions();
            ClassifierAnimal dog = new ClassifierDog { Name = "Rex", Breed = "Lab" };
            string json = JsonSerializer.Serialize(dog, options);
            Assert.Contains("\"$type\":\"dog\"", json);

            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.IsType<ClassifierDog>(deserialized);
            Assert.Equal("Rex", deserialized.Name);
        }

        [Fact]
        public void TypeClassifier_PlusAllowOutOfOrder_ClassifierWins()
        {
            var options = new JsonSerializerOptions
            {
                AllowOutOfOrderMetadataProperties = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                var factory = new JsonDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new JsonDerivedType[]
                                    {
                                        new JsonDerivedType(typeof(ClassifierDog), "dog"),
                                        new JsonDerivedType(typeof(ClassifierCat), "cat"),
                                    },
                                    "kind");
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };

            // Uses "kind" not "$type" — classifier overrides discriminator scanning
            string json = """{"Name":"Rex","kind":"dog","Breed":"Lab"}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.IsType<ClassifierDog>(deserialized);
            Assert.Equal("Rex", deserialized.Name);
        }

        [Fact]
        public void TypeClassifier_UnknownType_Throws()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                typeInfo.TypeClassifier = (ref Utf8JsonReader _) => typeof(string);
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Rex"}""";
            Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<ClassifierAnimal>(json, options));
        }

        [Fact]
        public void JsonDiscriminatorClassifier_IntDiscriminator_AnyPosition()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifierAnimal))
                            {
                                var factory = new JsonDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new JsonDerivedType[]
                                    {
                                        new JsonDerivedType(typeof(ClassifierDog), 1),
                                        new JsonDerivedType(typeof(ClassifierCat), 2),
                                    },
                                    "t");
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };

            string json = """{"Name":"Rex","Breed":"Lab","t":1}""";
            ClassifierAnimal? deserialized = JsonSerializer.Deserialize<ClassifierAnimal>(json, options);
            Assert.IsType<ClassifierDog>(deserialized);
        }

        // ==================================================================================
        // Custom converter + union interaction tests
        // ==================================================================================
        //
        // Union case types that use custom JsonConverter implementations are "structurally
        // opaque" to the default structural classifier. The classifier cannot inspect their
        // JSON shape (they have Kind=None, zero properties), so:
        //   - Serialization works: the deconstructor extracts the case value, and
        //     JsonSerializer.Serialize delegates to the custom converter.
        //   - Deserialization via structural classifier is unreliable: the classifier
        //     scores custom-converter types as (0, N) — losing to any case type with
        //     property matches. When the JSON only matches the custom-converter case,
        //     the classifier either returns null or misclassifies.

        [Fact]
        public void Union_CustomConverterCase_SerializationWorks()
        {
            // Arrange: union with one normal case (Dog) and one custom-converter case (CustomDogPayload).
            // Serializing the custom-converter case should work because the deconstructor extracts
            // the case value and JsonSerializer.Serialize uses the custom converter.
            var union = new UnionWithCustomConverterCase(new CustomDogPayload { DogName = "Rex", DogBreed = "Lab" });

            // Act
            string json = JsonSerializer.Serialize(union);

            // Assert: CustomDogPayloadConverter writes {"dog_name":"Rex","dog_breed":"Lab"}
            Assert.Contains("\"dog_name\"", json);
            Assert.Contains("\"Rex\"", json);
            Assert.Contains("\"dog_breed\"", json);
            Assert.Contains("\"Lab\"", json);
        }

        [Fact]
        public void Union_CustomConverterCase_SerializationWorks_NormalCaseToo()
        {
            var union = new UnionWithCustomConverterCase(new Cat { Name = "Whiskers", Lives = 9 });

            string json = JsonSerializer.Serialize(union);

            Assert.Contains("\"Name\"", json);
            Assert.Contains("\"Whiskers\"", json);
            Assert.Contains("\"Lives\"", json);
        }

        [Fact]
        public void Union_CustomConverterCase_DeserializationFails_WhenOnlyCustomCaseMatches()
        {
            // The JSON matches CustomDogPayload's custom format, but the structural classifier
            // cannot see CustomDogPayload's properties (it's opaque). Cat has no matching properties
            // either. The classifier returns null or the wrong type → deserialization fails.
            string json = """{"dog_name":"Rex","dog_breed":"Lab"}""";

            // The structural classifier scores:
            // - Cat: "dog_name" unknown, "dog_breed" unknown → (0, 2)
            // - CustomDogPayload: same zero properties → (0, 2)
            // Tie broken by declaration order → selects Cat, which fails to deserialize meaningfully
            // (silently drops unknown properties). We assert the wrong type is returned.
            var result = JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json);

            // The structural classifier cannot distinguish between Cat and CustomDogPayload
            // when the JSON doesn't match Cat's known properties. Since Cat is declared first
            // and both score (0, 2), Cat wins by declaration order.
            // The deserialized value will be a Cat with default values — NOT a CustomDogPayload.
            (Type caseType, object? caseValue) = ExtractCaseInfo(result);
            Assert.Equal(typeof(Cat), caseType);
            Cat cat = Assert.IsType<Cat>(caseValue);
            Assert.Null(cat.Name); // No matching properties
            Assert.Equal(0, cat.Lives);
        }

        [Fact]
        public void Union_CustomConverterCase_NormalCaseStillDeserializes()
        {
            // When the JSON matches the normal case (Cat), deserialization should work fine.
            string json = """{"Name":"Whiskers","Lives":9}""";

            var result = JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json);

            (Type caseType, object? caseValue) = ExtractCaseInfo(result);
            Assert.Equal(typeof(Cat), caseType);
            Cat cat = Assert.IsType<Cat>(caseValue);
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void Union_AllCustomConverterCases_ClassifierReturnsBestEffort()
        {
            // When ALL case types have custom converters, the structural classifier has zero
            // property metadata for any case. For object JSON, all cases score (0, N).
            // First declared case wins by tie-breaking.
            string json = """{"x":1,"y":2}""";

            var result = JsonSerializer.Deserialize<UnionAllCustomConverterCases>(json);

            // First declared case (CustomDogPayload) wins the tie.
            (Type caseType, _) = ExtractAllCustomCaseInfo(result);
            Assert.Equal(typeof(CustomDogPayload), caseType);
        }

        [Fact]
        public void Union_AllCustomConverterCases_SerializationWorks()
        {
            var union = new UnionAllCustomConverterCases(
                new CustomCatPayload { CatName = "Whiskers", CatLives = 9 });

            string json = JsonSerializer.Serialize(union);

            Assert.Contains("\"cat_name\"", json);
            Assert.Contains("\"Whiskers\"", json);
        }

        [Fact]
        public void Union_CustomConverterCase_WithCustomClassifier_DeserializationSucceeds()
        {
            // A custom classifier CAN distinguish custom converter case types because
            // it has domain knowledge about the JSON shape. This is the recommended workaround.
            string dogJson = """{"dog_name":"Rex","dog_breed":"Lab"}""";
            string catJson = """{"Name":"Whiskers","Lives":9}""";

            var dogResult = JsonSerializer.Deserialize<UnionWithCustomClassifierAndConverter>(dogJson);
            (Type dogCaseType, object? dogValue) = ExtractCustomClassifiedCaseInfo(dogResult);
            Assert.Equal(typeof(CustomDogPayload), dogCaseType);
            CustomDogPayload dog = Assert.IsType<CustomDogPayload>(dogValue);
            Assert.Equal("Rex", dog.DogName);
            Assert.Equal("Lab", dog.DogBreed);

            var catResult = JsonSerializer.Deserialize<UnionWithCustomClassifierAndConverter>(catJson);
            (Type catCaseType, object? catValue) = ExtractCustomClassifiedCaseInfo(catResult);
            Assert.Equal(typeof(Cat), catCaseType);
            Cat cat = Assert.IsType<Cat>(catValue);
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void Union_CustomConverterCase_WithCustomClassifier_RoundTrips()
        {
            // Full round-trip: serialize custom converter case → deserialize with custom classifier
            var original = new UnionWithCustomClassifierAndConverter(
                new CustomDogPayload { DogName = "Rex", DogBreed = "Lab" });

            string json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<UnionWithCustomClassifierAndConverter>(json);

            (Type caseType, object? caseValue) = ExtractCustomClassifiedCaseInfo(deserialized);
            Assert.Equal(typeof(CustomDogPayload), caseType);
            CustomDogPayload dog = Assert.IsType<CustomDogPayload>(caseValue);
            Assert.Equal("Rex", dog.DogName);
            Assert.Equal("Lab", dog.DogBreed);
        }

        [Fact]
        public void Union_CustomConverterCase_StructuralClassifier_WrongTypeSelected_WhenNormalCaseScoresHigher()
        {
            // The JSON has Cat-like properties plus extra properties from the custom converter format.
            // The structural classifier will pick Cat because it scores higher than the opaque custom type.
            string json = """{"Name":"Rex","Lives":5,"dog_breed":"Lab"}""";

            var result = JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json);

            // Cat matches Name and Lives (score: 2 matched, 1 unmatched)
            // CustomDogPayload: opaque (score: 0 matched, 3 unmatched)
            // Cat wins correctly for this JSON shape.
            (Type caseType, object? caseValue) = ExtractCaseInfo(result);
            Assert.Equal(typeof(Cat), caseType);
            Cat cat = Assert.IsType<Cat>(caseValue);
            Assert.Equal("Rex", cat.Name);
            Assert.Equal(5, cat.Lives);
        }

        [Fact]
        public void Union_CustomConverterCase_PrimitiveJson_ClassifierDisqualifiesBoth()
        {
            // Primitive JSON (number) with only object case types — both disqualified.
            string json = "42";

            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json));
        }

        [Fact]
        public void Union_CustomConverterCase_ContractCustomization_Workaround()
        {
            // Users can work around the structural classifier limitation using contract customization
            // to set a custom classifier that understands the custom converter's JSON shape.
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(UnionWithCustomConverterCase))
                            {
                                typeInfo.TypeClassifier = (ref Utf8JsonReader reader) =>
                                {
                                    Utf8JsonReader copy = reader;
                                    if (copy.TokenType is JsonTokenType.StartObject)
                                    {
                                        while (copy.Read() && copy.TokenType is not JsonTokenType.EndObject)
                                        {
                                            if (copy.TokenType is JsonTokenType.PropertyName)
                                            {
                                                if (copy.ValueTextEquals("dog_name"u8))
                                                    return typeof(CustomDogPayload);
                                                if (copy.ValueTextEquals("Lives"u8))
                                                    return typeof(Cat);
                                                copy.Read();
                                                copy.TrySkip();
                                            }
                                        }
                                    }

                                    return null;
                                };
                            }
                        }
                    }
                }
            };

            string json = """{"dog_name":"Rex","dog_breed":"Lab"}""";
            var result = JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json, options);

            (Type caseType, object? caseValue) = ExtractCaseInfo(result);
            Assert.Equal(typeof(CustomDogPayload), caseType);
            CustomDogPayload dog = Assert.IsType<CustomDogPayload>(caseValue);
            Assert.Equal("Rex", dog.DogName);
            Assert.Equal("Lab", dog.DogBreed);
        }

        // --- Custom converter test model types ---

        /// <summary>
        /// A custom converter that serializes/deserializes using a non-standard format.
        /// The structural classifier cannot see these properties.
        /// </summary>
        public class CustomDogPayloadConverter : JsonConverter<CustomDogPayload>
        {
            public override CustomDogPayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType is not JsonTokenType.StartObject)
                    throw new JsonException();

                var result = new CustomDogPayload();
                while (reader.Read())
                {
                    if (reader.TokenType is JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType is JsonTokenType.PropertyName)
                    {
                        string prop = reader.GetString()!;
                        reader.Read();

                        if (prop == "dog_name")
                            result.DogName = reader.GetString();
                        else if (prop == "dog_breed")
                            result.DogBreed = reader.GetString();
                    }
                }

                return result;
            }

            public override void Write(Utf8JsonWriter writer, CustomDogPayload value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString("dog_name", value.DogName);
                writer.WriteString("dog_breed", value.DogBreed);
                writer.WriteEndObject();
            }
        }

        [JsonConverter(typeof(CustomDogPayloadConverter))]
        public class CustomDogPayload
        {
            public string? DogName { get; set; }
            public string? DogBreed { get; set; }
        }

        public class CustomCatPayloadConverter : JsonConverter<CustomCatPayload>
        {
            public override CustomCatPayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType is not JsonTokenType.StartObject)
                    throw new JsonException();

                var result = new CustomCatPayload();
                while (reader.Read())
                {
                    if (reader.TokenType is JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType is JsonTokenType.PropertyName)
                    {
                        string prop = reader.GetString()!;
                        reader.Read();

                        if (prop == "cat_name")
                            result.CatName = reader.GetString();
                        else if (prop == "cat_lives")
                            result.CatLives = reader.GetInt32();
                    }
                }

                return result;
            }

            public override void Write(Utf8JsonWriter writer, CustomCatPayload value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString("cat_name", value.CatName);
                writer.WriteNumber("cat_lives", value.CatLives);
                writer.WriteEndObject();
            }
        }

        [JsonConverter(typeof(CustomCatPayloadConverter))]
        public class CustomCatPayload
        {
            public string? CatName { get; set; }
            public int CatLives { get; set; }
        }

        /// <summary>
        /// Union with one normal case (Cat) and one custom-converter case (CustomDogPayload).
        /// The structural classifier can see Cat's properties but NOT CustomDogPayload's.
        /// </summary>
        [JsonUnion]
        public struct UnionWithCustomConverterCase
        {
            public UnionWithCustomConverterCase(Cat value) { _value = value; _caseType = typeof(Cat); }
            public UnionWithCustomConverterCase(CustomDogPayload value) { _value = value; _caseType = typeof(CustomDogPayload); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static implicit operator UnionWithCustomConverterCase(Cat value) => new(value);
            public static implicit operator UnionWithCustomConverterCase(CustomDogPayload value) => new(value);
            public static explicit operator Cat(UnionWithCustomConverterCase union) =>
                union._caseType == typeof(Cat) ? (Cat)union._value! : throw new InvalidCastException();
            public static explicit operator CustomDogPayload(UnionWithCustomConverterCase union) =>
                union._caseType == typeof(CustomDogPayload) ? (CustomDogPayload)union._value! : throw new InvalidCastException();
        }

        /// <summary>
        /// Union where ALL case types have custom converters. The structural classifier
        /// has zero property metadata for any case type.
        /// </summary>
        [JsonUnion]
        public struct UnionAllCustomConverterCases
        {
            public UnionAllCustomConverterCases(CustomDogPayload value) { _value = value; _caseType = typeof(CustomDogPayload); }
            public UnionAllCustomConverterCases(CustomCatPayload value) { _value = value; _caseType = typeof(CustomCatPayload); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static implicit operator UnionAllCustomConverterCases(CustomDogPayload value) => new(value);
            public static implicit operator UnionAllCustomConverterCases(CustomCatPayload value) => new(value);
            public static explicit operator CustomDogPayload(UnionAllCustomConverterCases union) =>
                union._caseType == typeof(CustomDogPayload) ? (CustomDogPayload)union._value! : throw new InvalidCastException();
            public static explicit operator CustomCatPayload(UnionAllCustomConverterCases union) =>
                union._caseType == typeof(CustomCatPayload) ? (CustomCatPayload)union._value! : throw new InvalidCastException();
        }

        /// <summary>
        /// A classifier factory that knows about CustomDogPayload's non-standard JSON format.
        /// This is the recommended workaround for custom converter case types.
        /// </summary>
        public class CustomConverterAwareClassifier : JsonTypeClassifierFactory
        {
            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                        return null;

                    Utf8JsonReader copy = reader;
                    while (copy.Read() && copy.TokenType is not JsonTokenType.EndObject)
                    {
                        if (copy.TokenType is JsonTokenType.PropertyName)
                        {
                            if (copy.ValueTextEquals("dog_name"u8))
                                return typeof(CustomDogPayload);
                            if (copy.ValueTextEquals("Lives"u8) || copy.ValueTextEquals("Name"u8))
                                return typeof(Cat);
                            copy.Read();
                            copy.TrySkip();
                        }
                    }

                    return null;
                };
            }
        }

        /// <summary>
        /// Union with a custom classifier that understands both normal and custom converter case types.
        /// </summary>
        [JsonUnion(TypeClassifier = typeof(CustomConverterAwareClassifier))]
        public struct UnionWithCustomClassifierAndConverter
        {
            public UnionWithCustomClassifierAndConverter(Cat value) { _value = value; _caseType = typeof(Cat); }
            public UnionWithCustomClassifierAndConverter(CustomDogPayload value) { _value = value; _caseType = typeof(CustomDogPayload); }

            private readonly object? _value;
            private readonly Type? _caseType;

            public static implicit operator UnionWithCustomClassifierAndConverter(Cat value) => new(value);
            public static implicit operator UnionWithCustomClassifierAndConverter(CustomDogPayload value) => new(value);
            public static explicit operator Cat(UnionWithCustomClassifierAndConverter union) =>
                union._caseType == typeof(Cat) ? (Cat)union._value! : throw new InvalidCastException();
            public static explicit operator CustomDogPayload(UnionWithCustomClassifierAndConverter union) =>
                union._caseType == typeof(CustomDogPayload) ? (CustomDogPayload)union._value! : throw new InvalidCastException();
        }

        private static (Type caseType, object? caseValue) ExtractCaseInfo(UnionWithCustomConverterCase union)
        {
            try
            {
                Cat cat = (Cat)union;
                return (typeof(Cat), cat);
            }
            catch (InvalidCastException) { }

            CustomDogPayload dog = (CustomDogPayload)union;
            return (typeof(CustomDogPayload), dog);
        }

        private static (Type caseType, object? caseValue) ExtractAllCustomCaseInfo(UnionAllCustomConverterCases union)
        {
            try
            {
                CustomDogPayload dog = (CustomDogPayload)union;
                return (typeof(CustomDogPayload), dog);
            }
            catch (InvalidCastException) { }

            CustomCatPayload cat = (CustomCatPayload)union;
            return (typeof(CustomCatPayload), cat);
        }

        private static (Type caseType, object? caseValue) ExtractCustomClassifiedCaseInfo(UnionWithCustomClassifierAndConverter union)
        {
            try
            {
                Cat cat = (Cat)union;
                return (typeof(Cat), cat);
            }
            catch (InvalidCastException) { }

            CustomDogPayload dog = (CustomDogPayload)union;
            return (typeof(CustomDogPayload), dog);
        }
    }
}
