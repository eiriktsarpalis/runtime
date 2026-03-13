// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace System.Text.Json.Nodes
{
    /// <summary>
    /// A JsonValue encapsulating a primitive value using a built-in converter for the type.
    /// </summary>
    internal sealed class JsonValuePrimitive<TValue> : JsonValue<TValue>
    {
        private readonly JsonConverter<TValue> _converter;
        private readonly JsonValueKind _valueKind;

        public JsonValuePrimitive(TValue value, JsonConverter<TValue> converter, JsonNodeOptions? options) : base(value, options)
        {
            Debug.Assert(TypeIsSupportedPrimitive, $"The type {typeof(TValue)} is not a supported primitive.");
            Debug.Assert(converter is { IsInternalConverter: true, ConverterStrategy: ConverterStrategy.Value });

            _converter = converter;
            _valueKind = DetermineValueKind(value);
        }

        private protected override JsonValueKind GetValueKindCore() => _valueKind;
        internal override JsonNode DeepCloneCore() => new JsonValuePrimitive<TValue>(Value, _converter, Options);

        internal override bool DeepEqualsCore(JsonNode otherNode)
        {
            if (otherNode is JsonValue otherValue && otherValue.TryGetValue(out TValue? v))
            {
                // Because TValue is equatable and otherNode returns a matching
                // type we can short circuit the comparison in this case.
                if (EqualityComparer<TValue>.Default.Equals(Value, v))
                {
                    return true;
                }
            }

            return base.DeepEqualsCore(otherNode);
        }

        public override T GetValue<T>()
        {
            if (!TryGetValue(out T? value))
            {
                ThrowHelper.ThrowInvalidOperationException_NodeUnableToConvert(typeof(TValue), typeof(T));
            }

            return value;
        }

        public override bool TryGetValue<T>([NotNullWhen(true)] out T value)
        {
            // When TValue is JsonNumber and T is object, return the narrowest CLR numeric type
            // for backward compatibility with code that inspects the runtime type of GetValue<object>().
            if (typeof(T) == typeof(object) && typeof(TValue) == typeof(JsonNumber))
            {
                JsonNumber jn = (JsonNumber)(object)Value!;
                return TryGetNarrowestClrType(jn, out value);
            }

            if (Value is T returnValue)
            {
                value = returnValue;
                return true;
            }

            // When TValue is JsonNumber, support conversion to numeric types.
            if (typeof(TValue) == typeof(JsonNumber))
            {
                JsonNumber jn = (JsonNumber)(object)Value!;
                return TryConvertFromJsonNumber(jn, out value);
            }

            // When T is JsonNumber, support conversion from numeric types.
            if (typeof(T) == typeof(JsonNumber) || typeof(T) == typeof(JsonNumber?))
            {
                return TryConvertToJsonNumber(out value);
            }

            value = default!;
            return false;
        }

        private static bool TryConvertFromJsonNumber<T>(JsonNumber jn, [NotNullWhen(true)] out T value)
        {
            bool success;

            if (typeof(T) == typeof(JsonElement) || typeof(T) == typeof(JsonElement?))
            {
                value = (T)(object)JsonElement.Parse(jn.ToString());
                return true;
            }

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                success = jn.TryGetInt32(out int result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(long) || typeof(T) == typeof(long?))
            {
                success = jn.TryGetInt64(out long result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
            {
                success = jn.TryGetDouble(out double result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(short) || typeof(T) == typeof(short?))
            {
                success = jn.TryGetInt16(out short result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
            {
                success = jn.TryGetDecimal(out decimal result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(byte?))
            {
                success = jn.TryGetByte(out byte result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
            {
                success = jn.TryGetSingle(out float result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
            {
                success = jn.TryGetUInt32(out uint result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(ushort) || typeof(T) == typeof(ushort?))
            {
                success = jn.TryGetUInt16(out ushort result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(ulong) || typeof(T) == typeof(ulong?))
            {
                success = jn.TryGetUInt64(out ulong result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(sbyte) || typeof(T) == typeof(sbyte?))
            {
                success = jn.TryGetSByte(out sbyte result);
                value = (T)(object)result;
                return success;
            }

#if NET
            if (typeof(T) == typeof(Half) || typeof(T) == typeof(Half?))
            {
                success = jn.TryGetHalf(out Half result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(Int128) || typeof(T) == typeof(Int128?))
            {
                success = jn.TryGetInt128(out Int128 result);
                value = (T)(object)result;
                return success;
            }

            if (typeof(T) == typeof(UInt128) || typeof(T) == typeof(UInt128?))
            {
                success = jn.TryGetUInt128(out UInt128 result);
                value = (T)(object)result;
                return success;
            }
#endif

            value = default!;
            return false;
        }

        private bool TryConvertToJsonNumber<T>([NotNullWhen(true)] out T value)
        {
            JsonNumber jn;

            switch (Value)
            {
                case byte b: jn = b; break;
                case sbyte sb: jn = sb; break;
                case short s: jn = s; break;
                case ushort us: jn = us; break;
                case int i: jn = i; break;
                case uint ui: jn = ui; break;
                case long l: jn = l; break;
                case ulong ul: jn = ul; break;
                case decimal dec: jn = dec; break;
                case float f when float.IsFinite(f): jn = (JsonNumber)f; break;
                case double dbl when double.IsFinite(dbl): jn = (JsonNumber)dbl; break;
#if NET
                case Half h when Half.IsFinite(h): jn = (JsonNumber)h; break;
                case Int128 i128: jn = i128; break;
                case UInt128 u128: jn = u128; break;
#endif
                default:
                    value = default!;
                    return false;
            }

            value = (T)(object)jn;
            return true;
        }

        private static bool TryGetNarrowestClrType<T>(JsonNumber jn, [NotNullWhen(true)] out T value)
        {
            Debug.Assert(typeof(T) == typeof(object));

            if (jn.IsInteger)
            {
                if (jn.TryGetInt32(out int i))
                {
                    value = (T)(object)i;
                    return true;
                }

                if (jn.TryGetInt64(out long l))
                {
                    value = (T)(object)l;
                    return true;
                }

                if (jn.TryGetUInt64(out ulong ul))
                {
                    value = (T)(object)ul;
                    return true;
                }

                if (jn.TryGetDecimal(out decimal dec))
                {
                    value = (T)(object)dec;
                    return true;
                }
            }
            else
            {
                if (jn.TryGetDecimal(out decimal dec))
                {
                    value = (T)(object)dec;
                    return true;
                }

                if (jn.TryGetDouble(out double d))
                {
                    value = (T)(object)d;
                    return true;
                }
            }

            // For very large numbers that don't fit any CLR type, return JsonNumber itself.
            value = (T)(object)jn;
            return true;
        }

        public override void WriteTo(Utf8JsonWriter writer, JsonSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            JsonConverter<TValue> converter = _converter;
            options ??= s_defaultOptions;

            if (converter.IsInternalConverterForNumberType)
            {
                converter.WriteNumberWithCustomHandling(writer, Value, options.NumberHandling);
            }
            else
            {
                converter.Write(writer, Value, options);
            }
        }
    }
}
