// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    // TODO: recursive type, options.resolver provides different answer than what we pass in
    // TODO: ensure converter rooting happens for APIs taking JsonTypeInfo
    public static partial class DefaultJsonTypeInfoResolverTests
    {
        [Theory]
        [InlineData(typeof(string), "value", @"""value""")]
        [InlineData(typeof(int), 5, @"5")]
        [MemberData(nameof(JsonSerializerSerializeWithTypeInfoOfT_TestData))]
        public static void JsonSerializerSerializeWithTypeInfoOfT(Type type, object testObj, string expectedJson)
        {
            InvokeGeneric(type, nameof(JsonSerializerSerializeWithTypeInfoOfT_Generic), testObj, expectedJson);
        }

        public static IEnumerable<object[]> JsonSerializerSerializeWithTypeInfoOfT_TestData()
        {
            yield return new object[] { typeof(SomeClass), new SomeClass() { IntProp = 15, ObjProp = 17m }, @"{""ObjProp"":17,""IntProp"":15}" };
        }

        private static void JsonSerializerSerializeWithTypeInfoOfT_Generic<T>(T testObj, string expectedJson)
        {
            DefaultJsonTypeInfoResolver r = new();
            JsonSerializerOptions o = new();
            o.TypeInfoResolver = r;
            JsonTypeInfo<T> typeInfo = (JsonTypeInfo<T>)r.GetTypeInfo(typeof(T), o);
            string json = JsonSerializer.Serialize(testObj, typeInfo);
            Assert.Equal(expectedJson, json);
        }
    }
}
