// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace System.Text.Json
{
    internal abstract class JsonSeparatorNamingPolicy : JsonNamingPolicy
    {
        private readonly bool _lowercase;
        private readonly char _separator;

        internal JsonSeparatorNamingPolicy(bool lowercase, char separator)
        {
            Debug.Assert(!char.IsLetter(separator) && !char.IsWhiteSpace(separator));

            _lowercase = lowercase;
            _separator = separator;
        }

        public sealed override string ConvertName(string name)
        {
            if (name is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            return ConvertNameCore(_separator, _lowercase, name.AsSpan());
        }

        private static string ConvertNameCore(char separator, bool lowercase, ReadOnlySpan<char> chars)
        {
            char[]? rentedBuffer = null;

            // While we can't predict the expansion factor of the resultant string,
            // start with a buffer that is at least 20% larger than the input.
            int initialBufferLength = (int)(1.20 * chars.Length);
            Span<char> destination = initialBufferLength <= JsonConstants.StackallocCharThreshold
                ? stackalloc char[JsonConstants.StackallocCharThreshold]
                : (rentedBuffer = ArrayPool<char>.Shared.Rent(initialBufferLength));

            SeparatorState state = SeparatorState.NotStarted;
            int charsWritten = 0;

            scoped Span<char> tempBuffer = stackalloc char[2];

            while (!chars.IsEmpty)
            {
                // TODO replace with Rune APIs once a ns2.0 package becomes available
                // https://github.com/dotnet/runtime/issues/52947
                UnicodeCategory category = GetNextUnicodeCategory(chars, out int charsConsumed);

                if (category is UnicodeCategory.UppercaseLetter)
                {
                    switch (state)
                    {
                        case SeparatorState.NotStarted:
                            break;

                        case SeparatorState.OtherCharacter:
                        case SeparatorState.SpaceSeparator:
                            // An uppercase letter following a sequence of lowercase letters or spaces
                            // denotes the start of a new grouping: emit a separator character.
                            WriteChar(separator, ref destination);
                            break;

                        case SeparatorState.UppercaseLetter:
                            // We are reading through a sequence of two or more uppercase letters.
                            // Uppercase letters are grouped together with the exception of the
                            // final letter, assuming it is followed by additional characters.
                            // For example, the value 'XMLReader' should render as 'xml_reader'.
                            if (charsConsumed + 1 < chars.Length)
                            {
                                UnicodeCategory nextCategory = GetNextUnicodeCategory(chars.Slice(charsConsumed), out _);
                                if (nextCategory != UnicodeCategory.UppercaseLetter && chars[charsConsumed + 1] != separator)
                                {
                                    // This is the last uppercase letter in the sequence,
                                    // emit a separator before handling it.
                                    WriteChar(separator, ref destination);
                                }
                            }
                            break;

                        default:
                            Debug.Fail($"Unexpected state {state}");
                            break;
                    }

                    scoped ReadOnlySpan<char> charsToWrite = chars.Slice(0, charsConsumed);
                    if (lowercase)
                    {
                        int written = charsToWrite.ToLowerInvariant(tempBuffer);
                        Debug.Assert(written == charsConsumed);
                        charsToWrite = tempBuffer.Slice(0, written);
                    }

                    WriteChars(charsToWrite, ref destination);
                    state = SeparatorState.UppercaseLetter;
                }
                else if (category is UnicodeCategory.SpaceSeparator)
                {
                    // Space characters are trimmed from the start and end of the input string
                    // but are normalized to separator characters if between letters.
                    if (state != SeparatorState.NotStarted)
                    {
                        state = SeparatorState.SpaceSeparator;
                    }
                }
                else if (chars[0] == separator)
                {
                    // Json.NET compat: reset state if the separator character is encountered in the input string.
                    WriteChar(separator, ref destination);
                    state = SeparatorState.NotStarted;
                }
                else
                {
                    // Handle all remaining character categories
                    if (state is SeparatorState.SpaceSeparator)
                    {
                        // Normalize preceding spaces to one separator.
                        WriteChar(separator, ref destination);
                    }

                    scoped ReadOnlySpan<char> charsToWrite = chars.Slice(0, charsConsumed);
                    if (!lowercase)
                    {
                        int written = charsToWrite.ToUpperInvariant(tempBuffer);
                        Debug.Assert(written == charsConsumed);
                        charsToWrite = tempBuffer.Slice(0, written);
                    }

                    WriteChars(charsToWrite, ref destination);
                    state = SeparatorState.OtherCharacter;
                }

                chars = chars.Slice(charsConsumed);
            }

            string result = destination.Slice(0, charsWritten).ToString();

            if (rentedBuffer is not null)
            {
                destination.Slice(0, charsWritten).Clear();
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }

            return result;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void WriteChar(char value, ref Span<char> destination)
            {
                if (charsWritten == destination.Length)
                {
                    ExpandBuffer(ref destination);
                }

                destination[charsWritten++] = value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void WriteChars(scoped ReadOnlySpan<char> chars, ref Span<char> destination)
            {
                if (charsWritten + chars.Length > destination.Length)
                {
                    ExpandBuffer(ref destination);
                }

                chars.CopyTo(destination.Slice(charsWritten));
                charsWritten += chars.Length;
            }

            void ExpandBuffer(ref Span<char> destination)
            {
                int newSize = checked(destination.Length * 2);
                char[] newBuffer = ArrayPool<char>.Shared.Rent(newSize);
                destination.CopyTo(newBuffer);

                if (rentedBuffer is not null)
                {
                    destination.Slice(0, charsWritten).Clear();
                    ArrayPool<char>.Shared.Return(rentedBuffer);
                }

                rentedBuffer = newBuffer;
                destination = rentedBuffer;
            }
        }

        private static UnicodeCategory GetNextUnicodeCategory(ReadOnlySpan<char> buffer, out int charsConsumed)
        {
            UnicodeCategory category = char.GetUnicodeCategory(buffer[0]);
            if (category is UnicodeCategory.Surrogate &&
                buffer.Length > 1 && char.IsSurrogatePair(buffer[0], buffer[1]))
            {
                charsConsumed = 2;
#if NETCOREAPP
                int codepoint = char.ConvertToUtf32(buffer[0], buffer[1]);
                return CharUnicodeInfo.GetUnicodeCategory(codepoint);
#else
                string surrogate = buffer.Slice(0, 2).ToString();
                return CharUnicodeInfo.GetUnicodeCategory(surrogate, 0);
#endif
            }

            charsConsumed = 1;
            return category;
        }

        private enum SeparatorState
        {
            NotStarted,
            UppercaseLetter,
            SpaceSeparator,
            OtherCharacter,
        }
    }
}
