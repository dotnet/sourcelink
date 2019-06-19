// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.Build.Tasks.Git
{
    internal static class CharUtils
    {
        public static char[] AsciiWhitespace = { ' ', '\t', '\n', '\f', '\r', '\v' };

        public static bool IsHexadecimalDigit(char c)
            => c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f';
    }
}
