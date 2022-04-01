﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// Type used to hold tagged polymorphic serialization metadata for a given base type.
    /// </summary>
    internal sealed class PolymorphicTypeResolver
    {
        public Type BaseType { get; }
        public string? TypeDiscriminatorPropertyName { get; }

        // TypeId -> Type map; is not modified by the object
        private readonly Dictionary<string, Type>? _typeIdToType;
        // Type -> (KnownType, TypeId)? map; used as a cache for the subtype hierarchy so can be modified during the object's lifetime.
        // `null` values denote a subtype that is not associated with any known type (we want to cache negative results as well).
        private readonly ConcurrentDictionary<Type, CachedTypeResolution?> _typeToTypeId = new();

        private sealed class CachedTypeResolution
        {
            public CachedTypeResolution(Type type, string? typeIdentifier, CachedTypeResolution? conflictingResolution)
            {
                Type = type;
                TypeIdentifier = typeIdentifier;
                ConflictingResolution = conflictingResolution;
            }

            public Type Type { get; }
            public string? TypeIdentifier { get; }
            public CachedTypeResolution? ConflictingResolution { get; }
        }

        public PolymorphicTypeResolver(PolymorphicTypeConfiguration configuration)
        {
            Debug.Assert(!configuration.BaseType.IsValueType && !configuration.BaseType.IsSealed);

            BaseType = configuration.BaseType;
            TypeDiscriminatorPropertyName = configuration.TypeDiscriminatorPropertyName;

            if (TypeDiscriminatorPropertyName is not null)
            {
                _typeIdToType = new();
                foreach (var kvp in configuration.KnownTypes)
                {
                    Type subtype = kvp.Key;
                    string? typeDiscriminatorId = kvp.Value;
                    Debug.Assert(typeDiscriminatorId is not null);

                    _typeToTypeId[subtype] = new CachedTypeResolution(subtype, typeDiscriminatorId, conflictingResolution: null);
                    _typeIdToType.Add(typeDiscriminatorId, subtype);
                }
            }
        }

        public static PolymorphicTypeResolver? CreateFromAttributes(Type baseType)
        {
            if (!PolymorphicTypeConfiguration.TryCreateFromAttributes(baseType, out PolymorphicTypeConfiguration? config))
            {
                return null;
            }

            return new PolymorphicTypeResolver(config);
        }

        /// <summary>
        /// Used during polymorphic deserialization to recover the subtype corresponding to the supplied type id.
        /// </summary>
        public bool TryResolveTypeByTypeId(string typeId, [NotNullWhen(true)] out Type? result)
        {
            Debug.Assert(_typeIdToType is not null);
            return _typeIdToType.TryGetValue(typeId, out result);
        }

        /// <summary>
        /// Used during polymorphic serialization to recover the type identifier as well as the actual subtype to be used.
        /// The converter type we end up using could be different to the runtime type, for instance given the types
        ///   Baz : Bar : Foo
        /// with `Bar` being the only declared known type, then an instance of type `Baz` should be serialized using the `Bar` converter.
        /// </summary>
        public bool TryResolvePolymorphicSubtype(Type type, [NotNullWhen(true)] out Type? resolvedType, out string? typeIdentifier)
        {
            Debug.Assert(!type.IsInterface && !type.IsAbstract, "input should always be the reflected type of a serialized object.");
            Debug.Assert(BaseType.IsAssignableFrom(type));

            // check the cache for existing resolutions first
            if (!_typeToTypeId.TryGetValue(type, out var result))
            {
                result = ComputeResolution(type);
            }

            // there is no type discriminator resolution
            if (result is null)
            {
                resolvedType = null;
                typeIdentifier = null;
                return false;
            }

            // there is a diamond in the type discriminator resolution
            if (result.ConflictingResolution is not null)
            {
                throw new NotSupportedException($"Type implements conflicting type discriminators {result.TypeIdentifier} and {result.ConflictingResolution.TypeIdentifier}.");
            }

            resolvedType = result.Type;
            typeIdentifier = result.TypeIdentifier;
            return true;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
            Justification = "The 'type' must exist and so trimmer kept it. In which case " +
                "It also kept it on any type which implements it. The below call to GetInterfaces " +
                "may return fewer results when trimmed but it will return the 'interfaceType' " +
                "if the type implemented it, even after trimming.")]
        private CachedTypeResolution? ComputeResolution(Type type)
        {
            // walk up the inheritance hierarchy until the
            // nearest ancestor with a known type association is dicovered.

            CachedTypeResolution? result = null;

            for (Type? candidate = type.BaseType; BaseType.IsAssignableFrom(candidate); candidate = candidate.BaseType)
            {
                Debug.Assert(candidate != null);

                if (_typeToTypeId.TryGetValue(candidate, out result))
                {
                    break;
                }
            }

            // Interface hierarchies admit the possibility of diamond ambiguities in type discriminators
            // iterate through all interface implementations and identify potential conflicts.
            if (result?.ConflictingResolution is null && BaseType.IsInterface)
            {
                foreach (Type interfaceTy in type.GetInterfaces())
                {
                    if (BaseType.IsAssignableFrom(interfaceTy) &&
                        _typeToTypeId.TryGetValue(interfaceTy, out var interfaceResult) &&
                        interfaceResult is not null)
                    {
                        // interface inheritance hierarchies cannot contain diamonds
                        Debug.Assert(interfaceResult.ConflictingResolution is null);

                        if (result is null)
                        {
                            result = interfaceResult;
                        }
                        else if (result.Type != interfaceResult.Type)
                        {
                            result = new CachedTypeResolution(result.Type, result.TypeIdentifier, interfaceResult);
                        }
                    }
                }
            }

            // cache the result for future use
            _typeToTypeId[type] = result;
            return result;
        }
    }
}
