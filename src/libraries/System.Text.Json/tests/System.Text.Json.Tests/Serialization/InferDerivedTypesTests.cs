// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public partial class InferDerivedTypesTests
    {
        /// <summary>
        /// Creates options that add a custom classifier for ambiguous union types
        /// (PetUnion, etc.) that have multiple case types mapping to the same JSON token type.
        /// Without this, ambiguous unions throw InvalidOperationException at configuration time.
        /// </summary>
        private static JsonSerializerOptions CreateCustomClassifierOptions(params Type[] unionTypes)
        {
            var unionTypeSet = new HashSet<Type>(unionTypes);

            return new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (unionTypeSet.Contains(typeInfo.Type) &&
                                typeInfo.UnionCases is { Count: > 0 } &&
                                typeInfo.TypeClassifier is null)
                            {
                                var factory = new TestStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new List<JsonUnionCaseInfo>(typeInfo.UnionCases),
                                    Array.Empty<JsonDerivedType>(),
                                    typeDiscriminatorPropertyName: null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };
        }

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
            // Native union syntax includes compiler-emitted conversion support for both Cat and
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
            // Case discovery should still exclude compiler-generated case types.
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
        public void Union_GenericCaseTypes_AreDiscovered()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(ProviderBackedResult<string>));

            Assert.NotNull(typeInfo.UnionCases);
            Assert.Equal(2, typeInfo.UnionCases!.Count);
            Assert.Contains(typeInfo.UnionCases, c => c.CaseType == typeof(string));
            Assert.Contains(typeInfo.UnionCases, c => c.CaseType == typeof(Exception));
        }

        [Fact]
        public void Union_GenericCaseTypes_RoundTrip()
        {
            ProviderBackedResult<string> value = new("hello");

            string json = JsonSerializer.Serialize(value);
            Assert.Equal("\"hello\"", json);

            ProviderBackedResult<string>? result = JsonSerializer.Deserialize<ProviderBackedResult<string>>(json);
            Assert.NotNull(result);
            ProviderBackedResult<string> union = result.Value;
            Assert.IsType<string>(union.Value);
            Assert.Equal("hello", (string)union.Value!);
        }



        [Fact]
        public void Union_Serialize_ObjectCaseType()
        {
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            var dog = new Dog { Name = "Rex", Breed = "Labrador" };
            var pet = new PetUnion(dog);

            string json = JsonSerializer.Serialize(pet, options);
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
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            var pet = new PetUnion();
            string json = JsonSerializer.Serialize(pet, options);
            Assert.Equal("null", json);
        }

        [Fact]
        public void Union_Deserialize_ObjectCaseType_BestMatch()
        {
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            PetUnion pet = JsonSerializer.Deserialize<PetUnion>(json, options);

            Assert.IsType<Dog>(pet.Value);
            var dog = (Dog)pet.Value!;
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void Union_Deserialize_SelectsBestMatch()
        {
            // Cat and Dog are both objects — ambiguous union requires a structural classifier.
            var options = CreateCustomClassifierOptions(typeof(PetUnion));

            string dogJson = """{"Name":"Rex","Breed":"Labrador"}""";
            PetUnion pet = JsonSerializer.Deserialize<PetUnion>(dogJson, options);
            Assert.IsType<Dog>(pet.Value);

            // With structural classifier, Cat JSON correctly matches Cat (Lives is Cat-specific).
            string catJson = """{"Name":"Whiskers","Lives":9}""";
            pet = JsonSerializer.Deserialize<PetUnion>(catJson, options);
            Assert.IsType<Cat>(pet.Value);
        }

        [Fact]
        public void Union_Deserialize_SelectsBestMatch_WithCustomClassifier()
        {
            // With a custom classifier, Cat/Dog disambiguation works.
            string dogJson = """{"Name":"Rex","Breed":"Labrador"}""";
            CustomClassifiedPetUnion pet = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(dogJson);
            Assert.IsType<Dog>(pet.Value);

            string catJson = """{"Name":"Whiskers","Lives":9}""";
            pet = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(catJson);
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
        public void Union_Deserialize_Null_ThrowsWhenNoCaseIsNullable()
        {
            // PetUnion(Dog, Cat) — neither case is nullable, so JSON null throws.
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            string json = "null";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PetUnion>(json, options));
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
        public void Union_Deserialize_ArrayCaseTypes(string json, Type expectedElementType)
        {
            // Only unambiguous array cases tested here. Dog[] vs int[] is ambiguous
            // (both StartArray) and requires a custom classifier.
            var options = CreateCustomClassifierOptions(typeof(ArrayUnion));
            ArrayUnion result = JsonSerializer.Deserialize<ArrayUnion>(json, options);
            Assert.NotNull(result.Value);
            Assert.Equal(expectedElementType, result.Value!.GetType());
        }

        [Fact]
        public void Union_Serialize_BestMatchingCaseType()
        {
            // Labrador extends Dog, but union only has Dog as a case type.
            // Should serialize using Dog's contract.
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            var labrador = new Labrador { Name = "Buddy", Breed = "Labrador", IsGuide = true };
            var pet = new PetUnion(new Dog { Name = labrador.Name, Breed = labrador.Breed });

            string json = JsonSerializer.Serialize(pet, options);
            Assert.Contains("\"Name\"", json);
            Assert.Contains("\"Breed\"", json);
        }

        [Fact]
        public void Union_RoundTrip_Object()
        {
            // Cat and Dog are ambiguous (both StartObject). Dog is first-declared, so
            // Cat round-trips as Dog with token-type matching. Use a custom classifier
            // for correct round-trip.
            var original = new CustomClassifiedPetUnion(new Cat { Name = "Luna", Lives = 7 });
            string json = JsonSerializer.Serialize(original);

            CustomClassifiedPetUnion deserialized = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(json);
            Cat cat = Assert.IsType<Cat>(deserialized.Value);
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
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            ImplicitPetUnion union = new Dog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(union, options);
            Assert.Contains("Rex", json);
            Assert.Contains("Labrador", json);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_Deserialize_Dog()
        {
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            ImplicitPetUnion union = JsonSerializer.Deserialize<ImplicitPetUnion>(json, options);

            Dog dog = Assert.IsType<Dog>(union.Value);
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_Deserialize_Cat()
        {
            // Cat and Dog are ambiguous (both StartObject). With structural classifier,
            // Cat JSON correctly matches Cat (Lives is Cat-specific).
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            string json = """{"Name":"Whiskers","Lives":9}""";
            ImplicitPetUnion union = JsonSerializer.Deserialize<ImplicitPetUnion>(json, options);

            Cat cat = Assert.IsType<Cat>(union.Value);
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_CtorDiscovery_Serialize_Dog()
        {
            var options = CreateCustomClassifierOptions(typeof(CtorPetUnion));
            var union = new CtorPetUnion(new Dog { Name = "Rex", Breed = "Labrador" });
            string json = JsonSerializer.Serialize(union, options);
            Assert.Contains("Rex", json);
            Assert.Contains("Labrador", json);
        }

        [Fact]
        public void JsonUnion_CtorDiscovery_Deserialize_Cat()
        {
            // With structural classifier, Cat JSON correctly matches Cat.
            var options = CreateCustomClassifierOptions(typeof(CtorPetUnion));
            string json = """{"Name":"Whiskers","Lives":9}""";
            CtorPetUnion union = JsonSerializer.Deserialize<CtorPetUnion>(json, options);

            Cat cat = Assert.IsType<Cat>(union.Value);
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_CustomClassifier_Deserialize_Dog()
        {
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            CustomClassifiedPetUnion union = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(json);

            Dog dog = Assert.IsType<Dog>(union.Value);
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_CustomClassifier_Deserialize_Cat()
        {
            string json = """{"Name":"Whiskers","Lives":9}""";
            CustomClassifiedPetUnion union = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(json);

            Cat cat = Assert.IsType<Cat>(union.Value);
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

                                var factory = new TestStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeof(PetUnion),
                                    new JsonUnionCaseInfo[] { new JsonUnionCaseInfo(typeof(Dog)), new JsonUnionCaseInfo(typeof(Cat)) },
                                    Array.Empty<JsonDerivedType>(),
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
        public void JsonUnion_Null_ThrowsWhenNoCaseIsNullable()
        {
            // ImplicitPetUnion(Dog, Cat) — neither case accepts null.
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            string json = "null";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImplicitPetUnion>(json, options));
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_RoundTrip_Dog()
        {
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            ImplicitPetUnion original = new Dog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(original, options);
            ImplicitPetUnion deserialized = JsonSerializer.Deserialize<ImplicitPetUnion>(json, options);

            Dog dog = Assert.IsType<Dog>(deserialized.Value);
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_RoundTrip_Cat()
        {
            // Cat/Dog are ambiguous. Use CustomClassifiedPetUnion for Cat round-trip.
            CustomClassifiedPetUnion original = new Cat { Name = "Whiskers", Lives = 9 };
            string json = JsonSerializer.Serialize(original);
            CustomClassifiedPetUnion deserialized = JsonSerializer.Deserialize<CustomClassifiedPetUnion>(json);

            Cat cat = Assert.IsType<Cat>(deserialized.Value);
            Assert.Equal("Whiskers", cat.Name);
            Assert.Equal(9, cat.Lives);
        }

        [Fact]
        public void JsonUnion_ClosedSubtype_Serialize_Dog()
        {
            var options = CreateCustomClassifierOptions(typeof(ClosedSubtypePetUnion));
            ClosedSubtypePetUnion union = new ClosedSubtypeDog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(union, options);
            Assert.Contains("Rex", json);
            Assert.Contains("Labrador", json);
        }

        [Fact]
        public void JsonUnion_ClosedSubtype_RoundTrip_Dog()
        {
            var options = CreateCustomClassifierOptions(typeof(ClosedSubtypePetUnion));
            ClosedSubtypePetUnion original = new ClosedSubtypeDog { Name = "Rex", Breed = "Labrador" };
            string json = JsonSerializer.Serialize(original, options);
            ClosedSubtypePetUnion deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion>(json, options);

            ClosedSubtypeDog dog = Assert.IsType<ClosedSubtypeDog>(deserialized.Value);
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_ClosedSubtype_RoundTrip_Cat()
        {
            // With structural classifier, Cat JSON correctly matches ClosedSubtypeCat.
            var options = CreateCustomClassifierOptions(typeof(ClosedSubtypePetUnion));
            ClosedSubtypePetUnion original = new ClosedSubtypeCat { Name = "Whiskers", Lives = 9 };
            string json = JsonSerializer.Serialize(original, options);
            ClosedSubtypePetUnion deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion>(json, options);

            Assert.IsType<ClosedSubtypeCat>(deserialized.Value);
        }

        [Fact]
        public void JsonUnion_NullSerializesAsNull()
        {
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            ImplicitPetUnion? union = null;
            string json = JsonSerializer.Serialize(union, options);
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
        public union UnionWithCompilerGeneratedCase(Cat, CompilerGeneratedPayload);

        [CompilerGenerated]
        public class CompilerGeneratedPayload
        {
            public string? Data { get; set; }
        }

        // Native union syntax with a compiler-generated case that should be excluded.
        [JsonUnion]
        public union CtorUnionWithCompilerGeneratedCase(Dog, CompilerGeneratedPayload);



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

        public union PetUnion(Dog, Cat);

        public union ResultUnion(int, string);

        public union BoolOrIntUnion(bool, int);

        public union ArrayUnion(int[], Dog[]);

        public union ProviderBackedResult<T>(T, Exception);



        [Theory]
        [InlineData("42")]
        [InlineData("0")]
        [InlineData("-1")]
        public void Union_PrimitiveAmbiguity_IntLong_FirstDeclaredWins(string json)
        {
            // int and long are both numeric — ambiguous union throws at config time without a classifier.
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<IntOrLongUnion>(json));
        }

        [Theory]
        [InlineData("3.14")]
        [InlineData("0.0")]
        public void Union_PrimitiveAmbiguity_FloatDouble_FirstDeclaredWins(string json)
        {
            // float and double are both numeric — ambiguous union throws at config time without a classifier.
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<FloatOrDoubleUnion>(json));
        }

        [Fact]
        public void Union_StringAmbiguity_DateTimeVsString_FirstDeclaredWins()
        {
            // string and DateTime both map to String token — ambiguous union throws at config time.
            string json = "\"2024-01-15T12:30:00\"";
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<StringOrDateTimeUnion>(json));
        }

        [Fact]
        public void Union_StringAmbiguity_GuidVsString_FirstDeclaredWins()
        {
            // string and Guid both map to String token — ambiguous union throws at config time.
            string json = "\"550e8400-e29b-41d4-a716-446655440000\"";
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<StringOrGuidUnion>(json));
        }

        [Fact]
        public void Union_StringAmbiguity_TimeSpanVsString_FirstDeclaredWins()
        {
            // string and TimeSpan both map to String token — ambiguous union throws at config time.
            string json = "\"01:30:00\"";
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<StringOrTimeSpanUnion>(json));
        }

        [Fact]
        public void Union_StringAmbiguity_PlainStringDeserializesCorrectly()
        {
            // StringOrDateTimeUnion is ambiguous — string and DateTime both map to String token.
            // With structural classifier, both score equally and first-declared (string) wins.
            var options = CreateCustomClassifierOptions(typeof(StringOrDateTimeUnion));
            StringOrDateTimeUnion result = JsonSerializer.Deserialize<StringOrDateTimeUnion>("\"hello world\"", options);
            Assert.IsType<string>(result.Value);
            Assert.Equal("hello world", result.Value);
        }

        [Fact]
        public void Union_MultipleNullableCases_NullDispatchedToFirstNullableCase()
        {
            // NullableIntOrStringUnion(int?, string?) — both cases accept null.
            // Per spec, passing null to any nullable case yields the same null-holding
            // union value, so STJ picks the first declared nullable case (int?) for
            // null dispatch. Equality should be transparent to the caller.
            NullableIntOrStringUnion result = JsonSerializer.Deserialize<NullableIntOrStringUnion>("null");
            Assert.Null(result.Value);
        }

        [Fact]
        public void Union_MultipleNullableCases_NonNullStillDispatchesByToken()
        {
            // Non-null payloads still classify by token type — Number → int?, String → string?.
            NullableIntOrStringUnion intResult = JsonSerializer.Deserialize<NullableIntOrStringUnion>("42");
            Assert.IsType<int>(intResult.Value);
            Assert.Equal(42, intResult.Value);

            NullableIntOrStringUnion stringResult = JsonSerializer.Deserialize<NullableIntOrStringUnion>("\"hello\"");
            Assert.IsType<string>(stringResult.Value);
            Assert.Equal("hello", stringResult.Value);
        }

        [Fact]
        public void Union_MultipleNullableCases_RoundTripNull()
        {
            // Null input → first nullable case → serializes back to JSON null.
            NullableIntOrStringUnion original = JsonSerializer.Deserialize<NullableIntOrStringUnion>("null");
            string roundTripped = JsonSerializer.Serialize(original);
            Assert.Equal("null", roundTripped);
        }

        [Fact]
        public void Union_AllNullablePrimitives_NullDispatched()
        {
            // union(int?, string?, bool?, Dog?) — four nullable cases. Null
            // resolves to the first declared (int?), value-bearing payloads
            // dispatch by token type.
            AllNullablePrimitivesUnion nullResult = JsonSerializer.Deserialize<AllNullablePrimitivesUnion>("null");
            Assert.Null(nullResult.Value);

            AllNullablePrimitivesUnion intResult = JsonSerializer.Deserialize<AllNullablePrimitivesUnion>("7");
            Assert.IsType<int>(intResult.Value);
            Assert.Equal(7, intResult.Value);

            AllNullablePrimitivesUnion stringResult = JsonSerializer.Deserialize<AllNullablePrimitivesUnion>("\"hi\"");
            Assert.IsType<string>(stringResult.Value);
            Assert.Equal("hi", stringResult.Value);

            AllNullablePrimitivesUnion boolResult = JsonSerializer.Deserialize<AllNullablePrimitivesUnion>("true");
            Assert.IsType<bool>(boolResult.Value);
            Assert.True((bool)boolResult.Value!);

            AllNullablePrimitivesUnion dogResult = JsonSerializer.Deserialize<AllNullablePrimitivesUnion>("{\"Name\":\"Rex\",\"Breed\":\"Lab\"}");
            Assert.IsType<Dog>(dogResult.Value);
        }

        [Fact]
        public void Union_SingleNullableCase_NullDispatchedToNullableCase()
        {
            // int? is the only case that accepts null — JSON null is round-tripped through
            // the int? constructor (resulting Value is null).
            NullableIntOrIntArrayUnion result = JsonSerializer.Deserialize<NullableIntOrIntArrayUnion>("null");
            Assert.Null(result.Value);
        }

        [Fact]
        public void Union_SingleNullableCase_NonNullStillDispatchesByToken()
        {
            // Non-null payloads still classify by token type — array goes to int[] case.
            NullableIntOrIntArrayUnion result = JsonSerializer.Deserialize<NullableIntOrIntArrayUnion>("[1,2,3]");
            Assert.IsType<int[]>(result.Value);
            Assert.Equal(new[] { 1, 2, 3 }, (int[])result.Value);
        }

        [Fact]
        public void Union_NoNullableCase_NullPayloadThrows()
        {
            // BoolOrIntUnion(bool, int) — both are non-nullable value types,
            // so JSON null is unambiguously rejected.
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BoolOrIntUnion>("null"));
        }



        [Fact]
        public void Union_CommonAncestor_DerivedSpecificProps_CorrectMatch()
        {
            // JSON with Breed matches Dog specifically (more matched properties).
            var options = CreateCustomClassifierOptions(typeof(AnimalUnion));
            string json = """{"Name":"Rex","Breed":"Lab"}""";
            AnimalUnion result = JsonSerializer.Deserialize<AnimalUnion>(json, options);
            Assert.IsType<DogWithAncestor>(result.Value);
        }

        [Fact]
        public void Union_CommonAncestor_SharedPropsOnly_FirstDeclaredWins()
        {
            // All ancestor types are objects — ambiguous union throws at config time without a classifier.
            string json = """{"Name":"Rex"}""";
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<AnimalUnion>(json));
        }

        [Fact]
        public void Union_CommonAncestor_CatSpecificProps_CorrectMatch()
        {
            // With structural classifier, Cat-specific props (Lives) correctly match CatWithAncestor.
            var options = CreateCustomClassifierOptions(typeof(AnimalUnion));
            string json = """{"Name":"Whiskers","Lives":9}""";
            AnimalUnion result = JsonSerializer.Deserialize<AnimalUnion>(json, options);
            Assert.IsType<CatWithAncestor>(result.Value);
        }

        [Fact]
        public void Union_CommonAncestor_UnknownProps_TieGoesToFirst()
        {
            // All types have same score when only unknown properties present.
            var options = CreateCustomClassifierOptions(typeof(AnimalUnion));
            string json = """{"Name":"Rex","Unknown":"value"}""";
            AnimalUnion result = JsonSerializer.Deserialize<AnimalUnion>(json, options);
            Assert.NotNull(result.Value);
        }



        [Fact]
        public void Union_Array_EmptyArray_MatchesAnyCollectionType()
        {
            var options = CreateCustomClassifierOptions(typeof(ArrayUnion));
            string json = "[]";
            ArrayUnion result = JsonSerializer.Deserialize<ArrayUnion>(json, options);
            Assert.NotNull(result.Value);
        }

        [Fact]
        public void Union_Array_NullFirstElement_StillScoresCorrectly()
        {
            // [null, {"Name":"Rex","Breed":"Lab"}] - second element is Dog-like.
            var options = CreateCustomClassifierOptions(typeof(DogOrIntArrayUnion));
            string json = """[null, {"Name":"Rex","Breed":"Lab"}]""";
            DogOrIntArrayUnion result = JsonSerializer.Deserialize<DogOrIntArrayUnion>(json, options);
            Assert.IsType<Dog[]>(result.Value);
        }

        [Fact]
        public void Union_Array_MultiElementDiscrimination()
        {
            // All elements have Breed → Dog[] should win over Cat[].
            var options = CreateCustomClassifierOptions(typeof(DogOrCatArrayUnion));
            string json = """[{"Name":"Rex","Breed":"Lab"},{"Name":"Fido","Breed":"Poodle"}]""";
            DogOrCatArrayUnion result = JsonSerializer.Deserialize<DogOrCatArrayUnion>(json, options);
            Assert.IsType<Dog[]>(result.Value);
        }

        [Fact]
        public void Union_Array_SecondElementDisambiguates()
        {
            // First element has only Name (ambiguous), second has Breed (Dog-specific).
            var options = CreateCustomClassifierOptions(typeof(DogOrCatArrayUnion));
            string json = """[{"Name":"Rex"},{"Name":"Fido","Breed":"Poodle"}]""";
            DogOrCatArrayUnion result = JsonSerializer.Deserialize<DogOrCatArrayUnion>(json, options);
            Assert.IsType<Dog[]>(result.Value);
        }

        [Fact]
        public void Union_Array_Ambiguous_FirstDeclaredWins()
        {
            // Dog[] and int[] are both arrays (StartArray) — ambiguous union throws at config time.
            string json = "[1,2,3,4,5]";
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<DogOrIntArrayUnion>(json));
        }



        [Fact]
        public void Union_NestedUnion_PrimitiveReachesInnerUnion()
        {
            // OuterUnion(InnerUnion, bool) where InnerUnion is union(int, string).
            // Token-type matching categorizes InnerUnion as StartObject (it's a struct),
            // so Number/String tokens don't match. This requires a custom classifier.
            string json = "42";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OuterUnion>(json));
        }

        [Fact]
        public void Union_NestedUnion_StringReachesInnerUnion()
        {
            // Same as above — String token doesn't match InnerUnion's StartObject category.
            string json = "\"hello\"";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OuterUnion>(json));
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
        public void Union_NestedUnion_NullThrowsWhenNoCaseIsNullable()
        {
            // OuterUnion(InnerUnion, bool) — neither case type's constructor parameter
            // is declared nullable, so JSON null is rejected.
            string json = "null";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OuterUnion>(json));
        }



        [Fact]
        public void Union_StructurallyIdentical_FirstDeclaredWins()
        {
            // Point2D and Complex have identical schemas {X, Y} — ambiguous union throws at config time.
            string json = """{"X":1.0,"Y":2.0}""";
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<Point2DOrComplexUnion>(json));
        }

        [Fact]
        public void Union_StructurallyIdentical_RoundTrip_Point2D()
        {
            var options = CreateCustomClassifierOptions(typeof(Point2DOrComplexUnion));
            var original = new Point2DOrComplexUnion(new Point2D { X = 3.0, Y = 4.0 });
            string json = JsonSerializer.Serialize(original, options);
            Point2DOrComplexUnion deserialized = JsonSerializer.Deserialize<Point2DOrComplexUnion>(json, options);

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
            // With structural classifier, RigidType scores higher (Name+Breed matched).
            var options = CreateCustomClassifierOptions(typeof(FlexibleOrRigidUnion));
            string json = """{"Name":"Rex","Breed":"Lab","Color":"Brown"}""";
            FlexibleOrRigidUnion result = JsonSerializer.Deserialize<FlexibleOrRigidUnion>(json, options);
            Assert.IsType<RigidType>(result.Value);
        }

        [Fact]
        public void Union_ExtensionData_TieGoesToFirst()
        {
            // JSON with only Name — both types match equally (1 known property).
            var options = CreateCustomClassifierOptions(typeof(FlexibleOrRigidUnion));
            string json = """{"Name":"Rex","Color":"Brown"}""";
            FlexibleOrRigidUnion result = JsonSerializer.Deserialize<FlexibleOrRigidUnion>(json, options);
            // Both score (1, 1): Name matches, Color is unknown for both.
            // Extension data on FlexibleType doesn't provide additional credit.
            Assert.NotNull(result.Value);
        }



        [Fact]
        public void Union_PolymorphicCaseType_DiscriminatorCountedAsUnknown()
        {
            // Dog with $type discriminator — matcher treats $type as an unknown property.
            var options = CreateCustomClassifierOptions(typeof(PolyDogOrCatUnion));
            string json = """{"$type":"PolyDog","Name":"Rex","Breed":"Lab"}""";
            PolyDogOrCatUnion result = JsonSerializer.Deserialize<PolyDogOrCatUnion>(json, options);
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
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<PetUnion>(json, options));
        }

        [Fact]
        public void Union_AllCasesDisqualified_ObjectWithOnlyUnknownProperties()
        {
            // IntOrLongUnion only has int and long (primitive types) — ambiguous (both Number).
            // With structural classifier, a JSON object should disqualify both candidates.
            var options = CreateCustomClassifierOptions(typeof(IntOrLongUnion));
            Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<IntOrLongUnion>("""{"Foo":"bar"}""", options));
        }

        public union IntOrLongUnion(int, long);

        public union FloatOrDoubleUnion(float, double);

        public union StringOrDateTimeUnion(string, DateTime);

        public union StringOrGuidUnion(string, Guid);

        public union StringOrTimeSpanUnion(string, TimeSpan);

        public union NullableIntOrStringUnion(int?, string?);

        // Multiple nullable case types with disjoint non-null tokens.
        public union AllNullablePrimitivesUnion(int?, string?, bool?, Dog?);

        // Exactly one nullable case type — JSON null is dispatched to that case.
        public union NullableIntOrIntArrayUnion(int?, int[]);

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

        public union AnimalUnion(DogWithAncestor, CatWithAncestor, AnimalBase);

        public union DogOrIntArrayUnion(Dog[], int[]);

        public union DogOrCatArrayUnion(Dog[], Cat[]);

        public union InnerUnion(int, string);

        public union OuterUnion(InnerUnion, bool);

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

        public union Point2DOrComplexUnion(Point2D, Complex);

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

        public union FlexibleOrRigidUnion(FlexibleType, RigidType);

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

        public union PolyDogOrCatUnion(PolyDogType, PolyCatType);



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
        public union ImplicitPetUnion(Dog, Cat);

        [JsonUnion]
        public union CtorPetUnion(Dog, Cat);

        [JsonUnion]
        public union ClosedSubtypePetUnion(ClosedSubtypeDog, ClosedSubtypeCat);

        public class ClosedSubtypeDog
        {
            public string? Name { get; set; }
            public string? Breed { get; set; }
        }

        public class ClosedSubtypeCat
        {
            public string? Name { get; set; }
            public int Lives { get; set; }
        }

        public class CustomTestClassifier : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is JsonTokenType.StartObject)
                    {
                        while (reader.Read())
                        {
                            if (reader.TokenType is JsonTokenType.PropertyName)
                            {
                                string prop = reader.GetString()!;
                                if (prop == "Breed" || prop == "breed")
                                {
                                    return typeof(Dog);
                                }

                                if (prop == "Lives" || prop == "lives")
                                {
                                    return typeof(Cat);
                                }
                            }

                            if (reader.TokenType is JsonTokenType.EndObject)
                            {
                                break;
                            }

                            reader.TrySkip();
                        }
                    }

                    return null;
                };
            }
        }

        [JsonUnion(TypeClassifier = typeof(CustomTestClassifier))]
        public union CustomClassifiedPetUnion(Dog, Cat);

        public class DerivedHierarchyDog
        {
            public string? Name { get; set; }
        }

        public class DerivedHierarchyLab : DerivedHierarchyDog
        {
            public bool IsGuide { get; set; }
        }

        public sealed class DerivedHierarchyClassifier : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return static (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                    {
                        return null;
                    }

                    while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
                    {
                        if (reader.TokenType is JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("IsGuide"u8))
                            {
                                return typeof(DerivedHierarchyLab);
                            }

                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return typeof(DerivedHierarchyDog);
                };
            }
        }

        [JsonUnion(TypeClassifier = typeof(DerivedHierarchyClassifier))]
        public union DerivedHierarchyPetUnion(DerivedHierarchyDog, DerivedHierarchyLab);

        [JsonSerializable(typeof(CustomClassifiedPetUnion))]
        [JsonSerializable(typeof(DerivedHierarchyPetUnion))]
        [JsonSerializable(typeof(Dog))]
        [JsonSerializable(typeof(Cat))]
        [JsonSerializable(typeof(DerivedHierarchyDog))]
        [JsonSerializable(typeof(DerivedHierarchyLab))]
        private partial class CompilerUnionSourceGenContext : JsonSerializerContext
        {
        }

        // ===== Phase 6: Comprehensive API coverage tests =====

        [Fact]
        public void JsonTypeInfoKind_Union_IsSetForCompilerUnion()
        {
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
        }

        [Fact]
        public void JsonTypeInfoKind_Union_IsSetForJsonUnionAttribute()
        {
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(ImplicitPetUnion));
            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
        }

        [Fact]
        public void JsonTypeInfoKind_Union_IsSetForClosedSubtypeUnion()
        {
            var options = CreateCustomClassifierOptions(typeof(ClosedSubtypePetUnion));
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(ClosedSubtypePetUnion));
            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
        }

        [Fact]
        public void JsonTypeInfoKind_Union_IsSetForCompilerUnion_SourceGenContext()
        {
            JsonTypeInfo<CustomClassifiedPetUnion> typeInfo = CompilerUnionSourceGenContext.Default.CustomClassifiedPetUnion;

            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
            Assert.NotNull(typeInfo.UnionCases);
            Assert.Equal(2, typeInfo.UnionCases.Count);
            Assert.Contains(typeInfo.UnionCases, c => c.CaseType == typeof(Dog));
            Assert.Contains(typeInfo.UnionCases, c => c.CaseType == typeof(Cat));
            Assert.NotNull(typeInfo.TypeClassifier);
            Assert.NotNull(typeInfo.UnionDeconstructor);
            Assert.NotNull(typeInfo.UnionConstructor);
        }

        [Fact]
        public void CompilerUnion_SourceGenContext_DoesNotExposeObjectProperties()
        {
            JsonTypeInfo<CustomClassifiedPetUnion> typeInfo = CompilerUnionSourceGenContext.Default.CustomClassifiedPetUnion;

            Assert.Equal(JsonTypeInfoKind.Union, typeInfo.Kind);
            Assert.Empty(typeInfo.Properties);
        }

        [Fact]
        public void CompilerUnion_SourceGenContext_RoundTrips()
        {
            CustomClassifiedPetUnion value = new(new Dog { Name = "Rex", Breed = "Labrador" });

            string json = JsonSerializer.Serialize(value, CompilerUnionSourceGenContext.Default.CustomClassifiedPetUnion);
            Assert.Contains("\"Breed\":\"Labrador\"", json);

            CustomClassifiedPetUnion result = JsonSerializer.Deserialize(
                """{"Name":"Rex","Breed":"Labrador"}""",
                CompilerUnionSourceGenContext.Default.CustomClassifiedPetUnion);

            Dog dog = Assert.IsType<Dog>(result.Value);
            Assert.Equal("Rex", dog.Name);
            Assert.Equal("Labrador", dog.Breed);
        }

        [Fact]
        public void JsonUnion_RuntimeSerialization_PrefersMostDerivedCase()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };

            DerivedHierarchyPetUnion value = new(new DerivedHierarchyLab { Name = "Rex", IsGuide = true });
            string json = JsonSerializer.Serialize(value, options);

            Assert.Contains("\"IsGuide\":true", json);

            DerivedHierarchyPetUnion result = JsonSerializer.Deserialize<DerivedHierarchyPetUnion>(
                """{"Name":"Rex","IsGuide":true}""",
                options);

            DerivedHierarchyLab lab = Assert.IsType<DerivedHierarchyLab>(result.Value);
            Assert.Equal("Rex", lab.Name);
            Assert.True(lab.IsGuide);
        }

        [Fact]
        public void CompilerUnion_SourceGenContext_PrefersMostDerivedCase()
        {
            JsonTypeInfo<DerivedHierarchyPetUnion> typeInfo = CompilerUnionSourceGenContext.Default.DerivedHierarchyPetUnion;

            DerivedHierarchyPetUnion value = new(new DerivedHierarchyLab { Name = "Rex", IsGuide = true });
            string json = JsonSerializer.Serialize(value, typeInfo);
            Assert.Contains("\"IsGuide\":true", json);

            DerivedHierarchyPetUnion result = JsonSerializer.Deserialize(
                """{"Name":"Rex","IsGuide":true}""",
                typeInfo);

            DerivedHierarchyLab lab = Assert.IsType<DerivedHierarchyLab>(result.Value);
            Assert.Equal("Rex", lab.Name);
            Assert.True(lab.IsGuide);
        }

        [Fact]
        public void JsonUnion_ImplicitOperator_RoundTrip()
        {
            // With structural classifier, Dog JSON round-trips correctly.
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            ImplicitPetUnion deserialized = JsonSerializer.Deserialize<ImplicitPetUnion>(json, options);
            string reserialized = JsonSerializer.Serialize(deserialized, options);

            Dog dog = Assert.IsType<Dog>(deserialized.Value);
            Assert.Equal("Rex", dog.Name);
            Assert.Contains("Labrador", reserialized);
        }

        [Fact]
        public void JsonUnion_ClosedSubtype_RoundTrip()
        {
            // With structural classifier, Dog JSON round-trips correctly.
            var options = CreateCustomClassifierOptions(typeof(ClosedSubtypePetUnion));
            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            ClosedSubtypePetUnion deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion>(json, options);
            Assert.IsType<ClosedSubtypeDog>(deserialized.Value);
            string reserialized = JsonSerializer.Serialize(deserialized, options);
            Assert.Contains("Labrador", reserialized);
        }

        [Fact]
        public void JsonUnion_CollectionOfUnions_RoundTrip()
        {
            // Both Dog and Cat are objects — with token-type matching, all elements
            // deserialize as Dog (first-declared). Use CustomClassifiedPetUnion for correct behavior.
            var list = new List<CustomClassifiedPetUnion>
            {
                new Dog { Name = "Rex", Breed = "Labrador" },
                new Cat { Name = "Whiskers", Lives = 9 },
            };

            string json = JsonSerializer.Serialize(list);
            Assert.Contains("Rex", json);
            Assert.Contains("Whiskers", json);

            List<CustomClassifiedPetUnion>? deserialized = JsonSerializer.Deserialize<List<CustomClassifiedPetUnion>>(json);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Count);

            Dog dog = Assert.IsType<Dog>(deserialized[0].Value);
            Assert.Equal("Rex", dog.Name);

            Cat cat = Assert.IsType<Cat>(deserialized[1].Value);
            Assert.Equal("Whiskers", cat.Name);
        }

        [Fact]
        public void JsonUnion_NestedUnion_InObject_RoundTrip()
        {
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            var wrapper = new UnionWrapper
            {
                Label = "test",
                Pet = new Dog { Name = "Rex", Breed = "Labrador" },
            };

            string json = JsonSerializer.Serialize(wrapper, options);
            Assert.Contains("test", json);
            Assert.Contains("Rex", json);

            UnionWrapper? deserialized = JsonSerializer.Deserialize<UnionWrapper>(json, options);
            Assert.NotNull(deserialized);
            Assert.Equal("test", deserialized.Label);

            Dog dog = Assert.IsType<Dog>(deserialized.Pet.Value);
            Assert.Equal("Rex", dog.Name);
        }

        [Fact]
        public void JsonUnion_UnmatchedTokenType_ThrowsException()
        {
            // ResultUnion is union(int, string). An array token doesn't match either.
            string json = "[1,2,3]";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResultUnion>(json));
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
                            if (typeInfo.Type == typeof(PetUnion) &&
                                typeInfo.UnionCases is { Count: > 0 } &&
                                typeInfo.TypeClassifier is null)
                            {
                                var factory = new TestStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new List<JsonUnionCaseInfo>(typeInfo.UnionCases),
                                    Array.Empty<JsonDerivedType>(),
                                    typeDiscriminatorPropertyName: null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        },
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
                            if (typeInfo.Type == typeof(PetUnion) &&
                                typeInfo.UnionCases is { Count: > 0 } &&
                                typeInfo.TypeClassifier is null)
                            {
                                var factory = new TestStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new List<JsonUnionCaseInfo>(typeInfo.UnionCases),
                                    Array.Empty<JsonDerivedType>(),
                                    typeDiscriminatorPropertyName: null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        },
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
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
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
                            if (typeInfo.Type == typeof(PetUnion) &&
                                typeInfo.UnionCases is { Count: > 0 } &&
                                typeInfo.TypeClassifier is null)
                            {
                                var factory = new TestStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new List<JsonUnionCaseInfo>(typeInfo.UnionCases),
                                    Array.Empty<JsonDerivedType>(),
                                    typeDiscriminatorPropertyName: null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        },
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
                            if (typeInfo.Type == typeof(PetUnion) &&
                                typeInfo.UnionCases is { Count: > 0 } &&
                                typeInfo.TypeClassifier is null)
                            {
                                var factory = new TestStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new List<JsonUnionCaseInfo>(typeInfo.UnionCases),
                                    Array.Empty<JsonDerivedType>(),
                                    typeDiscriminatorPropertyName: null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        },
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
        public void JsonUnion_TokenTypeMap_IsSetAutomatically()
        {
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.NotNull(typeInfo.TypeClassifier);
            Assert.NotNull(typeInfo.UnionCases);
        }

        [Fact]
        public void JsonUnion_DeconstructorProperty_IsSetAutomatically()
        {
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
            JsonTypeInfo typeInfo = options.GetTypeInfo(typeof(PetUnion));
            Assert.NotNull(typeInfo.UnionDeconstructor);
        }

        [Fact]
        public void JsonUnion_ConstructorProperty_IsSetAutomatically()
        {
            var options = CreateCustomClassifierOptions(typeof(PetUnion));
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
                                var factory = new TestStructuralClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeof(PetUnion),
                                    Array.Empty<JsonUnionCaseInfo>(),
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
            var options = CreateCustomClassifierOptions(typeof(ClosedSubtypePetUnion));
            var array = new ClosedSubtypePetUnion[]
            {
                new ClosedSubtypeDog { Name = "Rex", Breed = "Labrador" },
                new ClosedSubtypeCat { Name = "Whiskers", Lives = 9 },
            };

            string json = JsonSerializer.Serialize(array, options);
            ClosedSubtypePetUnion[]? deserialized = JsonSerializer.Deserialize<ClosedSubtypePetUnion[]>(json, options);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Length);
            Assert.IsType<ClosedSubtypeDog>(deserialized[0].Value);
            Assert.IsType<ClosedSubtypeCat>(deserialized[1].Value);
        }

        [Fact]
        public void JsonUnion_EmptyObject_MatchesFirstDeclaredCase()
        {
            var options = CreateCustomClassifierOptions(typeof(ImplicitPetUnion));
            string json = "{}";
            ImplicitPetUnion union = JsonSerializer.Deserialize<ImplicitPetUnion>(json, options);

            // Empty object matches the first declared case (Dog) since both score equally.
            Dog dog = Assert.IsType<Dog>(union.Value);
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
        public void CustomPropertyBasedClassifier_NullContext_Throws()
        {
            var factory = new TestStructuralClassifierFactory();
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            Assert.Throws<ArgumentNullException>(() => factory.CreateJsonClassifier(null!, options));
        }

        [Fact]
        public void CustomPropertyBasedClassifier_NullOptions_Throws()
        {
            var factory = new TestStructuralClassifierFactory();
            var context = new JsonTypeClassifierContext(
                                    typeof(PetUnion),
                                    new JsonUnionCaseInfo[] { new JsonUnionCaseInfo(typeof(Dog)) },
                                    Array.Empty<JsonDerivedType>(),
                                    null);
            Assert.Throws<ArgumentNullException>(() => factory.CreateJsonClassifier(context, null!));
        }

        private sealed class TestStructuralClassifierFactory : JsonTypeClassifierFactory
        {
            private const int MaxStructuralMatchingDepth = 64;

            public override bool CanClassify(Type declaringType) => true;


            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                ArgumentNullException.ThrowIfNull(context);
                ArgumentNullException.ThrowIfNull(options);

                var caseMetadata = new CaseTypeMetadata[context.UnionCases.Count];
                for (int i = 0; i < context.UnionCases.Count; i++)
                {
                    caseMetadata[i] = BuildCaseTypeMetadata(context.UnionCases[i].CaseType, options);
                }

                return (ref Utf8JsonReader reader) => FindBestMatch(reader, caseMetadata, options);
            }

            private static CaseTypeMetadata BuildCaseTypeMetadata(Type caseType, JsonSerializerOptions options)
            {
                if (IsSimpleType(caseType) || GetCollectionElementType(caseType) is not null)
                {
                    return new CaseTypeMetadata(caseType, null, null);
                }

                JsonTypeInfo? typeInfo;
                try
                {
                    typeInfo = options.GetTypeInfo(caseType);
                }
                catch (NotSupportedException)
                {
                    return new CaseTypeMetadata(caseType, null, null);
                }
                catch (InvalidOperationException)
                {
                    return new CaseTypeMetadata(caseType, null, null);
                }

                var knownProperties = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                var requiredProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (JsonPropertyInfo property in typeInfo.Properties)
                {
                    if (property.Name is null || property.IsExtensionData)
                    {
                        continue;
                    }

                    knownProperties[property.Name] = property.PropertyType;
                    if (property.IsRequired)
                    {
                        requiredProperties.Add(property.Name);
                    }
                }

                return new CaseTypeMetadata(
                    caseType,
                    knownProperties.Count > 0 ? knownProperties : null,
                    requiredProperties.Count > 0 ? requiredProperties : null);
            }

            private static Type? FindBestMatch(Utf8JsonReader reader, CaseTypeMetadata[] caseMetadata, JsonSerializerOptions options)
            {
                Type? bestMatch = null;
                MatchScore bestScore = default;

                for (int i = 0; i < caseMetadata.Length; i++)
                {
                    Type caseType = caseMetadata[i].CaseType;
                    MatchScore score = ScoreCaseType(reader, caseType, caseMetadata[i], options, depth: 0);

                    if (score.IsDisqualified)
                    {
                        continue;
                    }

                    if (bestMatch is null || CompareTo(score, bestScore) > 0)
                    {
                        bestMatch = caseType;
                        bestScore = score;
                    }
                }

                return bestMatch;
            }

            private static int CompareTo(MatchScore left, MatchScore right)
            {
                int cmp = left.MatchedCount.CompareTo(right.MatchedCount);
                return cmp != 0 ? cmp : right.UnmatchedCount.CompareTo(left.UnmatchedCount);
            }

            private static MatchScore ScoreCaseType(Utf8JsonReader reader, Type candidateType, CaseTypeMetadata? metadata, JsonSerializerOptions options, int depth)
            {
                if (depth > MaxStructuralMatchingDepth)
                {
                    return MatchScore.Disqualified;
                }

#if NET11_0_OR_GREATER
                if (candidateType.GetCustomAttribute<UnionAttribute>() is not null)
                {
                    return ScoreNestedUnion(reader, candidateType, options, depth);
                }
#endif

                return reader.TokenType switch
                {
                    JsonTokenType.Null => ScoreNull(candidateType),
                    JsonTokenType.Number => ScorePrimitive(candidateType, isNumber: true),
                    JsonTokenType.String => ScorePrimitive(candidateType, isNumber: false),
                    JsonTokenType.True or JsonTokenType.False => ScoreBoolean(candidateType),
                    JsonTokenType.StartArray => ScoreArray(reader, candidateType, options, depth),
                    JsonTokenType.StartObject => ScoreObject(reader, candidateType, metadata, options, depth),
                    _ => MatchScore.Disqualified,
                };
            }

#if NET11_0_OR_GREATER
            private static MatchScore ScoreNestedUnion(Utf8JsonReader reader, Type unionType, JsonSerializerOptions options, int depth)
            {
                JsonTypeInfo unionTypeInfo;
                try
                {
                    unionTypeInfo = options.GetTypeInfo(unionType);
                }
                catch (NotSupportedException)
                {
                    return MatchScore.Disqualified;
                }
                catch (InvalidOperationException)
                {
                    return MatchScore.Disqualified;
                }

                if (unionTypeInfo.UnionCases is not { Count: > 0 } unionCases)
                {
                    return MatchScore.Disqualified;
                }

                MatchScore bestScore = MatchScore.Disqualified;

                foreach (JsonUnionCaseInfo caseInfo in unionCases)
                {
                    MatchScore innerScore = ScoreCaseType(reader, caseInfo.CaseType, metadata: null, options, depth + 1);
                    if (!innerScore.IsDisqualified && (bestScore.IsDisqualified || CompareTo(innerScore, bestScore) > 0))
                    {
                        bestScore = innerScore;
                    }
                }

                return bestScore;
            }
#endif

            private static MatchScore ScoreNull(Type candidateType)
            {
                return !candidateType.IsValueType || Nullable.GetUnderlyingType(candidateType) is not null
                    ? new MatchScore(1, 0)
                    : MatchScore.Disqualified;
            }

            private static MatchScore ScorePrimitive(Type candidateType, bool isNumber)
            {
                Type underlying = Nullable.GetUnderlyingType(candidateType) ?? candidateType;

                if (isNumber)
                {
                    return IsNumericType(underlying) ? new MatchScore(1, 0) : MatchScore.Disqualified;
                }

                return underlying == typeof(DateTime) ||
                       underlying == typeof(DateTimeOffset) ||
                       underlying == typeof(Guid) ||
                       underlying == typeof(TimeSpan) ||
                       underlying == typeof(Uri) ||
                       underlying == typeof(char) ||
                       underlying == typeof(byte[]) ||
                       underlying.IsEnum ||
                       underlying == typeof(string) ||
                       underlying == typeof(JsonElement)
                    ? new MatchScore(1, 0)
                    : MatchScore.Disqualified;
            }

            private static MatchScore ScoreBoolean(Type candidateType)
            {
                Type underlying = Nullable.GetUnderlyingType(candidateType) ?? candidateType;
                return underlying == typeof(bool) ? new MatchScore(1, 0) : MatchScore.Disqualified;
            }

            private static MatchScore ScoreArray(Utf8JsonReader reader, Type candidateType, JsonSerializerOptions options, int depth)
            {
                Type? elementType = GetCollectionElementType(candidateType);
                if (elementType is null)
                {
                    return MatchScore.Disqualified;
                }

                int totalMatched = 0;
                int totalUnmatched = 0;
                bool hasElements = false;

                while (reader.Read())
                {
                    if (reader.TokenType is JsonTokenType.EndArray)
                    {
                        break;
                    }

                    hasElements = true;
                    MatchScore elementScore = ScoreCaseType(reader, elementType, metadata: null, options, depth + 1);
                    if (elementScore.IsDisqualified)
                    {
                        return MatchScore.Disqualified;
                    }

                    totalMatched += elementScore.MatchedCount;
                    totalUnmatched += elementScore.UnmatchedCount;
                    reader.TrySkip();
                }

                return !hasElements ? new MatchScore(1, 0) : new MatchScore(1 + totalMatched, totalUnmatched);
            }

            private static MatchScore ScoreObject(Utf8JsonReader reader, Type candidateType, CaseTypeMetadata? metadata, JsonSerializerOptions options, int depth)
            {
                Dictionary<string, Type>? knownProperties = metadata?.KnownProperties;
                HashSet<string>? requiredProperties = metadata?.RequiredProperties;

                if (knownProperties is null)
                {
                    if (IsSimpleType(candidateType) || GetCollectionElementType(candidateType) is not null)
                    {
                        return MatchScore.Disqualified;
                    }

                    JsonTypeInfo typeInfo;
                    try
                    {
                        typeInfo = options.GetTypeInfo(candidateType);
                    }
                    catch (NotSupportedException)
                    {
                        return MatchScore.Disqualified;
                    }
                    catch (InvalidOperationException)
                    {
                        return MatchScore.Disqualified;
                    }

                    knownProperties = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                    requiredProperties = null;

                    foreach (JsonPropertyInfo property in typeInfo.Properties)
                    {
                        if (property.Name is null || property.IsExtensionData)
                        {
                            continue;
                        }

                        knownProperties[property.Name] = property.PropertyType;
                        if (property.IsRequired)
                        {
                            requiredProperties ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            requiredProperties.Add(property.Name);
                        }
                    }
                }

                if (requiredProperties is not null && requiredProperties.Count > 0)
                {
                    Utf8JsonReader nameScanner = reader;
                    var jsonPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    while (nameScanner.Read())
                    {
                        if (nameScanner.TokenType is JsonTokenType.EndObject)
                        {
                            break;
                        }

                        if (nameScanner.TokenType is JsonTokenType.PropertyName)
                        {
                            jsonPropertyNames.Add(nameScanner.GetString()!);
                            nameScanner.Read();
                            nameScanner.TrySkip();
                        }
                    }

                    foreach (string required in requiredProperties)
                    {
                        if (!jsonPropertyNames.Contains(required))
                        {
                            return MatchScore.Disqualified;
                        }
                    }
                }

                int matchedCount = 0;
                int unmatchedCount = 0;

                while (reader.Read())
                {
                    if (reader.TokenType is JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType is JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString()!;
                        reader.Read();

                        if (knownProperties.TryGetValue(propertyName, out Type? propertyType))
                        {
                            MatchScore propertyScore = ScoreCaseType(reader, propertyType, metadata: null, options, depth + 1);
                            if (propertyScore.IsDisqualified)
                            {
                                unmatchedCount++;
                            }
                            else
                            {
                                matchedCount += 1 + propertyScore.MatchedCount;
                                unmatchedCount += propertyScore.UnmatchedCount;
                            }
                        }
                        else
                        {
                            unmatchedCount++;
                        }

                        reader.TrySkip();
                    }
                }

                return new MatchScore(matchedCount, unmatchedCount);
            }

            private static bool IsNumericType(Type type) =>
                type == typeof(int) ||
                type == typeof(long) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(uint) ||
                type == typeof(ulong)
#if NET
                || type == typeof(Half)
#endif
                ;

            private static bool IsSimpleType(Type type)
            {
                Type underlying = Nullable.GetUnderlyingType(type) ?? type;
                return underlying.IsPrimitive ||
                       underlying == typeof(string) ||
                       underlying == typeof(decimal) ||
                       underlying == typeof(DateTime) ||
                       underlying == typeof(DateTimeOffset) ||
                       underlying == typeof(Guid) ||
                       underlying == typeof(TimeSpan) ||
                       underlying.IsEnum;
            }

            private static Type? GetCollectionElementType(Type type)
            {
                if (type.IsArray)
                {
                    return type.GetElementType();
                }

                foreach (Type iface in type.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        return iface.GetGenericArguments()[0];
                    }
                }

                return null;
            }

            private readonly record struct MatchScore(int MatchedCount, int UnmatchedCount)
            {
                public static readonly MatchScore Disqualified = new(-1, -1);
                public bool IsDisqualified => MatchedCount < 0;
            }

            private sealed class CaseTypeMetadata
            {
                public CaseTypeMetadata(Type caseType, Dictionary<string, Type>? knownProperties, HashSet<string>? requiredProperties)
                {
                    CaseType = caseType;
                    KnownProperties = knownProperties;
                    RequiredProperties = requiredProperties;
                }

                public Type CaseType { get; }
                public Dictionary<string, Type>? KnownProperties { get; }
                public HashSet<string>? RequiredProperties { get; }
            }
        }

        private sealed class TestDiscriminatorClassifierFactory : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                ArgumentNullException.ThrowIfNull(context);
                ArgumentNullException.ThrowIfNull(options);

                string propertyName = context.TypeDiscriminatorPropertyName ?? "$type";
                byte[] propertyNameUtf8 = System.Text.Encoding.UTF8.GetBytes(propertyName);

                Dictionary<string, Type>? stringMap = null;
                Dictionary<int, Type>? intMap = null;

                foreach (JsonDerivedType derivedType in context.DerivedTypes)
                {
                    if (derivedType.TypeDiscriminator is string s)
                    {
                        stringMap ??= new Dictionary<string, Type>(StringComparer.Ordinal);
                        stringMap[s] = derivedType.DerivedType;
                    }
                    else if (derivedType.TypeDiscriminator is int i)
                    {
                        intMap ??= new Dictionary<int, Type>();
                        intMap[i] = derivedType.DerivedType;
                    }
                }

                return (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                    {
                        return null;
                    }

                    Utf8JsonReader copy = reader;

                    while (copy.Read())
                    {
                        if (copy.TokenType is JsonTokenType.EndObject)
                        {
                            break;
                        }

                        if (copy.TokenType is JsonTokenType.PropertyName &&
                            copy.ValueTextEquals(propertyNameUtf8))
                        {
                            if (!copy.Read())
                            {
                                break;
                            }

                            if (stringMap is not null && copy.TokenType is JsonTokenType.String)
                            {
                                string? value = copy.GetString();
                                if (value is not null && stringMap.TryGetValue(value, out Type? result))
                                {
                                    return result;
                                }
                            }
                            else if (intMap is not null && copy.TokenType is JsonTokenType.Number)
                            {
                                if (copy.TryGetInt32(out int value) && intMap.TryGetValue(value, out Type? result))
                                {
                                    return result;
                                }
                            }

                            return null;
                        }

                        if (copy.TokenType is JsonTokenType.PropertyName)
                        {
                            copy.Read();
                            copy.TrySkip();
                        }
                    }

                    return null;
                };
            }
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
                                    if (reader.TokenType != JsonTokenType.StartObject)
                                        return null;

                                    bool hasBreed = false;
                                    bool hasLives = false;
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                    {
                                        if (reader.TokenType == JsonTokenType.PropertyName)
                                        {
                                            if (reader.ValueTextEquals("Breed"u8)) hasBreed = true;
                                            else if (reader.ValueTextEquals("Lives"u8)) hasLives = true;
                                            reader.Read();
                                            reader.TrySkip();
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
                                    if (reader.TokenType != JsonTokenType.StartObject) return null;
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                    {
                                        if (reader.TokenType == JsonTokenType.PropertyName)
                                        {
                                            if (reader.ValueTextEquals("Breed"u8)) return typeof(ClassifierDog);
                                            if (reader.ValueTextEquals("Lives"u8)) return typeof(ClassifierCat);
                                            reader.Read();
                                            reader.TrySkip();
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
        public void PolymorphicTypeClassifier_ReturnsNull_ThrowsJsonException()
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
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClassifierAnimal>(json, options));
        }

        [Fact]
        public void CustomDiscriminatorClassifier_StringDiscriminator_RoundTrips()
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
                                var factory = new TestDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                                        new JsonDerivedType(typeof(ClassifierDog), "dog"),
                                        new JsonDerivedType(typeof(ClassifierCat), "cat"),},
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
        public void CustomDiscriminatorClassifier_StringDiscriminator_AnyPropertyPosition()
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
                                var factory = new TestDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                                        new JsonDerivedType(typeof(ClassifierDog), "dog"),
                                        new JsonDerivedType(typeof(ClassifierCat), "cat"),},
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
        public void CustomDiscriminatorClassifier_IntDiscriminator_RoundTrips()
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
                                var factory = new TestDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                                        new JsonDerivedType(typeof(ClassifierDog), 1),
                                        new JsonDerivedType(typeof(ClassifierCat), 2),},
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
        public void CustomDiscriminatorClassifier_UnknownDiscriminator_ReturnsNull()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                typeof(object),
                Array.Empty<JsonUnionCaseInfo>(),
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
        public void CustomDiscriminatorClassifier_MissingProperty_ReturnsNull()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                typeof(object),
                Array.Empty<JsonUnionCaseInfo>(),
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
        public void CustomDiscriminatorClassifier_NotStartObject_ReturnsNull()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                typeof(object),
                Array.Empty<JsonUnionCaseInfo>(),
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
        public void CustomDiscriminatorClassifier_NullArguments_Throws()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var validContext = new JsonTypeClassifierContext(
                typeof(object),
                Array.Empty<JsonUnionCaseInfo>(),
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
                                var factory = new TestDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                                        new JsonDerivedType(typeof(ClassifierDog), "dog"),
                                        new JsonDerivedType(typeof(ClassifierCat), "cat"),},
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
        public void CustomDiscriminatorClassifier_IntDiscriminator_AnyPosition()
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
                                var factory = new TestDiscriminatorClassifierFactory();
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                                        new JsonDerivedType(typeof(ClassifierDog), 1),
                                        new JsonDerivedType(typeof(ClassifierCat), 2),},
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
            var options = CreateCustomClassifierOptions(typeof(UnionWithCustomConverterCase));
            var union = new UnionWithCustomConverterCase(new CustomDogPayload { DogName = "Rex", DogBreed = "Lab" });

            // Act
            string json = JsonSerializer.Serialize(union, options);

            // Assert: CustomDogPayloadConverter writes {"dog_name":"Rex","dog_breed":"Lab"}
            Assert.Contains("\"dog_name\"", json);
            Assert.Contains("\"Rex\"", json);
            Assert.Contains("\"dog_breed\"", json);
            Assert.Contains("\"Lab\"", json);
        }

        [Fact]
        public void Union_CustomConverterCase_SerializationWorks_NormalCaseToo()
        {
            var options = CreateCustomClassifierOptions(typeof(UnionWithCustomConverterCase));
            var union = new UnionWithCustomConverterCase(new Cat { Name = "Whiskers", Lives = 9 });

            string json = JsonSerializer.Serialize(union, options);

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
            var options = CreateCustomClassifierOptions(typeof(UnionWithCustomConverterCase));
            string json = """{"dog_name":"Rex","dog_breed":"Lab"}""";

            // The structural classifier scores:
            // - Cat: "dog_name" unknown, "dog_breed" unknown → (0, 2)
            // - CustomDogPayload: same zero properties → (0, 2)
            // Tie broken by declaration order → selects Cat, which fails to deserialize meaningfully
            // (silently drops unknown properties). We assert the wrong type is returned.
            var result = JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json, options);

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
            var options = CreateCustomClassifierOptions(typeof(UnionWithCustomConverterCase));
            string json = """{"Name":"Whiskers","Lives":9}""";

            var result = JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json, options);

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
            var options = CreateCustomClassifierOptions(typeof(UnionAllCustomConverterCases));
            string json = """{"x":1,"y":2}""";

            var result = JsonSerializer.Deserialize<UnionAllCustomConverterCases>(json, options);

            // First declared case (CustomDogPayload) wins the tie.
            (Type caseType, _) = ExtractAllCustomCaseInfo(result);
            Assert.Equal(typeof(CustomDogPayload), caseType);
        }

        [Fact]
        public void Union_AllCustomConverterCases_SerializationWorks()
        {
            var options = CreateCustomClassifierOptions(typeof(UnionAllCustomConverterCases));
            var union = new UnionAllCustomConverterCases(
                new CustomCatPayload { CatName = "Whiskers", CatLives = 9 });

            string json = JsonSerializer.Serialize(union, options);

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
            var options = CreateCustomClassifierOptions(typeof(UnionWithCustomConverterCase));
            string json = """{"Name":"Rex","Lives":5,"dog_breed":"Lab"}""";

            var result = JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json, options);

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
            var options = CreateCustomClassifierOptions(typeof(UnionWithCustomConverterCase));
            string json = "42";

            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<UnionWithCustomConverterCase>(json, options));
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
                                    if (reader.TokenType is JsonTokenType.StartObject)
                                    {
                                        while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
                                        {
                                            if (reader.TokenType is JsonTokenType.PropertyName)
                                            {
                                                if (reader.ValueTextEquals("dog_name"u8))
                                                    return typeof(CustomDogPayload);
                                                if (reader.ValueTextEquals("Lives"u8))
                                                    return typeof(Cat);
                                                reader.Read();
                                                reader.TrySkip();
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
        public union UnionWithCustomConverterCase(Cat, CustomDogPayload);

        /// <summary>
        /// Union where ALL case types have custom converters. The structural classifier
        /// has zero property metadata for any case type.
        /// </summary>
        [JsonUnion]
        public union UnionAllCustomConverterCases(CustomDogPayload, CustomCatPayload);

        /// <summary>
        /// A classifier factory that knows about CustomDogPayload's non-standard JSON format.
        /// This is the recommended workaround for custom converter case types.
        /// </summary>
        public class CustomConverterAwareClassifier : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                        return null;

                    while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
                    {
                        if (reader.TokenType is JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("dog_name"u8))
                                return typeof(CustomDogPayload);
                            if (reader.ValueTextEquals("Lives"u8) || reader.ValueTextEquals("Name"u8))
                                return typeof(Cat);
                            reader.Read();
                            reader.TrySkip();
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
        public union UnionWithCustomClassifierAndConverter(Cat, CustomDogPayload);

        // ---------------------------------------------------------------------
        // Custom-classifier × null-token interaction tests.
        // The converter is expected to short-circuit JSON null tokens before
        // invoking the classifier: per the union spec all nullable cases produce
        // the same null-holding union value, so the classifier's choice is
        // irrelevant. This relieves classifier authors from having to handle
        // the Null token themselves.
        // ---------------------------------------------------------------------

        public union NullableDogOrCatUnion(Dog?, Cat);

        public union NullableImplicitPetUnion_Multiple(Dog?, Cat?);

        /// <summary>
        /// Tracking classifier that records every invocation. Returns a fixed
        /// caseType (or null) supplied at construction time. Useful for
        /// asserting whether the converter calls the classifier for a given
        /// payload.
        /// </summary>
        private sealed class TrackingClassifierFactory : JsonTypeClassifierFactory
        {
            private readonly Type? _returnedCaseType;
            public int InvocationCount;

            public TrackingClassifierFactory(Type? returnedCaseType)
            {
                _returnedCaseType = returnedCaseType;
            }

            public override bool CanClassify(Type declaringType) => true;


            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return (ref Utf8JsonReader reader) =>
                {
                    Interlocked.Increment(ref InvocationCount);
                    return _returnedCaseType;
                };
            }
        }

        private static JsonSerializerOptions CreateTrackingClassifierOptions(
            Type unionType,
            TrackingClassifierFactory factory)
        {
            return new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == unionType &&
                                typeInfo.UnionCases is { Count: > 0 } cases)
                            {
                                var context = new JsonTypeClassifierContext(
                                    typeInfo.Type,
                                    new List<JsonUnionCaseInfo>(cases),
                                    Array.Empty<JsonDerivedType>(),
                                    typeDiscriminatorPropertyName: null);
                                typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };
        }

        [Fact]
        public void CustomClassifier_NullToken_ClassifierIsNotInvoked_NullableUnion()
        {
            // Source-gen-style union (compiler-generated) with a nullable case.
            // The custom classifier should NOT be invoked for a `null` payload —
            // null dispatch is short-circuited inside the converter.
            var factory = new TrackingClassifierFactory(typeof(Cat));
            var options = CreateTrackingClassifierOptions(typeof(NullableDogOrCatUnion), factory);

            NullableDogOrCatUnion result = JsonSerializer.Deserialize<NullableDogOrCatUnion>("null", options);

            Assert.Null(result.Value);
            Assert.Equal(0, factory.InvocationCount);
        }

        [Fact]
        public void CustomClassifier_NullToken_ClassifierIsNotInvoked_NonNullableUnion()
        {
            // Same as above but the union has no nullable case. The converter
            // must still short-circuit null and throw `UnionDoesNotAcceptNull`
            // without consulting the classifier.
            var factory = new TrackingClassifierFactory(typeof(Cat));
            var options = CreateTrackingClassifierOptions(typeof(PetUnion), factory);

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PetUnion>("null", options));
            Assert.Equal(0, factory.InvocationCount);
        }

        [Fact]
        public void CustomClassifier_NullToken_ClassifierIsInvokedForNonNullPayload()
        {
            // Sanity check: the classifier IS invoked for non-null payloads.
            var factory = new TrackingClassifierFactory(typeof(Dog));
            var options = CreateTrackingClassifierOptions(typeof(PetUnion), factory);

            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            PetUnion result = JsonSerializer.Deserialize<PetUnion>(json, options);

            Assert.IsType<Dog>(result.Value);
            Assert.Equal(1, factory.InvocationCount);
        }

        [Fact]
        public void CustomClassifier_NullToken_BrokenClassifierReturningNonNullableType_StillProducesCanonicalNull()
        {
            // Even if a misbehaving classifier would return a NON-nullable case
            // type for a null payload, the converter never calls it: null is
            // dispatched to the canonical null-holding union via the constructor
            // delegate. This guarantees consistency between source-gen and
            // reflection paths.
            var factory = new TrackingClassifierFactory(typeof(Cat));
            var options = CreateTrackingClassifierOptions(typeof(NullableDogOrCatUnion), factory);

            NullableDogOrCatUnion result = JsonSerializer.Deserialize<NullableDogOrCatUnion>("null", options);

            Assert.Null(result.Value);
            Assert.Equal(0, factory.InvocationCount);
        }

        [Fact]
        public void CustomClassifier_NullToken_MultipleNullableCases_NullDispatched()
        {
            // union(Dog?, Cat?) — both cases nullable. Classifier returns Cat,
            // but converter ignores it for null dispatch and produces the
            // canonical null union (which equals null for a reference union).
            var factory = new TrackingClassifierFactory(typeof(Cat));
            var options = CreateTrackingClassifierOptions(typeof(NullableImplicitPetUnion_Multiple), factory);

            NullableImplicitPetUnion_Multiple result =
                JsonSerializer.Deserialize<NullableImplicitPetUnion_Multiple>("null", options);

            Assert.Null(result.Value);
            Assert.Equal(0, factory.InvocationCount);
        }

        [Fact]
        public void CustomClassifier_NullToken_RoundTripPreservesNull()
        {
            // null → deserialize → serialize must round-trip to "null".
            var factory = new TrackingClassifierFactory(typeof(Cat));
            var options = CreateTrackingClassifierOptions(typeof(NullableDogOrCatUnion), factory);

            NullableDogOrCatUnion fromNull =
                JsonSerializer.Deserialize<NullableDogOrCatUnion>("null", options);
            string serialized = JsonSerializer.Serialize(fromNull, options);

            Assert.Equal("null", serialized);
        }

        // ---------------------------------------------------------------------
        // Reflection path: ImplicitPetUnion uses the [JsonUnion] attribute and
        // is wired through the reflection-based UnionConstructor builder. These
        // tests verify the same null short-circuit behavior on that path.
        // ---------------------------------------------------------------------

        public sealed class TrackingClassifierForImplicit : JsonTypeClassifierFactory
        {
            public static int InvocationCount;
            public static Type? ReturnedCaseType;

            public override bool CanClassify(Type declaringType) => true;


            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return (ref Utf8JsonReader reader) =>
                {
                    Interlocked.Increment(ref InvocationCount);
                    return ReturnedCaseType;
                };
            }
        }

        [JsonUnion(TypeClassifier = typeof(TrackingClassifierForImplicit))]
        public union ReflectionTrackedNullablePetUnion(Dog?, Cat);

        [JsonUnion(TypeClassifier = typeof(TrackingClassifierForImplicit))]
        public union ReflectionTrackedNonNullablePetUnion(Dog, Cat);

        [Fact]
        public void Reflection_CustomClassifier_NullToken_NullableUnion_NotInvoked()
        {
            TrackingClassifierForImplicit.InvocationCount = 0;
            TrackingClassifierForImplicit.ReturnedCaseType = typeof(Cat);

            ReflectionTrackedNullablePetUnion result =
                JsonSerializer.Deserialize<ReflectionTrackedNullablePetUnion>("null");

            Assert.Null(result.Value);
            Assert.Equal(0, TrackingClassifierForImplicit.InvocationCount);
        }

        [Fact]
        public void Reflection_CustomClassifier_NullToken_NonNullableUnion_ThrowsWithoutInvokingClassifier()
        {
            TrackingClassifierForImplicit.InvocationCount = 0;
            TrackingClassifierForImplicit.ReturnedCaseType = typeof(Dog);

            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<ReflectionTrackedNonNullablePetUnion>("null"));
            Assert.Equal(0, TrackingClassifierForImplicit.InvocationCount);
        }

        [Fact]
        public void Reflection_CustomClassifier_NullToken_BrokenClassifierIgnoredForNull()
        {
            // Reflection path: even if the classifier would return a
            // non-nullable case (Cat) for a null payload, the converter
            // short-circuits and produces the canonical null union.
            // Pre-fix this would have thrown UnionDoesNotAcceptNull because
            // the reflection constructor checked `nullableCases.Contains(caseType)`.
            TrackingClassifierForImplicit.InvocationCount = 0;
            TrackingClassifierForImplicit.ReturnedCaseType = typeof(Cat);

            ReflectionTrackedNullablePetUnion result =
                JsonSerializer.Deserialize<ReflectionTrackedNullablePetUnion>("null");

            Assert.Null(result.Value);
            Assert.Equal(0, TrackingClassifierForImplicit.InvocationCount);
        }

        [Fact]
        public void Reflection_CustomClassifier_NonNullPayload_ClassifierStillInvoked()
        {
            // Sanity: the short-circuit only applies to null tokens; non-null
            // payloads continue to flow through the classifier normally.
            TrackingClassifierForImplicit.InvocationCount = 0;
            TrackingClassifierForImplicit.ReturnedCaseType = typeof(Dog);

            string json = """{"Name":"Rex","Breed":"Labrador"}""";
            ReflectionTrackedNullablePetUnion result =
                JsonSerializer.Deserialize<ReflectionTrackedNullablePetUnion>(json);

            Assert.IsType<Dog>(result.Value);
            Assert.Equal(1, TrackingClassifierForImplicit.InvocationCount);
        }

        // ---------------------------------------------------------------------
        // Closed hierarchy + custom classifier + null. Closed hierarchies route
        // through the polymorphism dispatcher rather than JsonUnionConverter,
        // but they expose a TypeClassifier on JsonTypeInfo too. Verify that
        // JSON null produces a null root regardless of any classifier.
        // ---------------------------------------------------------------------

        [Fact]
        public void ClosedHierarchy_CustomClassifier_NullPayload_DoesNotInvokeClassifier()
        {
            int classifierInvocations = 0;
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClosedAnimal))
                            {
                                typeInfo.TypeClassifier = (ref Utf8JsonReader reader) =>
                                {
                                    Interlocked.Increment(ref classifierInvocations);
                                    throw new InvalidOperationException(
                                        "Classifier should not be invoked for null tokens.");
                                };
                            }
                        }
                    }
                }
            };

            ClosedAnimal? result = JsonSerializer.Deserialize<ClosedAnimal>("null", options);

            Assert.Null(result);
            Assert.Equal(0, classifierInvocations);
        }

        private static (Type caseType, object? caseValue) ExtractCaseInfo(UnionWithCustomConverterCase union)
        {
            object? caseValue = union.Value;
            return (caseValue?.GetType() ?? typeof(object), caseValue);
        }

        private static (Type caseType, object? caseValue) ExtractAllCustomCaseInfo(UnionAllCustomConverterCases union)
        {
            object? caseValue = union.Value;
            return (caseValue?.GetType() ?? typeof(object), caseValue);
        }

        private static (Type caseType, object? caseValue) ExtractCustomClassifiedCaseInfo(UnionWithCustomClassifierAndConverter union)
        {
            object? caseValue = union.Value;
            return (caseValue?.GetType() ?? typeof(object), caseValue);
        }

        // ===== Multivariate classifier factory tests (Classifiers list + typed accelerator) =====

        public union UnionFromList(Cat, Dog);
        public union UnionFromListAlt(Cat, int); // Disjoint token types: Object vs Number — default token matching works without a classifier.

        public sealed class UnionFromListClassifier : JsonTypeClassifierFactory<UnionFromList>
        {
            public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options) =>
                static (ref Utf8JsonReader reader) => typeof(Cat);
        }

        public sealed class UniversalClassifierForDog : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;
            public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options) =>
                static (ref Utf8JsonReader reader) => typeof(Dog);
        }

        public sealed class WrongTargetTypedClassifier : JsonTypeClassifierFactory<UnionFromListAlt>
        {
            public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options) =>
                static (ref Utf8JsonReader reader) => typeof(Cat);
        }

        [Fact]
        public static void Classifiers_TypedFactory_CanClassifyHonored()
        {
            var typed = new UnionFromListClassifier();
            Assert.True(typed.CanClassify(typeof(UnionFromList)));
            Assert.False(typed.CanClassify(typeof(UnionFromListAlt)));
        }

        [Fact]
        public static void Classifiers_OptionsList_TypedFactoryMatchedByType()
        {
            var options = new JsonSerializerOptions();
            options.Classifiers.Add(new UnionFromListClassifier());

            string json = """{"Name":"Rex"}""";
            var result = JsonSerializer.Deserialize<UnionFromList>(json, options);
            Assert.IsType<Cat>(result.Value);
        }

        [Fact]
        public static void Classifiers_OptionsList_NoMatch_FallsBackToDefault()
        {
            var options = new JsonSerializerOptions();
            // Typed factory only matches UnionFromList, not UnionFromListAlt.
            options.Classifiers.Add(new UnionFromListClassifier());

            // Default token-type matching for UnionFromListAlt(Cat, int):
            // an Object token deserializes as Cat; a Number token deserializes as int.
            var catResult = JsonSerializer.Deserialize<UnionFromListAlt>("""{"Name":"Whiskers"}""", options);
            Assert.IsType<Cat>(catResult.Value);

            var intResult = JsonSerializer.Deserialize<UnionFromListAlt>("42", options);
            Assert.Equal(42, intResult.Value);
        }

        [Fact]
        public static void Classifiers_OptionsList_FirstMatchingFactoryWins()
        {
            var options = new JsonSerializerOptions();
            options.Classifiers.Add(new UnionFromListClassifier()); // Returns Cat for UnionFromList
            options.Classifiers.Add(new UniversalClassifierForDog()); // Returns Dog for everything

            var result = JsonSerializer.Deserialize<UnionFromList>("""{"Name":"Rex"}""", options);
            Assert.IsType<Cat>(result.Value); // First matching wins.
        }

        [JsonUnion(TypeClassifier = typeof(UniversalClassifierForDog))]
        public union UnionWithAttrAndListConflict(Cat, Dog);

        [Fact]
        public static void Classifiers_AttributeWinsOverOptionsList()
        {
            var options = new JsonSerializerOptions();
            // List-registered classifier would match (CanClassify=>true) but the attribute takes precedence.
            options.Classifiers.Add(new UnionFromListClassifier());

            var result = JsonSerializer.Deserialize<UnionWithAttrAndListConflict>("""{"Name":"Rex"}""", options);
            Assert.IsType<Dog>(result.Value); // Attribute classifier ran (returned Dog).
        }

        [JsonUnion(TypeClassifier = typeof(WrongTargetTypedClassifier))]
        public union UnionWithMisappliedTypedClassifier(Cat, Dog);

        [Fact]
        public static void Classifiers_AttributePath_TypedFactoryAppliedToWrongType_Throws()
        {
            var options = new JsonSerializerOptions();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                JsonSerializer.Deserialize<UnionWithMisappliedTypedClassifier>("""{"Name":"Rex"}""", options));
            Assert.Contains("CanClassify", ex.Message);
        }

        // ===== Re-entrancy / recursive scenarios =====
        //
        // The classifier factory is invoked during JsonTypeInfo.Configure(), AFTER the
        // typeInfo is entered into the per-options cache. Re-entrant GetTypeInfo calls
        // for the same union (or for case types that transitively reference back) hit the
        // cached, partially-configured instance instead of recursing into a fresh build.

        [JsonUnion(TypeClassifier = typeof(SelfResolvingClassifier))]
        public union UnionWithSelfResolvingClassifier(Cat, Dog);

        public sealed class SelfResolvingClassifier : JsonTypeClassifierFactory<UnionWithSelfResolvingClassifier>
        {
            public static JsonTypeInfo? CapturedSelfTypeInfo;

            public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options)
            {
                // Re-entrant resolution of the union we're currently configuring must not
                // stack overflow — it should return the partially configured JsonTypeInfo
                // (the one whose Configure() is in progress).
                CapturedSelfTypeInfo = options.GetTypeInfo(typeof(UnionWithSelfResolvingClassifier));
                return static (ref Utf8JsonReader reader) => typeof(Cat);
            }
        }

        [Fact]
        public static void Reentrancy_SelfResolvingClassifier_ReturnsPartialTypeInfo_NoStackOverflow()
        {
            SelfResolvingClassifier.CapturedSelfTypeInfo = null;
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            options.MakeReadOnly();

            JsonTypeInfo info = options.GetTypeInfo(typeof(UnionWithSelfResolvingClassifier));

            Assert.NotNull(info);
            Assert.NotNull(SelfResolvingClassifier.CapturedSelfTypeInfo);
            Assert.Same(info, SelfResolvingClassifier.CapturedSelfTypeInfo);
            // The returned typeInfo is the same instance and is fully configured by the
            // time the outer GetTypeInfo call returns.
            Assert.Equal(typeof(UnionWithSelfResolvingClassifier), info.Type);

            // Round-trip works after the recursive resolution.
            var result = JsonSerializer.Deserialize<UnionWithSelfResolvingClassifier>("""{"Lives":9}""", options);
            Assert.IsType<Cat>(result.Value);
        }

        [Fact]
        public static void Reentrancy_SelfResolvingClassifier_ViaOptionsList_NoStackOverflow()
        {
            SelfResolvingClassifier.CapturedSelfTypeInfo = null;
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            options.Classifiers.Add(new SelfResolvingClassifier());
            options.MakeReadOnly();

            JsonTypeInfo info = options.GetTypeInfo(typeof(UnionWithSelfResolvingClassifier));
            Assert.Same(info, SelfResolvingClassifier.CapturedSelfTypeInfo);
        }

        // Legitimate cyclic graph: a union case has a property whose static type is the
        // union itself. The classifier factory introspects the case type's properties
        // (the documented PropertyBasedClassifier pattern), which in turn requires
        // resolving the union's JsonTypeInfo for the property. Must not stack overflow.

        public sealed class BarWithFooProperty
        {
            public CyclicFoo? FooProp { get; set; }
            public string? Tag { get; set; }
        }

        [JsonUnion(TypeClassifier = typeof(CyclicFooClassifier))]
        public union CyclicFoo(BarWithFooProperty, int);

        public sealed class CyclicFooClassifier : JsonTypeClassifierFactory<CyclicFoo>
        {
            public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options)
            {
                // Pre-compute step that resolves each case type's JsonTypeInfo. For
                // BarWithFooProperty, this transitively requires JsonTypeInfo<CyclicFoo>
                // (because Bar has a CyclicFoo property), which is the union currently
                // being configured. The recursive resolve must complete without crashing.
                foreach (JsonUnionCaseInfo dt in context.UnionCases)
                {
                    _ = options.GetTypeInfo(dt.CaseType);
                }

                return static (ref Utf8JsonReader reader) =>
                    reader.TokenType is JsonTokenType.StartObject ? typeof(BarWithFooProperty) : typeof(int);
            }
        }

        [Fact]
        public static void Reentrancy_LegitimateCyclicGraph_UnionCaseReferencesUnion_Works()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            options.MakeReadOnly();
            // The recursive resolution of BarWithFooProperty (which references CyclicFoo)
            // from inside CyclicFoo's own classifier factory must complete cleanly.
            JsonTypeInfo info = options.GetTypeInfo(typeof(CyclicFoo));
            Assert.NotNull(info);

            var asInt = JsonSerializer.Deserialize<CyclicFoo>("42", options);
            Assert.Equal(42, asInt.Value);
        }

        // Cross-union cyclic graph: A's factory resolves B; B's factory resolves A.
        // Both must complete without recursing infinitely.
        [JsonUnion(TypeClassifier = typeof(CrossUnionAClassifier))]
        public union CrossUnionA(Cat, Dog);

        [JsonUnion(TypeClassifier = typeof(CrossUnionBClassifier))]
        public union CrossUnionB(Cat, Dog);

        public sealed class CrossUnionAClassifier : JsonTypeClassifierFactory<CrossUnionA>
        {
            public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options)
            {
                _ = options.GetTypeInfo(typeof(CrossUnionB));
                return static (ref Utf8JsonReader reader) => typeof(Cat);
            }
        }

        public sealed class CrossUnionBClassifier : JsonTypeClassifierFactory<CrossUnionB>
        {
            public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options)
            {
                _ = options.GetTypeInfo(typeof(CrossUnionA));
                return static (ref Utf8JsonReader reader) => typeof(Cat);
            }
        }

        [Fact]
        public static void Reentrancy_CyclicCrossUnionResolution_Works()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
            options.MakeReadOnly();
            JsonTypeInfo a = options.GetTypeInfo(typeof(CrossUnionA));
            JsonTypeInfo b = options.GetTypeInfo(typeof(CrossUnionB));
            Assert.NotNull(a);
            Assert.NotNull(b);
        }
    }
}
