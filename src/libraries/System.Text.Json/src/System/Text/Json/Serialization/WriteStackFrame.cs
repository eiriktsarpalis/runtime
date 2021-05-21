// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json
{
    [DebuggerDisplay("ConverterStrategy.{JsonTypeInfo.PropertyInfoForTypeInfo.ConverterStrategy}, {JsonTypeInfo.Type.Name}")]
    internal struct WriteStackFrame
    {
        /// <summary>
        /// The enumerator for resumable collections.
        /// </summary>
        public IEnumerator? CollectionEnumerator;

        /// <summary>
        /// The enumerator for resumable async disposables.
        /// </summary>
        public IAsyncDisposable? AsyncEnumerator;

        /// <summary>
        /// The current stackframe has suspended serialization due to a pending task,
        /// stored in the <see cref="WriteStack.PendingTask"/> property.
        /// </summary>
        public bool AsyncEnumeratorIsPendingCompletion;

        /// <summary>
        /// The original JsonPropertyInfo that is not changed. It contains all properties.
        /// </summary>
        /// <remarks>
        /// For objects, it is either the actual (real) JsonPropertyInfo or the <see cref="JsonTypeInfo.PropertyInfoForTypeInfo"/> for the class.
        /// For collections, it is the <see cref="JsonTypeInfo.PropertyInfoForTypeInfo"/> for the class and current element.
        /// </remarks>
        public JsonPropertyInfo? DeclaredJsonPropertyInfo;

        /// <summary>
        /// Used when processing extension data dictionaries.
        /// </summary>
        public bool IgnoreDictionaryKeyPolicy;

        /// <summary>
        /// The class (POCO or IEnumerable) that is being populated.
        /// </summary>
        public JsonTypeInfo JsonTypeInfo;

        /// <summary>
        /// Validation state for a class.
        /// </summary>
        public int OriginalDepth;

        // Class-level state for collections.
        public bool ProcessedStartToken;
        public bool ProcessedEndToken;

        /// <summary>
        /// Property or Element state.
        /// </summary>
        public StackFramePropertyState PropertyState;

        /// <summary>
        /// The enumerator index for resumable collections.
        /// </summary>
        public int EnumeratorIndex;

        // This is used for re-entry cases for exception handling.
        public string? JsonPropertyNameAsString;

        // Preserve Reference
        public MetadataPropertyName MetadataPropertyName;

        // Indicates that the next converter is polymorphic and must serialize a type id
        public string? TaggedPolymorphicTypeId;

        /// <summary>
        /// The run-time JsonPropertyInfo that contains the TypeInfo and ConverterBase for polymorphic scenarios.
        /// </summary>
        /// <remarks>
        /// For objects, it is the <see cref="JsonTypeInfo.PropertyInfoForTypeInfo"/> for the class and current property.
        /// For collections, it is the <see cref="JsonTypeInfo.PropertyInfoForTypeInfo"/> for the class and current element.
        /// </remarks>
        private JsonPropertyInfo? CachedPolymorphicJsonPropertyInfo;

        /// <summary>
        /// Dictates how <see cref="CachedPolymorphicJsonPropertyInfo"/> is to be consumed.
        /// </summary>
        /// <remarks>
        /// If true we are dispatching serialization to a polymorphic converter that should consume it.
        /// If false it is simply a value we are caching for performance.
        /// </remarks>
        public bool IsPolymorphicReEntryStarted;

        // Whether to use custom number handling.
        public JsonNumberHandling? NumberHandling;

        public void EndDictionaryElement()
        {
            PropertyState = StackFramePropertyState.None;
        }

        public void EndProperty()
        {
            DeclaredJsonPropertyInfo = null!;
            JsonPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;
        }

        ///// <summary>
        ///// Return the property that contains the correct polymorphic properties including
        ///// the ConverterStrategy and ConverterBase.
        ///// </summary>
        //public JsonPropertyInfo GetPolymorphicJsonPropertyInfo()
        //{
        //    return IsPolymorphicReEntryStarted ? CachedPolymorphicJsonPropertyInfo! : DeclaredJsonPropertyInfo!;
        //}

        /// <summary>
        /// Initializes the state for polymorphic cases and returns the appropriate converter.
        /// </summary>
        public JsonConverter InitializeReEntry(Type type, JsonSerializerOptions options)
        {
            Debug.Assert(!IsPolymorphicReEntryStarted);
            // For perf, avoid the dictionary lookup in GetOrAddClass() for every element of a collection
            // if the current element is the same type as the previous element.
            if (CachedPolymorphicJsonPropertyInfo?.RuntimePropertyType != type)
            {
                JsonTypeInfo typeInfo = options.GetOrAddClass(type);
                CachedPolymorphicJsonPropertyInfo = typeInfo.PropertyInfoForTypeInfo;
            }

            JsonTypeInfo = CachedPolymorphicJsonPropertyInfo.RuntimeTypeInfo;
            DeclaredJsonPropertyInfo = JsonTypeInfo.PropertyInfoForTypeInfo;
            NumberHandling ??= DeclaredJsonPropertyInfo.NumberHandling;
            IsPolymorphicReEntryStarted = true;
            return CachedPolymorphicJsonPropertyInfo.ConverterBase;
        }

        public JsonConverter GetPolymorphicConverterForResumedContinuation()
        {
            Debug.Assert(IsPolymorphicReEntryStarted && CachedPolymorphicJsonPropertyInfo is not null);
            return CachedPolymorphicJsonPropertyInfo.ConverterBase;
        }

        public void Reset()
        {
            // Do not reset CachedPolymorphicJsonPropertyInfo
            CollectionEnumerator = null;
            EnumeratorIndex = 0;
            AsyncEnumerator = null;
            AsyncEnumeratorIsPendingCompletion = false;
            IgnoreDictionaryKeyPolicy = false;
            IsPolymorphicReEntryStarted = false;
            TaggedPolymorphicTypeId = null;
            JsonTypeInfo = null!;
            OriginalDepth = 0;
            ProcessedStartToken = false;
            ProcessedEndToken = false;
            MetadataPropertyName = MetadataPropertyName.NoMetadata;

            EndProperty();
        }
    }
}
