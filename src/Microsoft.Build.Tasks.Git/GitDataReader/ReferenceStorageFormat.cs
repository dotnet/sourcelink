// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Build.Tasks.Git;

/// <summary>
/// The format of storage for references.
/// </summary>
internal enum ReferenceStorageFormat
{
    /// <summary>
    /// References stored as files in refs directory or packed-refs.
    /// </summary>
    LooseFiles = 0,

    /// <summary>
    /// References stored in RefTable (<see cref="GitRefTableReader"/>).
    /// </summary>
    RefTable = 1,
}
