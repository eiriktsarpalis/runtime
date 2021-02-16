// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Linq
{
    public static partial class Enumerable
    {
        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            if (source == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            if (!TryGetElementAt(source, index, out TSource? element))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            return element;
        }

        /// <summary>Returns the element at a specified index in a sequence.</summary>
        /// <param name="source">An <see cref="IEnumerable{T}" /> to return an element from.</param>
        /// <param name="index">The index of the element to retrieve, which is either from the start or the end.</param>
        /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="source" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index" /> is outside the bounds of the <paramref name="source" /> sequence.
        /// </exception>
        /// <returns>The element at the specified position in the <paramref name="source" /> sequence.</returns>
        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, Index index)
        {
            if (source == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            bool success = false;
            TSource? element;

            if (index.IsFromEnd)
            {
                success = TryGetElementAtFromEnd(source, index.Value, out element);
            }
            else
            {
                success = TryGetElementAt(source, index.Value, out element);
            }

            if (!success)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            return element!;
        }

        public static TSource? ElementAtOrDefault<TSource>(this IEnumerable<TSource> source, int index)
        {
            if (source == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            TryGetElementAt(source, index, out TSource? element);
            return element;
        }

        /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
        /// <param name="source">An <see cref="IEnumerable{T}" /> to return an element from.</param>
        /// <param name="index">The index of the element to retrieve, which is either from the start or the end.</param>
        /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="source" /> is <see langword="null" />.
        /// </exception>
        /// <returns>
        ///   <see langword="default" /> if <paramref name="index" /> is outside the bounds of the <paramref name="source" /> sequence; otherwise, the element at the specified position in the <paramref name="source" /> sequence.
        /// </returns>
        public static TSource? ElementAtOrDefault<TSource>(this IEnumerable<TSource> source, Index index)
        {
            if (source == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            TSource? element;
            if (index.IsFromEnd)
            {
                TryGetElementAtFromEnd(source, index.Value, out element);
            }
            else
            {
                TryGetElementAt(source, index.Value, out element);
            }

            return element;
        }

        private static bool TryGetElementAt<TSource>(IEnumerable<TSource> source, int index, [MaybeNullWhen(false)] out TSource element)
        {
            Debug.Assert(source != null);

            if (source is IList<TSource> list)
            {
                if (0 <= index && index < list.Count)
                {
                    element = list[index];
                    return true;
                }

                element = default;
                return false;
            }

            if (source is IPartition<TSource> partition)
            {
                element = partition.TryGetElementAt(index, out bool found);
                return found;
            }

            if (index >= 0)
            {
                using IEnumerator<TSource> e = source.GetEnumerator();
                while (e.MoveNext())
                {
                    if (index == 0)
                    {
                        element = e.Current;
                        return true;
                    }

                    index--;
                }
            }

            element = default;
            return false;
        }

        private static bool TryGetElementAtFromEnd<TSource>(IEnumerable<TSource> source, int index, [MaybeNullWhen(false)] out TSource element)
        {
            Debug.Assert(source != null);

            if (source.TryGetNonEnumeratedCount(out int count))
            {
                return TryGetElementAt(source, count - index, out element);
            }

            using IEnumerator<TSource> e = source.GetEnumerator();
            if (e.MoveNext())
            {
                Queue<TSource> queue = new();
                queue.Enqueue(e.Current);
                while (e.MoveNext())
                {
                    if (queue.Count == index)
                    {
                        queue.Dequeue();
                    }

                    queue.Enqueue(e.Current);
                }

                if (queue.Count == index)
                {
                    element = queue.Dequeue();
                    return true;
                }
            }

            element = default;
            return false;
        }
    }
}
