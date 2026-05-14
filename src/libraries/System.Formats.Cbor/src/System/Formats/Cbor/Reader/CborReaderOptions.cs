// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Formats.Cbor;

/// <summary>Provides the configuration options for the <see cref="CborReader"/> class.</summary>
public struct CborReaderOptions
{
    // Stored as the actual value plus one so that the default-initialized
    // backing field (0) is interpreted as "unset" and reports the reader's
    // default of CborConformanceMode.Strict, matching the legacy CborReader
    // constructor default. User-set values round-trip through ConformanceMode.
    private CborConformanceMode _conformanceModePlusOne;
    private int _maxDepth;

    /// <summary>Gets or sets a value that indicates the conformance mode used to validate values read by the reader.</summary>
    /// <value>One of the enumeration values that indicates the conformance mode used by the reader. The default is <see cref="CborConformanceMode.Strict"/>.</value>
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

    /// <summary>Gets or sets a value that indicates whether the reader allows multiple root-level CBOR data items to be read.</summary>
    /// <value><see langword="true"/> to allow multiple root-level CBOR data items to be read; otherwise, <see langword="false"/>. The default is <see langword="false"/>.</value>
    public bool AllowMultipleRootLevelValues { readonly get; set; }

    /// <summary>Gets or sets the maximum CBOR document depth that the reader permits.</summary>
    /// <value>The maximum permitted nesting depth of a CBOR document. The default value of <c>0</c> indicates that the reader should use a default limit of 1000.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <remarks>Reaching the maximum depth causes the reader to throw <see cref="InvalidOperationException"/> from the next call that would push a new nested context (for example, <see cref="CborReader.ReadStartArray"/> or <see cref="CborReader.ReadStartMap"/>).</remarks>
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
