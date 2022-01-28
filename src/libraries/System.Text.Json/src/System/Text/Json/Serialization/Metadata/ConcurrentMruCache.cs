// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json.Serialization.Metadata
{
    internal sealed class ConcurrentMruCache<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, (LinkedListNode<TKey>, TValue)> _cache = new();
        private readonly LinkedList<TKey> _orderedKeys = new();

        public int MaxCapacity { get; }
        public int Count => _orderedKeys.Count;

        public ConcurrentMruCache(int maxCapacity)
        {
            if (maxCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapacity));
            }

            MaxCapacity = maxCapacity;
        }

        public TValue GetOrAdd<TState>(TKey key, Func<TKey, TState, TValue> valueFactory, TState state)
        {
            (LinkedListNode<TKey> node, TValue result) = _cache.GetOrAdd(key,
#if NETCOREAPP
                static (TKey key, (Func<TKey, TState, TValue> valueFactory, TState state) pair) => (new(key), pair.valueFactory(key, pair.state)),
                (valueFactory, state));
#else
                key => (new(key), valueFactory(key, state)));
#endif

            LinkedList<TKey> orderedKeys = _orderedKeys;
            bool shouldEvict = false;
            TKey? keyToEvict = default;

            lock (orderedKeys)
            {
                if (node.List is null)
                {
                    if (orderedKeys.Count == MaxCapacity)
                    {
                        // Schedule Most Recently Used node for eviction
                        keyToEvict = orderedKeys.First!.Value;
                        orderedKeys.RemoveFirst();
                        shouldEvict = true;
                    }
                }
                else
                {
                    orderedKeys.Remove(node);
                }

                orderedKeys.AddFirst(node);
            }

            if (shouldEvict)
            {
                Debug.Assert(keyToEvict != null);
                _cache.TryRemove(keyToEvict, out _);
            }

            return result;
        }
    }
}
