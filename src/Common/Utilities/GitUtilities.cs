// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Linq;

namespace Microsoft.Build.Tasks.SourceControl
{
    internal static class GitUtilities
    {
        public static bool IsValidObjectId(string? value)
            => value is { Length: 40 or 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
    }
}
