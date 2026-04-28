// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Specifies a direct subtype of a closed class. The compiler emits this attribute on the base
    /// class once for each direct subtype, enabling consumers to discover the complete set of
    /// derived types without scanning the assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ClosedSubtypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ClosedSubtypeAttribute"/> with the specified subtype.
        /// </summary>
        /// <param name="subtypeType">The direct subtype of the closed class.</param>
        public ClosedSubtypeAttribute(Type subtypeType)
        {
            SubtypeType = subtypeType;
        }

        /// <summary>
        /// Gets the direct subtype of the closed class.
        /// </summary>
        public Type SubtypeType { get; }
    }
}
