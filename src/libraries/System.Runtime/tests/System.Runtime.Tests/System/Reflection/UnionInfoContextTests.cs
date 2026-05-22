// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace System.Reflection.Tests
{
    public class UnionInfoContextTests
    {
        #region Fixtures

        // Canonical convention-based union written long-hand (no `union` keyword)
        // so the tests exercise the structural recognition rules directly.
        public class IntOrString
        {
            private readonly object? _value;
            public IntOrString(int value) => _value = value;
            public IntOrString(string? value) => _value = value;
            public object? Value => _value;
        }

        // Same as IntOrString but carries [Union]; HasUnionAttribute should be true.
        [Union]
        public class AnnotatedIntOrString
        {
            private readonly object? _value;
            public AnnotatedIntOrString(int value) => _value = value;
            public AnnotatedIntOrString(string value) => _value = value;
            public object? Value => _value;
        }

        // A single nullable-reference-type case: AdmitsNull should be true.
        public class NullableStringUnion
        {
            private readonly object? _value;
            public NullableStringUnion(int value) => _value = value;
            public NullableStringUnion(string? value) => _value = value;
            public object? Value => _value;
        }

        // Two ctor overloads — `int` and `int?` — should produce a single case
        // for `int` with AdmitsNull = true.
        public class NullableIntDedup
        {
            private readonly object? _value;
            public NullableIntDedup(int value) => _value = value;
            public NullableIntDedup(int? value) => _value = value;
            public object? Value => _value;
        }

        // A non-nullable reference-type case only: AdmitsNull must be false.
        public class NonNullableRef
        {
            private readonly object? _value;
            public NonNullableRef(string value) => _value = value;
            public NonNullableRef(int value) => _value = value;
            public object? Value => _value;
        }

        // TryGetValue overloads — both must be picked up.
        public class WithTryGetValue
        {
            private readonly object? _value;
            public WithTryGetValue(int value) => _value = value;
            public WithTryGetValue(string value) => _value = value;
            public object? Value => _value;
            public bool TryGetValue(out int value)
            {
                if (_value is int i) { value = i; return true; }
                value = 0;
                return false;
            }
            public bool TryGetValue(out string value)
            {
                if (_value is string s) { value = s; return true; }
                value = string.Empty;
                return false;
            }
        }

        // Inheritance hierarchy across declared cases — most-derived first dispatch.
        public class Animal { }
        public class Dog : Animal { }
        public class Poodle : Dog { }
        public class HierarchyUnion
        {
            private readonly object? _value;
            public HierarchyUnion(Animal value) => _value = value;
            public HierarchyUnion(Dog value) => _value = value;
            public object? Value => _value;
        }

        // IUnionMembers nested-interface provider shape.
        public class ProviderShape : ProviderShape.IUnionMembers
        {
            public interface IUnionMembers
            {
                static ProviderShape Create(int value) => new(value);
                static ProviderShape Create(string value) => new(value);
                object? Value { get; }
            }

            private readonly object? _value;
            private ProviderShape(object? value) => _value = value;
            public object? Value => _value;
        }

        // Negative fixtures.
        public class NoValueProperty
        {
            public NoValueProperty(int value) => Number = value;
            public int Number { get; }
        }

        public class WrongValueType
        {
            public WrongValueType(int value) => Value = value;
            public int Value { get; }
        }

        public class NoCreationMembers
        {
            public object? Value => null;
        }

        public class IndexedValueProperty
        {
            public object? this[int i] => null;
            public IndexedValueProperty(int v) { _ = v; }
            public object? Value(int i) => null;
        }

        // Mixed-case fixture for accessor round-trip.
        public class MixedCases
        {
            private readonly object? _value;
            public MixedCases(int value) => _value = value;
            public MixedCases(string? value) => _value = value;
            public MixedCases(Animal value) => _value = value;
            public object? Value => _value;
        }

        // A value-type union variant.
        public struct StructUnion
        {
            private readonly object? _value;
            public StructUnion(int value) => _value = value;
            public StructUnion(string value) => _value = value;
            public object? Value => _value;
        }

        #endregion

        #region IsUnion

        [Theory]
        [InlineData(typeof(IntOrString), true)]
        [InlineData(typeof(AnnotatedIntOrString), true)]
        [InlineData(typeof(NullableIntDedup), true)]
        [InlineData(typeof(WithTryGetValue), true)]
        [InlineData(typeof(HierarchyUnion), true)]
        [InlineData(typeof(ProviderShape), true)]
        [InlineData(typeof(StructUnion), true)]
        [InlineData(typeof(NoValueProperty), false)]
        [InlineData(typeof(WrongValueType), false)]
        [InlineData(typeof(NoCreationMembers), false)]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(string), false)]
        public void IsUnion_ReturnsExpected(Type type, bool expected)
        {
            Assert.Equal(expected, UnionInfoContext.IsUnion(type));
        }

        [Fact]
        public void IsUnion_NullType_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => UnionInfoContext.IsUnion(null!));
        }

        #endregion

        #region Create / TryCreate

        [Fact]
        public void Create_NullType_Throws()
        {
            UnionInfoContext ctx = new();
            Assert.Throws<ArgumentNullException>(() => ctx.Create(null!));
        }

        [Fact]
        public void Create_NonUnion_Throws()
        {
            UnionInfoContext ctx = new();
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ctx.Create(typeof(int)));
            Assert.Equal("type", ex.ParamName);
        }

        [Fact]
        public void TryCreate_NonUnion_ReturnsFalse()
        {
            UnionInfoContext ctx = new();
            Assert.False(ctx.TryCreate(typeof(int), out UnionInfo? info));
            Assert.Null(info);
        }

        [Fact]
        public void TryCreate_NullType_Throws()
        {
            UnionInfoContext ctx = new();
            Assert.Throws<ArgumentNullException>(() => ctx.TryCreate(null!, out _));
        }

        [Fact]
        public void Create_CachesResult()
        {
            UnionInfoContext ctx = new();
            UnionInfo info1 = ctx.Create(typeof(IntOrString));
            UnionInfo info2 = ctx.Create(typeof(IntOrString));
            Assert.Same(info1, info2);
        }

        [Fact]
        public void Create_PopulatesValuePropertyAndAttribute()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(AnnotatedIntOrString));

            Assert.Equal(typeof(AnnotatedIntOrString), info.Type);
            Assert.Equal(typeof(AnnotatedIntOrString), info.UnionDefiningType);
            Assert.True(info.HasUnionAttribute);
            Assert.NotNull(info.ValueProperty);
            Assert.Equal("Value", info.ValueProperty.Name);
            Assert.Equal(typeof(object), info.ValueProperty.PropertyType);
        }

        [Fact]
        public void Create_HasUnionAttribute_FalseWhenAbsent()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            Assert.False(info.HasUnionAttribute);
        }

        #endregion

        #region UnionCaseInfo

        [Fact]
        public void Cases_AreInDeclarationOrder()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            Assert.Equal(2, info.Cases.Count);
            Assert.Equal(typeof(int), info.Cases[0].CaseType);
            Assert.Equal(typeof(string), info.Cases[1].CaseType);
        }

        [Fact]
        public void Cases_DeduplicatedByCaseType()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(NullableIntDedup));
            // Two ctors (int and int?) collapse to a single case for `int`.
            Assert.Single(info.Cases);
            Assert.Equal(typeof(int), info.Cases[0].CaseType);
            Assert.True(info.Cases[0].AdmitsNull);
        }

        [Fact]
        public void Cases_NullableValueTypeUnwrapped()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(NullableIntDedup));
            Assert.Equal(typeof(int), info.Cases[0].CaseType);
        }

        [Fact]
        public void Cases_AdmitsNull_TrueForNullableReferenceType()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(NullableStringUnion));
            UnionCaseInfo stringCase = Assert.Single(info.Cases, c => c.CaseType == typeof(string));
            Assert.True(stringCase.AdmitsNull);
        }

        [Fact]
        public void Cases_AdmitsNull_OredAcrossValueTypeOverloads()
        {
            // Two ctors `int` and `int?` collapse to a single case for `int` with
            // AdmitsNull = true. This is the only way the OR logic is exercised in
            // C# (you cannot declare two single-parameter ctors with the same
            // reference type but different nullability annotations).
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(NullableIntDedup));
            UnionCaseInfo intCase = Assert.Single(info.Cases);
            Assert.Equal(typeof(int), intCase.CaseType);
            Assert.True(intCase.AdmitsNull);
        }

        [Fact]
        public void Cases_NonNullableReference_AdmitsNullFalse()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(NonNullableRef));
            UnionCaseInfo stringCase = Assert.Single(info.Cases, c => c.CaseType == typeof(string));
            Assert.False(stringCase.AdmitsNull);
        }

        [Fact]
        public void Case_CreationMemberIsConstructor()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            foreach (UnionCaseInfo c in info.Cases)
            {
                Assert.IsAssignableFrom<ConstructorInfo>(c.CreationMember);
            }
        }

        [Fact]
        public void Case_DeclaringUnion_PointsBack()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            foreach (UnionCaseInfo c in info.Cases)
            {
                Assert.Same(info, c.DeclaringUnion);
            }
        }

        [Fact]
        public void Case_TryGetValueMethod_DiscoveredForMatchingOverload()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(WithTryGetValue));
            foreach (UnionCaseInfo c in info.Cases)
            {
                Assert.NotNull(c.TryGetValueMethod);
                Assert.Equal("TryGetValue", c.TryGetValueMethod!.Name);
                ParameterInfo[] parameters = c.TryGetValueMethod.GetParameters();
                Assert.Single(parameters);
                Assert.True(parameters[0].IsOut);
                Assert.Equal(c.CaseType, parameters[0].ParameterType.GetElementType());
            }
        }

        [Fact]
        public void Case_TryGetValueMethod_NullWhenAbsent()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            foreach (UnionCaseInfo c in info.Cases)
            {
                Assert.Null(c.TryGetValueMethod);
            }
        }

        #endregion

        #region IUnionMembers provider

        [Fact]
        public void ProviderShape_UnionDefiningTypeIsNestedInterface()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(ProviderShape));

            Assert.Equal(typeof(ProviderShape), info.Type);
            Assert.Equal(typeof(ProviderShape.IUnionMembers), info.UnionDefiningType);
            Assert.Equal(2, info.Cases.Count);
            Assert.Equal(typeof(int), info.Cases[0].CaseType);
            Assert.Equal(typeof(string), info.Cases[1].CaseType);
        }

        [Fact]
        public void ProviderShape_CreationMembersAreStaticCreateMethods()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(ProviderShape));
            foreach (UnionCaseInfo c in info.Cases)
            {
                MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(c.CreationMember);
                Assert.True(method.IsStatic);
                Assert.Equal("Create", method.Name);
                Assert.Equal(typeof(ProviderShape), method.ReturnType);
            }
        }

        #endregion

        #region UnionAccessors<TUnion>

        [Fact]
        public void Accessors_Create_NullInfo_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => UnionAccessors<IntOrString>.Create(null!));
        }

        [Fact]
        public void Accessors_Create_TypeMismatch_Throws()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => UnionAccessors<WithTryGetValue>.Create(info));
            Assert.Equal("info", ex.ParamName);
        }

        [Fact]
        public void Accessors_Info_RoundTrips()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            UnionAccessors<IntOrString> accessors = UnionAccessors<IntOrString>.Create(info);
            Assert.Same(info, accessors.Info);
        }

        [Fact]
        public void Accessors_Deconstructor_ReturnsCaseTypeAndValue()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            UnionAccessors<IntOrString> accessors = UnionAccessors<IntOrString>.Create(info);

            (Type? caseType, object? value) = accessors.Deconstructor(new IntOrString(42));
            Assert.Equal(typeof(int), caseType);
            Assert.Equal(42, value);

            (caseType, value) = accessors.Deconstructor(new IntOrString("hello"));
            Assert.Equal(typeof(string), caseType);
            Assert.Equal("hello", value);
        }

        [Fact]
        public void Accessors_Deconstructor_NullReferenceUnion_ReturnsNullNull()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            UnionAccessors<IntOrString> accessors = UnionAccessors<IntOrString>.Create(info);

            (Type? caseType, object? value) = accessors.Deconstructor(null!);
            Assert.Null(caseType);
            Assert.Null(value);
        }

        [Fact]
        public void Accessors_Deconstructor_UsesTryGetValueWhenPresent()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(WithTryGetValue));
            UnionAccessors<WithTryGetValue> accessors = UnionAccessors<WithTryGetValue>.Create(info);

            (Type? caseType, object? value) = accessors.Deconstructor(new WithTryGetValue(7));
            Assert.Equal(typeof(int), caseType);
            Assert.Equal(7, value);
        }

        [Fact]
        public void Accessors_Constructor_RoundTripsCases()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            UnionAccessors<IntOrString> accessors = UnionAccessors<IntOrString>.Create(info);

            IntOrString fromInt = accessors.Constructor(typeof(int), 99);
            Assert.Equal(99, fromInt.Value);

            IntOrString fromString = accessors.Constructor(typeof(string), "world");
            Assert.Equal("world", fromString.Value);
        }

        [Fact]
        public void Accessors_Constructor_InfersCaseTypeFromValue()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            UnionAccessors<IntOrString> accessors = UnionAccessors<IntOrString>.Create(info);

            IntOrString result = accessors.Constructor(null, 5);
            Assert.Equal(5, result.Value);
        }

        [Fact]
        public void Accessors_Constructor_NullValue_NoNullableCase_Throws()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(NonNullableRef));
            UnionAccessors<NonNullableRef> accessors = UnionAccessors<NonNullableRef>.Create(info);

            Assert.Throws<InvalidOperationException>(() => accessors.Constructor(typeof(string), null));
        }

        [Fact]
        public void Accessors_Constructor_NullValue_RoutesToNullableCase()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(NullableStringUnion));
            UnionAccessors<NullableStringUnion> accessors =
                UnionAccessors<NullableStringUnion>.Create(info);

            NullableStringUnion result = accessors.Constructor(typeof(string), null);
            Assert.Null(result.Value);
        }

        [Fact]
        public void Accessors_RoundTrip_ThroughDeconstructorAndConstructor()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(MixedCases));
            UnionAccessors<MixedCases> accessors = UnionAccessors<MixedCases>.Create(info);

            object?[] payloads = { 1, "abc", new Animal() };
            foreach (object payload in payloads)
            {
                MixedCases original = accessors.Constructor(payload!.GetType(), payload);
                (Type? caseType, object? value) = accessors.Deconstructor(original);
                Assert.Equal(payload, value);
                Assert.NotNull(caseType);
            }
        }

        [Fact]
        public void Accessors_ResolveCase_ReturnsExactMatch()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(HierarchyUnion));
            UnionAccessors<HierarchyUnion> accessors = UnionAccessors<HierarchyUnion>.Create(info);

            Assert.Equal(typeof(Animal), accessors.ResolveCase(typeof(Animal))!.CaseType);
            Assert.Equal(typeof(Dog), accessors.ResolveCase(typeof(Dog))!.CaseType);
        }

        [Fact]
        public void Accessors_ResolveCase_MostDerivedFirst()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(HierarchyUnion));
            UnionAccessors<HierarchyUnion> accessors = UnionAccessors<HierarchyUnion>.Create(info);

            // Poodle : Dog : Animal. With both Dog and Animal declared, Poodle should
            // resolve to the nearest declared ancestor — Dog.
            UnionCaseInfo? resolved = accessors.ResolveCase(typeof(Poodle));
            Assert.NotNull(resolved);
            Assert.Equal(typeof(Dog), resolved!.CaseType);
        }

        [Fact]
        public void Accessors_Deconstructor_MostDerivedFirst()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(HierarchyUnion));
            UnionAccessors<HierarchyUnion> accessors = UnionAccessors<HierarchyUnion>.Create(info);

            Poodle poodle = new();
            (Type? caseType, object? value) = accessors.Deconstructor(new HierarchyUnion(poodle));
            Assert.Equal(typeof(Dog), caseType);
            Assert.Same(poodle, value);
        }

        [Fact]
        public void Accessors_ResolveCase_NullType_Throws()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(IntOrString));
            UnionAccessors<IntOrString> accessors = UnionAccessors<IntOrString>.Create(info);

            Assert.Throws<ArgumentNullException>(() => accessors.ResolveCase(null!));
        }

        #endregion

        #region Struct union

        [Fact]
        public void StructUnion_IsRecognized()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(StructUnion));
            Assert.Equal(2, info.Cases.Count);
            Assert.Equal(typeof(int), info.Cases[0].CaseType);
            Assert.Equal(typeof(string), info.Cases[1].CaseType);
        }

        [Fact]
        public void StructUnion_Accessors_RoundTrip()
        {
            UnionInfoContext ctx = new();
            UnionInfo info = ctx.Create(typeof(StructUnion));
            UnionAccessors<StructUnion> accessors = UnionAccessors<StructUnion>.Create(info);

            StructUnion value = accessors.Constructor(typeof(int), 7);
            (Type? caseType, object? boxed) = accessors.Deconstructor(value);
            Assert.Equal(typeof(int), caseType);
            Assert.Equal(7, boxed);
        }

        #endregion
    }
}
