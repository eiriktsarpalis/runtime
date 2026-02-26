// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    public partial class SortedDictionary<TKey, TValue>
    {
        /// <summary>
        /// Gets an instance of a type that may be used to perform operations on the current <see cref="SortedDictionary{TKey, TValue}"/>
        /// using a <typeparamref name="TAlternateKey"/> as a key instead of a <typeparamref name="TKey"/>.
        /// </summary>
        /// <typeparam name="TAlternateKey">The alternate type of a key for performing lookups.</typeparam>
        /// <returns>The created lookup instance.</returns>
        /// <exception cref="InvalidOperationException">This instance's comparer is not compatible with <typeparamref name="TAlternateKey"/>.</exception>
        public AlternateLookup<TAlternateKey> GetAlternateLookup<TAlternateKey>()
            where TAlternateKey : allows ref struct
        {
            if (!AlternateLookup<TAlternateKey>.IsCompatibleComparer(this))
            {
                ThrowHelper.ThrowInvalidOperationException_IncompatibleComparer();
            }

            return new AlternateLookup<TAlternateKey>(this);
        }

        /// <summary>
        /// Gets an instance of a type that may be used to perform operations on the current <see cref="SortedDictionary{TKey, TValue}"/>
        /// using a <typeparamref name="TAlternateKey"/> as a key instead of a <typeparamref name="TKey"/>.
        /// </summary>
        /// <typeparam name="TAlternateKey">The alternate type of a key for performing lookups.</typeparam>
        /// <param name="lookup">The created lookup instance when the method returns true, or a default instance that should not be used if the method returns false.</param>
        /// <returns>true if a lookup could be created; otherwise, false.</returns>
        public bool TryGetAlternateLookup<TAlternateKey>(out AlternateLookup<TAlternateKey> lookup)
            where TAlternateKey : allows ref struct
        {
            if (AlternateLookup<TAlternateKey>.IsCompatibleComparer(this))
            {
                lookup = new AlternateLookup<TAlternateKey>(this);
                return true;
            }

            lookup = default;
            return false;
        }

        /// <summary>
        /// Provides a type that may be used to perform operations on a <see cref="SortedDictionary{TKey, TValue}"/>
        /// using a <typeparamref name="TAlternateKey"/> as a key instead of a <typeparamref name="TKey"/>.
        /// </summary>
        /// <typeparam name="TAlternateKey">The alternate type of a key for performing lookups.</typeparam>
        public readonly struct AlternateLookup<TAlternateKey>
            where TAlternateKey : allows ref struct
        {
            internal AlternateLookup(SortedDictionary<TKey, TValue> dictionary)
            {
                Debug.Assert(dictionary is not null);
                Debug.Assert(IsCompatibleComparer(dictionary));
                Dictionary = dictionary;
            }

            /// <summary>Gets the <see cref="SortedDictionary{TKey, TValue}"/> against which this instance performs operations.</summary>
            public SortedDictionary<TKey, TValue> Dictionary { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static bool IsCompatibleComparer(SortedDictionary<TKey, TValue> dictionary)
            {
                Debug.Assert(dictionary is not null);
                return dictionary.Comparer is IAlternateComparer<TAlternateKey, TKey>;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static IAlternateComparer<TAlternateKey, TKey> GetAlternateComparer(SortedDictionary<TKey, TValue> dictionary)
            {
                Debug.Assert(IsCompatibleComparer(dictionary));
                return Unsafe.As<IAlternateComparer<TAlternateKey, TKey>>(dictionary.Comparer)!;
            }

            /// <summary>Gets or sets the value associated with the specified alternate key.</summary>
            /// <param name="key">The alternate key of the value to get or set.</param>
            /// <exception cref="KeyNotFoundException">The property is retrieved and the alternate key does not exist in the collection.</exception>
            public TValue this[TAlternateKey key]
            {
                get
                {
                    TreeSet<KeyValuePair<TKey, TValue>>.Node? node = FindNode(key);
                    if (node is null)
                    {
                        IAlternateComparer<TAlternateKey, TKey> comparer = GetAlternateComparer(Dictionary);
                        throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, comparer.Create(key)!.ToString()));
                    }

                    return node.Item.Value;
                }
                set
                {
                    SortedDictionary<TKey, TValue> dictionary = Dictionary;
                    TreeSet<KeyValuePair<TKey, TValue>>.Node? node = FindNode(key);
                    if (node is null)
                    {
                        IAlternateComparer<TAlternateKey, TKey> comparer = GetAlternateComparer(dictionary);
                        TKey createdKey = comparer.Create(key);
                        dictionary._set.Add(new KeyValuePair<TKey, TValue>(createdKey, value));
                    }
                    else
                    {
                        node.Item = new KeyValuePair<TKey, TValue>(node.Item.Key, value);
                        dictionary._set.UpdateVersion();
                    }
                }
            }

            /// <summary>Determines whether the dictionary contains the specified alternate key.</summary>
            /// <param name="key">The alternate key to locate.</param>
            /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
            public bool ContainsKey(TAlternateKey key) => FindNode(key) is not null;

            /// <summary>Removes the value with the specified alternate key from the dictionary.</summary>
            /// <param name="key">The alternate key of the element to remove.</param>
            /// <returns>true if the element is found and removed; otherwise, false.</returns>
            public bool Remove(TAlternateKey key)
            {
                TreeSet<KeyValuePair<TKey, TValue>>.Node? node = FindNode(key);
                if (node is not null)
                {
                    return Dictionary._set.Remove(node.Item);
                }

                return false;
            }

            /// <summary>Removes the value with the specified alternate key from the dictionary, returning the removed value.</summary>
            /// <param name="key">The alternate key of the element to remove.</param>
            /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value.</param>
            /// <returns>true if the element is found and removed; otherwise, false.</returns>
            public bool Remove(TAlternateKey key, [MaybeNullWhen(false)] out TValue value)
            {
                TreeSet<KeyValuePair<TKey, TValue>>.Node? node = FindNode(key);
                if (node is not null)
                {
                    value = node.Item.Value;
                    Dictionary._set.Remove(node.Item);
                    return true;
                }

                value = default;
                return false;
            }

            /// <summary>Attempts to add the specified key and value to the dictionary.</summary>
            /// <param name="key">The alternate key of the element to add.</param>
            /// <param name="value">The value of the element to add.</param>
            /// <returns>true if the key/value pair was added successfully; false if the key already exists.</returns>
            public bool TryAdd(TAlternateKey key, TValue value)
            {
                if (FindNode(key) is not null)
                {
                    return false;
                }

                SortedDictionary<TKey, TValue> dictionary = Dictionary;
                IAlternateComparer<TAlternateKey, TKey> comparer = GetAlternateComparer(dictionary);
                TKey createdKey = comparer.Create(key);
                dictionary._set.Add(new KeyValuePair<TKey, TValue>(createdKey, value));
                return true;
            }

            /// <summary>Gets the value associated with the specified alternate key.</summary>
            /// <param name="key">The alternate key of the value to get.</param>
            /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value.</param>
            /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
            public bool TryGetValue(TAlternateKey key, [MaybeNullWhen(false)] out TValue value)
            {
                TreeSet<KeyValuePair<TKey, TValue>>.Node? node = FindNode(key);
                if (node is not null)
                {
                    value = node.Item.Value;
                    return true;
                }

                value = default;
                return false;
            }

            private TreeSet<KeyValuePair<TKey, TValue>>.Node? FindNode(TAlternateKey key)
            {
                SortedDictionary<TKey, TValue> dictionary = Dictionary;
                IAlternateComparer<TAlternateKey, TKey> alternateComparer = GetAlternateComparer(dictionary);

                SortedSet<KeyValuePair<TKey, TValue>>.Node? current = dictionary._set.Root;
                while (current is not null)
                {
                    int order = alternateComparer.Compare(key, current.Item.Key);
                    if (order == 0)
                    {
                        return current;
                    }

                    current = order < 0 ? current.Left : current.Right;
                }

                return null;
            }
        }
    }
}
