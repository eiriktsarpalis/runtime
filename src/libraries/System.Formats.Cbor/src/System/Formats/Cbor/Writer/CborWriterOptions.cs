// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Formats.Cbor;

/// <summary>Provides the configuration options for the <see cref="CborWriter"/> class.</summary>
public struct CborWriterOptions
{
    // Stored as the actual value plus one so that the default-initialized
    // backing field (0) is interpreted as "unset" and reports the writer's
    // default of CborConformanceMode.Strict, matching the legacy CborWriter
    // constructor default. User-set values round-trip through ConformanceMode.
    private CborConformanceMode _conformanceModePlusOne;
    private int _initialCapacity;
    private int _maxDepth;

    /// <summary>Gets or sets a value that indicates the conformance mode used to validate values written by the writer.</summary>
    /// <value>One of the enumeration values that indicates the conformance mode used by the writer. The default is <see cref="CborConformanceMode.Strict"/>.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="CborConformanceMode"/>.</exception>
    public CborConformanceMode ConformanceMode
    {
        readonly get => _conformanceModePlusOne == default
            ? CborConformanceMode.Strict
            : _conformanceModePlusOne - 1;
        set
        {
            CborConformanceModeHelpers.Validate(value);
            _conformanceModePlusOne = value + 1;
        }
    }

    /// <summary>Gets or sets a value that indicates whether the writer automatically converts indefinite-length encodings into definite-length equivalents.</summary>
    /// <value><see langword="true"/> to automatically convert indefinite-length encodings into definite-length equivalents and to permit use of indefinite-length write APIs in conformance modes that otherwise do not allow it; otherwise, <see langword="false"/>. The default is <see langword="false"/>.</value>
    public bool ConvertIndefiniteLengthEncodings { readonly get; set; }

    /// <summary>Gets or sets a value that indicates whether the writer allows multiple root-level CBOR data items to be written.</summary>
    /// <value><see langword="true"/> to allow multiple root-level CBOR data items to be written; otherwise, <see langword="false"/>. The default is <see langword="false"/>.</value>
    public bool AllowMultipleRootLevelValues { readonly get; set; }

    /// <summary>Gets or sets the initial capacity, in bytes, of the writer's underlying buffer.</summary>
    /// <value>The initial capacity of the writer's underlying buffer. The default value of <c>0</c> indicates that the writer should choose its own initial capacity.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int InitialCapacity
    {
        readonly get => _initialCapacity;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _initialCapacity = value;
        }
    }

    /// <summary>Gets or sets the maximum CBOR document depth that the writer permits.</summary>
    /// <value>The maximum permitted nesting depth of a CBOR document. The default value of <c>0</c> indicates that the writer should use a default limit of 1000.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <remarks>Reaching the maximum depth causes the writer to throw <see cref="InvalidOperationException"/> from the next call that would push a new nested context (for example, <see cref="CborWriter.WriteStartArray(int?)"/> or <see cref="CborWriter.WriteStartMap(int?)"/>).</remarks>
    public int MaxDepth
    {
        readonly get => _maxDepth;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _maxDepth = value;
        }
    }
}
