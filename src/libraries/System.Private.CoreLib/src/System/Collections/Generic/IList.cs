// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if MONO
using System.Diagnostics.CodeAnalysis;
#endif

namespace System.Collections.Generic
{
    // An IList is an ordered collection of objects.  The exact ordering
    // is up to the implementation of the list, ranging from a sorted
    // order to insertion order.
    public interface IList<T> : ICollection<T>
    {
        // The Item property provides methods to read and edit entries in the List.
        T this[int index]
        {
#if MONO
            [DynamicDependency(nameof(Array.InternalArray__get_Item) + "``1", typeof(Array))]
#endif
            get;
#if MONO
            [DynamicDependency(nameof(Array.InternalArray__set_Item) + "``1", typeof(Array))]
#endif
            set;
        }

        // Returns the index of a particular item, if it is in the list.
        // Returns -1 if the item isn't in the list.
#if MONO
        [DynamicDependency(nameof(Array.InternalArray__IndexOf) + "``1", typeof(Array))]
#endif
        int IndexOf(T item);

        // Inserts value into the list at position index.
        // index must be non-negative and less than or equal to the
        // number of elements in the list.  If index equals the number
        // of items in the list, then value is appended to the end.
#if MONO
        [DynamicDependency(nameof(Array.InternalArray__Insert) + "``1", typeof(Array))]
#endif
        void Insert(int index, T item);

        // Removes the item at position index.
#if MONO
        [DynamicDependency(nameof(Array.InternalArray__RemoveAt), typeof(Array))]
#endif
        void RemoveAt(int index);

        /// <summary>Inserts the elements of the specified collection at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
        /// <param name="collection">The collection whose elements should be inserted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <see langword="null"/>.</exception>
        void InsertRange(int index, IEnumerable<T> collection)
        {
            ArgumentNullException.ThrowIfNull(collection);

            foreach (T item in collection)
            {
                Insert(index++, item);
            }
        }

        /// <summary>Removes a range of elements from the list.</summary>
        /// <param name="index">The zero-based starting index of the range of elements to remove.</param>
        /// <param name="count">The number of elements to remove.</param>
        void RemoveRange(int index, int count)
        {
            for (int i = 0; i < count; i++)
            {
                RemoveAt(index);
            }
        }

        /// <summary>Reverses the order of the elements in the list.</summary>
        void Reverse()
        {
            for (int i = 0, j = Count - 1; i < j; i++, j--)
            {
                (this[i], this[j]) = (this[j], this[i]);
            }
        }

        /// <summary>Removes all the elements that match the conditions defined by the specified predicate.</summary>
        /// <param name="match">The delegate that defines the conditions of the elements to remove.</param>
        /// <returns>The number of elements removed from the list.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> is <see langword="null"/>.</exception>
        int ICollection<T>.RemoveAll(Predicate<T> match)
        {
            ArgumentNullException.ThrowIfNull(match);

            int removed = 0;
            for (int i = Count - 1; i >= 0; i--)
            {
                if (match(this[i]))
                {
                    RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }
    }
}
