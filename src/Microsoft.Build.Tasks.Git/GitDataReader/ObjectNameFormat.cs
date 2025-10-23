// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Build.Tasks.Git;

internal enum ObjectNameFormat
{
    Sha1,
    Sha256
}

internal static class ObjectNameFormatExtensions
{
    extension(ObjectNameFormat format)
    {
        public int HashSize
            => format switch
            {
                ObjectNameFormat.Sha1 => 20,
                ObjectNameFormat.Sha256 => 32,
                _ => throw new InvalidOperationException()
            };
    }
}


