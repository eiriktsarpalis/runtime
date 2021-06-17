// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Defines polymorphic type configuration for a given type.
    /// </summary>
    public class PolymorphicTypeConfiguration
    {
        private Dictionary<Type, string?>? _knownTypes;
        internal int Count => _knownTypes?.Count ?? 0;

        /// <summary>
        /// Creates a new polymorphic configuration instance for a given base type.
        /// </summary>
        /// <param name="baseType">The base type for which to configure polymorphic serialization.</param>
        /// <param name="typeDiscriminatorPropertyName">
        /// If <see langword="null" />, indicates that polymorphic values should not use type discriminators.
        /// Otherwise, should write and read type discriminator identifiers using the specified property name.
        /// </param>
        public PolymorphicTypeConfiguration(Type baseType, string? typeDiscriminatorPropertyName = null)
        {
            if (!SupportsPolymorphism(baseType))
            {
                throw new ArgumentException("The specified base type does not support known types configuration.", nameof(baseType));
            }

            BaseType = baseType;
            TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName;
        }

        /// <summary>
        /// Gets the base type for which polymorphic serialization is being configured.
        /// </summary>
        public Type BaseType { get; }

        /// <summary>
        /// Gets the type discriminator used for polymorphic serialization, if not <see langword="null"/>.
        /// </summary>
        public string? TypeDiscriminatorPropertyName { get; }

        /// <summary>
        /// Gets an enumeration of the registered known subtypes.
        /// </summary>
        public IEnumerable<KeyValuePair<Type, string?>> KnownTypes => _knownTypes ?? (IEnumerable<KeyValuePair<Type, string?>>)Array.Empty<KeyValuePair<Type, string?>>(); // TODO do not expose internal dictionary

        /// <summary>
        /// Enables polymorphic serialization for the specified subtype.
        /// </summary>
        /// <param name="subtype">The derived type for which to enable polymorphism.</param>
        /// <param name="typeDiscriminatorId">The type discriminator id to use for the specified derived type.</param>
        /// <returns>The same <see cref="PolymorphicTypeConfiguration"/> instance after it has been updated.</returns>
        public PolymorphicTypeConfiguration WithKnownType(Type subtype, string? typeDiscriminatorId = null)
        {
            VerifyMutable();

            if (TypeDiscriminatorPropertyName is null != typeDiscriminatorId is null)
            {
                string message = TypeDiscriminatorPropertyName is null
                    ? "PolymorphicTypeConfiguration instance does not support type discriminators"
                    : "PolymorphicTypeConfiguration instance requires type discriminators";

                if (TypeDiscriminatorPropertyName is null)
                {
                    throw new ArgumentException(message, nameof(typeDiscriminatorId));
                }
            }

            if (!BaseType.IsAssignableFrom(subtype))
            {
                throw new ArgumentException("Specified type is not assignable to the base type.", nameof(subtype));
            }

            // TODO: this check might be removed depending on final type discriminator semantics
            if (subtype == BaseType)
            {
                throw new ArgumentException("Specified type must be a proper subtype of the base type.", nameof(subtype));
            }

            _knownTypes ??= new();

            if (_knownTypes.ContainsKey(subtype))
            {
                throw new ArgumentException("Specified type has already been assigned as a known type.", nameof(subtype));
            }

            // linear traversal is probably appropriate here, but might consider using a bidirectional map
            foreach (string? otherId in _knownTypes.Values)
            {
                if (otherId == typeDiscriminatorId)
                {
                    throw new ArgumentException("A subtype with specified identifier has already been registered.", nameof(typeDiscriminatorId));
                }
            }

            _knownTypes.Add(subtype, typeDiscriminatorId);

            return this;
        }

        internal static bool TryCreateFromAttributes(Type baseType, [NotNullWhen(true)] out PolymorphicTypeConfiguration? config)
        {
            if (!SupportsPolymorphism(baseType))
            {
                // TODO: should we fail silently if an unsupported type contains attributes?
                config = null;
                return false;
            }

            JsonPolymorphicTypeAttribute? polymorphicTypeAttr = baseType.GetCustomAttribute<JsonPolymorphicTypeAttribute>();
            if (polymorphicTypeAttr is null)
            {
                config = null;
                return false;
            }

            var cfg = new PolymorphicTypeConfiguration(baseType, polymorphicTypeAttr.TypeDiscriminatorPropertyName);
            foreach (JsonKnownTypeAttribute attribute in baseType.GetCustomAttributes<JsonKnownTypeAttribute>(inherit: false))
            {
                // this can throw an exception
                cfg.WithKnownType(attribute.Subtype, attribute.TypeDiscriminatorId);
            }

            if (cfg.Count == 0)
            {
                // TODO: silent failure?
                config = null;
                return false;
            }

            config = cfg;
            return true;
        }

        internal bool IsAssignedToOptionsInstance { get; set; }

        private void VerifyMutable()
        {
            if (IsAssignedToOptionsInstance)
            {
                ThrowHelper.ThrowInvalidOperationException_SerializerOptionsImmutable(null);
            }
        }

        private static bool SupportsPolymorphism(Type type) =>
            (type.IsClass || type.IsInterface) &&
            !type.IsGenericTypeDefinition && !type.IsValueType && !type.IsSealed &&
            type != JsonTypeInfo.ObjectType; // System.Object is special, polymorphism settings cannot be overriden.
    }

    /// <summary>
    /// Defines polymorphic type configuration for a given type.
    /// </summary>
    /// <typeparam name="TBaseType">The type for which polymorphic configuration is provided.</typeparam>
    public class PolymorphicTypeConfiguration<TBaseType> : PolymorphicTypeConfiguration where TBaseType : class
    {
        /// <summary>
        /// Creates a new polymorphic configuration instance for a given base type.
        /// </summary>
        /// <param name="typeDiscriminatorPropertyName">
        /// If <see langword="null" />, indicates that polymorphic values should not use type discriminators.
        /// Otherwise, should write and read type discriminator identifiers using the specified property name.
        /// </param>
        public PolymorphicTypeConfiguration(string? typeDiscriminatorPropertyName = null) : base(typeof(TBaseType), typeDiscriminatorPropertyName)
        {
        }

        /// <summary>
        /// Associates specified derived type with supplied string identifier.
        /// </summary>
        /// <typeparam name="TDerivedType">The derived type with which to associate a type identifier.</typeparam>
        /// <param name="identifier">The type identifier to use for the specified derived type.</param>
        /// <returns>The same <see cref="PolymorphicTypeConfiguration"/> instance after it has been updated.</returns>
        public PolymorphicTypeConfiguration<TBaseType> WithKnownType<TDerivedType>(string? identifier = null) where TDerivedType : TBaseType
        {
            WithKnownType(typeof(TDerivedType), identifier);
            return this;
        }
    }
}
