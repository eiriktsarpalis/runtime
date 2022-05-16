// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Reflection;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// Represents a strongly-typed property to prevent boxing and to create a direct delegate to the getter\setter.
    /// </summary>
    /// <typeparamref name="T"/> is the <see cref="JsonConverter{T}.TypeToConvert"/> for either the property's converter,
    /// or a type's converter, if the current instance is a <see cref="JsonTypeInfo.PropertyInfoForTypeInfo"/>.
    internal sealed class JsonPropertyInfo<T> : JsonPropertyInfo
    {
        /// <summary>
        /// Returns true if the property's converter is external (a user's custom converter)
        /// and the type to convert is not the same as the declared property type (polymorphic).
        /// Used to determine whether to perform additional validation on the value returned by the
        /// converter on deserialization.
        /// </summary>
        private bool _converterIsExternalAndPolymorphic;

        // Since a converter's TypeToConvert (which is the T value in this type) can be different than
        // the property's type, we track that and whether the property type can be null.
        private bool _propertyTypeEqualsTypeToConvert;

        private Func<object, object?>? _untypedGet;
        private Action<object, object?>? _untypedSet;

        // We do not need to worry about invalidating _untypedGet/_untypedSet
        // because these are only set during construction
        // If these ever became public we'd need to ensure respective value is set to null here
        private Func<object, T>? TypedGetValue { get; set; }

        private Action<object, T>? TypedSetValue { get; set; }

        private protected override Func<object, object?>? UntypedGetValue
        {
            get
            {
                // We use local here so that we don't capture 'this'
                Func<object, T>? typedGetValue = TypedGetValue;
                return _untypedGet ??= typedGetValue == null ? null : (o) => typedGetValue(o);
            }
            set
            {
                _untypedGet = value;
                TypedGetValue = value == null ? null : (o) => (T)value(o)!;
                HasGetter = value != null;
            }
        }

        private protected override Action<object, object?>? UntypedSetValue
        {
            get
            {
                // We use local here so that we don't capture 'this'
                Action<object, T>? typedSetValue = TypedSetValue;
                return _untypedSet ??= typedSetValue == null ? null : (o, v) => typedSetValue(o, (T)v!);
            }
            set
            {
                _untypedSet = value;
                TypedSetValue = value == null ? null : (o, v) => value(o, v);
                HasSetter = value != null;
            }
        }

        internal override object? DefaultValue => default(T);

        internal JsonConverter<T> TypedEffectiveConverter { get; set; } = null!;

        internal override void Initialize(
            Type declaringType,
            Type declaredPropertyType,
            ConverterStrategy converterStrategy,
            MemberInfo? memberInfo,
            bool isVirtual,
            JsonConverter converter,
            JsonIgnoreCondition? ignoreCondition,
            JsonSerializerOptions options,
            JsonTypeInfo? jsonTypeInfo = null,
            bool isCustomProperty = false)
        {
            Debug.Assert(converter != null);

            PropertyType = declaredPropertyType;
            ConverterStrategy = converterStrategy;
            if (jsonTypeInfo != null)
            {
                JsonTypeInfo = jsonTypeInfo;
            }

            NonCustomConverter = converter;
            Options = options;
            DeclaringType = declaringType;
            MemberInfo = memberInfo;
            IsVirtual = isVirtual;
            IgnoreCondition = ignoreCondition;

            if (memberInfo != null)
            {
                switch (memberInfo)
                {
                    case PropertyInfo propertyInfo:
                        {
                            bool useNonPublicAccessors = GetAttribute<JsonIncludeAttribute>(propertyInfo) != null;

                            MethodInfo? getMethod = propertyInfo.GetMethod;
                            if (getMethod != null && (getMethod.IsPublic || useNonPublicAccessors))
                            {
                                HasGetter = true;
                                TypedGetValue = options.MemberAccessorStrategy.CreatePropertyGetter<T>(propertyInfo);
                            }

                            MethodInfo? setMethod = propertyInfo.SetMethod;
                            if (setMethod != null && (setMethod.IsPublic || useNonPublicAccessors))
                            {
                                HasSetter = true;
                                TypedSetValue = options.MemberAccessorStrategy.CreatePropertySetter<T>(propertyInfo);
                            }

                            MemberType = MemberTypes.Property;

                            break;
                        }

                    case FieldInfo fieldInfo:
                        {
                            Debug.Assert(fieldInfo.IsPublic);

                            HasGetter = true;
                            TypedGetValue = options.MemberAccessorStrategy.CreateFieldGetter<T>(fieldInfo);

                            if (!fieldInfo.IsInitOnly)
                            {
                                HasSetter = true;
                                TypedSetValue = options.MemberAccessorStrategy.CreateFieldSetter<T>(fieldInfo);
                            }

                            MemberType = MemberTypes.Field;

                            break;
                        }

                    default:
                        {
                            Debug.Fail($"Invalid memberInfo type: {memberInfo.GetType().FullName}");
                            break;
                        }
                }

                GetPolicies();
            }
            else if (!isCustomProperty)
            {
                IsForTypeInfo = true;
                HasGetter = true;
                HasSetter = true;
            }
        }

        internal void InitializeForSourceGen(JsonSerializerOptions options, JsonPropertyInfoValues<T> propertyInfo)
        {
            Options = options;
            ClrName = propertyInfo.PropertyName;

            // Property name settings.
            if (propertyInfo.JsonPropertyName != null)
            {
                Name = propertyInfo.JsonPropertyName;
            }
            else if (options.PropertyNamingPolicy == null)
            {
                Name = ClrName;
            }
            else
            {
                Name = options.PropertyNamingPolicy.ConvertName(ClrName);
                if (Name == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(DeclaringType, this);
                }
            }

            SrcGen_IsPublic = propertyInfo.IsPublic;
            SrcGen_HasJsonInclude = propertyInfo.HasJsonInclude;
            SrcGen_IsExtensionData = propertyInfo.IsExtensionData;
            PropertyType = typeof(T);

            JsonTypeInfo propertyTypeInfo = propertyInfo.PropertyTypeInfo;
            Type declaringType = propertyInfo.DeclaringType;

            JsonConverter<T>? typedCustomConverter = propertyInfo.Converter;
            CustomConverter = typedCustomConverter;

            JsonConverter<T>? typedNonCustomConverter = propertyTypeInfo.Converter as JsonConverter<T>;
            NonCustomConverter = typedNonCustomConverter;
            JsonConverter<T>? typedEffectiveConverter = typedCustomConverter ?? typedNonCustomConverter;
            if (typedEffectiveConverter == null)
            {
                throw new InvalidOperationException(SR.Format(SR.ConverterForPropertyMustBeValid, declaringType, ClrName, typeof(T)));
            }

            if (propertyInfo.IgnoreCondition == JsonIgnoreCondition.Always)
            {
                IsIgnored = true;
                Debug.Assert(!CanSerialize);
                Debug.Assert(!CanDeserialize);
            }
            else
            {
                TypedGetValue = propertyInfo.Getter!;
                TypedSetValue = propertyInfo.Setter;
                HasGetter = TypedGetValue != null;
                HasSetter = TypedSetValue != null;
                JsonTypeInfo = propertyTypeInfo;
                DeclaringType = declaringType;
                IgnoreCondition = propertyInfo.IgnoreCondition;
                MemberType = propertyInfo.IsProperty ? MemberTypes.Property : MemberTypes.Field;
                ConverterStrategy = typedEffectiveConverter.ConverterStrategy;
                NumberHandling = propertyInfo.NumberHandling;
            }
        }

        internal override void Configure(JsonTypeInfo typeInfo)
        {
            base.Configure(typeInfo);

            if (!IsForTypeInfo && !IsIgnored)
            {
                _converterIsExternalAndPolymorphic = !EffectiveConverter.IsInternalConverter && PropertyType != EffectiveConverter.TypeToConvert;
                _propertyTypeEqualsTypeToConvert = typeof(T) == PropertyType;
            }
        }

        internal override void SetEffectiveConverter()
        {
            JsonConverter? customConverter = CustomConverter;
            if (customConverter != null)
            {
                customConverter = Options.ExpandFactoryConverter(customConverter, PropertyType);
                JsonSerializerOptions.CheckConverterNullabilityIsSameAsPropertyType(customConverter, PropertyType);
            }

            JsonConverter effectiveConverter = customConverter ?? NonCustomConverter ?? Options.GetConverterForType(PropertyType);
            if (effectiveConverter.TypeToConvert == PropertyType)
            {
                EffectiveConverter = effectiveConverter;
            }
            else
            {
                EffectiveConverter = effectiveConverter.CreateCastingConverter<T>();
            }
        }

        internal override JsonConverter EffectiveConverter
        {
            get
            {
                return TypedEffectiveConverter;
            }
            set
            {
                TypedEffectiveConverter = (JsonConverter<T>)value;
            }
        }

        internal override object? GetValueAsObject(object obj)
        {
            if (IsForTypeInfo)
            {
                return obj;
            }

            Debug.Assert(HasGetter);
            return TypedGetValue!(obj);
        }

        internal override bool GetMemberAndWriteJson(object obj, ref WriteStack state, Utf8JsonWriter writer)
        {
            T value = TypedGetValue!(obj);

            if (ShouldSerialize != null)
            {
                if (!ShouldSerialize(obj, value))
                {
                    // We return true here.
                    // False means that there is not enough data.
                    return true;
                }
            }

            if (
#if NETCOREAPP
                !typeof(T).IsValueType && // treated as a constant by recent versions of the JIT.
#else
                !TypedEffectiveConverter.IsValueType &&
#endif
                Options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.IgnoreCycles &&
                value is not null &&
                !state.IsContinuation &&
                // .NET types that are serialized as JSON primitive values don't need to be tracked for cycle detection e.g: string.
                ConverterStrategy != ConverterStrategy.Value &&
                state.ReferenceResolver.ContainsReferenceForCycleDetection(value))
            {
                // If a reference cycle is detected, treat value as null.
                value = default!;
                Debug.Assert(value == null);
            }

            if (IgnoreDefaultValuesOnWrite)
            {
                // If value is null, it is a reference type or nullable<T>.
                if (value == null)
                {
                    return true;
                }

                if (!PropertyTypeCanBeNull)
                {
                    if (_propertyTypeEqualsTypeToConvert)
                    {
                        // The converter and property types are the same, so we can use T for EqualityComparer<>.
                        if (EqualityComparer<T>.Default.Equals(default, value))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        Debug.Assert(JsonTypeInfo.Type == PropertyType);

                        // Use a late-bound call to EqualityComparer<DeclaredPropertyType>.
                        if (JsonTypeInfo.DefaultValueHolder.IsDefaultValue(value))
                        {
                            return true;
                        }
                    }
                }
            }

            if (value == null)
            {
                Debug.Assert(PropertyTypeCanBeNull);

                if (TypedEffectiveConverter.HandleNullOnWrite)
                {
                    if (state.Current.PropertyState < StackFramePropertyState.Name)
                    {
                        state.Current.PropertyState = StackFramePropertyState.Name;
                        writer.WritePropertyNameSection(EscapedNameSection);
                    }

                    int originalDepth = writer.CurrentDepth;
                    TypedEffectiveConverter.Write(writer, value, Options);
                    if (originalDepth != writer.CurrentDepth)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterWrite(TypedEffectiveConverter);
                    }
                }
                else
                {
                    writer.WriteNullSection(EscapedNameSection);
                }

                return true;
            }
            else
            {
                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    state.Current.PropertyState = StackFramePropertyState.Name;
                    writer.WritePropertyNameSection(EscapedNameSection);
                }

                return TypedEffectiveConverter.TryWrite(writer, value, Options, ref state);
            }
        }

        internal override bool GetMemberAndWriteJsonExtensionData(object obj, ref WriteStack state, Utf8JsonWriter writer)
        {
            bool success;
            T value = TypedGetValue!(obj);

            if (ShouldSerialize != null)
            {
                if (!ShouldSerialize(obj, value))
                {
                    // We return true here.
                    // False means that there is not enough data.
                    return true;
                }
            }

            if (value == null)
            {
                success = true;
            }
            else
            {
                success = TypedEffectiveConverter.TryWriteDataExtensionProperty(writer, value, Options, ref state);
            }

            return success;
        }

        internal override bool ReadJsonAndSetMember(object obj, ref ReadStack state, ref Utf8JsonReader reader)
        {
            bool success;

            bool isNullToken = reader.TokenType == JsonTokenType.Null;
            if (isNullToken && !TypedEffectiveConverter.HandleNullOnRead && !state.IsContinuation)
            {
                if (!PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypedEffectiveConverter.TypeToConvert);
                }

                Debug.Assert(default(T) == null);

                if (!IgnoreDefaultValuesOnRead)
                {
                    T? value = default;
                    TypedSetValue!(obj, value!);
                }

                success = true;
            }
            else if (TypedEffectiveConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling == null)
            {
                // CanUseDirectReadOrWrite == false when using streams
                Debug.Assert(!state.IsContinuation);

                if (!isNullToken || !IgnoreDefaultValuesOnRead || !PropertyTypeCanBeNull)
                {
                    // Optimize for internal converters by avoiding the extra call to TryRead.
                    T? fastValue = TypedEffectiveConverter.Read(ref reader, PropertyType, Options);
                    TypedSetValue!(obj, fastValue!);
                }

                success = true;
            }
            else
            {
                success = true;
                if (!isNullToken || !IgnoreDefaultValuesOnRead || !PropertyTypeCanBeNull || state.IsContinuation)
                {
                    success = TypedEffectiveConverter.TryRead(ref reader, PropertyType, Options, ref state, out T? value);
                    if (success)
                    {
#if !DEBUG
                        if (_converterIsExternalAndPolymorphic)
#endif
                        {
                            if (value != null)
                            {
                                Type typeOfValue = value.GetType();
                                if (!PropertyType.IsAssignableFrom(typeOfValue))
                                {
                                    ThrowHelper.ThrowInvalidCastException_DeserializeUnableToAssignValue(typeOfValue, PropertyType);
                                }
                            }
                            else if (!PropertyTypeCanBeNull)
                            {
                                ThrowHelper.ThrowInvalidOperationException_DeserializeUnableToAssignNull(PropertyType);
                            }
                        }

                        TypedSetValue!(obj, value!);
                    }
                }
            }

            return success;
        }

        internal override bool ReadJsonAsObject(ref ReadStack state, ref Utf8JsonReader reader, out object? value)
        {
            bool success;
            bool isNullToken = reader.TokenType == JsonTokenType.Null;
            if (isNullToken && !TypedEffectiveConverter.HandleNullOnRead && !state.IsContinuation)
            {
                if (!PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypedEffectiveConverter.TypeToConvert);
                }

                value = default(T);
                success = true;
            }
            else
            {
                // Optimize for internal converters by avoiding the extra call to TryRead.
                if (TypedEffectiveConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling == null)
                {
                    // CanUseDirectReadOrWrite == false when using streams
                    Debug.Assert(!state.IsContinuation);

                    value = TypedEffectiveConverter.Read(ref reader, PropertyType, Options);
                    success = true;
                }
                else
                {
                    success = TypedEffectiveConverter.TryRead(ref reader, PropertyType, Options, ref state, out T? typedValue);
                    value = typedValue;
                }
            }

            return success;
        }

        internal override void SetExtensionDictionaryAsObject(object obj, object? extensionDict)
        {
            Debug.Assert(HasSetter);
            T typedValue = (T)extensionDict!;
            TypedSetValue!(obj, typedValue);
        }
    }
}
