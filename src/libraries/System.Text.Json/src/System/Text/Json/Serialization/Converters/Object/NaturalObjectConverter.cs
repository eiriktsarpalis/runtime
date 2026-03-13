// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    /// <summary>
    /// Reads JSON values into natural .NET primitive representations when the declared type is <see cref="object"/>.
    /// </summary>
    internal static class NaturalObjectConverter
    {
        /// <summary>
        /// Reads a single JSON value and returns its natural .NET representation.
        /// </summary>
        public static object? Read(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number => ReadNumber(ref reader),
                JsonTokenType.String => ReadString(ref reader),
                JsonTokenType.StartArray => ReadArray(ref reader, options),
                JsonTokenType.StartObject => ReadObject(ref reader, options),
                _ => throw new JsonException(),
            };
        }

        private static object ReadNumber(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt32(out int intValue))
            {
                return intValue;
            }

            if (reader.TryGetInt64(out long longValue))
            {
                return longValue;
            }

            if (reader.TryGetDouble(out double doubleValue))
            {
                // Check whether the double representation preserves the
                // full precision of the original JSON number by comparing
                // with the decimal representation.
                if (reader.TryGetDecimal(out decimal decimalValue) &&
                    (decimal)doubleValue != decimalValue)
                {
                    return decimalValue;
                }

                return doubleValue;
            }

            if (reader.TryGetDecimal(out decimal fallbackDecimal))
            {
                return fallbackDecimal;
            }

            throw new JsonException();
        }

        private static object ReadString(ref Utf8JsonReader reader)
        {
            // Attempt to detect well-known built-in formats that serialize as strings.
            if (reader.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset))
            {
                return dateTimeOffset;
            }

            if (reader.TryGetDateTime(out DateTime dateTime))
            {
                return dateTime;
            }

            if (reader.TryGetGuid(out Guid guid))
            {
                return guid;
            }

            return reader.GetString()!;
        }

        private static List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var list = new List<object?>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return list;
                }

                list.Add(Read(ref reader, options));
            }

            throw new JsonException();
        }

        private static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var dict = new Dictionary<string, object?>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dict;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                string key = reader.GetString()!;
                reader.Read();

                if (!options.AllowDuplicateProperties && dict.ContainsKey(key))
                {
                    ThrowHelper.ThrowJsonException_DuplicatePropertyNotAllowed(key);
                }

                dict[key] = Read(ref reader, options);
            }

            throw new JsonException();
        }
    }
}
