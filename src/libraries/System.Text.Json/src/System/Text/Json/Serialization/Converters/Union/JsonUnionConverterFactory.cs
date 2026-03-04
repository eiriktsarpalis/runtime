// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization.Converters
{
    [RequiresDynamicCode(JsonSerializer.SerializationRequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(JsonSerializer.SerializationUnreferencedCodeMessage)]
    internal sealed class JsonUnionConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
#if NET11_0_OR_GREATER
            // Level 0: Compiler unions — [Union] + IUnion
            if (typeToConvert.GetCustomAttribute<UnionAttribute>() is not null
                && typeof(IUnion).IsAssignableFrom(typeToConvert))
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
        /// Auto-configures union JTI properties for Level 0 compiler unions ([Union] + IUnion).
        /// Called during JsonTypeInfo configuration.
        /// </summary>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "Union case type discovery uses reflection at configuration time.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "Union case type discovery uses reflection at configuration time.")]
        internal static void ConfigureIUnionDefaults(JsonTypeInfo typeInfo)
        {
            Type unionType = typeInfo.Type;

            // Discover case types from single-parameter constructors.
            var caseTypes = DiscoverCaseTypesFromConstructors(unionType);
            if (caseTypes.Count == 0)
            {
                return;
            }

            // Populate UnionCases.
            typeInfo.UnionCases = new List<JsonUnionCaseInfo>(caseTypes.Count);
            foreach (Type ct in caseTypes)
            {
                typeInfo.UnionCases.Add(new JsonUnionCaseInfo(ct));
            }

            // Set up JSON classifier (structural matching by default).
            var factory = new JsonStructuralClassifierFactory();
            var context = new JsonTypeClassifierContext(
                unionType,
                caseTypes.ConvertAll(t => new JsonDerivedType(t)),
                typeDiscriminatorPropertyName: null);
            typeInfo.TypeClassifier = factory.CreateJsonClassifier(context, typeInfo.Options);

            // Deconstructor/Constructor are set via reflection-based helpers.
            // For IUnion types, deconstruct via .Value + GetType().
            typeInfo.UnionDeconstructor = obj =>
            {
                object? value = ((IUnion)obj).Value;
                Type caseType = value?.GetType() ?? typeof(object);
                return (caseType, value);
            };

            // Constructor: find the single-parameter ctor matching the case type.
            var ctorMap = new Dictionary<Type, ConstructorInfo>(caseTypes.Count);
            foreach (ConstructorInfo ctor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length == 1)
                {
                    Type paramType = Nullable.GetUnderlyingType(parameters[0].ParameterType) ?? parameters[0].ParameterType;
                    ctorMap.TryAdd(paramType, ctor);
                }
            }

            typeInfo.UnionConstructor = (Type caseType, object? value) =>
            {
                if (ctorMap.TryGetValue(caseType, out ConstructorInfo? ctor))
                {
                    return ctor.Invoke([value])!;
                }

                // Fall back to most-derived matching constructor.
                foreach (KeyValuePair<Type, ConstructorInfo> kvp in ctorMap)
                {
                    if (kvp.Key.IsAssignableFrom(caseType))
                    {
                        return kvp.Value.Invoke([value])!;
                    }
                }

                ThrowHelper.ThrowJsonException();
                return default!;
            };
        }
#endif

        /// <summary>
        /// Auto-configures union JTI properties for Level 1 convention-based unions ([JsonUnion]).
        /// Called during JsonTypeInfo configuration.
        /// </summary>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "Union case type discovery uses reflection at configuration time.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "Union case type discovery uses reflection at configuration time.")]
        internal static void ConfigureConventionUnion(JsonTypeInfo typeInfo)
        {
            Type unionType = typeInfo.Type;
            JsonUnionAttribute attr = unionType.GetCustomAttribute<JsonUnionAttribute>()!;

            // Case type discovery: [ClosedSubtype] > implicit operators > single-param ctors
            var caseTypes = DiscoverCaseTypes(unionType);
            if (caseTypes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No union case types could be discovered for '{unionType}'. " +
                    "Use contract customization to populate UnionCases and set deconstructor/constructor delegates.");
            }

            // Populate UnionCases.
            typeInfo.UnionCases = new List<JsonUnionCaseInfo>(caseTypes.Count);
            foreach (Type ct in caseTypes)
            {
                typeInfo.UnionCases.Add(new JsonUnionCaseInfo(ct));
            }

            // Set up JSON classifier.
            JsonTypeClassifierFactory classifierFactory;
            if (attr.TypeClassifier is not null)
            {
                // Level 2: custom classifier factory from attribute.
                if (!typeof(JsonTypeClassifierFactory).IsAssignableFrom(attr.TypeClassifier))
                {
                    throw new InvalidOperationException(
                        $"The TypeClassifier type '{attr.TypeClassifier}' must derive from JsonTypeClassifierFactory.");
                }

                classifierFactory = (JsonTypeClassifierFactory)Activator.CreateInstance(attr.TypeClassifier)!;
            }
            else
            {
                // Default: structural matching.
                classifierFactory = new JsonStructuralClassifierFactory();
            }

            var context = new JsonTypeClassifierContext(
                unionType,
                caseTypes.ConvertAll(t => new JsonDerivedType(t)),
                typeDiscriminatorPropertyName: null);
            typeInfo.TypeClassifier = classifierFactory.CreateJsonClassifier(context, typeInfo.Options);

            // Build deconstructor and constructor from convention.
            BuildDeconstructorFromConvention(typeInfo, unionType, caseTypes);
            BuildConstructorFromConvention(typeInfo, unionType, caseTypes);

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
                return closedSubtypes;
            }
