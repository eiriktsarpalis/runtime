// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Collections.Generic
{
    /// <summary>
    /// Implemented by an <see cref="IComparer{T}"/> to support comparing
    /// a <typeparamref name="TAlternate"/> instance with a <typeparamref name="T"/> instance.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate type to compare.</typeparam>
    /// <typeparam name="T">The type to compare.</typeparam>
    public interface IAlternateComparer<in TAlternate, T>
        where TAlternate : allows ref struct
        where T : allows ref struct
    {
        /// <summary>Compares the specified <paramref name="alternate"/> with the specified <paramref name="other"/>.</summary>
        /// <param name="alternate">The instance of type <typeparamref name="TAlternate"/> to compare.</param>
        /// <param name="other">The instance of type <typeparamref name="T"/> to compare.</param>
        /// <returns>
        /// A signed integer that indicates the relative order of <paramref name="alternate"/> and <paramref name="other"/>:
        /// less than zero if <paramref name="alternate"/> precedes <paramref name="other"/>,
        /// zero if they are equal,
        /// greater than zero if <paramref name="alternate"/> follows <paramref name="other"/>.
        /// </returns>
        /// <remarks>
        /// This interface is intended to be implemented on a type that also implements <see cref="IComparer{T}"/>.
        /// The result of this method must be consistent with the result of <see cref="IComparer{T}.Compare"/>
        /// for any <typeparamref name="T"/> for which the comparison would be equivalent.
        /// </remarks>
        int Compare(TAlternate alternate, T other);

        /// <summary>
        /// Creates a <typeparamref name="T"/> that is considered by <see cref="IComparer{T}.Compare"/> to be equal
        /// to the specified <paramref name="alternate"/>.
        /// </summary>
        /// <param name="alternate">The instance of type <typeparamref name="TAlternate"/> for which an equal <typeparamref name="T"/> is required.</param>
        /// <returns>A <typeparamref name="T"/> considered equal to the specified <paramref name="alternate"/>.</returns>
        T Create(TAlternate alternate);
    }
}
