// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Classifies JSON payloads into union case types by comparing their JSON value types
    /// and, for JSON objects, their property names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This classifier supports union types only. For non-object JSON values, every JSON
    /// value type must map to at most one union case. String contents, array elements, and
    /// other nested values are not inspected.
    /// </para>
    /// <para>
    /// JSON objects are classified by the presence of property names at the root level.
    /// The object case that declares the greatest number of the payload's property names
    /// is selected. Property values and nested structures are not inspected. Classification
    /// fails when multiple object cases tie for the greatest number of matching properties.
    /// </para>
    /// <para>
    /// The classifier honors <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/>,
    /// required properties, and <see cref="JsonUnmappedMemberHandling.Disallow"/>.
    /// </para>
    /// </remarks>
    public class JsonUnionTypeStructuralClassifier : JsonTypeClassifierFactory
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonUnionTypeStructuralClassifier"/> class.
        /// </summary>
        public JsonUnionTypeStructuralClassifier()
        {
        }

        /// <inheritdoc/>
        public override bool CanClassify(JsonTypeClassifierContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return context.Kind is JsonTypeClassifierKind.Union;
        }

        /// <inheritdoc/>
        public override JsonTypeClassifier CreateJsonClassifier(
            JsonTypeClassifierContext context,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(options);

            if (context.Kind is not JsonTypeClassifierKind.Union)
            {
                throw new InvalidOperationException(
                    SR.Format(SR.UnionTypeStructuralClassifierOnlyForUnions, context.DeclaringType));
            }

            ClassifierState state = BuildClassifierState(
                context.DeclaringType,
                context.UnionCases,
                options);

            return (ref Utf8JsonReader reader) => Classify(state, ref reader);
        }

        private static ClassifierState BuildClassifierState(
            Type unionType,
            IReadOnlyList<JsonUnionCaseInfo> unionCases,
            JsonSerializerOptions options)
        {
            var nonObjectCases = new Dictionary<JsonValueType, Type>();
            var objectCases = new List<ObjectCase>();

            foreach (JsonUnionCaseInfo unionCase in unionCases)
            {
                AddCase(
                    unionType,
                    unionCase.CaseType,
                    options,
                    nonObjectCases,
                    objectCases);
            }

            ValidateObjectCases(unionType, objectCases);
            return new ClassifierState(nonObjectCases, objectCases.ToArray());
        }

        private static void AddCase(
            Type unionType,
            Type caseType,
            JsonSerializerOptions options,
            Dictionary<JsonValueType, Type> nonObjectCases,
            List<ObjectCase> objectCases)
        {
            Type effectiveType = Nullable.GetUnderlyingType(caseType) ?? caseType;
            JsonTypeInfo typeInfo = options.GetTypeInfo(effectiveType);

            if (typeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators is true ||
                typeInfo.Kind is JsonTypeInfoKind.Object or JsonTypeInfoKind.Dictionary)
            {
                objectCases.Add(BuildObjectCase(caseType, typeInfo, options));
                return;
            }

            JsonValueType valueTypes;
            if (typeInfo.Kind is JsonTypeInfoKind.Enumerable)
            {
                valueTypes = JsonValueType.Array;
            }
            else if (typeInfo.Kind is JsonTypeInfoKind.Union)
            {
                ThrowUnsupportedCase(unionType, caseType);
                return;
            }
            else
            {
                JsonNumberHandling numberHandling = typeInfo.NumberHandling ?? options.NumberHandling;
                valueTypes = typeInfo.Converter.GetSupportedJsonValueTypes(numberHandling);
            }

            const JsonValueType SupportedValueTypes =
                JsonValueType.Array |
                JsonValueType.String |
                JsonValueType.Number |
                JsonValueType.Boolean;

            valueTypes &= SupportedValueTypes;
            if (valueTypes is JsonValueType.None)
            {
                ThrowUnsupportedCase(unionType, caseType);
                return;
            }

            AddValueType(JsonValueType.Array);
            AddValueType(JsonValueType.String);
            AddValueType(JsonValueType.Number);
            AddValueType(JsonValueType.Boolean);

            void AddValueType(JsonValueType valueType)
            {
                if ((valueTypes & valueType) == 0)
                {
                    return;
                }

                if (!nonObjectCases.TryAdd(valueType, caseType))
                {
                    Type conflictingCaseType = nonObjectCases[valueType];
                    throw new NotSupportedException(
                        SR.Format(
                            SR.UnionTypeStructuralClassifierAmbiguousCases,
                            unionType,
                            conflictingCaseType,
                            caseType,
                            valueType));
                }
            }

            static void ThrowUnsupportedCase(Type unionType, Type caseType) =>
                throw new NotSupportedException(
                    SR.Format(
                        SR.UnionTypeStructuralClassifierUnsupportedCase,
                        unionType,
                        caseType));
        }

        private static ObjectCase BuildObjectCase(
            Type caseType,
            JsonTypeInfo typeInfo,
            JsonSerializerOptions options)
        {
            bool caseInsensitive = options.PropertyNameCaseInsensitive;
            var properties = new List<PropertyShape>(typeInfo.Properties.Count);
            int requiredCount = 0;
            bool hasExtensionData = false;
            bool usesTypeDiscriminators = typeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators is true;

            if (usesTypeDiscriminators)
            {
                string discriminatorPropertyName = typeInfo.PolymorphismOptions!.TypeDiscriminatorPropertyName;
                properties.Add(new PropertyShape(
                    discriminatorPropertyName,
                    isRequired: true,
                    isCaseSensitive: true));
                requiredCount++;
            }

            foreach (JsonPropertyInfo property in typeInfo.Properties)
            {
                if (property.IsExtensionData)
                {
                    hasExtensionData = true;
                    continue;
                }

                properties.Add(new PropertyShape(
                    property.Name,
                    property.IsRequired,
                    isCaseSensitive: false));

                if (property.IsRequired)
                {
                    requiredCount++;
                }
            }

            // Dictionaries accept arbitrary keys, so JsonUnmappedMemberHandling does not apply to them.
            JsonUnmappedMemberHandling unmappedMemberHandling = typeInfo.UnmappedMemberHandling ??
                (hasExtensionData ? JsonUnmappedMemberHandling.Skip : options.UnmappedMemberHandling);

            bool disallowUnmapped = typeInfo.Kind is JsonTypeInfoKind.Object
                && !usesTypeDiscriminators
                && unmappedMemberHandling is JsonUnmappedMemberHandling.Disallow;

            return new ObjectCase(
                caseType,
                properties.ToArray(),
                caseInsensitive,
                disallowUnmapped,
                requiredCount);
        }

        private static void ValidateObjectCases(Type unionType, List<ObjectCase> objectCases)
        {
            for (int i = 0; i < objectCases.Count; i++)
            {
                ObjectCase left = objectCases[i];

                for (int j = i + 1; j < objectCases.Count; j++)
                {
                    ObjectCase right = objectCases[j];
                    if (left.CaseSensitivePropertyNames.SetEquals(right.CaseSensitivePropertyNames) &&
                        left.CaseInsensitivePropertyNames.SetEquals(right.CaseInsensitivePropertyNames))
                    {
                        throw new NotSupportedException(
                            SR.Format(
                                SR.UnionTypeStructuralClassifierIndistinguishableObjectCases,
                                unionType,
                                left.CaseType,
                                right.CaseType));
                    }
                }
            }
        }

        private static Type? Classify(ClassifierState state, ref Utf8JsonReader reader)
        {
            JsonValueType valueType = reader.TokenType switch
            {
                JsonTokenType.StartObject => JsonValueType.Object,
                JsonTokenType.StartArray => JsonValueType.Array,
                JsonTokenType.String => JsonValueType.String,
                JsonTokenType.Number => JsonValueType.Number,
                JsonTokenType.True or JsonTokenType.False => JsonValueType.Boolean,
                _ => JsonValueType.None,
            };

            if (valueType is JsonValueType.Object)
            {
                return ClassifyObject(state.ObjectCases, ref reader);
            }

            state.NonObjectCases.TryGetValue(valueType, out Type? caseType);
            return caseType;
        }

        private static Type? ClassifyObject(ObjectCase[] cases, ref Utf8JsonReader reader)
        {
            Type? bestType = null;
            int bestScore = -1;
            bool tied = false;

            foreach (ObjectCase candidate in cases)
            {
                Utf8JsonReader readerCopy = reader;
                int score = candidate.Score(ref readerCopy);
                if (score < 0)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestType = candidate.CaseType;
                    bestScore = score;
                    tied = false;
                }
                else if (score == bestScore)
                {
                    tied = true;
                }
            }

            return tied ? null : bestType;
        }

        private sealed class ClassifierState(
            Dictionary<JsonValueType, Type> nonObjectCases,
            ObjectCase[] objectCases)
        {
            public Dictionary<JsonValueType, Type> NonObjectCases { get; } = nonObjectCases;
            public ObjectCase[] ObjectCases { get; } = objectCases;
        }

        private sealed class ObjectCase
        {
            private const int InlinePropertyCount = 64;
            private const int PropertiesPerWord = 64;
            private const int MaxStackallocWords = 16;

            private readonly PropertyShape[] _properties;
            private readonly Dictionary<string, int>? _caseInsensitivePropertyMap;
            private readonly bool _disallowUnmapped;
            private readonly int _requiredCount;

            public ObjectCase(
                Type caseType,
                PropertyShape[] properties,
                bool propertyNameCaseInsensitive,
                bool disallowUnmapped,
                int requiredCount)
            {
                CaseType = caseType;
                _properties = properties;
                _disallowUnmapped = disallowUnmapped;
                _requiredCount = requiredCount;
                CaseSensitivePropertyNames = new(StringComparer.Ordinal);
                CaseInsensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyShape property = properties[i];
                    if (!propertyNameCaseInsensitive || property.IsCaseSensitive)
                    {
                        CaseSensitivePropertyNames.Add(property.Name);
                    }
                    else
                    {
                        CaseInsensitivePropertyNames.Add(property.Name);
                        (_caseInsensitivePropertyMap ??= new(properties.Length, StringComparer.OrdinalIgnoreCase))
                            .TryAdd(property.Name, i);
                    }
                }
            }

            public Type CaseType { get; }
            public HashSet<string> CaseInsensitivePropertyNames { get; }
            public HashSet<string> CaseSensitivePropertyNames { get; }

            public int Score(ref Utf8JsonReader reader)
            {
                if (reader.TokenType is not JsonTokenType.StartObject)
                {
                    return -1;
                }

                int score = 0;
                int requiredSeen = 0;
                ulong seenProperties = 0;
                ulong[]? rentedSeenPropertyWords = null;
                scoped Span<ulong> seenPropertyWords = default;

                if (_properties.Length > InlinePropertyCount)
                {
                    int wordCount = (_properties.Length + PropertiesPerWord - 1) / PropertiesPerWord;
                    if (wordCount <= MaxStackallocWords)
                    {
                        seenPropertyWords = stackalloc ulong[wordCount];
                    }
                    else
                    {
                        rentedSeenPropertyWords = ArrayPool<ulong>.Shared.Rent(wordCount);
                        seenPropertyWords = rentedSeenPropertyWords.AsSpan(0, wordCount);
                    }

                    seenPropertyWords.Clear();
                }

                try
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType is JsonTokenType.EndObject)
                        {
                            break;
                        }

                        if (reader.TokenType is not JsonTokenType.PropertyName)
                        {
                            return -1;
                        }

                        int matchedIndex = FindProperty(ref reader);

                        if (!reader.Read())
                        {
                            break;
                        }

                        if (matchedIndex < 0)
                        {
                            if (_disallowUnmapped)
                            {
                                return -1;
                            }
                        }
                        else if (MarkPropertyAsSeen(matchedIndex, ref seenProperties, seenPropertyWords))
                        {
                            score++;

                            if (_properties[matchedIndex].IsRequired)
                            {
                                requiredSeen++;
                            }
                        }

                        reader.TrySkip();
                    }

                    return requiredSeen < _requiredCount ? -1 : score;
                }
                finally
                {
                    if (rentedSeenPropertyWords is not null)
                    {
                        ArrayPool<ulong>.Shared.Return(rentedSeenPropertyWords);
                    }
                }
            }

            private static bool MarkPropertyAsSeen(
                int propertyIndex,
                ref ulong seenProperties,
                scoped Span<ulong> seenPropertyWords)
            {
                ulong propertyMask = 1UL << (propertyIndex % PropertiesPerWord);
                if (seenPropertyWords.IsEmpty)
                {
                    if ((seenProperties & propertyMask) != 0)
                    {
                        return false;
                    }

                    seenProperties |= propertyMask;
                    return true;
                }

                ref ulong seenPropertyWord = ref seenPropertyWords[propertyIndex / PropertiesPerWord];
                if ((seenPropertyWord & propertyMask) != 0)
                {
                    return false;
                }

                seenPropertyWord |= propertyMask;
                return true;
            }

            private int FindProperty(ref Utf8JsonReader reader)
            {
                for (int i = 0; i < _properties.Length; i++)
                {
                    if (reader.ValueTextEquals(_properties[i].NameUtf8))
                    {
                        return i;
                    }
                }

                return _caseInsensitivePropertyMap is not null &&
                    _caseInsensitivePropertyMap.TryLookupUtf8Key(reader.GetUnescapedSpan(), out int index)
                        ? index
                        : -1;
            }
        }

        private sealed class PropertyShape
        {
            public PropertyShape(string name, bool isRequired, bool isCaseSensitive)
            {
                Name = name;
                NameUtf8 = Encoding.UTF8.GetBytes(name);
                IsRequired = isRequired;
                IsCaseSensitive = isCaseSensitive;
            }

            public string Name { get; }
            public byte[] NameUtf8 { get; }
            public bool IsRequired { get; }
            public bool IsCaseSensitive { get; }
        }
    }
}
