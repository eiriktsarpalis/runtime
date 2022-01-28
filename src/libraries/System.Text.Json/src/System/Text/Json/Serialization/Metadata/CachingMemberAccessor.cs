// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace System.Text.Json.Serialization.Metadata
{
    internal sealed partial class CachingMemberAccessor : MemberAccessor
    {
        private readonly MemberAccessor _source;
        private readonly ConcurrentMruCache<(string id, Type declaringType, MemberInfo? member), object?> _cache = new(maxCapacity: 3);

        public CachingMemberAccessor(MemberAccessor source)
        {
            _source = source;
        }

        private TResult GetOrAdd<TResult>((string id, Type declaringType, MemberInfo? member) key, Func<(string id, Type declaringType, MemberInfo? member), MemberAccessor, TResult> factory)
            where TResult : class?
        {
            return (TResult)_cache.GetOrAdd(key, factory, _source)!;
        }

        public override Action<TCollection, object?> CreateAddMethodDelegate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TCollection>()
            => GetOrAdd((nameof(CreateAddMethodDelegate), typeof(TCollection), null), static (_, source) => source.CreateAddMethodDelegate<TCollection>());

        public override JsonTypeInfo.ConstructorDelegate? CreateConstructor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type classType)
            => GetOrAdd((nameof(CreateConstructor), classType, null), static (key, source) => source.CreateConstructor(key.declaringType));

        public override Func<object, TProperty> CreateFieldGetter<TProperty>(FieldInfo fieldInfo)
            => GetOrAdd((nameof(CreateFieldGetter), typeof(TProperty), fieldInfo), static (key, source) => source.CreateFieldGetter<TProperty>((FieldInfo)key.member!));

        public override Action<object, TProperty> CreateFieldSetter<TProperty>(FieldInfo fieldInfo)
            => GetOrAdd((nameof(CreateFieldSetter), typeof(TProperty), fieldInfo), static (key, source) => source.CreateFieldSetter<TProperty>((FieldInfo)key.member!));

        [RequiresUnreferencedCode(IEnumerableConverterFactoryHelpers.ImmutableConvertersUnreferencedCodeMessage)]
        public override Func<IEnumerable<KeyValuePair<TKey, TValue>>, TCollection> CreateImmutableDictionaryCreateRangeDelegate<TCollection, TKey, TValue>()
            => GetOrAdd((nameof(CreateImmutableDictionaryCreateRangeDelegate), typeof((TCollection, TKey, TValue)), null), static (_, source) => source.CreateImmutableDictionaryCreateRangeDelegate<TCollection, TKey, TValue>());

        [RequiresUnreferencedCode(IEnumerableConverterFactoryHelpers.ImmutableConvertersUnreferencedCodeMessage)]
        public override Func<IEnumerable<TElement>, TCollection> CreateImmutableEnumerableCreateRangeDelegate<TCollection, TElement>()
            => GetOrAdd((nameof(CreateImmutableEnumerableCreateRangeDelegate), typeof((TCollection, TElement)), null), static (_, source) => source.CreateImmutableEnumerableCreateRangeDelegate<TCollection, TElement>());

        public override Func<object[], T>? CreateParameterizedConstructor<T>(ConstructorInfo constructor)
            => GetOrAdd((nameof(CreateParameterizedConstructor), typeof(T), constructor), static (key, source) => source.CreateParameterizedConstructor<T>((ConstructorInfo)key.member!));

        public override JsonTypeInfo.ParameterizedConstructorDelegate<T, TArg0, TArg1, TArg2, TArg3>? CreateParameterizedConstructor<T, TArg0, TArg1, TArg2, TArg3>(ConstructorInfo constructor)
            => GetOrAdd((nameof(CreateParameterizedConstructor), typeof(T), constructor), static (key, source) => source.CreateParameterizedConstructor<T, TArg0, TArg1, TArg2, TArg3>((ConstructorInfo)key.member!));

        public override Func<object, TProperty> CreatePropertyGetter<TProperty>(PropertyInfo propertyInfo)
            => GetOrAdd((nameof(CreatePropertyGetter), typeof(TProperty), propertyInfo), static (key, source) => source.CreatePropertyGetter<TProperty>((PropertyInfo)key.member!));

        public override Action<object, TProperty> CreatePropertySetter<TProperty>(PropertyInfo propertyInfo)
            => GetOrAdd((nameof(CreatePropertySetter), typeof(TProperty), propertyInfo), static (key, source) => source.CreatePropertySetter<TProperty>((PropertyInfo)key.member!));
    }
}
