// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// A built-in <see cref="JsonTypeClassifierFactory"/> that uses structural matching
    /// to classify JSON payloads to candidate types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The structural matching algorithm scores each candidate type against the JSON payload
    /// by inspecting token types, property names, array elements, and nested structures.
    /// The candidate type with the highest score wins.
    /// </para>
    /// <para>
    /// This is the default classifier used when no custom <see cref="JsonTypeClassifierFactory"/>
    /// is specified on the <see cref="JsonUnionAttribute"/>.
    /// </para>
    /// </remarks>
    [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
    public class JsonStructuralClassifierFactory : JsonTypeClassifierFactory
    {
        private const int MaxStructuralMatchingDepth = 64;

        /// <inheritdoc/>
        public override JsonTypeClassifier CreateJsonClassifier(
            JsonTypeClassifierContext context,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(options);

            // Extract candidate types from context (ignore discriminator values — structural matching
            // doesn't use them).
            var caseMetadata = new CaseTypeMetadata[context.CandidateTypes.Count];
            for (int i = 0; i < context.CandidateTypes.Count; i++)
            {
                caseMetadata[i] = BuildCaseTypeMetadata(context.CandidateTypes[i].DerivedType, options);
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

            // When multiple case types score equally, the first declared case type wins.
            // This is deterministic because case type order is user-controlled (declaration order).
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
            if (cmp != 0)
            {
                return cmp;
            }

            return right.UnmatchedCount.CompareTo(left.UnmatchedCount);
        }

        internal static MatchScore ScoreCaseType(Utf8JsonReader reader, Type candidateType, CaseTypeMetadata? metadata, JsonSerializerOptions options, int depth)
        {
            if (depth > MaxStructuralMatchingDepth)
            {
                return MatchScore.Disqualified;
            }

#if NET11_0_OR_GREATER
            if (candidateType.GetCustomAttribute<UnionAttribute>() is not null
                && typeof(IUnion).IsAssignableFrom(candidateType))
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
            MatchScore bestScore = MatchScore.Disqualified;

            foreach (ConstructorInfo ctor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length == 1)
                {
                    Type innerCaseType = Nullable.GetUnderlyingType(parameters[0].ParameterType) ?? parameters[0].ParameterType;
                    MatchScore innerScore = ScoreCaseType(reader, innerCaseType, metadata: null, options, depth + 1);

                    if (!innerScore.IsDisqualified && (bestScore.IsDisqualified || CompareTo(innerScore, bestScore) > 0))
                    {
                        bestScore = innerScore;
                    }
                }
            }

            return bestScore;
        }
#endif

        private static MatchScore ScoreNull(Type candidateType)
        {
            if (!candidateType.IsValueType || Nullable.GetUnderlyingType(candidateType) is not null)
            {
                return new MatchScore(1, 0);
            }

            return MatchScore.Disqualified;
        }

        private static MatchScore ScorePrimitive(Type candidateType, bool isNumber)
        {
            Type underlying = Nullable.GetUnderlyingType(candidateType) ?? candidateType;

            if (isNumber)
            {
                if (IsNumericType(underlying))
                {
                    return new MatchScore(1, 0);
                }
            }
            else
            {
                if (underlying == typeof(DateTime) ||
                    underlying == typeof(DateTimeOffset) ||
                    underlying == typeof(Guid) ||
                    underlying == typeof(TimeSpan) ||
                    underlying == typeof(Uri) ||
                    underlying == typeof(char) ||
                    underlying == typeof(byte[]) ||
                    underlying.IsEnum ||
                    underlying == typeof(string) ||
                    underlying == typeof(JsonElement))
                {
                    return new MatchScore(1, 0);
                }
            }

            return MatchScore.Disqualified;
        }

        private static MatchScore ScoreBoolean(Type candidateType)
        {
            Type underlying = Nullable.GetUnderlyingType(candidateType) ?? candidateType;

            if (underlying == typeof(bool))
            {
                return new MatchScore(1, 0);
            }

            return MatchScore.Disqualified;
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

            if (!hasElements)
            {
                return new MatchScore(1, 0);
            }

            return new MatchScore(1 + totalMatched, totalUnmatched);
        }

        private static MatchScore ScoreObject(Utf8JsonReader reader, Type candidateType, CaseTypeMetadata? metadata, JsonSerializerOptions options, int depth)
        {
            // Use cached metadata if available (top-level case types), otherwise build on the fly (nested properties).
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
                        MatchScore propScore = ScoreCaseType(reader, propertyType, metadata: null, options, depth + 1);

                        if (propScore.IsDisqualified)
                        {
                            unmatchedCount++;
                        }
                        else
                        {
                            matchedCount += 1 + propScore.MatchedCount;
                            unmatchedCount += propScore.UnmatchedCount;
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

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) ||
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
        }

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

        internal readonly record struct MatchScore(int MatchedCount, int UnmatchedCount)
        {
            public static readonly MatchScore Disqualified = new(-1, -1);
            public bool IsDisqualified => MatchedCount < 0;
        }

        internal sealed class CaseTypeMetadata
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
}
