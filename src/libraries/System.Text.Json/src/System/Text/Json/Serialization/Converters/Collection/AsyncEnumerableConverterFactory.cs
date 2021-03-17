// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace System.Text.Json.Serialization.Converters
{
    /// <summary>
    /// Converter factory for all IEnumerable types.
    /// </summary>
    internal class AsyncEnumerableConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            // only support IAsyncEnumerable types
            return typeToConvert.IsGenericType && typeof(IAsyncEnumerable<>) == typeToConvert.GetGenericTypeDefinition();
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(typeToConvert.IsGenericType && typeof(IAsyncEnumerable<>) == typeToConvert.GetGenericTypeDefinition());
            Type elementType = typeToConvert.GetGenericArguments()[0];
            Type converterType = typeof(AsyncEnumerableOfTConverter<,>).MakeGenericType(typeToConvert, elementType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }
}
