// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Tasks
{
    internal static class PathUtilities
    {
        private static readonly char[] s_directorySeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private const string UncPrefix = @"\\\\";
        private const string UnixRoot = "/";

        public static string[] Split(string path)
        {
            var result = path.Split(s_directorySeparators, StringSplitOptions.RemoveEmptyEntries);

            if (Path.DirectorySeparatorChar == '\\' && path.StartsWith(UncPrefix, StringComparison.Ordinal))
            {
                var list = new List<string> { UncPrefix };
                list.AddRange(result);
                result = list.ToArray();
            }
            else if (path.StartsWith(UnixRoot, StringComparison.Ordinal))
            {
                var list = new List<string> { UnixRoot };
                list.AddRange(result);
                result = list.ToArray();
            }

            return result;
        }

        public static bool EndsWithSeparator(this string path)
        {
            char last = path[path.Length - 1];
            return last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar;
        }

        public static string EndWithSeparator(this string path)
            => path.EndsWithSeparator() ? path : path + Path.DirectorySeparatorChar;

        public static string EndWithSeparator(this string path, char separator)
            => path.EndsWithSeparator() ? path : path + separator;
    }
}