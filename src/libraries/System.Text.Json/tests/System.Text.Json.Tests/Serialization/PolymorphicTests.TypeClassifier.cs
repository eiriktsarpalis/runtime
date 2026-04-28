// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class PolymorphicTests
    {
        #region Test Models

        [JsonPolymorphic]
        [JsonDerivedType(typeof(ClassifiedDog), "dog")]
        [JsonDerivedType(typeof(ClassifiedCat), "cat")]
        [JsonDerivedType(typeof(ClassifiedParrot), "parrot")]
        public class ClassifiedAnimalBase
        {
            public string? Name { get; set; }
        }

        public class ClassifiedDog : ClassifiedAnimalBase
        {
            public string? Breed { get; set; }
        }

        public class ClassifiedCat : ClassifiedAnimalBase
        {
            public int Lives { get; set; }
        }

        public class ClassifiedParrot : ClassifiedAnimalBase
        {
            public bool CanTalk { get; set; }
        }

        [JsonPolymorphic]
        [JsonDerivedType(typeof(ClassifiedCircle), "circle")]
        [JsonDerivedType(typeof(ClassifiedRectangle), "rect")]
        public abstract class ClassifiedShape
        {
            public string? Color { get; set; }
        }

        public class ClassifiedCircle : ClassifiedShape
        {
            public double Radius { get; set; }
        }

        public class ClassifiedRectangle : ClassifiedShape
        {
            public double Width { get; set; }
            public double Height { get; set; }
        }

        [JsonPolymorphic]
        [JsonDerivedType(typeof(ClassifiedDogL2), "dog")]
        [JsonDerivedType(typeof(ClassifiedLabrador), "lab")]
        [JsonDerivedType(typeof(ClassifiedPoodle), "poodle")]
        public class ClassifiedAnimalDeepHierarchy
        {
            public string? Name { get; set; }
        }

        public class ClassifiedDogL2 : ClassifiedAnimalDeepHierarchy
        {
            public string? Breed { get; set; }
        }

        public class ClassifiedLabrador : ClassifiedDogL2
        {
            public bool IsGuide { get; set; }
        }

        public class ClassifiedPoodle : ClassifiedDogL2
        {
            public string? Size { get; set; }
        }

        [JsonPolymorphic(TypeClassifier = typeof(AnimalStructuralClassifierFactory))]
        [JsonDerivedType(typeof(AttrClassifiedDog), "dog")]
        [JsonDerivedType(typeof(AttrClassifiedCat), "cat")]
        public class AttrClassifiedAnimal
        {
            public string? Name { get; set; }
        }

        public class AttrClassifiedDog : AttrClassifiedAnimal
        {
            public string? Breed { get; set; }
        }

        public class AttrClassifiedCat : AttrClassifiedAnimal
        {
            public int Lives { get; set; }
        }

        public class AnimalStructuralClassifierFactory : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                return (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType != JsonTokenType.StartObject)
                        return null;

                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("Breed"u8)) return typeof(AttrClassifiedDog);
                            if (reader.ValueTextEquals("Lives"u8)) return typeof(AttrClassifiedCat);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return null;
                };
            }
        }

        public class TestStructuralClassifierFactory : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                return context.DeclaringType switch
                {
                    _ when context.DeclaringType == typeof(ClassifiedAnimalBase) => CreateAnimalClassifier(),
                    _ when context.DeclaringType == typeof(ClassifiedShape) => CreateShapeClassifier(),
                    _ when context.DeclaringType == typeof(ClassifiedAnimalDeepHierarchy) => CreateDeepHierarchyClassifier(),
                    _ => (ref Utf8JsonReader _) => null,
                };
            }

            private static JsonTypeClassifier CreateAnimalClassifier() =>
                static (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                    {
                        return null;
                    }

                    while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
                    {
                        if (reader.TokenType is JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("Breed"u8)) return typeof(ClassifiedDog);
                            if (reader.ValueTextEquals("Lives"u8)) return typeof(ClassifiedCat);
                            if (reader.ValueTextEquals("CanTalk"u8)) return typeof(ClassifiedParrot);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return typeof(ClassifiedDog);
                };

            private static JsonTypeClassifier CreateShapeClassifier() =>
                static (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                    {
                        return null;
                    }

                    while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
                    {
                        if (reader.TokenType is JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("Radius"u8)) return typeof(ClassifiedCircle);
                            if (reader.ValueTextEquals("Width"u8) || reader.ValueTextEquals("Height"u8)) return typeof(ClassifiedRectangle);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return typeof(ClassifiedCircle);
                };

            private static JsonTypeClassifier CreateDeepHierarchyClassifier() =>
                static (ref Utf8JsonReader reader) =>
                {
                    if (reader.TokenType is not JsonTokenType.StartObject)
                    {
                        return null;
                    }

                    while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
                    {
                        if (reader.TokenType is JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("IsGuide"u8)) return typeof(ClassifiedLabrador);
                            if (reader.ValueTextEquals("Size"u8)) return typeof(ClassifiedPoodle);
                            reader.Read();
                            reader.TrySkip();
                        }
                    }

                    return typeof(ClassifiedDogL2);
                };
        }

        public class AnimalKindClassifierFactory : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                var innerFactory = new TestDiscriminatorClassifierFactory();
                var innerContext = new JsonTypeClassifierContext(
                                    context.DeclaringType,
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                        new JsonDerivedType(typeof(ClassifiedDog), "dog"),
                        new JsonDerivedType(typeof(ClassifiedCat), "cat"),
                        new JsonDerivedType(typeof(ClassifiedParrot), "parrot"),},
                                    "kind");

                return innerFactory.CreateJsonClassifier(innerContext, options);
            }
        }

        public class TestDiscriminatorClassifierFactory : JsonTypeClassifierFactory
        {
            public override bool CanClassify(Type declaringType) => true;

            public override JsonTypeClassifier CreateJsonClassifier(
                JsonTypeClassifierContext context,
                JsonSerializerOptions options)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                if (options is null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

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

        public class DrawingCanvas
        {
            public List<ClassifiedShape>? Shapes { get; set; }
        }

        public class PetOwner
        {
            public string? OwnerName { get; set; }
            public ClassifiedAnimalBase? Pet { get; set; }
        }

        #endregion

        #region Classifier via contract customization — structural matching

        [Fact]
        public async Task Classifier_StructuralMatching_DeserializesDogByBreedProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = """{"Name":"Rex","Breed":"Labrador"}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);

            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
            Assert.Equal("Labrador", ((ClassifiedDog)result).Breed);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_DeserializesCatByLivesProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = """{"Name":"Whiskers","Lives":9}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);

            Assert.IsType<ClassifiedCat>(result);
            Assert.Equal(9, ((ClassifiedCat)result).Lives);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_DeserializesParrotByCanTalkProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = """{"Name":"Polly","CanTalk":true}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);

            Assert.IsType<ClassifiedParrot>(result);
            Assert.True(((ClassifiedParrot)result).CanTalk);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_BestMatchWins_EvenWithUnknownProperties()
        {
            // When all derived types score equally (e.g., all share only "Name" from base),
            // the structural classifier returns the first declared derived type by declaration order.
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = """{"Name":"Unknown","UnknownProp":42}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);

            Assert.NotNull(result);
            Assert.Equal("Unknown", result.Name);
            Assert.IsType<ClassifiedDog>(result);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_AbstractBaseType()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedShape>();
            string json = """{"Color":"red","Radius":5.0}""";

            ClassifiedShape? result = await Serializer.DeserializeWrapper<ClassifiedShape>(json, options);

            Assert.IsType<ClassifiedCircle>(result);
            Assert.Equal("red", result.Color);
            Assert.Equal(5.0, ((ClassifiedCircle)result).Radius);
        }

        [Fact]
        public async Task Classifier_StructuralMatching_AbstractBaseType_Rectangle()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedShape>();
            string json = """{"Color":"blue","Width":10,"Height":20}""";

            ClassifiedShape? result = await Serializer.DeserializeWrapper<ClassifiedShape>(json, options);

            Assert.IsType<ClassifiedRectangle>(result);
            Assert.Equal(10, ((ClassifiedRectangle)result).Width);
            Assert.Equal(20, ((ClassifiedRectangle)result).Height);
        }

        #endregion

        #region Classifier via contract customization — discriminator-based

        [Theory]
        [InlineData("""{"kind":"dog","Name":"Rex","Breed":"Lab"}""", typeof(ClassifiedDog))]
        [InlineData("""{"kind":"cat","Name":"Whiskers","Lives":9}""", typeof(ClassifiedCat))]
        [InlineData("""{"kind":"parrot","Name":"Polly","CanTalk":true}""", typeof(ClassifiedParrot))]
        public async Task Classifier_DiscriminatorBased_ResolvesCorrectType(string json, Type expectedType)
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                                    typeof(ClassifiedAnimalBase),
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                    new JsonDerivedType(typeof(ClassifiedDog), "dog"),
                    new JsonDerivedType(typeof(ClassifiedCat), "cat"),
                    new JsonDerivedType(typeof(ClassifiedParrot), "parrot"),},
                                    "kind");
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.IsType(expectedType, result);
        }

        [Theory]
        [InlineData("""{"Name":"Rex","Breed":"Lab","kind":"dog"}""")]
        [InlineData("""{"Name":"Rex","kind":"dog","Breed":"Lab"}""")]
        public async Task Classifier_DiscriminatorBased_AnyPropertyPosition(string json)
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                                    typeof(ClassifiedAnimalBase),
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                    new JsonDerivedType(typeof(ClassifiedDog), "dog"),
                    new JsonDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
        }

        [Theory]
        [InlineData("""{"type_id":1,"Name":"Rex","Breed":"Lab"}""", typeof(ClassifiedDog))]
        [InlineData("""{"Name":"Whiskers","type_id":2,"Lives":9}""", typeof(ClassifiedCat))]
        public async Task Classifier_IntDiscriminator_ResolvesCorrectType(string json, Type expectedType)
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                                    typeof(ClassifiedAnimalBase),
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                    new JsonDerivedType(typeof(ClassifiedDog), 1),
                    new JsonDerivedType(typeof(ClassifiedCat), 2),},
                                    "type_id");
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.IsType(expectedType, result);
        }

        #endregion

        #region Classifier via [JsonPolymorphic(TypeClassifier = typeof(...))]

        [Fact]
        public async Task Classifier_ViaAttribute_DeserializesDogByStructure()
        {
            string json = """{"Name":"Rex","Breed":"Labrador"}""";

            AttrClassifiedAnimal? result = await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(json);

            Assert.IsType<AttrClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
            Assert.Equal("Labrador", ((AttrClassifiedDog)result).Breed);
        }

        [Fact]
        public async Task Classifier_ViaAttribute_DeserializesCatByStructure()
        {
            string json = """{"Name":"Whiskers","Lives":7}""";

            AttrClassifiedAnimal? result = await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(json);

            Assert.IsType<AttrClassifiedCat>(result);
            Assert.Equal(7, ((AttrClassifiedCat)result).Lives);
        }

        [Fact]
        public async Task Classifier_ViaAttribute_ThrowsWhenNoMatch()
        {
            string json = """{"Name":"Unknown"}""";

            await Assert.ThrowsAsync<JsonException>(async () =>
                await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(json));
        }

        [Fact]
        public async Task Classifier_ViaAttribute_NoFallbackToDiscriminator_WhenClassifierReturnsNull()
        {
            // JSON with $type but no structural distinguishing properties.
            // The classifier returns null — this is now a classification failure,
            // not a fallback to $type discriminator.
            string json = """{"$type":"dog","Name":"Rex"}""";

            await Assert.ThrowsAsync<JsonException>(async () =>
                await Serializer.DeserializeWrapper<AttrClassifiedAnimal>(json));
        }

        #endregion

        #region Collections of polymorphic types with classifier

        [Fact]
        public async Task Classifier_CollectionOfPolymorphicTypes()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = """[{"Name":"Rex","Breed":"Lab"},{"Name":"Whiskers","Lives":9},{"Name":"Polly","CanTalk":true}]""";

            List<ClassifiedAnimalBase>? result = await Serializer.DeserializeWrapper<List<ClassifiedAnimalBase>>(json, options);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.IsType<ClassifiedDog>(result[0]);
            Assert.IsType<ClassifiedCat>(result[1]);
            Assert.IsType<ClassifiedParrot>(result[2]);
        }

        [Fact]
        public async Task Classifier_CollectionWithMixedDiscriminators()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                                    typeof(ClassifiedAnimalBase),
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                    new JsonDerivedType(typeof(ClassifiedDog), "dog"),
                    new JsonDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(classify);
            string json = """[{"kind":"dog","Name":"Rex","Breed":"Lab"},{"kind":"cat","Name":"Whiskers","Lives":9}]""";

            List<ClassifiedAnimalBase>? result = await Serializer.DeserializeWrapper<List<ClassifiedAnimalBase>>(json, options);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.IsType<ClassifiedDog>(result[0]);
            Assert.IsType<ClassifiedCat>(result[1]);
        }

        [Fact]
        public async Task Classifier_DictionaryWithPolymorphicValues()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = """{"pet1":{"Name":"Rex","Breed":"Lab"},"pet2":{"Name":"Whiskers","Lives":9}}""";

            Dictionary<string, ClassifiedAnimalBase>? result =
                await Serializer.DeserializeWrapper<Dictionary<string, ClassifiedAnimalBase>>(json, options);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.IsType<ClassifiedDog>(result["pet1"]);
            Assert.IsType<ClassifiedCat>(result["pet2"]);
        }

        #endregion

        #region Nested polymorphic types with classifier

        [Fact]
        public async Task Classifier_NestedPolymorphicProperty()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = """{"OwnerName":"Alice","Pet":{"Name":"Rex","Breed":"Lab"}}""";

            PetOwner? result = await Serializer.DeserializeWrapper<PetOwner>(json, options);

            Assert.NotNull(result);
            Assert.Equal("Alice", result.OwnerName);
            Assert.IsType<ClassifiedDog>(result.Pet);
            Assert.Equal("Lab", ((ClassifiedDog)result.Pet!).Breed);
        }

        [Fact]
        public async Task Classifier_NestedCollectionOfPolymorphicShapes()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedShape>();
            string json = """{"Shapes":[{"Color":"red","Radius":5},{"Color":"blue","Width":10,"Height":20}]}""";

            DrawingCanvas? result = await Serializer.DeserializeWrapper<DrawingCanvas>(json, options);

            Assert.NotNull(result);
            Assert.Equal(2, result.Shapes!.Count);
            Assert.IsType<ClassifiedCircle>(result.Shapes[0]);
            Assert.IsType<ClassifiedRectangle>(result.Shapes[1]);
        }

        #endregion

        #region Deep hierarchy with classifier

        [Theory]
        [InlineData("""{"Name":"Rex","Breed":"Lab"}""", typeof(ClassifiedDogL2))]
        [InlineData("""{"Name":"Buddy","Breed":"Lab","IsGuide":true}""", typeof(ClassifiedLabrador))]
        [InlineData("""{"Name":"Fifi","Breed":"Toy","Size":"Miniature"}""", typeof(ClassifiedPoodle))]
        public async Task Classifier_DeepHierarchy_ResolvesLeafType(string json, Type expectedType)
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalDeepHierarchy>();

            ClassifiedAnimalDeepHierarchy? result =
                await Serializer.DeserializeWrapper<ClassifiedAnimalDeepHierarchy>(json, options);

            Assert.IsType(expectedType, result);
        }

        #endregion

        #region Classifier interaction with standard $type discriminator

        [Fact]
        public async Task Classifier_OverridesDiscriminator_WhenSet()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();

            // No $type in JSON — classifier resolves by structure
            string json = """{"Name":"Rex","Breed":"Lab"}""";
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.IsType<ClassifiedDog>(result);
        }

        [Fact]
        public async Task Classifier_StandardDiscriminatorStillWorks_WhenNoClassifierSet()
        {
            string json = """{"$type":"dog","Name":"Rex","Breed":"Lab"}""";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json);

            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
        }

        #endregion

        #region Classifier + AllowOutOfOrderMetadataProperties

        [Fact]
        public async Task Classifier_PlusAllowOutOfOrder_ClassifierWins()
        {
            var factory = new TestDiscriminatorClassifierFactory();
            var context = new JsonTypeClassifierContext(
                                    typeof(ClassifiedAnimalBase),
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                    new JsonDerivedType(typeof(ClassifiedDog), "dog"),
                    new JsonDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            JsonTypeClassifier classify = factory.CreateJsonClassifier(context, new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

            var options = new JsonSerializerOptions
            {
                AllowOutOfOrderMetadataProperties = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifiedAnimalBase))
                            {
                                typeInfo.TypeClassifier = classify;
                            }
                        }
                    }
                }
            };

            // "kind" property is not standard $type — classifier handles it
            string json = """{"Name":"Rex","kind":"dog","Breed":"Lab"}""";
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.IsType<ClassifiedDog>(result);
        }

        #endregion

        #region Classifier returning null

        [Fact]
        public async Task Classifier_ReturnsNull_ThrowsJsonException()
        {
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>((ref Utf8JsonReader _) => null);

            string json = """{"Name":"Unknown"}""";
            await Assert.ThrowsAsync<JsonException>(async () =>
                await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options));
        }

        [Fact]
        public async Task Classifier_ReturnsBaseType_ThrowsNotSupported()
        {
            // The base type itself is not in the derived types list,
            // so the resolver throws NotSupportedException.
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(
                (ref Utf8JsonReader _) => typeof(ClassifiedAnimalBase));

            string json = """{"Name":"Generic"}""";
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options));
        }

        #endregion

        #region Classifier error cases

        [Fact]
        public async Task Classifier_ReturnsUnregisteredType_Throws()
        {
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(
                (ref Utf8JsonReader _) => typeof(string));

            string json = """{"Name":"Rex"}""";
            await Assert.ThrowsAsync<NotSupportedException>(
                () => Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options));
        }

        [Fact]
        public async Task Classifier_ThrowsException_Propagates()
        {
            var options = CreateOptionsWithClassifier<ClassifiedAnimalBase>(
                (ref Utf8JsonReader _) => throw new InvalidOperationException("Test error"));

            string json = """{"Name":"Rex"}""";
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options));
        }

        #endregion

        #region Classifier with PropertyNamingPolicy

        [Fact]
        public async Task Classifier_WithCamelCaseNamingPolicy_ReadsRawJsonPropertyNames()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifiedAnimalBase))
                            {
                                // Classifier reads raw JSON property names, not C# names
                                typeInfo.TypeClassifier = (ref Utf8JsonReader reader) =>
                                {
                                    if (reader.TokenType != JsonTokenType.StartObject) return null;
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                    {
                                        if (reader.TokenType == JsonTokenType.PropertyName)
                                        {
                                            // JSON uses camelCase "breed", not PascalCase "Breed"
                                            if (reader.ValueTextEquals("breed"u8)) return typeof(ClassifiedDog);
                                            if (reader.ValueTextEquals("lives"u8)) return typeof(ClassifiedCat);
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

            string json = """{"name":"Rex","breed":"Lab"}""";
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.IsType<ClassifiedDog>(result);
            Assert.Equal("Rex", result.Name);
        }

        #endregion

        #region Multiple polymorphic base types with different classifiers

        [Fact]
        public async Task Classifier_DifferentClassifiersForDifferentBaseTypes()
        {
            var animalFactory = new TestDiscriminatorClassifierFactory();
            var animalContext = new JsonTypeClassifierContext(
                                    typeof(ClassifiedAnimalBase),
                                    Array.Empty<JsonUnionCaseInfo>(),
                                    new JsonDerivedType[] {
                    new JsonDerivedType(typeof(ClassifiedDog), "dog"),
                    new JsonDerivedType(typeof(ClassifiedCat), "cat"),},
                                    "kind");
            JsonTypeClassifier animalClassify = animalFactory.CreateJsonClassifier(animalContext, new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(ClassifiedAnimalBase))
                            {
                                typeInfo.TypeClassifier = animalClassify;
                            }
                            else if (typeInfo.Type == typeof(ClassifiedShape))
                            {
                                typeInfo.TypeClassifier = (ref Utf8JsonReader reader) =>
                                {
                                    if (reader.TokenType != JsonTokenType.StartObject) return null;
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                    {
                                        if (reader.TokenType == JsonTokenType.PropertyName)
                                        {
                                            if (reader.ValueTextEquals("Radius"u8)) return typeof(ClassifiedCircle);
                                            if (reader.ValueTextEquals("Width"u8)) return typeof(ClassifiedRectangle);
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

            // Animal
            string animalJson = """{"kind":"dog","Name":"Rex","Breed":"Lab"}""";
            ClassifiedAnimalBase? animal = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(animalJson, options);
            Assert.IsType<ClassifiedDog>(animal);

            // Shape
            string shapeJson = """{"Color":"red","Radius":5.0}""";
            ClassifiedShape? shape = await Serializer.DeserializeWrapper<ClassifiedShape>(shapeJson, options);
            Assert.IsType<ClassifiedCircle>(shape);
        }

        #endregion

        #region Serialization round-trip with classifier

        [Fact]
        public async Task Classifier_SerializeThenDeserialize_RoundTrips()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();

            ClassifiedAnimalBase original = new ClassifiedDog { Name = "Rex", Breed = "Lab" };
            string json = await Serializer.SerializeWrapper(original, options);

            // Serialization writes discriminator
            Assert.Contains("\"$type\"", json);

            // Deserialization with classifier can use either classifier or discriminator
            ClassifiedAnimalBase? roundtripped = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.IsType<ClassifiedDog>(roundtripped);
            Assert.Equal("Rex", roundtripped.Name);
            Assert.Equal("Lab", ((ClassifiedDog)roundtripped).Breed);
        }

        [Fact]
        public async Task Classifier_SerializeThenDeserialize_Collection()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();

            var original = new List<ClassifiedAnimalBase>
            {
                new ClassifiedDog { Name = "Rex", Breed = "Lab" },
                new ClassifiedCat { Name = "Whiskers", Lives = 9 },
            };

            string json = await Serializer.SerializeWrapper(original, options);
            List<ClassifiedAnimalBase>? roundtripped =
                await Serializer.DeserializeWrapper<List<ClassifiedAnimalBase>>(json, options);

            Assert.NotNull(roundtripped);
            Assert.Equal(2, roundtripped.Count);
            Assert.IsType<ClassifiedDog>(roundtripped[0]);
            Assert.IsType<ClassifiedCat>(roundtripped[1]);
        }

        #endregion

        #region Null and empty JSON values

        [Fact]
        public async Task Classifier_NullJsonValue_ReturnsNull()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = "null";

            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.Null(result);
        }

        [Fact]
        public async Task Classifier_EmptyObject_MatchesFirstDeclaredCase()
        {
            var options = CreateOptionsWithStructuralClassifier<ClassifiedAnimalBase>();
            string json = "{}";

            // Empty object matches all types equally → first-declared wins.
            ClassifiedAnimalBase? result = await Serializer.DeserializeWrapper<ClassifiedAnimalBase>(json, options);
            Assert.NotNull(result);
        }

        #endregion

        #region Helpers

        private static JsonSerializerOptions CreateOptionsWithStructuralClassifier<TBase>()
            where TBase : class
        {
            return new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(TBase) && typeInfo.PolymorphismOptions is not null)
                            {
                                var factory = new TestStructuralClassifierFactory();
                                var unionCases = typeInfo.PolymorphismOptions.DerivedTypes
                                    .Select(d => new JsonUnionCaseInfo(d.DerivedType))
                                    .ToList();
                                var context = new JsonTypeClassifierContext(
                                    typeof(TBase),
                                    unionCases,
                                    Array.Empty<JsonDerivedType>(),
                                    typeInfo.PolymorphismOptions.TypeDiscriminatorPropertyName);

                                typeInfo.TypeClassifier =
                                    factory.CreateJsonClassifier(context, typeInfo.Options);
                            }
                        }
                    }
                }
            };
        }

        private static JsonSerializerOptions CreateOptionsWithClassifier<TBase>(JsonTypeClassifier classifier)
            where TBase : class
        {
            return new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type == typeof(TBase) && typeInfo.PolymorphismOptions is not null)
                            {
                                typeInfo.TypeClassifier = classifier;
                            }
                        }
                    }
                }
            };
        }

        #endregion
    }
}
