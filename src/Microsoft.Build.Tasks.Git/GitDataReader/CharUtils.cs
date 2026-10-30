// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

namespace Microsoft.Build.Tasks.Git
{
    internal static class CharUtils
    {
        public static char[] AsciiWhitespace = { ' ', '\t', '\n', '\f', '\r', '\v' };
        public static char[] WhitespaceSeparators = { ' ', '\t', '\f', '\v' };

        public static char ToHexDigit(byte nibble)
            => nibble < 10 ? (char)('0' + nibble) : (char)('a' + (nibble - 10));
    }
}
