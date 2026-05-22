// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace System.Text.Json.Serialization.Metadata
{
    public partial class DefaultJsonTypeInfoResolver
    {
        [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
        internal static void PopulateUnionMetadata(JsonTypeInfo typeInfo)
        {
            Debug.Assert(!typeInfo.IsReadOnly);
            Debug.Assert(typeInfo.Kind is JsonTypeInfoKind.Union);

            Type unionType = typeInfo.Type;

            // Single source of truth for the structural union discovery is the
            // reflection-side System.Reflection.UnionInfoContext API. For older
            // target frameworks STJ links a private polyfill of the same types
            // (see System.Text.Json.csproj).
            UnionInfoContext context = new();
            if (!context.TryCreate(unionType, out UnionInfo? info))
            {
                // Type does not follow the structural union convention. Leave the
                // type info with its empty case list and null delegates so that
                // user-side contract customization can still wire things up.
                return;
            }

            Type builderType = typeof(UnionMetadataBuilder<>).MakeGenericType(unionType);
            var builder = (UnionMetadataBuilder)Activator.CreateInstance(builderType, nonPublic: true)!;
            builder.Build(typeInfo, info);
        }

        private abstract class UnionMetadataBuilder
        {
            [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
            public abstract void Build(JsonTypeInfo typeInfo, UnionInfo info);
        }

        private sealed class UnionMetadataBuilder<TUnion> : UnionMetadataBuilder
        {
            [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
            public override void Build(JsonTypeInfo typeInfo, UnionInfo info)
            {
                JsonTypeInfo<TUnion> typeInfoOfT = (JsonTypeInfo<TUnion>)typeInfo;

                PopulateUnionCases(typeInfoOfT, info);
                if (typeInfoOfT.UnionCases.Count == 0)
                {
                    return;
                }

                PopulateUnionTypeClassifier(typeInfoOfT); // Must happen after union case population.
                PopulateUnionDelegates(typeInfoOfT, info);
            }

            /// <summary>
            /// Projects the discovered <see cref="UnionInfo.Cases"/> onto
            /// <see cref="JsonTypeInfo.UnionCases"/>, skipping compiler-generated
            /// case types (closure displays, anonymous types, etc.) that should
            /// not participate in the JSON contract.
            /// </summary>
            private static void PopulateUnionCases(JsonTypeInfo<TUnion> typeInfo, UnionInfo info)
            {
                Debug.Assert(typeInfo.UnionCases.Count == 0,
                    "PopulateUnionCases is only invoked from the built-in resolver, before any contract customization. " +
                    "UnionCases must therefore not be populated yet.");

                foreach (UnionCaseInfo c in info.Cases)
                {
                    if (c.CaseType.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
                    {
                        continue;
                    }

                    typeInfo.UnionCases.Add(new JsonUnionCaseInfo(c.CaseType) { IsNullable = c.AdmitsNull });
                }
            }

            private static void PopulateUnionTypeClassifier(JsonTypeInfo<TUnion> typeInfo)
            {
                Debug.Assert(typeInfo.TypeClassifier is null,
                    "PopulateTypeClassifier is only invoked from the built-in resolver, before any contract customization. " +
                    "TypeClassifier must therefore not be set yet.");

                JsonUnionAttribute? attr = typeof(TUnion).GetCustomAttribute<JsonUnionAttribute>();
                if (attr?.TypeClassifier is { } attrClassifierType)
                {
                    if (!typeof(JsonTypeClassifierFactory).IsAssignableFrom(attrClassifierType))
                    {
                        ThrowHelper.ThrowInvalidOperationException_TypeClassifierMustDeriveFromJsonTypeClassifierFactory(attrClassifierType, typeof(TUnion));
                    }

                    typeInfo.TypeClassifierFactory = (JsonTypeClassifierFactory)Activator.CreateInstance(attrClassifierType)!;
                }

                // Resolution is deferred to first read of
                // JsonTypeInfo.TypeClassifier — at that point the typeInfo is in the
                // per-options cache, so re-entrant lookups for the union type itself find the
                // partial typeInfo instead of recursing into a fresh resolution.
                typeInfo.TypeClassifierResolutionPending = true;
            }

            /// <summary>
            /// Wires the convention-based <see cref="JsonTypeInfo{T}.UnionDeconstructor"/>
            /// and <see cref="JsonTypeInfo{T}.UnionConstructor"/> delegates to the
            /// reflection-side <see cref="UnionAccessors{TUnion}"/> implementation.
            /// </summary>
            /// <remarks>
            /// <see cref="UnionAccessors{TUnion}"/> already encodes the topologically
            /// sorted dispatch, the <see cref="UnionCaseInfo.TryGetValueMethod"/>
            /// chain, the <c>Value</c> property fallback, and the
            /// nullable-case routing behavior that STJ previously re-implemented
            /// in-place. The wrapper around <see cref="UnionAccessors{TUnion}.Constructor"/>
            /// only exists to adapt the public <c>Func&lt;Type?, object?, TUnion&gt;</c>
            /// signature to the typed <see cref="JsonTypeInfo{T}.UnionConstructor"/>
            /// signature which declares the case type as non-nullable.
            /// </remarks>
            [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
            private static void PopulateUnionDelegates(JsonTypeInfo<TUnion> typeInfo, UnionInfo info)
            {
                Debug.Assert(typeInfo.UnionDeconstructor is null);
                Debug.Assert(typeInfo.UnionConstructor is null);

                UnionAccessors<TUnion> accessors = UnionAccessors<TUnion>.Create(info);

                Func<TUnion, (Type? CaseType, object? Value)> deconstructor = accessors.Deconstructor;
                Func<Type?, object?, TUnion> ctor = accessors.Constructor;
                typeInfo.UnionDeconstructor = (TUnion v) =>
                {
                    var (caseType, value) = deconstructor(v);
                    return (caseType, value);
                };
                typeInfo.UnionConstructor = (Type t, object? v) => ctor(t, v);
            }
        }
    }
}
