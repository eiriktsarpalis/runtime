// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json
{
    [DebuggerDisplay("Path:{JsonPath()} Current: ConverterStrategy.{Current.JsonTypeInfo.PropertyInfoForTypeInfo.ConverterStrategy}, {Current.JsonTypeInfo.Type.Name}")]
    internal struct ReadStack
    {
        private ReadStackFrame[] _stack;

        /// <summary>
        /// Index to the current stackframe.
        /// </summary>
        private int _currentIndex;
        private bool _isFirstFramePushed;

        internal static readonly char[] SpecialCharacters = { '.', ' ', '\'', '/', '"', '[', ']', '(', ')', '\t', '\n', '\r', '\f', '\b', '\\', '\u0085', '\u2028', '\u2029' };

        /// <summary>
        /// The number of stack frames when the continuation started.
        /// </summary>
        private int _continuationCount;


        // State cache when deserializing objects with parameterized constructors.
        private List<ArgumentState>? _ctorArgStateCache;

        /// <summary>
        /// Bytes consumed in the current loop.
        /// </summary>
        public long BytesConsumed;

        //// A field is used instead of a property to avoid value semantics.
        public ref ReadStackFrame Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(_stack is not null);
                return ref _stack[_currentIndex];
            }
        }

        public bool IsContinuation => _continuationCount != 0;
        public bool IsLastContinuation => _continuationCount == _currentIndex;

        /// <summary>
        /// Internal flag to let us know that we need to read ahead in the inner read loop.
        /// </summary>
        public bool ReadAhead;

        /// <summary>
        /// Flag indicating that we have a pending type discriminator read-ahead operation.
        /// </summary>
        public bool IsConverterReadAheadOperationPendingBytes;

        // The bag of preservable references.
        public ReferenceResolver ReferenceResolver;

        /// <summary>
        /// Whether we need to read ahead in the inner read loop.
        /// </summary>
        public bool SupportContinuation;

        /// <summary>
        /// Whether we can read without the need of saving state for stream and preserve references cases.
        /// </summary>
        public bool UseFastPath;

        //private void AddCurrent()
        //{
        //    if (_stack == null)
        //    {
        //        _stack = new List<ReadStackFrame>();
        //    }

        //    if (_currentIndex > _stack.Count)
        //    {
        //        // Need to allocate a new array element.
        //        _stack.Add(Current);
        //    }
        //    else
        //    {
        //        // Use a previously allocated slot.
        //        _stack[_currentIndex - 1] = Current;
        //    }

        //    _currentIndex++;
        //}
        private void EnsurePushCapacity()
        {
            Debug.Assert(_stack is not null);

            if (_currentIndex == _stack.Length - 1)
            {
                Array.Resize(ref _stack, 2 * _stack.Length);
            }
            else
            {
                _stack[_currentIndex + 1] = default;
            }
        }

        public void Initialize(Type type, JsonSerializerOptions options, bool supportContinuation)
        {
            JsonTypeInfo jsonTypeInfo = options.GetOrAddClassForRootType(type);
            Initialize(jsonTypeInfo, supportContinuation);
        }

        internal void Initialize(JsonTypeInfo jsonTypeInfo, bool supportContinuation = false)
        {
            _stack = new ReadStackFrame[5];

            Current.JsonTypeInfo = jsonTypeInfo;
            // The initial JsonPropertyInfo will be used to obtain the converter.
            Current.JsonPropertyInfo = jsonTypeInfo.PropertyInfoForTypeInfo;
            Current.NumberHandling = Current.JsonPropertyInfo.NumberHandling;

            JsonSerializerOptions options = jsonTypeInfo.Options;
            bool preserveReferences = options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.Preserve;
            if (preserveReferences)
            {
                ReferenceResolver = options.ReferenceHandler!.CreateResolver(writing: false);
            }

            SupportContinuation = supportContinuation;
            UseFastPath = !supportContinuation && !preserveReferences;
        }

        //public bool IsNestedPolymorphicReEntryStarted => _count > 1 ? _previous[_count - 2].IsPolymorphicReEntryStarted : Current.IsPolymorphicReEntryStarted;

        ///// <summary>Used to obtain the JsonTypeInfo for the current converter _before_ the call to ReadStack.Push() has been made.</summary>
        //public JsonTypeInfo PeekNextJsonTypeInfo()
        //{
        //    if (_continuationCount == 0)
        //    {
        //        if (_count > 0 || Current.IsPolymorphicReEntryStarted)
        //        {
        //            return Current.GetPolymorphicJsonTypeInfo();
        //        }
        //    }

        //    return Current.JsonTypeInfo;
        //}

        public void Push()
        {
            if (_continuationCount == 0)
            {
                if (!_isFirstFramePushed)
                {
                    // First push operation must be a no-op
                    _isFirstFramePushed = true;
                }
                else
                {
                    EnsurePushCapacity();

                    JsonTypeInfo jsonTypeInfo;
                    JsonNumberHandling? numberHandling = Current.NumberHandling;
                    ConverterStrategy converterStrategy = Current.JsonTypeInfo.PropertyInfoForTypeInfo.ConverterStrategy;

                    if (converterStrategy == ConverterStrategy.Object)
                    {
                        if (Current.JsonPropertyInfo != null)
                        {
                            jsonTypeInfo = Current.JsonPropertyInfo.RuntimeTypeInfo;
                        }
                        else
                        {
                            jsonTypeInfo = Current.CtorArgumentState!.JsonParameterInfo!.RuntimeTypeInfo;
                        }
                    }
                    else if (converterStrategy == ConverterStrategy.Value)
                    {
                        // Although ConverterStrategy.Value doesn't push, a custom custom converter may re-enter serialization.
                        jsonTypeInfo = Current.JsonPropertyInfo!.RuntimeTypeInfo;
                    }
                    else
                    {
                        Debug.Assert(((ConverterStrategy.Enumerable | ConverterStrategy.Dictionary) & converterStrategy) != 0);
                        jsonTypeInfo = Current.JsonTypeInfo.ElementTypeInfo!;
                    }

                    _currentIndex++;

                    Current.JsonTypeInfo = jsonTypeInfo;
                    Current.JsonPropertyInfo = jsonTypeInfo.PropertyInfoForTypeInfo;
                    // Allow number handling on property to win over handling on type.
                    Current.NumberHandling = numberHandling ?? Current.JsonPropertyInfo.NumberHandling;
                }
            }
            else
            {
                // A continuation; adjust the index.
                if (!_isFirstFramePushed)
                {
                    // No need for a push since there is only one stack frame.
                    Debug.Assert(_currentIndex == 0);
                    _isFirstFramePushed = true;
                    if (_continuationCount == 1)
                    {
                        _continuationCount = 0;
                    }
                }
                else
                {
                    if (++_currentIndex == _continuationCount - 1)
                    {
                        _continuationCount = 0;
                    }
                }
                //else if (_continuationCount == 1)
                //{
                //    // No need for a push since there is only one stack frame.
                //    Debug.Assert(_currentIndex == 1);
                //    _continuationCount = 0;
                //}
                //else
                //{
                //    Current = _stack[_currentIndex - 1];

                //    // Check if we are done.
                //    if (_currentIndex == _continuationCount)
                //    {
                //        _continuationCount = 0;
                //    }
                //    else
                //    {
                //        _currentIndex++;
                //    }
            }

            SetConstructorArgumentState();
        }

        public void Pop(bool success)
        {
            Debug.Assert(_isFirstFramePushed is true);

            if (!success)
            {
                // Check if we need to initialize the continuation.
                if (_continuationCount == 0)
                {
                    _continuationCount = _currentIndex + 1;
                    //if (_currentIndex == 1)
                    //{
                    //    // No need for a continuation since there is only one stack frame.
                    //    _continuationCount = 1;
                    //}
                    //else
                    //{
                    //    AddCurrent();
                    //    _currentIndex--;
                    //    _continuationCount = _currentIndex;
                    //    _currentIndex--;
                    //    Current = _stack[_currentIndex - 1];
                    //}

                    //return;
                }

                //if (_continuationCount == 1)
                //{
                //    // No need for a pop since there is only one stack frame.
                //    Debug.Assert(_currentIndex == 1);
                //    return;
                //}

                //// Update the list entry to the current value.
                //_stack[_currentIndex - 1] = Current;

                //Debug.Assert(_currentIndex > 0);
            }
            else
            {
                Debug.Assert(_continuationCount == 0);
            }

            if (_currentIndex == 0)
            {
                _isFirstFramePushed = false;
            }
            else
            {
                _currentIndex--;
            }

            SetConstructorArgumentState();
        }

        // Return a JSONPath using simple dot-notation when possible. When special characters are present, bracket-notation is used:
        // $.x.y[0].z
        // $['PropertyName.With.Special.Chars']
        public string JsonPath()
        {
            StringBuilder sb = new StringBuilder("$");

            // If a continuation, always report back full stack.
            int count = Math.Max(_currentIndex + 1, _continuationCount);

            for (int i = 0; i < count - 1; i++)
            {
                AppendStackFrame(sb, ref _stack[i]);
            }

            if (_continuationCount == 0)
            {
                AppendStackFrame(sb, ref Current);
            }

            return sb.ToString();

            static void AppendStackFrame(StringBuilder sb, ref ReadStackFrame frame)
            {
                // Append the property name.
                string? propertyName = GetPropertyName(ref frame);
                AppendPropertyName(sb, propertyName);

                if (frame.JsonTypeInfo != null && frame.IsProcessingEnumerable())
                {
                    IEnumerable? enumerable = (IEnumerable?)frame.ReturnValue;
                    if (enumerable == null)
                    {
                        return;
                    }

                    // For continuation scenarios only, before or after all elements are read, the exception is not within the array.
                    if (frame.ObjectState == StackFrameObjectState.None ||
                        frame.ObjectState == StackFrameObjectState.CreatedObject ||
                        frame.ObjectState == StackFrameObjectState.ReadElements)
                    {
                        sb.Append('[');
                        sb.Append(GetCount(enumerable));
                        sb.Append(']');
                    }
                }
            }

           static int GetCount(IEnumerable enumerable)
            {
                if (enumerable is ICollection collection)
                {
                    return collection.Count;
                }

                int count = 0;
                IEnumerator enumerator = enumerable.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    count++;
                }

                return count;
            }

            static void AppendPropertyName(StringBuilder sb, string? propertyName)
            {
                if (propertyName != null)
                {
                    if (propertyName.IndexOfAny(SpecialCharacters) != -1)
                    {
                        sb.Append(@"['");
                        sb.Append(propertyName);
                        sb.Append(@"']");
                    }
                    else
                    {
                        sb.Append('.');
                        sb.Append(propertyName);
                    }
                }
            }

            static string? GetPropertyName(ref ReadStackFrame frame)
            {
                string? propertyName = null;

                // Attempt to get the JSON property name from the frame.
                byte[]? utf8PropertyName = frame.JsonPropertyName;
                if (utf8PropertyName == null)
                {
                    if (frame.JsonPropertyNameAsString != null)
                    {
                        // Attempt to get the JSON property name set manually for dictionary
                        // keys and KeyValuePair property names.
                        propertyName = frame.JsonPropertyNameAsString;
                    }
                    else
                    {
                        // Attempt to get the JSON property name from the JsonPropertyInfo or JsonParameterInfo.
                        utf8PropertyName = frame.JsonPropertyInfo?.NameAsUtf8Bytes ??
                            frame.CtorArgumentState?.JsonParameterInfo?.NameAsUtf8Bytes;
                    }
                }

                if (utf8PropertyName != null)
                {
                    propertyName = JsonHelpers.Utf8GetString(utf8PropertyName);
                }

                return propertyName;
            }
        }

        /// <summary>
        /// Initializes the state for polymorphic cases and returns the appropriate converter.
        /// </summary>
        public JsonConverter InitializeReEntry(Type type, JsonSerializerOptions options)
        {
            JsonConverter converter = Current.InitializeReEntry(type, options);
            SetConstructorArgumentState();
            return converter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetConstructorArgumentState()
        {
            if (Current.JsonTypeInfo.ParameterCount > 0)
            {
                // A zero index indicates a new stack frame.
                if (Current.CtorArgumentStateIndex == 0)
                {
                    if (_ctorArgStateCache == null)
                    {
                        _ctorArgStateCache = new List<ArgumentState>();
                    }

                    var newState = new ArgumentState();
                    _ctorArgStateCache.Add(newState);

                    (Current.CtorArgumentStateIndex, Current.CtorArgumentState) = (_ctorArgStateCache.Count, newState);
                }
                else
                {
                    Current.CtorArgumentState = _ctorArgStateCache![Current.CtorArgumentStateIndex - 1];
                }
            }
        }
    }
}
