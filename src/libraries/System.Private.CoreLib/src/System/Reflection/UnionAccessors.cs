// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace System.Reflection
{
    /// <summary>
    /// Precomputed accessors for constructing and deconstructing instances of a
    /// union type. Created from a discovered <see cref="UnionInfo" /> via
    /// <see cref="Create(UnionInfo)" />.
    /// </summary>
    /// <typeparam name="TUnion">The union type these accessors operate on.</typeparam>
    /// <remarks>
    /// <para>
    /// <see cref="UnionAccessors{TUnion}" /> is intended for serializers and
    /// metadata-driven frameworks that need to repeatedly create and inspect
    /// union values without paying the cost of reflection on each operation.
    /// The delegates are eagerly bound once at construction time.
    /// </para>
    /// <para>
    /// Creation requires dynamic code and unreferenced-code annotations because
    /// the underlying delegates close over the union's creation members. Use
    /// <see cref="UnionInfo" /> alone if you only need metadata.
    /// </para>
    /// </remarks>
#if NET11_0_OR_GREATER
    public
#else
    internal
#endif
    sealed class UnionAccessors<TUnion>
    {
        private readonly UnionCaseInfo[] _topologicallySortedCases;
        private readonly ConcurrentDictionary<Type, UnionCaseInfo?> _caseIndex;
        private readonly UnionCaseInfo? _nullCase;

        internal UnionAccessors(
            UnionInfo info,
            Func<TUnion, (Type? CaseType, object? Value)> deconstructor,
            Func<Type?, object?, TUnion> constructor,
            UnionCaseInfo[] topologicallySortedCases,
            ConcurrentDictionary<Type, UnionCaseInfo?> caseIndex,
            UnionCaseInfo? nullCase)
        {
            Info = info;
            Deconstructor = deconstructor;
            Constructor = constructor;
            _topologicallySortedCases = topologicallySortedCases;
            _caseIndex = caseIndex;
            _nullCase = nullCase;
        }

        /// <summary>Gets the <see cref="UnionInfo" /> these accessors were built from.</summary>
        public UnionInfo Info { get; }

        /// <summary>
        /// Gets a delegate that returns the case type and case value held by a
        /// union instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The returned tuple's <c>CaseType</c> identifies which declared case
        /// the instance currently holds; it is <see langword="null" /> when the
        /// union itself is <see langword="null" /> (reference-type unions only).
        /// The <c>Value</c> is <see langword="null" /> when the union holds a
        /// nullable case populated with <see langword="null" />, in which case
        /// <c>CaseType</c> is the case type of the matched nullable case.
        /// </para>
        /// <para>
        /// When the runtime value's type does not exactly match a declared case
        /// but is assignable to one, the nearest declared ancestor case is
        /// returned. The deconstructor throws <see cref="InvalidOperationException" />
        /// if no declared case matches.
        /// </para>
        /// <para>
        /// When the union exposes more than one nullable case and the underlying
        /// <c>Value</c> property returns <see langword="null" />, the deconstructor
        /// returns the first declared nullable case. Callers that need to
        /// preserve the originally constructed nullable case across a round-trip
        /// should ensure the union exposes at most one nullable case (this is
        /// also the shape produced by the C# <c>union</c> keyword).
        /// </para>
        /// <para>
        /// The delegate is safe for concurrent use across threads.
        /// </para>
        /// </remarks>
        public Func<TUnion, (Type? CaseType, object? Value)> Deconstructor { get; }

        /// <summary>
        /// Gets a delegate that produces a union instance from a case type and
        /// a case value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the supplied value is <see langword="null" />, the constructor
        /// invokes the first nullable case's creation member with
        /// <see langword="null" />. The supplied case type may be
        /// <see langword="null" /> in that scenario.
        /// </para>
        /// <para>
        /// When the case type is not a declared case but is assignable to one,
        /// the nearest declared ancestor case's creation member is invoked.
        /// </para>
        /// <para>
        /// The constructor throws <see cref="InvalidOperationException" /> if a
        /// <see langword="null" /> value is supplied but no nullable case
        /// exists, or if no declared case can be matched to the supplied case
        /// type.
        /// </para>
        /// </remarks>
        public Func<Type?, object?, TUnion> Constructor { get; }

        /// <summary>
        /// Looks up the <see cref="UnionCaseInfo" /> that <see cref="Deconstructor" />
        /// and <see cref="Constructor" /> would route the supplied
        /// <paramref name="runtimeType" /> to. Caches lookups for derived
        /// non-declared types on subsequent calls.
        /// </summary>
        /// <param name="runtimeType">The runtime type to classify.</param>
        public UnionCaseInfo? ResolveCase(Type runtimeType)
        {
            ArgumentNullException.ThrowIfNull(runtimeType);
            return ResolveCaseCore(runtimeType);
        }

        internal UnionCaseInfo? ResolveCaseCore(Type runtimeType)
        {
            if (_caseIndex.TryGetValue(runtimeType, out UnionCaseInfo? cached))
            {
                return cached;
            }

            UnionCaseInfo? found = null;
            foreach (UnionCaseInfo entry in _topologicallySortedCases)
            {
                if (entry.CaseType.IsAssignableFrom(runtimeType))
                {
                    found = entry;
                    break;
                }
            }

            _caseIndex[runtimeType] = found;
            return found;
        }

        internal UnionCaseInfo? NullCase => _nullCase;

        /// <summary>
        /// Builds compiled accessors for a union type. Reflects over the
        /// creation members of <paramref name="info" />.
        /// </summary>
        /// <param name="info">The union metadata describing <typeparamref name="TUnion" />.</param>
        /// <exception cref="ArgumentNullException"><paramref name="info" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException"><paramref name="info" /> describes a type other than <typeparamref name="TUnion" />.</exception>
        [RequiresDynamicCode("UnionAccessors{TUnion}.Create may emit code to invoke union creation members.")]
        [RequiresUnreferencedCode("UnionAccessors{TUnion}.Create reflects over the union type's case constructors and TryGetValue overloads.")]
        public static UnionAccessors<TUnion> Create(UnionInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);

            if (info.Type != typeof(TUnion))
            {
                throw new ArgumentException(SR.Format(SR.Arg_UnionAccessorTypeMismatch, info.Type, typeof(TUnion)), nameof(info));
            }

            UnionCaseInfo[] topo = SortTopologically(info.CasesArray);
            ConcurrentDictionary<Type, UnionCaseInfo?> caseIndex = new();
            foreach (UnionCaseInfo c in topo)
            {
                caseIndex[c.CaseType] = c;
            }

            UnionCaseInfo? nullCase = null;
            foreach (UnionCaseInfo c in info.CasesArray)
            {
                if (c.AdmitsNull)
                {
                    nullCase = c;
                    break;
                }
            }

            PropertyInfo valueProperty = info.ValueProperty;

            UnionAccessors<TUnion>[] selfBox = new UnionAccessors<TUnion>[1];

            Func<TUnion, (Type? CaseType, object? Value)> deconstructor = BuildDeconstructor(valueProperty, topo, info, selfBox);
            Func<Type?, object?, TUnion> constructor = BuildConstructor(info.CasesArray, selfBox);

            UnionAccessors<TUnion> accessors = new(info, deconstructor, constructor, topo, caseIndex, nullCase);
            selfBox[0] = accessors;
            return accessors;
        }

        private static Func<TUnion, (Type? CaseType, object? Value)> BuildDeconstructor(
            PropertyInfo valueProperty,
            UnionCaseInfo[] topologicallySortedCases,
            UnionInfo info,
            UnionAccessors<TUnion>[] selfBox)
        {
            // Compile a small chain that mirrors the C# compiler's pattern-match
            // lowering: try every TryGetValue overload (most-derived first), and
            // fall back to the public Value property.
            List<(Type CaseType, MethodInfo Method)> tryGetValueChain = new();
            foreach (UnionCaseInfo c in topologicallySortedCases)
            {
                if (c.TryGetValueMethod is not null)
                {
                    tryGetValueChain.Add((c.CaseType, c.TryGetValueMethod));
                }
            }

            Type unionType = typeof(TUnion);
            bool unionIsReferenceType = !unionType.IsValueType;

            return union =>
            {
                if (unionIsReferenceType && (object?)union is null)
                {
                    return ((Type?)null, (object?)null);
                }

                foreach ((Type caseType, MethodInfo method) in tryGetValueChain)
                {
                    object?[] args = new object?[] { null };
                    bool matched = (bool)method.Invoke(union, args)!;
                    if (matched)
                    {
                        return ((Type?)caseType, (object?)args[0]);
                    }
                }

                object? raw = valueProperty.GetValue(union);
                if (raw is null)
                {
                    UnionCaseInfo? nullCase = selfBox[0]!.NullCase;
                    if (nullCase is null)
                    {
                        throw new InvalidOperationException(SR.Format(SR.InvalidOperation_UnionDoesNotAcceptNull, info.Type));
                    }

                    return ((Type?)nullCase.CaseType, (object?)null);
                }

                UnionCaseInfo? resolved = selfBox[0]!.ResolveCaseCore(raw.GetType());
                if (resolved is null)
                {
                    throw new InvalidOperationException(SR.Format(SR.InvalidOperation_UnionRuntimeTypeNotMatchedToCase, info.Type, raw.GetType()));
                }

                return ((Type?)resolved.CaseType, (object?)raw);
            };
        }

        private static Func<Type?, object?, TUnion> BuildConstructor(
            UnionCaseInfo[] cases,
            UnionAccessors<TUnion>[] selfBox)
        {
            // Pre-bind one invoker per declared case. The boxing/unboxing here
            // is the same cost as the source-generator-emitted path through a
            // strongly-typed lambda, only spread across more delegates.
            Dictionary<MemberInfo, Func<object?, TUnion>> invokerByMember = new();
            foreach (UnionCaseInfo c in cases)
            {
                if (!invokerByMember.ContainsKey(c.CreationMember))
                {
                    invokerByMember.Add(c.CreationMember, CreateInvoker(c.CreationMember));
                }
            }

            return (caseType, value) =>
            {
                UnionAccessors<TUnion> self = selfBox[0]!;
                UnionInfo info = self.Info;

                if (value is null)
                {
                    UnionCaseInfo? nullCase = self.NullCase;
                    if (nullCase is null)
                    {
                        throw new InvalidOperationException(SR.Format(SR.InvalidOperation_UnionDoesNotAcceptNull, info.Type));
                    }

                    return invokerByMember[nullCase.CreationMember](null);
                }

                caseType ??= value.GetType();

                UnionCaseInfo? resolved = self.ResolveCaseCore(caseType);
                if (resolved is null)
                {
                    throw new InvalidOperationException(SR.Format(SR.InvalidOperation_UnionRuntimeTypeNotMatchedToCase, info.Type, caseType));
                }

                return invokerByMember[resolved.CreationMember](value);
            };
        }

        private static Func<object?, TUnion> CreateInvoker(MemberInfo creationMember)
        {
            switch (creationMember)
            {
                case ConstructorInfo ctor:
                    return arg => (TUnion)ctor.Invoke(new[] { arg });
                case MethodInfo method:
                    return arg => (TUnion)method.Invoke(null, new[] { arg })!;
                default:
                    throw new ArgumentException(SR.Format(SR.Arg_UnionUnsupportedCreationMember, creationMember.GetType()), nameof(creationMember));
            }
        }

        private static UnionCaseInfo[] SortTopologically(UnionCaseInfo[] cases)
        {
            // Most-derived-first topological sort. For unrelated types the input
            // order is preserved so that callers see a deterministic listing.
            if (cases.Length <= 1)
            {
                return cases;
            }

            UnionCaseInfo[] result = new UnionCaseInfo[cases.Length];
            bool[] taken = new bool[cases.Length];
            int written = 0;

            while (written < cases.Length)
            {
                bool madeProgress = false;
                for (int i = 0; i < cases.Length; i++)
                {
                    if (taken[i])
                    {
                        continue;
                    }

                    bool hasUntakenStrictSubtype = false;
                    for (int j = 0; j < cases.Length; j++)
                    {
                        if (taken[j] || j == i)
                        {
                            continue;
                        }

                        if (cases[i].CaseType.IsAssignableFrom(cases[j].CaseType) &&
                            cases[i].CaseType != cases[j].CaseType)
                        {
                            hasUntakenStrictSubtype = true;
                            break;
                        }
                    }

                    if (!hasUntakenStrictSubtype)
                    {
                        result[written++] = cases[i];
                        taken[i] = true;
                        madeProgress = true;
                    }
                }

                if (!madeProgress)
                {
                    // Defensive: should never fire for a valid case set.
                    for (int i = 0; i < cases.Length; i++)
                    {
                        if (!taken[i])
                        {
                            result[written++] = cases[i];
                        }
                    }

                    break;
                }
            }

            return result;
        }
    }
}
