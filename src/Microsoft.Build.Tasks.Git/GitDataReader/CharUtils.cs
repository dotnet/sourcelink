// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;

namespace Microsoft.Build.Tasks.Git
{
    internal static class CharUtils
    {
        public static char[] AsciiWhitespace = { ' ', '\t', '\n', '\f', '\r', '\v' };
        public static char[] WhitespaceSeparators = { ' ', '\t', '\f', '\v' };

        public static bool IsHexadecimalDigit(char c)
            => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';
    }
}
