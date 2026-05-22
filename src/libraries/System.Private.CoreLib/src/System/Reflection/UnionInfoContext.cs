// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace System.Reflection
{
    /// <summary>
    /// Provides APIs for populating union metadata
    /// (<see cref="UnionInfo" />, <see cref="UnionCaseInfo" />) for a given
    /// <see cref="Type" />, following the conventions of the C# union types
    /// language proposal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Discovery is purely structural — the
    /// <c>System.Runtime.CompilerServices.UnionAttribute</c> is not
    /// required, but its presence is surfaced through
    /// <see cref="UnionInfo.HasUnionAttribute" />.
    /// </para>
    /// <para>
    /// Instances cache discovery results across calls. Re-use a single
    /// <see cref="UnionInfoContext" /> when probing many types in the same
    /// workflow (for example, when building a serializer metadata cache).
    /// </para>
    /// <para>
    /// Instances are not thread-safe and should not be shared across threads
    /// without external synchronization. For long-lived discovery in
    /// thread-shared scenarios, callers should snapshot the resulting
    /// <see cref="UnionInfo" /> into their own thread-safe cache or use
    /// <see cref="UnionAccessors{TUnion}" />, whose delegates are themselves
    /// safe for concurrent use.
    /// </para>
    /// </remarks>
    /// <seealso cref="UnionInfo" />
    /// <seealso cref="NullabilityInfoContext" />
#if NET11_0_OR_GREATER
    public
#else
    internal
#endif
    sealed class UnionInfoContext
    {
        private const string ValuePropertyName = "Value";
        private const string TryGetValueMethodName = "TryGetValue";
        private const string FactoryMethodName = "Create";
        private const string UnionMemberProviderInterfaceName = "IUnionMembers";
        private const string UnionAttributeFullName = "System.Runtime.CompilerServices.UnionAttribute";

        private readonly NullabilityInfoContext _nullabilityContext = new();
        private readonly Dictionary<Type, UnionInfo?> _cache = new();

        /// <summary>
        /// Returns <see langword="true" /> if <paramref name="type" /> follows
        /// the structural union pattern.
        /// </summary>
        /// <param name="type">The candidate type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null" />.</exception>
        /// <remarks>
        /// This is a fast structural probe; it does not allocate the full
        /// <see cref="UnionInfo" />. Equivalent to calling
        /// <see cref="TryCreate(Type, out UnionInfo)" /> and discarding the
        /// result, but cheaper because metadata for the cases is not built.
        /// </remarks>
        [RequiresUnreferencedCode("Union discovery reflects over the type's public constructors, methods, properties and nested types.")]
        public static bool IsUnion(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicNestedTypes |
                DynamicallyAccessedMemberTypes.Interfaces)] Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            Type definingType = GetUnionDefiningType(type);
            if (GetValueProperty(definingType) is null)
            {
                return false;
            }

            return HasAnyCreationMember(type, definingType);
        }

        /// <summary>
        /// Builds union metadata for <paramref name="type" /> and caches it on
        /// this context.
        /// </summary>
        /// <param name="type">The union type.</param>
        /// <returns>The discovered <see cref="UnionInfo" />.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException"><paramref name="type" /> is not a union type.</exception>
        [RequiresUnreferencedCode("Union discovery reflects over the type's public constructors, methods, properties and nested types.")]
        public UnionInfo Create(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicNestedTypes |
                DynamicallyAccessedMemberTypes.Interfaces)] Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (!TryCreateCore(type, out UnionInfo? info))
            {
                throw new ArgumentException(SR.Format(SR.Arg_NotAUnionType, type), nameof(type));
            }

            return info;
        }

        /// <summary>
        /// Attempts to build union metadata for <paramref name="type" />.
        /// </summary>
        /// <param name="type">The candidate type.</param>
        /// <param name="unionInfo">When this method returns, the discovered
        /// <see cref="UnionInfo" /> if <paramref name="type" /> is a union;
        /// otherwise <see langword="null" />.</param>
        /// <returns><see langword="true" /> if <paramref name="type" /> is a union;
        /// otherwise <see langword="false" />.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null" />.</exception>
        [RequiresUnreferencedCode("Union discovery reflects over the type's public constructors, methods, properties and nested types.")]
        public bool TryCreate(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicNestedTypes |
                DynamicallyAccessedMemberTypes.Interfaces)] Type type,
            [NotNullWhen(true)] out UnionInfo? unionInfo)
        {
            ArgumentNullException.ThrowIfNull(type);
            return TryCreateCore(type, out unionInfo);
        }

        [RequiresUnreferencedCode("Union discovery reflects over the type's public constructors, methods, properties and nested types.")]
        private bool TryCreateCore(Type type, [NotNullWhen(true)] out UnionInfo? unionInfo)
        {
            if (_cache.TryGetValue(type, out unionInfo))
            {
                return unionInfo is not null;
            }

            unionInfo = BuildUnionInfo(type);
            _cache[type] = unionInfo;
            return unionInfo is not null;
        }

        [RequiresUnreferencedCode("Union discovery reflects over the type's public constructors, methods, properties and nested types.")]
        private UnionInfo? BuildUnionInfo(Type type)
        {
            Type definingType = GetUnionDefiningType(type);

            PropertyInfo? valueProperty = GetValueProperty(definingType);
            if (valueProperty is null)
            {
                return null;
            }

            List<UnionCaseInfo> cases = new();
            Dictionary<Type, int> caseIndexByType = new();

            if (definingType == type)
            {
                foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    if (parameters.Length != 1)
                    {
                        continue;
                    }

                    AddCase(parameters[0], ctor, cases, caseIndexByType);
                }
            }
            else
            {
                foreach (MethodInfo method in definingType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != FactoryMethodName ||
                        method.IsGenericMethodDefinition ||
                        !IsIdentityConvertibleTo(method.ReturnType, type))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1)
                    {
                        continue;
                    }

                    AddCase(parameters[0], method, cases, caseIndexByType);
                }
            }

            if (cases.Count == 0)
            {
                return null;
            }

            AttachTryGetValueMethods(definingType, cases, caseIndexByType);

            return new UnionInfo(type, definingType, HasUnionAttribute(type), valueProperty, cases.ToArray());
        }

        private static bool HasUnionAttribute(Type type)
        {
            // Looked up by full name so the source compiles unchanged when used as
            // a polyfill in libraries that target framework versions that pre-date
            // the System.Runtime.CompilerServices.UnionAttribute type.
            IList<CustomAttributeData> data = CustomAttributeData.GetCustomAttributes(type);
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].AttributeType.FullName == UnionAttributeFullName)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddCase(ParameterInfo parameter, MemberInfo creationMember, List<UnionCaseInfo> cases, Dictionary<Type, int> caseIndexByType)
        {
            if (!TryGetCaseType(parameter, out Type? caseType, out bool admitsNull))
            {
                return;
            }

            if (caseIndexByType.TryGetValue(caseType, out int existingIndex))
            {
                if (admitsNull && !cases[existingIndex].AdmitsNull)
                {
                    // Replace the existing entry with the null-accepting creation member so
                    // that constructor dispatch with a null value routes to the nullable
                    // overload (e.g., ctor(int?)) rather than the non-nullable one
                    // (e.g., ctor(int)), which would coerce null to default(T).
                    UnionCaseInfo existing = cases[existingIndex];
                    cases[existingIndex] = new UnionCaseInfo(existing.CaseType, admitsNull: true, creationMember, existing.TryGetValueMethod);
                }

                return;
            }

            caseIndexByType[caseType] = cases.Count;
            cases.Add(new UnionCaseInfo(caseType, admitsNull, creationMember, tryGetValueMethod: null));
        }

        private bool TryGetCaseType(ParameterInfo parameter, [NotNullWhen(true)] out Type? caseType, out bool admitsNull)
        {
            admitsNull = false;
            Type parameterType = parameter.ParameterType;
            if (parameterType.IsByRef)
            {
                caseType = null;
                return false;
            }

            // Restrict to by-value and 'in' parameters; 'out' is filtered above.
            // 'ref' would have hit the IsByRef guard.

            caseType = parameterType;
            if (Nullable.GetUnderlyingType(caseType) is Type underlying)
            {
                caseType = underlying;
                admitsNull = true;
            }
            else if (!caseType.IsValueType)
            {
                // For reference types and unconstrained type parameters, consult the
                // C#-emitted nullability annotation on the parameter to decide whether
                // null is accepted.
                NullabilityInfo nullability = _nullabilityContext.Create(parameter);
                admitsNull = nullability.WriteState is not NullabilityState.NotNull;
            }

            return true;
        }

        private static void AttachTryGetValueMethods(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type definingType,
            List<UnionCaseInfo> cases,
            Dictionary<Type, int> caseIndexByType)
        {
            foreach (MethodInfo method in definingType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != TryGetValueMethodName ||
                    method.ReturnType != typeof(bool) ||
                    method.IsGenericMethodDefinition)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1)
                {
                    continue;
                }

                ParameterInfo parameter = parameters[0];
                if (!parameter.IsOut || !parameter.ParameterType.IsByRef)
                {
                    continue;
                }

                Type caseType = parameter.ParameterType.GetElementType()!;
                if (!caseIndexByType.TryGetValue(caseType, out int index))
                {
                    continue;
                }

                UnionCaseInfo existing = cases[index];
                if (existing.TryGetValueMethod is not null)
                {
                    continue;
                }

                cases[index] = new UnionCaseInfo(existing.CaseType, existing.AdmitsNull, existing.CreationMember, method);
            }
        }

        [return: DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties)]
        [UnconditionalSuppressMessage("Trimming", "IL2063",
            Justification = "When a nested IUnionMembers interface is found via reflection, the interface is preserved alongside the union type because of the PublicNestedTypes / Interfaces annotations on the parameter.")]
        [UnconditionalSuppressMessage("Trimming", "IL2073",
            Justification = "The PublicNestedTypes annotation on the parameter preserves the nested IUnionMembers interface members alongside the union type. The Interfaces annotation preserves the interface implementation.")]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2121:RedundantSuppression",
            Justification = "IL2073 is reported by the analyzer in some configurations but not by the IL linker in others; suppression covers both.")]
        private static Type GetUnionDefiningType(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicNestedTypes |
                DynamicallyAccessedMemberTypes.Interfaces)] Type type)
        {
            Type[] nested = type.GetNestedTypes(BindingFlags.Public);
            Type[]? implementedInterfaces = null;
            for (int i = 0; i < nested.Length; i++)
            {
                Type nestedType = nested[i];
                if (!nestedType.IsInterface ||
                    nestedType.Name != UnionMemberProviderInterfaceName ||
                    nestedType.DeclaringType != type)
                {
                    continue;
                }

                implementedInterfaces ??= type.GetInterfaces();
                if (Array.IndexOf(implementedInterfaces, nestedType) >= 0)
                {
                    return nestedType;
                }
            }

            return type;
        }

        private static PropertyInfo? GetValueProperty(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type definingType)
        {
            PropertyInfo? property = definingType.GetProperty(ValuePropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null ||
                property.PropertyType != typeof(object) ||
                property.GetMethod is not { IsPublic: true } ||
                property.GetIndexParameters().Length != 0)
            {
                return null;
            }

            return property;
        }

        private static bool HasAnyCreationMember(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type definingType)
        {
            if (definingType == type)
            {
                foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    if (parameters.Length == 1 && !parameters[0].ParameterType.IsByRef)
                    {
                        return true;
                    }
                }
            }
            else
            {
                foreach (MethodInfo method in definingType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != FactoryMethodName || method.IsGenericMethodDefinition)
                    {
                        continue;
                    }

                    if (!IsIdentityConvertibleTo(method.ReturnType, type))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && !parameters[0].ParameterType.IsByRef)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsIdentityConvertibleTo(Type from, Type to) => from == to;
    }
}
