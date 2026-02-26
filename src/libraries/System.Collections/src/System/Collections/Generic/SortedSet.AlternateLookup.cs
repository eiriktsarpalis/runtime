// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    public partial class SortedSet<T>
    {
        /// <summary>
        /// Gets an instance of a type that may be used to perform operations on the current <see cref="SortedSet{T}"/>
        /// using a <typeparamref name="TAlternate"/> instead of a <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TAlternate">The alternate type of instance for performing lookups.</typeparam>
        /// <returns>The created lookup instance.</returns>
        /// <exception cref="InvalidOperationException">This instance's comparer is not compatible with <typeparamref name="TAlternate"/>.</exception>
        public AlternateLookup<TAlternate> GetAlternateLookup<TAlternate>()
            where TAlternate : allows ref struct
        {
            if (!AlternateLookup<TAlternate>.IsCompatibleComparer(this))
            {
                ThrowHelper.ThrowInvalidOperationException_IncompatibleComparer();
            }

            return new AlternateLookup<TAlternate>(this);
        }

        /// <summary>
        /// Gets an instance of a type that may be used to perform operations on the current <see cref="SortedSet{T}"/>
        /// using a <typeparamref name="TAlternate"/> instead of a <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TAlternate">The alternate type of instance for performing lookups.</typeparam>
        /// <param name="lookup">The created lookup instance when the method returns true, or a default instance that should not be used if the method returns false.</param>
        /// <returns>true if a lookup could be created; otherwise, false.</returns>
        public bool TryGetAlternateLookup<TAlternate>(out AlternateLookup<TAlternate> lookup)
            where TAlternate : allows ref struct
        {
            if (AlternateLookup<TAlternate>.IsCompatibleComparer(this))
            {
                lookup = new AlternateLookup<TAlternate>(this);
                return true;
            }

            lookup = default;
            return false;
        }

        /// <summary>
        /// Provides a type that may be used to perform operations on a <see cref="SortedSet{T}"/>
        /// using a <typeparamref name="TAlternate"/> as a key instead of a <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TAlternate">The alternate type to use for lookups.</typeparam>
        public readonly struct AlternateLookup<TAlternate>
            where TAlternate : allows ref struct
        {
            internal AlternateLookup(SortedSet<T> set)
            {
                Debug.Assert(set is not null);
                Debug.Assert(IsCompatibleComparer(set));
                Set = set;
            }

            /// <summary>Gets the <see cref="SortedSet{T}"/> against which this instance performs operations.</summary>
            public SortedSet<T> Set { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static bool IsCompatibleComparer(SortedSet<T> set)
            {
                Debug.Assert(set is not null);
                return set.comparer is IAlternateComparer<TAlternate, T>;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static IAlternateComparer<TAlternate, T> GetAlternateComparer(SortedSet<T> set)
            {
                Debug.Assert(IsCompatibleComparer(set));
                return Unsafe.As<IAlternateComparer<TAlternate, T>>(set.comparer)!;
            }

            /// <summary>Adds the specified element to a set.</summary>
            /// <param name="item">The element to add to the set.</param>
            /// <returns>true if the element is added to the set; false if the element is already present.</returns>
            public bool Add(TAlternate item)
            {
                SortedSet<T> set = Set;
                IAlternateComparer<TAlternate, T> alternateComparer = GetAlternateComparer(set);
                T created = alternateComparer.Create(item);
                return set.AddIfNotPresent(created);
            }

            /// <summary>Determines whether the set contains a specific element.</summary>
            /// <param name="item">The element to locate in the set.</param>
            /// <returns>true if the set contains the specified element; otherwise, false.</returns>
            public bool Contains(TAlternate item) => FindNode(item) is not null;

            /// <summary>Removes a specified value from the set.</summary>
            /// <param name="item">The element to remove.</param>
            /// <returns>true if the element is found and removed; otherwise, false.</returns>
            public bool Remove(TAlternate item)
            {
                SortedSet<T> set = Set;
                Node? node = FindNode(item);
                if (node is not null)
                {
                    return set.DoRemove(node.Item);
                }

                return false;
            }

            /// <summary>Searches the set for a given value and returns the equal value it finds, if any.</summary>
            /// <param name="equalValue">The value to search for.</param>
            /// <param name="actualValue">The value from the set that the search found, or the default value of <typeparamref name="T"/> when the search yielded no match.</param>
            /// <returns>A value indicating whether the search was successful.</returns>
            public bool TryGetValue(TAlternate equalValue, [MaybeNullWhen(false)] out T actualValue)
            {
                Node? node = FindNode(equalValue);
                if (node is not null)
                {
                    actualValue = node.Item;
                    return true;
                }

                actualValue = default;
                return false;
            }

            private Node? FindNode(TAlternate item)
            {
                SortedSet<T> set = Set;
                IAlternateComparer<TAlternate, T> alternateComparer = GetAlternateComparer(set);

                Node? current = set.root;
                while (current is not null)
                {
                    int order = alternateComparer.Compare(item, current.Item);
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
