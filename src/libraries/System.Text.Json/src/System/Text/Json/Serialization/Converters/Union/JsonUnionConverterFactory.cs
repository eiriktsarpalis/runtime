// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization.Converters
{
    [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
    internal sealed class JsonUnionConverterFactory : JsonConverterFactory
    {
        private const string UnionMemberProviderName = "IUnionMembers";
        private const string UnionFactoryMethodName = "Create";
        private const string UnionValuePropertyName = "Value";

        public override bool CanConvert(Type typeToConvert)
        {
#if NET11_0_OR_GREATER
            // Level 0: Compiler unions — [Union] and the basic union pattern.
            if (typeToConvert.GetCustomAttribute<UnionAttribute>() is not null)
            {
                return true;
            }
#endif

            // Level 1/2: User unions — [JsonUnion]
            if (typeToConvert.GetCustomAttribute<JsonUnionAttribute>() is not null)
            {
                return true;
            }

            return false;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type converterType = typeof(JsonUnionConverter<>).MakeGenericType(typeToConvert);

            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

#if NET11_0_OR_GREATER
        /// <summary>
        /// Auto-configures union JTI properties for compiler unions that follow the current
        /// C# union pattern, including nested IUnionMembers providers.
        /// Called during JsonTypeInfo configuration.
        /// </summary>
        internal static void ConfigureUnionDefaults(JsonTypeInfo typeInfo)
        {
            Type unionType = typeInfo.Type;
            JsonUnionAttribute? attr = unionType.GetCustomAttribute<JsonUnionAttribute>();

            List<Type> caseTypes = GetConfiguredCaseTypes(typeInfo);
            if (caseTypes.Count == 0)
            {
                // Discover case types from the compiler-emitted union-defining type:
                // nested IUnionMembers.Create(...) methods if present, otherwise
                // single-parameter constructors on the union type itself.
                caseTypes = DiscoverCaseTypesFromUnionDefinition(unionType);
            }

            if (caseTypes.Count == 0)
            {
                return;
            }

            PopulateUnionCases(typeInfo, caseTypes);

            ConfigureTypeClassifier(typeInfo, unionType, caseTypes, attr);

            if (typeInfo.UnionDeconstructor is null)
            {
                BuildDeconstructorFromConvention(typeInfo, unionType, caseTypes);
            }

            if (typeInfo.UnionConstructor is null)
            {
                BuildConstructorFromConvention(typeInfo, unionType, caseTypes);
            }
        }
#endif

        /// <summary>
        /// Auto-configures union JTI properties for Level 1 convention-based unions ([JsonUnion]).
        /// Called during JsonTypeInfo configuration.
        /// </summary>
        internal static void ConfigureConventionUnion(JsonTypeInfo typeInfo)
        {
            Type unionType = typeInfo.Type;
            JsonUnionAttribute attr = unionType.GetCustomAttribute<JsonUnionAttribute>()!;

            List<Type> caseTypes = GetConfiguredCaseTypes(typeInfo);
            if (caseTypes.Count == 0)
            {
                // Case type discovery: [ClosedSubtype] > implicit operators > single-param ctors
                caseTypes = DiscoverCaseTypes(unionType);
            }

            if (caseTypes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No union case types could be discovered for '{unionType}'. " +
                    "Use contract customization to populate UnionCases and set deconstructor/constructor delegates.");
            }

            PopulateUnionCases(typeInfo, caseTypes);

            ConfigureTypeClassifier(typeInfo, unionType, caseTypes, attr);

            // Build deconstructor and constructor from convention.
            if (typeInfo.UnionDeconstructor is null)
            {
                BuildDeconstructorFromConvention(typeInfo, unionType, caseTypes);
            }

            if (typeInfo.UnionConstructor is null)
            {
                BuildConstructorFromConvention(typeInfo, unionType, caseTypes);
            }

            if (typeInfo.UnionDeconstructor is null)
            {
                throw new InvalidOperationException(
                    $"Unable to infer a union deconstructor for type '{unionType}'. " +
                    "Use contract customization to set the UnionDeconstructor delegate.");
            }

            if (typeInfo.UnionConstructor is null)
            {
                throw new InvalidOperationException(
                    $"Unable to infer a union constructor for type '{unionType}'. " +
                    "Use contract customization to set the UnionConstructor delegate.");
            }
        }

        private static void ConfigureTypeClassifier(JsonTypeInfo typeInfo, Type unionType, List<Type> caseTypes, JsonUnionAttribute? attr)
        {
            if (typeInfo.TypeClassifier is not null)
            {
                return;
            }

            JsonTypeClassifierFactory? classifierFactory = null;

            // Per-type opt-in via [JsonUnion(TypeClassifier = ...)] takes precedence over the
            // options-level Classifiers list.
            if (attr?.TypeClassifier is not null)
            {
                if (!typeof(JsonTypeClassifierFactory).IsAssignableFrom(attr.TypeClassifier))
                {
                    throw new InvalidOperationException(
                        $"The TypeClassifier type '{attr.TypeClassifier}' must derive from JsonTypeClassifierFactory.");
                }

                classifierFactory = (JsonTypeClassifierFactory)Activator.CreateInstance(attr.TypeClassifier)!;

                if (!classifierFactory.CanClassify(unionType))
                {
                    throw new InvalidOperationException(
                        $"The classifier factory '{attr.TypeClassifier}' specified on union type '{unionType}' " +
                        $"reports CanClassify('{unionType}') == false.");
                }
            }
            else
            {
                classifierFactory = typeInfo.Options.GetUnionClassifierFactoryFromList(unionType);
            }

            if (classifierFactory is not null)
            {
                // Defer the actual factory invocation until JsonTypeInfo.Configure() time.
                // At that point this typeInfo has been entered into the per-options cache and
                // its configuration state is 'Configuring', so a re-entrant GetTypeInfo call
                // for the same union type from inside the factory body returns the (partially
                // configured) JsonTypeInfo instead of recursing infinitely. This also enables
                // legitimate cyclic graphs (e.g. union Foo(Bar, int); record Bar(Foo Foo)).
                JsonTypeClassifierFactory capturedFactory = classifierFactory;
                Dictionary<Type, JsonUnionCaseInfo> caseInfoByType = new(typeInfo.UnionCases!.Count);
                foreach (JsonUnionCaseInfo caseInfo in typeInfo.UnionCases!)
                {
                    caseInfoByType[caseInfo.CaseType] = caseInfo;
                }
                List<JsonUnionCaseInfo> unionCases = caseTypes.ConvertAll(t => caseInfoByType[t]);
                typeInfo.DeferredUnionConfigure = static (info, state) =>
                {
                    var (factory, cases, type) = ((JsonTypeClassifierFactory, List<JsonUnionCaseInfo>, Type))state!;
                    var ctx = new JsonTypeClassifierContext(type, cases, Array.Empty<JsonDerivedType>(), typeDiscriminatorPropertyName: null);
                    info.SetTypeClassifierFromConfigure(factory.CreateJsonClassifier(ctx, info.Options));
                };
                typeInfo.DeferredUnionConfigureState = (capturedFactory, unionCases, unionType);
            }
            else
            {
                // Default: token-type matching (no classifier, no read-ahead).
                // typeInfo.UnionCases has been populated by PopulateUnionCases above.
                typeInfo.UnionTokenTypeMap = BuildTokenTypeMap(typeInfo.UnionCases!, typeInfo.Options);
            }
        }

        private static List<Type> GetConfiguredCaseTypes(JsonTypeInfo typeInfo)
        {
            if (typeInfo.UnionCases is not { Count: > 0 } unionCases)
            {
                return [];
            }

            var caseTypes = new List<Type>(unionCases.Count);
            foreach (JsonUnionCaseInfo caseInfo in unionCases)
            {
                caseTypes.Add(caseInfo.CaseType);
            }

            return SortCaseTypesTopologically(caseTypes);
        }

        // When the user has not pre-populated UnionCases, look up which discovered
        // case types accept null by walking constructor parameters with a
        // NullabilityInfoContext. Falls back to false for unknown cases.
        private static Dictionary<Type, bool> ComputeIsNullableMap(Type unionType, List<Type> caseTypes)
        {
            var map = new Dictionary<Type, bool>(caseTypes.Count);
            foreach (Type ct in caseTypes)
            {
                map[ct] = false;
            }

            NullabilityInfoContext? ctx = null;
            try { ctx = new NullabilityInfoContext(); } catch { }

            // Walk single-parameter constructors on the union type.
            foreach (ConstructorInfo ctor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length == 1 &&
                    TryGetCaseType(parameters[0], ctx, out Type? paramType, out bool acceptsNull) &&
                    map.TryGetValue(paramType, out bool existing))
                {
                    map[paramType] = existing | acceptsNull;
                }
            }

#if NET11_0_OR_GREATER
            // Walk IUnionMembers.Create factory methods if present.
            if (TryGetUnionDefiningType(unionType, out Type unionDefiningType, out bool usesUnionMemberProvider) && usesUnionMemberProvider)
            {
                foreach (MethodInfo method in unionDefiningType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name == UnionFactoryMethodName &&
                        method.ReturnType == unionType &&
                        method.GetParameters().Length == 1 &&
                        TryGetCaseType(method.GetParameters()[0], ctx, out Type? paramType, out bool acceptsNull) &&
                        map.TryGetValue(paramType, out bool existing))
                    {
                        map[paramType] = existing | acceptsNull;
                    }
                }
            }
#endif

            // Walk implicit conversion operators.
            foreach (MethodInfo method in unionType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == "op_Implicit" &&
                    method.ReturnType == unionType &&
                    method.GetParameters().Length == 1 &&
                    TryGetCaseType(method.GetParameters()[0], ctx, out Type? paramType, out bool acceptsNull) &&
                    map.TryGetValue(paramType, out bool existing))
                {
                    map[paramType] = existing | acceptsNull;
                }
            }

            return map;
        }

        private static void PopulateUnionCases(JsonTypeInfo typeInfo, List<Type> caseTypes)
        {
            // Preserve user-set IsNullable values for case types that already exist;
            // otherwise compute from convention.
            Dictionary<Type, bool>? userIsNullable = null;
            if (typeInfo.UnionCases is { Count: > 0 } existing)
            {
                userIsNullable = new Dictionary<Type, bool>(existing.Count);
                foreach (JsonUnionCaseInfo info in existing)
                {
                    userIsNullable[info.CaseType] = info.IsNullable;
                }
            }

            Dictionary<Type, bool> conventionMap = ComputeIsNullableMap(typeInfo.Type, caseTypes);

            typeInfo.UnionCases = new List<JsonUnionCaseInfo>(caseTypes.Count);
            foreach (Type ct in caseTypes)
            {
                bool acceptsNull = userIsNullable is not null && userIsNullable.TryGetValue(ct, out bool userVal)
                    ? userVal
                    : conventionMap.TryGetValue(ct, out bool convVal) && convVal;

                typeInfo.UnionCases.Add(new JsonUnionCaseInfo(ct) { IsNullable = acceptsNull });
            }
        }

        /// <summary>
        /// Builds a token→type map from the case metadata. Each case is categorized
        /// by its primary JSON token type via <see cref="JsonConverter.GetSupportedJsonTokenTypes"/>;
        /// converters that decline to advertise (return <see langword="null"/>) fall back to a
        /// <see cref="ConverterStrategy"/>-derived default. First-declared wins for duplicate
        /// categories. The <see cref="JsonTokenType.Null"/> entry is populated only from cases
        /// with <see cref="JsonUnionCaseInfo.IsNullable"/> set to <see langword="true"/>.
        /// </summary>
        internal static Dictionary<JsonTokenType, Type> BuildTokenTypeMap(IList<JsonUnionCaseInfo> unionCases, JsonSerializerOptions options)
        {
            var map = new Dictionary<JsonTokenType, Type>();

            foreach (JsonUnionCaseInfo info in unionCases)
            {
                Type caseType = info.CaseType;
                JsonTypeInfo? caseTypeInfo = options.GetTypeInfoInternal(caseType, ensureNotNull: null);

                JsonTokenType[]? tokens = null;
                if (caseTypeInfo is not null)
                {
                    JsonNumberHandling effectiveNumberHandling =
                        caseTypeInfo.NumberHandling ?? options.NumberHandling;
                    JsonConverter converter = caseTypeInfo.Converter;
                    tokens = converter.GetSupportedJsonTokenTypes(effectiveNumberHandling);

                    if (tokens is null)
                    {
                        JsonTokenType fallback = converter.ConverterStrategy switch
                        {
                            ConverterStrategy.Enumerable => JsonTokenType.StartArray,
                            _ => JsonTokenType.StartObject,
                        };
                        tokens = [fallback];
                    }
                }
                else
                {
                    // Resolver does not know about this case type (e.g., source-generated
                    // context that registers only the union itself). Default to StartObject
                    // since most user-defined case types are reference/object-shaped.
                    tokens = [JsonTokenType.StartObject];
                }

                foreach (JsonTokenType token in tokens)
                {
                    map.TryAdd(token, caseType);
                }
            }

            // JSON null is permitted iff at least one case opts into IsNullable.
            // Multiple nullable cases are allowed: by spec, passing null to any of
            // them yields the same null-holding union value, so picking the first
            // declared nullable case is semantically transparent. The chosen case
            // type is only used as a discriminator for the constructor delegate;
            // the delegate itself dispatches null uniformly.
            foreach (JsonUnionCaseInfo info in unionCases)
            {
                if (info.IsNullable)
                {
                    map[JsonTokenType.Null] = info.CaseType;
                    break;
                }
            }

            return map;
        }

        /// <summary>
        /// Discovers case types using the full convention chain:
        /// [ClosedSubtype] > implicit operators > single-param constructors.
        /// </summary>
        private static List<Type> DiscoverCaseTypes(Type unionType)
        {
#if NET11_0_OR_GREATER
            // 1. ClosedSubtype attributes (closed hierarchies)
            var closedSubtypes = new List<Type>();
            foreach (ClosedSubtypeAttribute attr in unionType.GetCustomAttributes<ClosedSubtypeAttribute>())
            {
                closedSubtypes.Add(attr.SubtypeType);
            }

            if (closedSubtypes.Count > 0)
            {
                return SortCaseTypesTopologically(closedSubtypes);
            }
#endif

            // 2. Compiler union member providers: nested public IUnionMembers interface
            // with static Create(...) members.
#if NET11_0_OR_GREATER
            if (TryGetUnionDefiningType(unionType, out Type unionDefiningType, out bool usesUnionMemberProvider)
                && usesUnionMemberProvider)
            {
                return DiscoverCaseTypesFromFactoryMethods(unionType, unionDefiningType);
            }
#endif

            // 3. Implicit conversion operators → union type
            var fromOperators = new List<Type>();
            foreach (MethodInfo method in unionType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == "op_Implicit" &&
                    method.ReturnType == unionType &&
                    method.GetParameters().Length == 1)
                {
                    if (TryGetCaseType(method.GetParameters()[0], out Type? paramType) &&
                        !fromOperators.Contains(paramType) &&
                        paramType.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
                    {
                        fromOperators.Add(paramType);
                    }
                }
            }

            if (fromOperators.Count > 0)
            {
                return SortCaseTypesTopologically(fromOperators);
            }

            // 4. Single-parameter constructors
            return DiscoverCaseTypesFromConstructors(unionType);
        }

        private static List<Type> DiscoverCaseTypesFromUnionDefinition(Type unionType)
        {
            if (!TryGetUnionDefiningType(unionType, out Type unionDefiningType, out bool usesUnionMemberProvider))
            {
                return [];
            }

            return usesUnionMemberProvider
                ? DiscoverCaseTypesFromFactoryMethods(unionType, unionDefiningType)
                : DiscoverCaseTypesFromConstructors(unionType);
        }

        private static List<Type> DiscoverCaseTypesFromFactoryMethods(Type unionType, Type unionDefiningType)
        {
            var caseTypes = new List<Type>();

            foreach (MethodInfo method in unionDefiningType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == UnionFactoryMethodName &&
                    method.ReturnType == unionType &&
                    method.GetParameters().Length == 1 &&
                    TryGetCaseType(method.GetParameters()[0], out Type? paramType) &&
                    !caseTypes.Contains(paramType) &&
                    paramType.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
                {
                    caseTypes.Add(paramType);
                }
            }

            return SortCaseTypesTopologically(caseTypes);
        }

        private static List<Type> DiscoverCaseTypesFromConstructors(Type unionType)
        {
            var caseTypes = new List<Type>();
            foreach (ConstructorInfo ctor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length == 1 &&
                    TryGetCaseType(parameters[0], out Type? paramType))
                {
                    if (!caseTypes.Contains(paramType) &&
                        paramType.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
                    {
                        caseTypes.Add(paramType);
                    }
                }
            }

            return SortCaseTypesTopologically(caseTypes);
        }

        private static List<Type> SortCaseTypesTopologically(List<Type> caseTypes)
        {
            if (caseTypes.Count <= 1)
            {
                return caseTypes;
            }

            var sortedCaseTypes = new List<Type>(caseTypes.Count);

            foreach (Type caseType in caseTypes)
            {
                Type[] hierarchy = caseType.GetSortedTypeHierarchy();
                int insertIndex = sortedCaseTypes.Count;

                for (int i = 0; i < sortedCaseTypes.Count; i++)
                {
                    if (Array.IndexOf(hierarchy, sortedCaseTypes[i]) >= 0)
                    {
                        insertIndex = i;
                        break;
                    }
                }

                sortedCaseTypes.Insert(insertIndex, caseType);
            }

            return sortedCaseTypes;
        }

        /// <summary>
        /// Builds a deconstructor delegate from convention:
        /// Assignability (identity) → op_Explicit → op_Implicit → fail.
        /// </summary>
        private static void BuildDeconstructorFromConvention(JsonTypeInfo typeInfo, Type unionType, List<Type> caseTypes)
        {
            // For closed hierarchies where all case types are assignable, use identity.
            bool allAssignable = true;
            foreach (Type ct in caseTypes)
            {
                if (!unionType.IsAssignableFrom(ct))
                {
                    allAssignable = false;
                    break;
                }
            }

            if (allAssignable)
            {
                typeInfo.UnionDeconstructor = obj => (obj.GetType(), obj);

                return;
            }

            if (TryCreateUnionValueAccessor(unionType, out Func<object, object?>? valueAccessor))
            {
                typeInfo.UnionDeconstructor = obj =>
                {
                    object? value = valueAccessor(obj);
                    return (value?.GetType(), value);
                };

                return;
            }

            // Try to build from explicit/implicit operators.
            // Build a map of case type → operator for deconstruction.
            var operatorMap = new Dictionary<Type, MethodInfo>(caseTypes.Count);
            foreach (Type ct in caseTypes)
            {
                if (unionType.IsAssignableFrom(ct))
                {
                    continue;
                }

                MethodInfo? op = FindConversionOperator(unionType, unionType, ct, "op_Explicit")
                    ?? FindConversionOperator(unionType, unionType, ct, "op_Implicit");

                if (op is not null)
                {
                    operatorMap[ct] = op;
                }
            }

            // For conversion-operator-based unions, try each operator.
            if (operatorMap.Count > 0)
            {
                List<Type> orderedCaseTypes = caseTypes;
                Dictionary<Type, MethodInfo> ops = operatorMap;

                // Try to find backing fields for fast-path deconstruction.
                // Many union structs store a (Type _caseType, object _value) pair internally.
                FieldInfo? caseTypeField = null;
                FieldInfo? valueField = null;
                foreach (FieldInfo field in unionType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(Type))
                    {
                        caseTypeField = field;
                    }
                    else if (field.FieldType == typeof(object))
                    {
                        valueField = field;
                    }
                }

                if (caseTypeField is not null && valueField is not null)
                {
                    // Fast path: direct field access without exception-based probing.
                    FieldInfo ctField = caseTypeField;
                    FieldInfo valField = valueField;

                    typeInfo.UnionDeconstructor = obj =>
                    {
                        Type? ct = (Type?)ctField.GetValue(obj);
                        object? value = valField.GetValue(obj);
                        Debug.Assert(ct is not null || value is null);
                        return (ct, value);
                    };
                }
                else
                {
                    // Slow path: probe each operator. This is correct but uses exception-based control flow
                    // when the wrong operator is tried first.
                    typeInfo.UnionDeconstructor = obj =>
                    {
                        foreach (Type ct in orderedCaseTypes)
                        {
                            if (ops.TryGetValue(ct, out MethodInfo? op))
                            {
                                try
                                {
                                    object? value = op.Invoke(null, [obj]);
                                    return (ct, value);
                                }
                                catch (TargetInvocationException ex) when (ex.InnerException is InvalidCastException)
                                {
                                    continue;
                                }
                            }
                        }

                        ThrowHelper.ThrowJsonException($"Unable to deconstruct union instance of type '{unionType}'. No conversion operator matched.");
                        return default;
                    };
                }

                return;
            }

            // No deconstructor could be inferred. Leave null — will fail at serialization time
            // unless the user provides one via contract customization.
        }

        /// <summary>
        /// Builds a constructor delegate from convention:
        /// Assignability → op_Implicit → single-param ctor → fail.
        /// </summary>
        private static void BuildConstructorFromConvention(JsonTypeInfo typeInfo, Type unionType, List<Type> caseTypes)
        {
            // Build a per-case nullability set so that the constructor delegate
            // itself encapsulates the null-handling policy: the converter just
            // forwards a null payload to the delegate, and the delegate decides
            // whether to wrap it (canonical null union) or throw. This mirrors
            // the source-generated `null => new T((Case?)null)` /
            // `_ => throw new JsonException()` pattern. The case-type argument
            // is intentionally ignored on the null path: per the union spec all
            // nullable cases collapse to the same null-holding instance, so the
            // classifier's choice is irrelevant (and the converter short-circuits
            // null tokens before invoking the classifier at all).
            HashSet<Type> nullableCases = GetNullableCaseSet(typeInfo);
            bool hasNullableCase = nullableCases.Count > 0;

            // For closed hierarchies where all case types are assignable, use identity cast.
            bool allAssignable = true;
            foreach (Type ct in caseTypes)
            {
                if (!unionType.IsAssignableFrom(ct))
                {
                    allAssignable = false;
                    break;
                }
            }

            if (allAssignable)
            {
                typeInfo.UnionConstructor = (Type caseType, object? value) =>
                {
                    if (value is null)
                    {
                        if (!hasNullableCase)
                        {
                            ThrowHelper.ThrowJsonException_UnionDoesNotAcceptNull(unionType);
                        }

                        // Canonical null for a reference-type union hierarchy is null itself.
                        return null!;
                    }

                    return value;
                };

                return;
            }

            // Build a map of case type → construction method.
            var constructionMap = new Dictionary<Type, Func<object?, object>>(caseTypes.Count);
            TryGetUnionDefiningType(unionType, out Type unionDefiningType, out bool usesUnionMemberProvider);

            foreach (Type ct in caseTypes)
            {
                if (unionType.IsAssignableFrom(ct))
                {
                    constructionMap[ct] = value => value!;
                    continue;
                }

                if (usesUnionMemberProvider)
                {
                    MethodInfo? createMethod = FindFactoryMethod(unionDefiningType, unionType, ct);
                    if (createMethod is not null)
                    {
                        MethodInfo create = createMethod;
                        constructionMap[ct] = value => create.Invoke(null, [value])!;
                    }

                    continue;
                }

                // Try implicit operator: ct → unionType
                MethodInfo? implicitOp = FindConversionOperator(unionType, ct, unionType, "op_Implicit");
                if (implicitOp is not null)
                {
                    MethodInfo op = implicitOp;
                    constructionMap[ct] = value => op.Invoke(null, [value])!;
                    continue;
                }

                // Try single-parameter constructor.
                ConstructorInfo? ctor = FindSingleParameterConstructor(unionType, ct);
                if (ctor is not null)
                {
                    ConstructorInfo c = ctor;
                    constructionMap[ct] = value => c.Invoke([value])!;
                    continue;
                }
            }

            if (constructionMap.Count > 0)
            {
                // Precompute the canonical null factory: pick any nullable case's
                // construction method (e.g. `new Pet((Cat?)null)`). All nullable
                // cases produce the same null-holding union value per the spec, so
                // the choice is arbitrary; we pick the first one in declaration order.
                Func<object?, object>? nullFactory = null;
                foreach (Type ct in caseTypes)
                {
                    if (nullableCases.Contains(ct) && constructionMap.TryGetValue(ct, out Func<object?, object>? f))
                    {
                        nullFactory = f;
                        break;
                    }
                }

                typeInfo.UnionConstructor = (Type caseType, object? value) =>
                {
                    if (value is null)
                    {
                        if (nullFactory is null)
                        {
                            ThrowHelper.ThrowJsonException_UnionDoesNotAcceptNull(unionType);
                        }

                        return nullFactory(null);
                    }

                    if (constructionMap.TryGetValue(caseType, out Func<object?, object>? factory))
                    {
                        return factory(value);
                    }

                    // Try assignable fallback for derived types.
                    foreach (KeyValuePair<Type, Func<object?, object>> kvp in constructionMap)
                    {
                        if (kvp.Key.IsAssignableFrom(caseType))
                        {
                            return kvp.Value(value);
                        }
                    }

                    ThrowHelper.ThrowJsonException();
                    return default!;
                };

                return;
            }

            // No constructor could be inferred — leave null.
        }

        private static HashSet<Type> GetNullableCaseSet(JsonTypeInfo typeInfo)
        {
            var set = new HashSet<Type>();
            if (typeInfo.UnionCases is { } cases)
            {
                foreach (JsonUnionCaseInfo caseInfo in cases)
                {
                    if (caseInfo.IsNullable)
                    {
                        set.Add(caseInfo.CaseType);
                    }
                }
            }

            return set;
        }

        private static MethodInfo? FindConversionOperator(Type declaringType, Type fromType, Type toType, string operatorName)
        {
            foreach (MethodInfo method in declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == operatorName &&
                    method.ReturnType == toType &&
                    parameters.Length == 1 &&
                    parameters[0].ParameterType == fromType)
                {
                    return method;
                }
            }

            return null;
        }

        private static ConstructorInfo? FindSingleParameterConstructor(Type unionType, Type parameterType)
        {
            foreach (ConstructorInfo ctor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length == 1 &&
                    TryGetCaseType(parameters[0], out Type? paramType))
                {
                    if (paramType == parameterType || paramType.IsAssignableFrom(parameterType))
                    {
                        return ctor;
                    }
                }
            }

            return null;
        }

        private static MethodInfo? FindFactoryMethod(Type unionDefiningType, Type unionType, Type parameterType)
        {
            foreach (MethodInfo method in unionDefiningType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == UnionFactoryMethodName &&
                    method.ReturnType == unionType &&
                    parameters.Length == 1 &&
                    TryGetCaseType(parameters[0], out Type? caseType) &&
                    (caseType == parameterType || caseType.IsAssignableFrom(parameterType)))
                {
                    return method;
                }
            }

            return null;
        }

        private static bool TryGetUnionDefiningType(Type unionType, out Type unionDefiningType, out bool usesUnionMemberProvider)
        {
            // For generic unions, GetNestedType() returns the open interface definition
            // (e.g. IUnionMembers[T]), which is not assignable from the closed union type.
            // Prefer the actual implemented interface so static Create(...) methods and Value
            // accessors are discovered with the constructed generic arguments.
            foreach (Type interfaceType in unionType.GetInterfaces())
            {
                if (IsUnionMemberProviderInterface(unionType, interfaceType))
                {
                    unionDefiningType = interfaceType;
                    usesUnionMemberProvider = true;
                    return true;
                }
            }

            Type? nestedType = unionType.GetNestedType(UnionMemberProviderName, BindingFlags.Public);
            if (nestedType is { IsInterface: true } &&
                IsUnionMemberProviderInterface(unionType, nestedType) &&
                nestedType.IsAssignableFrom(unionType))
            {
                unionDefiningType = nestedType;
                usesUnionMemberProvider = true;
                return true;
            }

            unionDefiningType = unionType;
            usesUnionMemberProvider = false;
            return true;
        }

        private static bool IsUnionMemberProviderInterface(Type unionType, Type interfaceType)
        {
            if (!interfaceType.IsInterface || interfaceType.Name != UnionMemberProviderName)
            {
                return false;
            }

            Type? declaringType = interfaceType.DeclaringType;
            if (declaringType is null)
            {
                return false;
            }

            if (declaringType.IsConstructedGenericType)
            {
                declaringType = declaringType.GetGenericTypeDefinition();
            }

            if (unionType.IsConstructedGenericType)
            {
                unionType = unionType.GetGenericTypeDefinition();
            }

            return declaringType == unionType;
        }

        private static bool TryGetCaseType(ParameterInfo parameter, [NotNullWhen(true)] out Type? caseType)
            => TryGetCaseType(parameter, nullabilityContext: null, out caseType, out _);

        private static bool TryGetCaseType(
            ParameterInfo parameter,
            NullabilityInfoContext? nullabilityContext,
            [NotNullWhen(true)] out Type? caseType,
            out bool acceptsNull)
        {
            acceptsNull = false;
            caseType = parameter.ParameterType;
            if (parameter.IsOut || (caseType.IsByRef && !parameter.IsIn))
            {
                caseType = null;
                return false;
            }

            if (caseType.IsByRef)
            {
                caseType = caseType.GetElementType();
            }

            // A parameter accepts null when it is Nullable<T> or, for reference types,
            // declared with a nullable annotation in the source.
            if (caseType is not null && Nullable.GetUnderlyingType(caseType) is Type underlying)
            {
                acceptsNull = true;
                caseType = underlying;
            }
            else if (caseType is not null && !caseType.IsValueType && nullabilityContext is not null)
            {
                try
                {
                    NullabilityInfo info = nullabilityContext.Create(parameter);
                    NullabilityState state = info.WriteState != NullabilityState.Unknown ? info.WriteState : info.ReadState;
                    // Treat Unknown as nullable for compatibility with assemblies compiled
                    // without nullable annotations.
                    acceptsNull = state is NullabilityState.Nullable or NullabilityState.Unknown;
                }
                catch
                {
                    acceptsNull = true;
                }
            }

            return caseType is not null;
        }

        private static bool TryCreateUnionValueAccessor(Type unionType, [NotNullWhen(true)] out Func<object, object?>? valueAccessor)
        {
            TryGetUnionDefiningType(unionType, out Type unionDefiningType, out _);

            PropertyInfo? valueProperty = unionDefiningType.GetProperty(UnionValuePropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (valueProperty is not null &&
                valueProperty.PropertyType == typeof(object) &&
                valueProperty.CanRead &&
                valueProperty.GetIndexParameters().Length == 0)
            {
                valueAccessor = valueProperty.GetValue;
                return true;
            }

#if NET11_0_OR_GREATER
            if (typeof(IUnion).IsAssignableFrom(unionType))
            {
                valueAccessor = static obj => ((IUnion)obj).Value;
                return true;
            }
#endif

            valueAccessor = null;
            return false;
        }
    }
}