#endif

            // 2. Implicit conversion operators → union type
            var fromOperators = new List<Type>();
            foreach (MethodInfo method in unionType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == "op_Implicit" &&
                    method.ReturnType == unionType &&
                    method.GetParameters().Length == 1)
                {
                    Type paramType = method.GetParameters()[0].ParameterType;
                    if (!fromOperators.Contains(paramType) &&
                        paramType.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
                    {
                        fromOperators.Add(paramType);
                    }
                }
            }

            if (fromOperators.Count > 0)
            {
                return fromOperators;
            }

            // 3. Single-parameter constructors
            return DiscoverCaseTypesFromConstructors(unionType);
        }

        private static List<Type> DiscoverCaseTypesFromConstructors(Type unionType)
        {
            var caseTypes = new List<Type>();
            foreach (ConstructorInfo ctor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length == 1)
                {
                    Type paramType = Nullable.GetUnderlyingType(parameters[0].ParameterType) ?? parameters[0].ParameterType;
                    if (!caseTypes.Contains(paramType) &&
                        paramType.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
                    {
                        caseTypes.Add(paramType);
                    }
                }
            }

            return caseTypes;
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

            // For IUnion types, use .Value directly.
#if NET11_0_OR_GREATER
            if (typeof(IUnion).IsAssignableFrom(unionType))
            {
                typeInfo.UnionDeconstructor = obj =>
                {
                    object? value = ((IUnion)obj).Value;
                    Type caseType = value?.GetType() ?? typeof(object);
                    return (caseType, value);
                };

                return;
            }
#endif

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
                        if (ct is not null)
                        {
                            object? value = valField.GetValue(obj);
                            return (ct, value);
                        }

                        ThrowHelper.ThrowJsonException($"Unable to deconstruct union instance of type '{unionType}'. The internal case type discriminator is null.");
                        return default;
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
                typeInfo.UnionConstructor = (Type _, object? value) => value!;

                return;
            }

            // Build a map of case type → construction method.
            var constructionMap = new Dictionary<Type, Func<object?, object>>(caseTypes.Count);

            foreach (Type ct in caseTypes)
            {
                if (unionType.IsAssignableFrom(ct))
                {
                    constructionMap[ct] = value => value!;
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
                typeInfo.UnionConstructor = (Type caseType, object? value) =>
                {
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
                if (parameters.Length == 1)
                {
                    Type paramType = Nullable.GetUnderlyingType(parameters[0].ParameterType) ?? parameters[0].ParameterType;
                    if (paramType == parameterType || paramType.IsAssignableFrom(parameterType))
                    {
                        return ctor;
                    }
                }
            }

            return null;
        }
    }
}
