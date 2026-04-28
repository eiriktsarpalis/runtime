// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that a class or enum type is closed. A closed class cannot be directly derived from
    /// outside its assembly. A closed enum cannot take on values beyond its declared members.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public sealed class ClosedAttribute : Attribute
    {
    }
}
