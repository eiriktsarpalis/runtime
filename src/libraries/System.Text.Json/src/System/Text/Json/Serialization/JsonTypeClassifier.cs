// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// A delegate that classifies a JSON payload to determine the concrete type it corresponds to.
    /// Used by both union types and polymorphic types with custom classifiers.
    /// </summary>
    /// <param name="reader">A reader positioned at the start of the JSON value to classify.
    /// The delegate should not advance the reader — use a struct copy for lookahead.</param>
    /// <returns>The <see cref="Type"/> of the matching type, or <see langword="null"/> if no match is found.</returns>
    public delegate Type? JsonTypeClassifier(ref Utf8JsonReader reader);
}
