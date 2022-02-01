// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK || NETCOREAPP
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace System.Text.Json.Serialization.Metadata
{
    internal sealed partial class ReflectionEmitCachingMemberAccessor
    {
        private sealed class Cache<TKey> where TKey : notnull
        {
            private int _invocationCount; // tracks total number of invocations to the cache; can be allowed to overflow.
            private readonly int _evictionTriggerInterval; // number of cache invocations needed before triggering an eviction run.
            private readonly long _slidingExpirationTicks; // max timespan allowed for cache entries to remain inactive.
            private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();

            public Cache(TimeSpan slidingExpiration, int evictionTriggerInterval)
            {
                _slidingExpirationTicks = slidingExpiration.Ticks;
                _evictionTriggerInterval = evictionTriggerInterval;
            }

            public TValue GetOrAdd<TValue>(TKey key, Func<TKey, TValue> valueFactory) where TValue : class?
            {
                CacheEntry entry = _cache.GetOrAdd(key,
#if NETCOREAPP
                    static (TKey key, Func<TKey, TValue> valueFactory) => new(valueFactory(key)),
                    valueFactory);
#else
                    key => new(valueFactory(key)));
#endif
                Volatile.Write(ref entry.LastUsedTicks, DateTime.UtcNow.Ticks);

                if (Interlocked.Increment(ref _invocationCount) % _evictionTriggerInterval == 0)
                {
                    EvictStaleCacheEntries();
                }

                return (TValue)entry.Value!;
            }

            private void EvictStaleCacheEntries()
            {
                long utcNowTicks = DateTime.UtcNow.Ticks;

                foreach (KeyValuePair<TKey, CacheEntry> kvp in _cache)
                {
                    if (utcNowTicks - Volatile.Read(ref kvp.Value.LastUsedTicks) >= _slidingExpirationTicks)
                    {
                        _cache.TryRemove(kvp.Key, out _);
                    }
                }
            }

            private class CacheEntry
            {
                public readonly object? Value;
                public long LastUsedTicks;

                public CacheEntry(object? value)
                {
                    Value = value;
                }
            }
        }
    }
}
#endif
