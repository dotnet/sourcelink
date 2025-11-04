// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;

namespace Microsoft.Build.Tasks.Git;

/// <summary>
/// Format of object names.
/// https://git-scm.com/docs/hash-function-transition.html#_object_format
/// </summary>
internal enum ObjectFormat
{
    Sha1 = 1,
    Sha256 = 2,
}

internal static class ObjectFormatExtensions
{
    extension(ObjectFormat self)
    {
        public int HashLength
            => self switch
            {
                ObjectFormat.Sha1 => 20,
                ObjectFormat.Sha256 => 32,
                _ => throw new InvalidOperationException()
            };
    }
}
