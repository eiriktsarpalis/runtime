// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    public partial class SortedList<TKey, TValue>
    {
        /// <summary>
        /// Gets an instance of a type that may be used to perform operations on the current <see cref="SortedList{TKey, TValue}"/>
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
        /// Gets an instance of a type that may be used to perform operations on the current <see cref="SortedList{TKey, TValue}"/>
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
        /// Provides a type that may be used to perform operations on a <see cref="SortedList{TKey, TValue}"/>
        /// using a <typeparamref name="TAlternateKey"/> as a key instead of a <typeparamref name="TKey"/>.
        /// </summary>
        /// <typeparam name="TAlternateKey">The alternate type of a key for performing lookups.</typeparam>
        public readonly struct AlternateLookup<TAlternateKey>
            where TAlternateKey : allows ref struct
        {
            internal AlternateLookup(SortedList<TKey, TValue> list)
            {
                Debug.Assert(list is not null);
                Debug.Assert(IsCompatibleComparer(list));
                List = list;
            }

            /// <summary>Gets the <see cref="SortedList{TKey, TValue}"/> against which this instance performs operations.</summary>
            public SortedList<TKey, TValue> List { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static bool IsCompatibleComparer(SortedList<TKey, TValue> list)
            {
                Debug.Assert(list is not null);
                return list.comparer is IAlternateComparer<TAlternateKey, TKey>;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static IAlternateComparer<TAlternateKey, TKey> GetAlternateComparer(SortedList<TKey, TValue> list)
            {
                Debug.Assert(IsCompatibleComparer(list));
                return Unsafe.As<IAlternateComparer<TAlternateKey, TKey>>(list.comparer)!;
            }

            /// <summary>Gets or sets the value associated with the specified alternate key.</summary>
            /// <param name="key">The alternate key of the value to get or set.</param>
            /// <exception cref="KeyNotFoundException">The property is retrieved and the alternate key does not exist in the collection.</exception>
            public TValue this[TAlternateKey key]
            {
                get
                {
                    int i = IndexOfKey(key);
                    if (i >= 0)
                    {
                        return List.values[i];
                    }

                    IAlternateComparer<TAlternateKey, TKey> comparer = GetAlternateComparer(List);
                    throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, comparer.Create(key)!.ToString()));
                }
                set
                {
                    SortedList<TKey, TValue> list = List;
                    IAlternateComparer<TAlternateKey, TKey> alternateComparer = GetAlternateComparer(list);

                    int i = BinarySearch(key, alternateComparer, list);
                    if (i >= 0)
                    {
                        list.values[i] = value;
                        list.version++;
                        return;
                    }

                    TKey createdKey = alternateComparer.Create(key);
                    list.Insert(~i, createdKey, value);
                }
            }

            /// <summary>Determines whether the sorted list contains the specified alternate key.</summary>
            /// <param name="key">The alternate key to locate.</param>
            /// <returns>true if the sorted list contains an element with the specified key; otherwise, false.</returns>
            public bool ContainsKey(TAlternateKey key) => IndexOfKey(key) >= 0;

            /// <summary>Searches for the specified alternate key and returns the zero-based index within the sorted list.</summary>
            /// <param name="key">The alternate key to search for.</param>
            /// <returns>The zero-based index of <paramref name="key"/> within the sorted list, if found; otherwise, -1.</returns>
            public int IndexOfKey(TAlternateKey key)
            {
                SortedList<TKey, TValue> list = List;
                IAlternateComparer<TAlternateKey, TKey> alternateComparer = GetAlternateComparer(list);

                int ret = BinarySearch(key, alternateComparer, list);
                return ret >= 0 ? ret : -1;
            }

            /// <summary>Removes the value with the specified alternate key from the sorted list.</summary>
            /// <param name="key">The alternate key of the element to remove.</param>
            /// <returns>true if the element is found and removed; otherwise, false.</returns>
            public bool Remove(TAlternateKey key)
            {
                int i = IndexOfKey(key);
                if (i >= 0)
                {
                    List.RemoveAt(i);
                }

                return i >= 0;
            }

            /// <summary>Removes the value with the specified alternate key from the sorted list, returning the removed value.</summary>
            /// <param name="key">The alternate key of the element to remove.</param>
            /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
            /// <returns>true if the element is found and removed; otherwise, false.</returns>
            public bool Remove(TAlternateKey key, [MaybeNullWhen(false)] out TValue value)
            {
                int i = IndexOfKey(key);
                if (i >= 0)
                {
                    value = List.values[i];
                    List.RemoveAt(i);
                    return true;
                }

                value = default;
                return false;
            }

            /// <summary>Attempts to add the specified key and value to the sorted list.</summary>
            /// <param name="key">The alternate key of the element to add.</param>
            /// <param name="value">The value of the element to add.</param>
            /// <returns>true if the key/value pair was added successfully; false if the key already exists.</returns>
            public bool TryAdd(TAlternateKey key, TValue value)
            {
                SortedList<TKey, TValue> list = List;
                IAlternateComparer<TAlternateKey, TKey> alternateComparer = GetAlternateComparer(list);

                int i = BinarySearch(key, alternateComparer, list);
                if (i >= 0)
                {
                    return false;
                }

                TKey createdKey = alternateComparer.Create(key);
                list.Insert(~i, createdKey, value);
                return true;
            }

            /// <summary>Gets the value associated with the specified alternate key.</summary>
            /// <param name="key">The alternate key of the value to get.</param>
            /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
            /// <returns>true if the sorted list contains an element with the specified key; otherwise, false.</returns>
            public bool TryGetValue(TAlternateKey key, [MaybeNullWhen(false)] out TValue value)
            {
                int i = IndexOfKey(key);
                if (i >= 0)
                {
                    value = List.values[i];
                    return true;
                }

                value = default;
                return false;
            }

            private static int BinarySearch(TAlternateKey key, IAlternateComparer<TAlternateKey, TKey> alternateComparer, SortedList<TKey, TValue> list)
            {
                int lo = 0;
                int hi = list._size - 1;
                while (lo <= hi)
                {
                    int i = lo + ((hi - lo) >> 1);
                    int order = alternateComparer.Compare(key, list.keys[i]);
                    if (order == 0)
                    {
                        return i;
                    }

                    if (order > 0)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        hi = i - 1;
                    }
                }

                return ~lo;
            }
        }
    }
}
