// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// Defines polymorphic configuration for a specified base type.
    /// </summary>
    public class JsonPolymorphismOptions
    {
        private DerivedTypeList? _derivedTypes;
        private bool _ignoreUnrecognizedTypeDiscriminators;
        private JsonUnknownDerivedTypeHandling _unknownDerivedTypeHandling;
        private string? _typeDiscriminatorPropertyName;
        private bool _inferDerivedTypes;
        private JsonNamingPolicy? _typeDiscriminatorNamingPolicy;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        internal Type? _pendingClassifierFactoryType;

        /// <summary>
        /// Creates an empty <see cref="JsonPolymorphismOptions"/> instance.
        /// </summary>
        public JsonPolymorphismOptions()
        {
        }

        /// <summary>
        /// Gets the list of derived types supported in the current polymorphic type configuration.
        /// </summary>
        public IList<JsonDerivedType> DerivedTypes => _derivedTypes ??= new(this);

        /// <summary>
        /// When set to <see langword="true"/>, instructs the serializer to ignore any
        /// unrecognized type discriminator id's and reverts to the contract of the base type.
        /// Otherwise, it will fail the deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="JsonTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        public bool IgnoreUnrecognizedTypeDiscriminators
        {
            get => _ignoreUnrecognizedTypeDiscriminators;
            set
            {
                VerifyMutable();
                _ignoreUnrecognizedTypeDiscriminators = value;
            }
        }

        /// <summary>
        /// Gets or sets the behavior when serializing an undeclared derived runtime type.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="JsonTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        public JsonUnknownDerivedTypeHandling UnknownDerivedTypeHandling
        {
            get => _unknownDerivedTypeHandling;
            set
            {
                VerifyMutable();
                _unknownDerivedTypeHandling = value;
            }
        }

        /// <summary>
        /// Gets or sets a custom type discriminator property name for the polymorhic type.
        /// Uses the default '$type' property name if left unset.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="JsonTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        [AllowNull]
        public string TypeDiscriminatorPropertyName
        {
            get => _typeDiscriminatorPropertyName ?? JsonSerializer.TypePropertyName;
            set
            {
                VerifyMutable();
                _typeDiscriminatorPropertyName = value;
            }
        }

        /// <summary>
        /// When set to <see langword="true"/>, instructs the serializer to automatically discover
        /// derived types and register them with type discriminators derived from the type name.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="JsonTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        public bool InferDerivedTypes
        {
            get => _inferDerivedTypes;
            set
            {
                VerifyMutable();
                _inferDerivedTypes = value;
            }
        }

        /// <summary>
        /// Gets or sets the naming policy used to transform inferred type discriminator names
        /// when <see cref="InferDerivedTypes"/> is <see langword="true"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The parent <see cref="JsonTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        public JsonNamingPolicy? TypeDiscriminatorNamingPolicy
        {
            get => _typeDiscriminatorNamingPolicy;
            set
            {
                VerifyMutable();
                _typeDiscriminatorNamingPolicy = value;
            }
        }

        private void VerifyMutable() => DeclaringTypeInfo?.VerifyMutable();

        internal JsonTypeInfo? DeclaringTypeInfo { get; set; }

        private sealed class DerivedTypeList : ConfigurationList<JsonDerivedType>
        {
            private readonly JsonPolymorphismOptions _parent;

            public DerivedTypeList(JsonPolymorphismOptions parent)
            {
                _parent = parent;
            }

            public override bool IsReadOnly => _parent.DeclaringTypeInfo?.IsReadOnly == true;
            protected override void OnCollectionModifying() => _parent.DeclaringTypeInfo?.VerifyMutable();
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "InferDerivedTypes is an opt-in feature that requires reflection. Callers explicitly opt in.")]
        internal static JsonPolymorphismOptions? CreateFromAttributeDeclarations(Type baseType)
        {
            JsonPolymorphismOptions? options = null;
            JsonPolymorphicAttribute? polymorphicAttribute = baseType.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false);

            if (polymorphicAttribute is not null)
            {
                options = new()
                {
                    IgnoreUnrecognizedTypeDiscriminators = polymorphicAttribute.IgnoreUnrecognizedTypeDiscriminators,
                    UnknownDerivedTypeHandling = polymorphicAttribute.UnknownDerivedTypeHandling,
                    TypeDiscriminatorPropertyName = polymorphicAttribute.TypeDiscriminatorPropertyName,
                    InferDerivedTypes = polymorphicAttribute.InferDerivedTypes,
                    TypeDiscriminatorNamingPolicy = polymorphicAttribute.TypeDiscriminatorNamingPolicy is JsonKnownNamingPolicy.Unspecified
                        ? null
                        : JsonNamingPolicy.GetNamingPolicy(polymorphicAttribute.TypeDiscriminatorNamingPolicy),
                };
            }

            foreach (JsonDerivedTypeAttribute attr in baseType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false))
            {
                (options ??= new()).DerivedTypes.Add(new JsonDerivedType(attr.DerivedType, attr.TypeDiscriminator));
            }

            if (options is { InferDerivedTypes: true })
            {
                InferDerivedTypesFromMetadata(baseType, options);
            }

            // Store the factory type for deferred resolution in PolymorphicTypeResolver,
            // which has access to JsonSerializerOptions needed by the factory.
            if (options is not null && polymorphicAttribute?.TypeClassifier is Type classifierFactoryType)
            {
                if (!typeof(JsonTypeClassifierFactory).IsAssignableFrom(classifierFactoryType))
                {
                    throw new InvalidOperationException(
                        $"The TypeClassifier type '{classifierFactoryType}' specified on [JsonPolymorphic] " +
                        $"for type '{baseType}' must derive from {nameof(JsonTypeClassifierFactory)}.");
                }

                options._pendingClassifierFactoryType = classifierFactoryType;
            }

            return options;
        }

        [RequiresUnreferencedCode("Derived type inference uses reflection to discover subtypes.")]
        private static void InferDerivedTypesFromMetadata(Type baseType, JsonPolymorphismOptions options)
        {
            var existingDerivedTypes = new HashSet<Type>();
            foreach (JsonDerivedType derivedType in options.DerivedTypes)
            {
                existingDerivedTypes.Add(derivedType.DerivedType);
            }

            JsonNamingPolicy? namingPolicy = options.TypeDiscriminatorNamingPolicy;
            var subtypes = new List<Type>();

#if NET11_0_OR_GREATER
            // Fast path: read compiler-emitted [ClosedSubtype] attributes (new in .NET 11).
            foreach (ClosedSubtypeAttribute attr in baseType.GetCustomAttributes<ClosedSubtypeAttribute>(inherit: false))
            {
                subtypes.Add(attr.SubtypeType);
            }
#endif

            // Slow path: scan the assembly for direct subtypes.
            if (subtypes.Count == 0)
            {
                foreach (Type type in baseType.Assembly.GetTypes())
                {
                    if (type.BaseType == baseType && !type.IsAbstract &&
                        type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
                    {
                        subtypes.Add(type);
                    }
                }
            }

            foreach (Type subtype in subtypes)
            {
                if (!existingDerivedTypes.Contains(subtype))
                {
                    string discriminator = namingPolicy?.ConvertName(subtype.Name) ?? subtype.Name;
                    options.DerivedTypes.Add(new JsonDerivedType(subtype, discriminator));
                }
            }
        }
    }
}
