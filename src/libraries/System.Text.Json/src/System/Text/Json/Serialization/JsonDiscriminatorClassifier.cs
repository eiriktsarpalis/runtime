// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// A <see cref="JsonTypeClassifierFactory"/> that builds a classifier delegate which scans
    /// JSON objects for a discriminator property and maps its value to a <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All configuration is read from the <see cref="JsonTypeClassifierContext"/>:
    /// <see cref="JsonTypeClassifierContext.TypeDiscriminatorPropertyName"/> provides the JSON
    /// property to scan for, and <see cref="JsonTypeClassifierContext.CandidateTypes"/> provides
    /// the discriminator-value-to-type mapping.
    /// </para>
    /// <para>
    /// When <see cref="JsonTypeClassifierContext.TypeDiscriminatorPropertyName"/> is
    /// <see langword="null"/>, defaults to <c>"$type"</c>.
    /// </para>
    /// </remarks>
    public class JsonDiscriminatorClassifierFactory : JsonTypeClassifierFactory
    {
        /// <inheritdoc/>
        public override JsonTypeClassifier CreateJsonClassifier(
            JsonTypeClassifierContext context,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(options);

            string propertyName = context.TypeDiscriminatorPropertyName ?? JsonSerializer.TypePropertyName;
            byte[] propertyNameUtf8 = System.Text.Encoding.UTF8.GetBytes(propertyName);

            Dictionary<string, Type>? stringMap = null;
            Dictionary<int, Type>? intMap = null;

            foreach (JsonDerivedType dt in context.CandidateTypes)
            {
                if (dt.TypeDiscriminator is string s)
                {
                    stringMap ??= new Dictionary<string, Type>(StringComparer.Ordinal);
                    stringMap[s] = dt.DerivedType;
                }
                else if (dt.TypeDiscriminator is int i)
                {
                    intMap ??= new Dictionary<int, Type>();
                    intMap[i] = dt.DerivedType;
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
}
