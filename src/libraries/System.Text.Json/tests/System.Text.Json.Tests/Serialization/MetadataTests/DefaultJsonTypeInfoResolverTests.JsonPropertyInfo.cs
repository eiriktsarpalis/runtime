// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public static partial class DefaultJsonTypeInfoResolverTests
    {
        [Fact]
        public static void JsonPropertyInfoOptionsAreSet()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = JsonTypeInfo.CreateJsonTypeInfo(typeof(MyClass), options);
            CreatePropertyAndCheckOptions(options, typeInfo);

            typeInfo = JsonTypeInfo.CreateJsonTypeInfo<MyClass>(options);
            CreatePropertyAndCheckOptions(options, typeInfo);

            typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            CreatePropertyAndCheckOptions(options, typeInfo);

            static void CreatePropertyAndCheckOptions(JsonSerializerOptions expectedOptions, JsonTypeInfo typeInfo)
            {
                JsonPropertyInfo propertyInfo = typeInfo.CreateJsonPropertyInfo(typeof(string), "test");
                Assert.Same(expectedOptions, propertyInfo.Options);
            }
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(MyClass))]
        public static void JsonPropertyInfoPropertyTypeIsSetWhenUsingCreateJsonPropertyInfo(Type propertyType)
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = JsonTypeInfo.CreateJsonTypeInfo(typeof(MyClass), options);
            JsonPropertyInfo propertyInfo = typeInfo.CreateJsonPropertyInfo(propertyType, "test");

            Assert.Equal(propertyType, propertyInfo.PropertyType);
        }

        [Fact]
        public static void JsonPropertyInfoPropertyTypeIsSet()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            JsonPropertyInfo propertyInfo = typeInfo.Properties[0];
            Assert.Equal(typeof(string), propertyInfo.PropertyType);
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(MyClass))]
        public static void JsonPropertyInfoNameIsSetAndIsMutableWhenUsingCreateJsonPropertyInfo(Type propertyType)
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = JsonTypeInfo.CreateJsonTypeInfo(typeof(MyClass), options);
            JsonPropertyInfo propertyInfo = typeInfo.CreateJsonPropertyInfo(propertyType, "test");

            Assert.Equal("test", propertyInfo.Name);

            propertyInfo.Name = "foo";
            Assert.Equal("foo", propertyInfo.Name);

            Assert.Throws<ArgumentNullException>(() => propertyInfo.Name = null);
        }

        [Fact]
        public static void JsonPropertyInfoNameIsSetAndIsMutableForDefaultResolver()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            JsonPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.Equal(nameof(MyClass.Value), propertyInfo.Name);

            propertyInfo.Name = "foo";
            Assert.Equal("foo", propertyInfo.Name);

            Assert.Throws<ArgumentNullException>(() => propertyInfo.Name = null);
        }

        [Fact]
        public static void JsonPropertyInfoForDefaultResolverHasNamingPoliciesRulesApplied()
        {
            JsonSerializerOptions options = new();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(MyClass), options);
            JsonPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.Equal(nameof(MyClass.Value).ToLowerInvariant(), propertyInfo.Name);

            // explicitly setting does not change casing
            propertyInfo.Name = "Foo";
            Assert.Equal("Foo", propertyInfo.Name);
        }

        [Fact]
        public static void JsonPropertyInfoCustomConverterIsNullWhenUsingCreateJsonPropertyInfo()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            JsonPropertyInfo propertyInfo = typeInfo.CreateJsonPropertyInfo(typeof(MyClass), "test");

            Assert.Null(propertyInfo.CustomConverter);
        }

        [Fact]
        public static void JsonPropertyInfoCustomConverterIsNotNullForPropertyWithCustomConverter()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            JsonPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.NotNull(propertyInfo.CustomConverter);
            Assert.IsType<MyClassConverterOriginal>(propertyInfo.CustomConverter);
        }

        [Fact]
        public static void JsonPropertyInfoCustomConverterSetToNullIsRespected()
        {
            JsonSerializerOptions options = new();
            DefaultJsonTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    JsonPropertyInfo propertyInfo = ti.Properties[0];
                    Assert.NotNull(propertyInfo.CustomConverter);
                    Assert.IsType<MyClassConverterOriginal>(propertyInfo.CustomConverter);
                    propertyInfo.CustomConverter = null;
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string json = JsonSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":{"Value":"SomeValue","Thing":null}}""", json);

            TestClassWithCustomConverterOnProperty deserialized = JsonSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(json, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }

        [Fact]
        public static void JsonPropertyInfoCustomConverterIsRespected()
        {
            JsonSerializerOptions options = new();
            DefaultJsonTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    JsonPropertyInfo propertyInfo = ti.Properties[0];
                    Assert.NotNull(propertyInfo.CustomConverter);
                    Assert.IsType<MyClassConverterOriginal>(propertyInfo.CustomConverter);
                    propertyInfo.CustomConverter = new MyClassCustomConverter("test_");
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string json = JsonSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":"test_SomeValue"}""", json);

            TestClassWithCustomConverterOnProperty deserialized = JsonSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(json, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }

        [Fact]
        public static void JsonPropertyInfoGetIsNullAndMutableWhenUsingCreateJsonPropertyInfo()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            JsonPropertyInfo propertyInfo = typeInfo.CreateJsonPropertyInfo(typeof(MyClass), "test");
            Assert.Null(propertyInfo.Get);
            Func<object, object> get = (obj) =>
            {
                throw new NotImplementedException();
            };

            propertyInfo.Get = get;
            Assert.Same(get, propertyInfo.Get);
        }

        [Fact]
        public static void JsonPropertyInfoGetIsNotNullForDefaultResolver()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            JsonPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.NotNull(propertyInfo.Get);

            TestClassWithCustomConverterOnProperty obj = new();

            Assert.Null(propertyInfo.Get(obj));

            obj.MyClassProperty = new MyClass();
            Assert.Same(obj.MyClassProperty, propertyInfo.Get(obj));

            MyClass sentinel = new();
            Func<object, object> get = (obj) => sentinel;
            propertyInfo.Get = get;
            Assert.Same(get, propertyInfo.Get);
            Assert.Same(sentinel, propertyInfo.Get(obj));
        }

        [Fact]
        public static void JsonPropertyInfoGetPropertyNotSerializableButDeserializableWhenNull()
        {
            JsonSerializerOptions options = new();
            DefaultJsonTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    JsonPropertyInfo propertyInfo = ti.Properties[0];
                    propertyInfo.Get = null;
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string json = JsonSerializer.Serialize(obj, options);
            Assert.Equal("{}", json);

            json = """{"MyClassProperty":"SomeValue"}""";
            TestClassWithCustomConverterOnProperty deserialized = JsonSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(json, options);
            Assert.Equal(obj.MyClassProperty.Value, deserialized.MyClassProperty.Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static void JsonPropertyInfoGetIsRespected(bool useCustomConverter)
        {
            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            MyClass substitutedValue = new MyClass() { Value = "SomeOtherValue" };

            bool getterCalled = false;

            JsonSerializerOptions options = new();
            DefaultJsonTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    JsonPropertyInfo propertyInfo = ti.Properties[0];
                    if (!useCustomConverter)
                    {
                        propertyInfo.CustomConverter = null;
                    }

                    propertyInfo.Get = (o) =>
                    {
                        Assert.Same(obj, o);
                        Assert.False(getterCalled);
                        getterCalled = true;
                        return substitutedValue;
                    };
                }
            });

            options.TypeInfoResolver = r;

            string json = JsonSerializer.Serialize(obj, options);
            if (useCustomConverter)
            {
                Assert.Equal("""{"MyClassProperty":"SomeOtherValue"}""", json);
            }
            else
            {
                Assert.Equal("""{"MyClassProperty":{"Value":"SomeOtherValue","Thing":null}}""", json);
            }

            TestClassWithCustomConverterOnProperty deserialized = JsonSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(json, options);
            Assert.Equal(substitutedValue.Value, deserialized.MyClassProperty.Value);

            Assert.True(getterCalled);
        }

        [Fact]
        public static void JsonPropertyInfoSetIsNullAndMutableWhenUsingCreateJsonPropertyInfo()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            JsonPropertyInfo propertyInfo = typeInfo.CreateJsonPropertyInfo(typeof(MyClass), "test");
            Assert.Null(propertyInfo.Set);
            Action<object, object> set = (obj, val) =>
            {
                throw new NotImplementedException();
            };

            propertyInfo.Set = set;
            Assert.Same(set, propertyInfo.Set);
        }

        [Fact]
        public static void JsonPropertyInfoSetIsNotNullForDefaultResolver()
        {
            JsonSerializerOptions options = new();
            JsonTypeInfo typeInfo = options.TypeInfoResolver.GetTypeInfo(typeof(TestClassWithCustomConverterOnProperty), options);
            JsonPropertyInfo propertyInfo = typeInfo.Properties[0];

            Assert.NotNull(propertyInfo.Set);

            TestClassWithCustomConverterOnProperty obj = new();

            MyClass value = new MyClass();
            propertyInfo.Set(obj, value);
            Assert.Same(value, obj.MyClassProperty);

            MyClass sentinel = new();
            Action<object, object> set = (o, value) =>
            {
                Assert.Same(obj, o);
                Assert.Same(sentinel, value);
                obj.MyClassProperty = sentinel;
            };

            propertyInfo.Set = set;
            Assert.Same(set, propertyInfo.Set);

            propertyInfo.Set(obj, sentinel);
            Assert.Same(obj.MyClassProperty, sentinel);
        }

        [Fact]
        public static void JsonPropertyInfoSetPropertyDeserializableButNotSerializableWhenNull()
        {
            JsonSerializerOptions options = new();
            DefaultJsonTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    JsonPropertyInfo propertyInfo = ti.Properties[0];
                    propertyInfo.Set = null;
                }
            });

            options.TypeInfoResolver = r;

            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            string json = JsonSerializer.Serialize(obj, options);
            Assert.Equal("""{"MyClassProperty":"SomeValue"}""", json);

            TestClassWithCustomConverterOnProperty deserialized = JsonSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(json, options);
            Assert.Null(deserialized.MyClassProperty);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static void JsonPropertyInfoSetIsRespected(bool useCustomConverter)
        {
            TestClassWithCustomConverterOnProperty obj = new()
            {
                MyClassProperty = new MyClass() { Value = "SomeValue" },
            };

            MyClass substitutedValue = new MyClass() { Value = "SomeOtherValue" };
            bool setterCalled = false;

            JsonSerializerOptions options = new();
            DefaultJsonTypeInfoResolver r = new();
            r.Modifiers.Add(ti =>
            {
                if (ti.Type == typeof(TestClassWithCustomConverterOnProperty))
                {
                    JsonPropertyInfo propertyInfo = ti.Properties[0];
                    if (!useCustomConverter)
                    {
                        propertyInfo.CustomConverter = null;
                    }

                    propertyInfo.Set = (o, val) =>
                    {
                        var testClass = (TestClassWithCustomConverterOnProperty)o;
                        Assert.IsType<MyClass>(val);
                        MyClass myClass = (MyClass)val;
                        Assert.Equal(obj.MyClassProperty.Value, myClass.Value);

                        testClass.MyClassProperty = substitutedValue;
                        Assert.False(setterCalled);
                        setterCalled = true;
                    };
                }
            });

            options.TypeInfoResolver = r;

            string json = JsonSerializer.Serialize(obj, options);
            if (useCustomConverter)
            {
                Assert.Equal("""{"MyClassProperty":"SomeValue"}""", json);
            }
            else
            {
                Assert.Equal("""{"MyClassProperty":{"Value":"SomeValue","Thing":null}}""", json);
            }

            TestClassWithCustomConverterOnProperty deserialized = JsonSerializer.Deserialize<TestClassWithCustomConverterOnProperty>(json, options);
            Assert.Same(substitutedValue, deserialized.MyClassProperty);
            Assert.True(setterCalled);
        }

        [Fact]
        public static void AddingNumberHandlingToPropertyIsRespected()
        {
            DefaultJsonTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumber))
                {
                    Assert.Null(ti.Properties[0].NumberHandling);
                    ti.Properties[0].NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString;
                }
            });

            JsonSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            TestClassWithNumber obj = new()
            {
                IntProperty = 37,
            };

            string json = JsonSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":"37"}""", json);

            TestClassWithNumber deserialized = JsonSerializer.Deserialize<TestClassWithNumber>(json, o);
            Assert.Equal(obj.IntProperty, deserialized.IntProperty);
        }

        private class TestClassWithNumber
        {
            public int IntProperty { get; set; }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(JsonNumberHandling.Strict)]
        public static void RemovingOrChangingNumberHandlingFromPropertyIsRespected(JsonNumberHandling? numberHandling)
        {
            DefaultJsonTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumberHandlingOnProperty))
                {
                    Assert.Equal(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString, ti.Properties[0].NumberHandling);
                    ti.Properties[0].NumberHandling = numberHandling;
                }
            });

            JsonSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            TestClassWithNumberHandlingOnProperty obj = new()
            {
                IntProperty = 37,
            };

            string json = JsonSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", json);

            TestClassWithNumberHandlingOnProperty deserialized = JsonSerializer.Deserialize<TestClassWithNumberHandlingOnProperty>(json, o);
            Assert.Equal(obj.IntProperty, deserialized.IntProperty);
        }

        private class TestClassWithNumberHandlingOnProperty
        {
            [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
            public int IntProperty { get; set; }
        }


        [Fact]
        public static void NumberHandlingFromTypeDoesntFlowToPropertyAndOverrideIsRespected()
        {
            DefaultJsonTypeInfoResolver resolver = new();
            resolver.Modifiers.Add((ti) =>
            {
                if (ti.Type == typeof(TestClassWithNumberHandling))
                {
                    Assert.Null(ti.Properties[0].NumberHandling);
                    ti.Properties[0].NumberHandling = JsonNumberHandling.Strict;
                }
            });

            JsonSerializerOptions o = new();
            o.TypeInfoResolver = resolver;

            TestClassWithNumberHandling obj = new()
            {
                IntProperty = 37,
            };

            string json = JsonSerializer.Serialize(obj, o);
            Assert.Equal("""{"IntProperty":37}""", json);

            TestClassWithNumberHandling deserialized = JsonSerializer.Deserialize<TestClassWithNumberHandling>(json, o);
            Assert.Equal(obj.IntProperty, deserialized.IntProperty);
        }

        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        private class TestClassWithNumberHandling
        {
            public int IntProperty { get; set; }
        }

        private class TestClassWithCustomConverterOnProperty
        {
            [JsonConverter(typeof(MyClassConverterOriginal))]
            public MyClass MyClassProperty { get; set; }
        }

        private class MyClassConverterOriginal : JsonConverter<MyClass>
        {
            public override MyClass? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new InvalidOperationException($"Wrong token type: {reader.TokenType}");

                MyClass myClass = new MyClass();
                myClass.Value = reader.GetString();
                return myClass;
            }

            public override void Write(Utf8JsonWriter writer, MyClass value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.Value);
            }
        }

        private class MyClassCustomConverter : JsonConverter<MyClass>
        {
            private string _prefix;

            public MyClassCustomConverter(string prefix)
            {
                _prefix = prefix;
            }

            public override MyClass? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new InvalidOperationException($"Wrong token type: {reader.TokenType}");

                MyClass myClass = new MyClass();
                myClass.Value = reader.GetString().Substring(_prefix.Length);
                return myClass;
            }

            public override void Write(Utf8JsonWriter writer, MyClass value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(_prefix + value.Value);
            }
        }
    }
}
