// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json.Schema;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonNumberConverter : JsonPrimitiveConverter<JsonNumber>
    {
        public JsonNumberConverter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override JsonNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not JsonNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetJsonNumber();
        }

        public override void Write(Utf8JsonWriter writer, JsonNumber value, JsonSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not JsonNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            writer.WriteNumberValue(value);
        }

        internal override JsonNumber ReadAsPropertyNameCore(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
            return reader.GetJsonNumberWithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8JsonWriter writer, JsonNumber value, JsonSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value.ToString());
        }

        internal override JsonNumber ReadNumberWithCustomHandling(ref Utf8JsonReader reader, JsonNumberHandling handling, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String &&
                (JsonNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return reader.GetJsonNumberWithQuotes();
            }

            return reader.GetJsonNumber();
        }

        internal override void WriteNumberWithCustomHandling(Utf8JsonWriter writer, JsonNumber value, JsonNumberHandling handling)
        {
            if ((JsonNumberHandling.WriteAsString & handling) != 0)
            {
                writer.WriteNumberValueAsString(value);
            }
            else
            {
                writer.WriteNumberValue(value);
            }
        }

        internal override JsonSchema? GetSchema(JsonNumberHandling numberHandling) =>
            GetSchemaForNumericType(JsonSchemaType.Number, numberHandling);
    }
}
