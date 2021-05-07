// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Contains tagged polymorphic serialization information for a given type.
    /// </summary>
    public class TaggedPolymorphicTypeConfiguration
    {
        private KnownTypesDictionary? _dict;

        /// <summary>
        /// Creates a new tagged polymorphic configuration instance for a given base type.
        /// </summary>
        /// <param name="baseType">The base type for which to configure polymorphic serialization.</param>
        public TaggedPolymorphicTypeConfiguration(Type baseType)
        {
            if (!SupportsTaggedPolymorphism(baseType))
            {
                throw new ArgumentException("The specified base type does not support known types configuration.", nameof(baseType));
            }

            BaseType = baseType;
        }

        /// <summary>The base type for which polymorphic serialization is configured.</summary>
        public Type BaseType { get; }

        /// <summary>The dictionary containing subtypes of the base type that should be serialized polymorphically.</summary>
        public IDictionary<Type, string> KnownTypes => _dict ??= new KnownTypesDictionary(this);

        internal bool IsAssignedToOptionsInstance { get; set; }

        private static bool SupportsTaggedPolymorphism(Type type) => !type.IsGenericTypeDefinition && !type.IsValueType && !type.IsSealed && type != JsonTypeInfo.ObjectType;

        internal static bool TryCreateFromKnownTypeAttributes(Type baseType, [NotNullWhen(true)] out TaggedPolymorphicTypeConfiguration? config)
        {
            if (!SupportsTaggedPolymorphism(baseType))
            {
                config = null;
                return false;
            }

            object[] attributes = baseType.GetCustomAttributes(typeof(JsonKnownTypeAttribute), inherit: false);

            if (attributes.Length == 0)
            {
                config = null;
                return false;
            }

            var cfg = new TaggedPolymorphicTypeConfiguration(baseType);
            foreach (JsonKnownTypeAttribute attribute in attributes)
            {
                cfg.KnownTypes.Add(attribute.Subtype, attribute.Identifier);
            }

            config = cfg;
            return true;
        }

        // A dictionary implementation that incorporates validation
        // for correct subtype relations and value uniqueness
        private sealed class KnownTypesDictionary : IDictionary<Type, string>
        {
            private readonly Dictionary<Type, string> _dict = new();
            private readonly TaggedPolymorphicTypeConfiguration _config;

            public KnownTypesDictionary(TaggedPolymorphicTypeConfiguration config)
            {
                _config = config;
            }

            private void VerifyMutable()
            {
                if (_config.IsAssignedToOptionsInstance)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerOptionsImmutable(null);
                }
            }

            public void Add(Type subtype, string identifier)
            {
                VerifyMutable();

                if (!_config.BaseType.IsAssignableFrom(subtype))
                {
                    throw new ArgumentException("Specified type is not assignable to the base type.", nameof(subtype));
                }

                if (subtype == _config.BaseType)
                {
                    throw new ArgumentException("Specified type must be a proper subtype of the base type.", nameof(subtype));
                }

                if (_dict.ContainsKey(subtype))
                {
                    throw new ArgumentException("Specified type has already been assigned as a known type.", nameof(subtype));
                }

                // linear traversal is probably appropriate here, but might consider using a HashSet storing the id's
                foreach (string id in _dict.Values)
                {
                    if (id == identifier)
                    {
                        throw new ArgumentException("A subtype with specified identifier has already been registered.", nameof(identifier));
                    }
                }

                _dict.Add(subtype, identifier);
            }

            public string this[Type key]
            {
                get => _dict[key];
                set => Add(key, value);
            }

            public void Add(KeyValuePair<Type, string> item) => Add(item.Key, item.Value);

            public ICollection<Type> Keys => _dict.Keys;
            public ICollection<string> Values => _dict.Values;
            public int Count => _dict.Count;
            public bool IsReadOnly => false;
            public bool TryGetValue(Type key, [MaybeNullWhen(false)] out string value) => _dict.TryGetValue(key, out value);
            public bool Contains(KeyValuePair<Type, string> item) => ((IDictionary<Type, string>)_dict).Contains(item);
            public bool ContainsKey(Type key) => _dict.ContainsKey(key);
            public void CopyTo(KeyValuePair<Type, string>[] array, int arrayIndex) => ((IDictionary<Type, string>)_dict).CopyTo(array, arrayIndex);
            public bool Remove(Type key) { VerifyMutable(); return _dict.Remove(key); }
            public bool Remove(KeyValuePair<Type, string> item) { VerifyMutable(); return ((IDictionary<Type, string>)_dict).Remove(item); }
            public void Clear() { VerifyMutable(); _dict.Clear(); }
            public IEnumerator<KeyValuePair<Type, string>> GetEnumerator() => _dict.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _dict.GetEnumerator();
        }
    }
}
