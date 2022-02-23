// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

namespace System.Text.Json
{
    public sealed partial class JsonSerializerOptions
    {
        /// <summary>
        /// Encapsulates all cached metadata referenced by the current <see cref="JsonSerializerOptions" /> instance.
        /// Context can be shared across multiple equivalent options instances.
        /// </summary>
        private CachingContext? _cachingContext;

        // Simple LRU cache for the public (de)serialize entry points that avoid some lookups in _cachingContext.
        private volatile JsonTypeInfo? _lastTypeInfo;

        internal JsonTypeInfo GetOrAddJsonTypeInfo(Type type)
        {
            if (_cachingContext == null)
            {
                InitializeCachingContext();
                Debug.Assert(_cachingContext != null);
            }

            return _cachingContext.GetOrAddJsonTypeInfo(type);
        }

        internal bool TryGetJsonTypeInfo(Type type, [NotNullWhen(true)] out JsonTypeInfo? typeInfo)
        {
            if (_cachingContext == null)
            {
                typeInfo = null;
                return false;
            }

            return _cachingContext.TryGetJsonTypeInfo(type, out typeInfo);
        }

        internal bool IsJsonTypeInfoCached(Type type) => _cachingContext?.IsJsonTypeInfoCached(type) == true;

        /// <summary>
        /// Return the TypeInfo for root API calls.
        /// This has an LRU cache that is intended only for public API calls that specify the root type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal JsonTypeInfo GetOrAddJsonTypeInfoForRootType(Type type)
        {
            JsonTypeInfo? jsonTypeInfo = _lastTypeInfo;

            if (jsonTypeInfo?.Type != type)
            {
                jsonTypeInfo = GetOrAddJsonTypeInfo(type);
                _lastTypeInfo = jsonTypeInfo;
            }

            return jsonTypeInfo;
        }

        internal void ClearCaches()
        {
            _cachingContext?.Clear();
            _lastTypeInfo = null;
        }

        private void InitializeCachingContext()
        {
            _cachingContext ??= new(this);
        }
        /// <summary>
        /// Stores and manages all reflection caches for one or more <see cref="JsonSerializerOptions"/> instances.
        /// NB the type encapsulates the original options instance and only consults that one when building new types;
        /// this is to prevent multiple options instances from leaking into the object graphs of converters which
        /// could break user invariants.
        /// </summary>
        internal sealed class CachingContext
        {
            private readonly ConcurrentDictionary<Type, JsonConverter> _converterCache = new();
            private readonly ConcurrentDictionary<Type, JsonTypeInfo> _jsonTypeInfoCache = new();

            public CachingContext(JsonSerializerOptions options)
            {
                Options = options;
            }

            public JsonSerializerOptions Options { get; }
            // Property only accessed by reflection in testing -- do not remove.
            // If changing please ensure that src/ILLink.Descriptors.LibraryBuild.xml is up-to-date.
            public int Count => _converterCache.Count + _jsonTypeInfoCache.Count;
            public JsonConverter GetOrAddConverter(Type type) => _converterCache.GetOrAdd(type, Options.GetConverterFromType);
            public JsonTypeInfo GetOrAddJsonTypeInfo(Type type) => _jsonTypeInfoCache.GetOrAdd(type, Options.GetJsonTypeInfoFromContextOrCreate);
            public bool TryGetJsonTypeInfo(Type type, [NotNullWhen(true)] out JsonTypeInfo? typeInfo) => _jsonTypeInfoCache.TryGetValue(type, out typeInfo);
            public bool IsJsonTypeInfoCached(Type type) => _jsonTypeInfoCache.ContainsKey(type);

            public void Clear()
            {
                _converterCache.Clear();
                _jsonTypeInfoCache.Clear();
            }
        }
    }
}
