// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Json.Serialization.Converters
{
    /// <summary>
    /// Converter factory for all IEnumerable types.
    /// </summary>
    internal class IAsyncEnumerableConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => TryGetAsyncEnumerableInterface(typeToConvert, out _);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (!TryGetAsyncEnumerableInterface(typeToConvert, out Type? asyncEnumerableInterface))
            {
                throw new ArgumentException("type not supported by the converter.", nameof(typeToConvert));
            }

            Type elementType = asyncEnumerableInterface.GetGenericArguments()[0];
            Type converterType = typeof(IAsyncEnumerableOfTConverter<,>).MakeGenericType(typeToConvert, elementType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private static bool TryGetAsyncEnumerableInterface(Type type, [NotNullWhen(true)] out Type? asyncEnumerableInterface)
        {
            if (type.IsInterface && IsAsyncEnumerableInterface(type))
            {
                asyncEnumerableInterface = type;
                return true;
            }

            foreach (Type interfaceTy in type.GetInterfaces())
            {
                if (IsAsyncEnumerableInterface(interfaceTy))
                {
                    asyncEnumerableInterface = interfaceTy;
                    return true;
                }
            }

            asyncEnumerableInterface = null;
            return false;

            static bool IsAsyncEnumerableInterface(Type interfaceTy)
                => interfaceTy.IsGenericType && interfaceTy.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
        }
    }
}
