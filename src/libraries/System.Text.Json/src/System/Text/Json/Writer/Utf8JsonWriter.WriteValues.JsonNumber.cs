// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace System.Text.Json
{
    public sealed partial class Utf8JsonWriter
    {
        /// <summary>
        /// Writes the <see cref="JsonNumber"/> value (as a JSON number) as an element of a JSON array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid JSON being written (while validation is enabled).
        /// </exception>
        public void WriteNumberValue(JsonNumber value)
        {
            int maxLen = value.GetMaxUtf8Length();
            byte[]? rented = null;
            Span<byte> buffer = maxLen <= 256
                ? stackalloc byte[256]
                : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

            try
            {
                int written = value.WriteUtf8(buffer);
                WriteNumberValue(buffer.Slice(0, written));
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// Writes the property name and <see cref="JsonNumber"/> value (as a JSON number) as part of a name/value pair of a JSON object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid JSON being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNumber(string propertyName, JsonNumber value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteNumber(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the property name and <see cref="JsonNumber"/> value (as a JSON number) as part of a name/value pair of a JSON object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid JSON being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNumber(ReadOnlySpan<char> propertyName, JsonNumber value)
        {
            int maxLen = value.GetMaxUtf8Length();
            byte[]? rented = null;
            Span<byte> buffer = maxLen <= 256
                ? stackalloc byte[256]
                : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

            try
            {
                int written = value.WriteUtf8(buffer);
                WriteNumber(propertyName, buffer.Slice(0, written));
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// Writes the property name and <see cref="JsonNumber"/> value (as a JSON number) as part of a name/value pair of a JSON object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid JSON being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, JsonNumber value)
        {
            int maxLen = value.GetMaxUtf8Length();
            byte[]? rented = null;
            Span<byte> buffer = maxLen <= 256
                ? stackalloc byte[256]
                : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

            try
            {
                int written = value.WriteUtf8(buffer);
                WriteNumber(utf8PropertyName, buffer.Slice(0, written));
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// Writes the property name and <see cref="JsonNumber"/> value (as a JSON number) as part of a name/value pair of a JSON object.
        /// </summary>
        /// <param name="propertyName">The pre-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid JSON being written (while validation is enabled).
        /// </exception>
        public void WriteNumber(JsonEncodedText propertyName, JsonNumber value)
        {
            int maxLen = value.GetMaxUtf8Length();
            byte[]? rented = null;
            Span<byte> buffer = maxLen <= 256
                ? stackalloc byte[256]
                : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

            try
            {
                int written = value.WriteUtf8(buffer);
                WriteNumber(propertyName, buffer.Slice(0, written));
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// Writes the <see cref="JsonNumber"/> value (as a JSON string) as an element of a JSON array.
        /// </summary>
        internal void WriteNumberValueAsString(JsonNumber value)
        {
            WriteStringValue(value.ToString());
        }
    }
}
