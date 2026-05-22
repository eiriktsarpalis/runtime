// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Reflection
{
    /// <summary>
    /// Describes a single case within a union type — its case type, the union
    /// creation member that produces it, and whether it admits <see langword="null" />.
    /// </summary>
    /// <seealso cref="UnionInfo" />
#if NET11_0_OR_GREATER
    public
#else
    internal
#endif
    sealed class UnionCaseInfo
    {
        private UnionInfo? _declaringUnion;

        internal UnionCaseInfo(Type caseType, bool admitsNull, MemberInfo creationMember, MethodInfo? tryGetValueMethod)
        {
            CaseType = caseType;
            AdmitsNull = admitsNull;
            CreationMember = creationMember;
            TryGetValueMethod = tryGetValueMethod;
        }

        /// <summary>Gets the union type that this case belongs to.</summary>
        public UnionInfo DeclaringUnion => _declaringUnion!;

        /// <summary>
        /// Gets the type of the case value. If the underlying creation member
        /// declared the case as <see cref="Nullable{T}" />, the underlying value
        /// type is returned here and <see cref="AdmitsNull" /> is set to
        /// <see langword="true" />.
        /// </summary>
        public Type CaseType { get; }

        /// <summary>
        /// Gets a value indicating whether this case may hold <see langword="null" />.
        /// </summary>
        /// <remarks>
        /// This is <see langword="true" /> when the creation member's parameter
        /// was declared as <see cref="Nullable{T}" />, or — for reference
        /// types — when nullability annotations on the parameter indicate that
        /// <see langword="null" /> is accepted. When the same case type appears
        /// across multiple creation members, the value here is the logical OR
        /// across all of them so that any nullable-accepting overload makes the
        /// case nullable.
        /// </remarks>
        public bool AdmitsNull { get; }

        /// <summary>
        /// Gets the union creation member that produces this case — a
        /// <see cref="ConstructorInfo" /> when the union exposes its cases via
        /// constructors, or a <see cref="MethodInfo" /> for a static
        /// <c>Create</c> factory declared on a union member provider interface.
        /// </summary>
        public MemberInfo CreationMember { get; }

        /// <summary>
        /// Gets the public instance <c>bool TryGetValue(out T value)</c> method
        /// declared on <see cref="UnionInfo.UnionDefiningType" /> that
        /// strongly-typed pattern matching may use to obtain this case without
        /// boxing, or <see langword="null" /> if no such overload was found.
        /// </summary>
        public MethodInfo? TryGetValueMethod { get; }

        internal void SetDeclaringUnion(UnionInfo unionInfo) => _declaringUnion = unionInfo;
    }
}
