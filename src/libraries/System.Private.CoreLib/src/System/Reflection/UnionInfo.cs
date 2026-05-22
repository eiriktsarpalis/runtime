// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace System.Reflection
{
    /// <summary>
    /// Represents the metadata that describes a union type — including its case
    /// types, the convention-based <c>Value</c> property, and any non-boxing
    /// <c>TryGetValue</c> accessors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Union types are classes or structs that follow the structural pattern
    /// described in the C# union types language proposal. A type is considered
    /// a union when it exposes:
    /// </para>
    /// <list type="bullet">
    ///   <item>A public instance property named <c>Value</c> whose type is
    ///         <see cref="object" />, and</item>
    ///   <item>At least one public single-parameter creation member — either a
    ///         constructor on the union type itself, or a static <c>Create</c>
    ///         method declared on a nested <c>IUnionMembers</c> provider
    ///         interface that the union type implements.</item>
    /// </list>
    /// <para>
    /// Instances of this class are typically obtained through
    /// <see cref="UnionInfoContext.Create(Type)" />.
    /// </para>
    /// </remarks>
    /// <seealso cref="UnionInfoContext" />
    /// <seealso cref="UnionCaseInfo" />
    /// <seealso cref="UnionAccessors{TUnion}" />
#if NET11_0_OR_GREATER
    public
#else
    internal
#endif
    sealed class UnionInfo
    {
        private readonly UnionCaseInfo[] _cases;
        private ReadOnlyCollection<UnionCaseInfo>? _casesView;

        internal UnionInfo(
            Type type,
            Type unionDefiningType,
            bool hasUnionAttribute,
            PropertyInfo valueProperty,
            UnionCaseInfo[] cases)
        {
            Type = type;
            UnionDefiningType = unionDefiningType;
            HasUnionAttribute = hasUnionAttribute;
            ValueProperty = valueProperty;
            _cases = cases;
            foreach (UnionCaseInfo c in cases)
            {
                c.SetDeclaringUnion(this);
            }
        }

        /// <summary>Gets the union type that this metadata describes.</summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the type on which the union members are declared. This is normally
        /// equal to <see cref="Type" />, but for unions that use a "union member
        /// provider" (a nested public <c>IUnionMembers</c> interface declaring
        /// static <c>Create</c> factories and the <c>Value</c> property), this
        /// returns that provider interface instead.
        /// </summary>
        public Type UnionDefiningType { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Type" /> carries the
        /// <c>System.Runtime.CompilerServices.UnionAttribute</c>.
        /// </summary>
        /// <remarks>
        /// Union behaviors do not strictly require the attribute — the discovery
        /// is purely structural — but its presence is a strong signal that the
        /// author intends the type to be treated as a union by the compiler and
        /// downstream tools.
        /// </remarks>
        public bool HasUnionAttribute { get; }

        /// <summary>
        /// Gets the public instance property of type <see cref="object" />
        /// named <c>Value</c> declared on <see cref="UnionDefiningType" />,
        /// through which the contents of the union are exposed.
        /// </summary>
        public PropertyInfo ValueProperty { get; }

        /// <summary>
        /// Gets the case types of the union in declaration order, with
        /// duplicates merged into a single entry. The <see cref="UnionCaseInfo.AdmitsNull" />
        /// flag is set to <see langword="true" /> if any merged duplicate allowed
        /// <see langword="null" />.
        /// </summary>
        /// <remarks>
        /// Case types declared as <see cref="Nullable{T}" /> are unwrapped to
        /// their underlying type — for example, a constructor parameter of type
        /// <c>int?</c> yields a case whose <see cref="UnionCaseInfo.CaseType" />
        /// is <c>int</c> and whose <see cref="UnionCaseInfo.AdmitsNull" /> is
        /// <see langword="true" />.
        /// </remarks>
        public IReadOnlyList<UnionCaseInfo> Cases => _casesView ??= new ReadOnlyCollection<UnionCaseInfo>(_cases);

        internal UnionCaseInfo[] CasesArray => _cases;
    }
}
