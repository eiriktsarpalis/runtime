// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        // Pre-encoded metadata properties.
        internal static readonly JsonEncodedText s_metadataId = JsonEncodedText.Encode("$id", encoder: null);
        internal static readonly JsonEncodedText s_metadataRef = JsonEncodedText.Encode("$ref", encoder: null);
        internal static readonly JsonEncodedText s_metadataValues = JsonEncodedText.Encode("$values", encoder: null);
        internal static readonly JsonEncodedText s_metadataType = JsonEncodedText.Encode("$type", encoder: null);

        internal static MetadataPropertyName WriteMetadataForObject(
            JsonConverter jsonConverter,
            ref WriteStack state,
            Utf8JsonWriter writer)
        {
            Debug.Assert(jsonConverter.CanHaveMetadata);
            Debug.Assert(!state.IsContinuation);
            Debug.Assert(state.CurrentContainsMetadata);

            MetadataPropertyName writtenMetadataName = MetadataPropertyName.None;

            if (state.NewReferenceId != null)
            {
                writer.WriteString(s_metadataId, state.NewReferenceId);
                writtenMetadataName |= MetadataPropertyName.Id;
                state.NewReferenceId = null;
            }

            if (state.PolymorphicTypeDiscriminator is string typeId)
            {
                writer.WriteString(s_metadataType, typeId);
                writtenMetadataName |= MetadataPropertyName.Type;
                state.PolymorphicTypeDiscriminator = null;
            }

            return writtenMetadataName;
        }

        internal static MetadataPropertyName WriteMetadataForCollection(
            JsonConverter jsonConverter,
            ref WriteStack state,
            Utf8JsonWriter writer)
        {
            Debug.Assert(jsonConverter.CanHaveMetadata);
            Debug.Assert(!state.IsContinuation);
            Debug.Assert(state.CurrentContainsMetadata);

            // For collections with metadata, we nest the array payload within a JSON object.
            writer.WriteStartObject();

            MetadataPropertyName metadataPropertyName = MetadataPropertyName.None;

            if (state.NewReferenceId != null)
            {
                writer.WriteString(s_metadataId, state.NewReferenceId);
                metadataPropertyName |= MetadataPropertyName.Id;
                state.NewReferenceId = null;
            }

            if (state.PolymorphicTypeDiscriminator != null)
            {
                writer.WriteString(s_metadataType, state.PolymorphicTypeDiscriminator);
                metadataPropertyName |= MetadataPropertyName.Type;
                state.PolymorphicTypeDiscriminator = null;
            }

            // property name containing metadata values
            writer.WritePropertyName(s_metadataValues);

            Debug.Assert(metadataPropertyName != MetadataPropertyName.None);
            return metadataPropertyName;
        }

        /// <summary>
        /// Compute reference id for the next value to be serialized.
        /// </summary>
        internal static bool TryGetReferenceForValue(object currentValue, ref WriteStack state, Utf8JsonWriter writer)
        {
            Debug.Assert(state.NewReferenceId == null);

            string referenceId = state.ReferenceResolver.GetReference(currentValue, out bool alreadyExists);
            Debug.Assert(referenceId != null);

            if (alreadyExists)
            {
                // Instance already serialized, write as { "$ref" : "referenceId" }
                writer.WriteStartObject();
                writer.WriteString(s_metadataRef, referenceId);
                writer.WriteEndObject();
            }
            else
            {
                // New instance, store computed reference id in the state
                state.NewReferenceId = referenceId;
            }

            return alreadyExists;
        }
    }
}
