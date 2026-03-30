// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if MONO
using System.Diagnostics.CodeAnalysis;
#endif

namespace System.Collections.Generic
{
    // Base interface for all collections, defining enumerators, size, and
    // synchronization methods.
    public interface ICollection<T> : IEnumerable<T>
    {
        int Count
        {
#if MONO
            [DynamicDependency(nameof(Array.InternalArray__ICollection_get_Count), typeof(Array))]
#endif
            get;
        }

        bool IsReadOnly
        {
#if MONO
            [DynamicDependency(nameof(Array.InternalArray__ICollection_get_IsReadOnly), typeof(Array))]
#endif
            get;
        }

#if MONO
        [DynamicDependency(nameof(Array.InternalArray__ICollection_Add) + "``1", typeof(Array))]
#endif
        void Add(T item);

#if MONO
        [DynamicDependency(nameof(Array.InternalArray__ICollection_Clear), typeof(Array))]
#endif
        void Clear();

#if MONO
        [DynamicDependency(nameof(Array.InternalArray__ICollection_Contains) + "``1", typeof(Array))]
#endif
        bool Contains(T item);

        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
#if MONO
        [DynamicDependency(nameof(Array.InternalArray__ICollection_CopyTo) + "``1", typeof(Array))]
#endif
        void CopyTo(T[] array, int arrayIndex);

#if MONO
        [DynamicDependency(nameof(Array.InternalArray__ICollection_Remove) + "``1", typeof(Array))]
#endif
        bool Remove(T item);

        /// <summary>Adds the elements of the specified collection to this collection.</summary>
        /// <param name="collection">The collection whose elements should be added.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <see langword="null"/>.</exception>
        void AddRange(IEnumerable<T> collection)
        {
            ArgumentNullException.ThrowIfNull(collection);

            foreach (T item in collection)
            {
                Add(item);
            }
        }

        /// <summary>Removes all the elements that match the conditions defined by the specified predicate.</summary>
        /// <param name="match">The delegate that defines the conditions of the elements to remove.</param>
        /// <returns>The number of elements removed from the collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> is <see langword="null"/>.</exception>
        int RemoveAll(Predicate<T> match)
        {
            ArgumentNullException.ThrowIfNull(match);

            List<T>? toRemove = null;
            foreach (T item in this)
            {
                if (match(item))
                {
                    (toRemove ??= new()).Add(item);
                }
            }

            if (toRemove is null)
            {
                return 0;
            }

            int removed = 0;
            foreach (T item in toRemove)
            {
                if (Remove(item))
                {
                    removed++;
                }
            }

            return removed;
        }
    }
}
