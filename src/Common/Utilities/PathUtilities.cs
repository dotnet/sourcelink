// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Microsoft.Build.Tasks.SourceControl
{
    internal static class PathUtilities
    {
        internal static bool IsUnixLikePlatform => Path.DirectorySeparatorChar == '/';

        private static readonly char[] s_directorySeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private const string UncPrefix = @"\\";
        private const string UnixRoot = "/";

        public static string[] Split(string fullPath)
        {
            var result = fullPath.Split(s_directorySeparators, StringSplitOptions.RemoveEmptyEntries);

            if (Path.DirectorySeparatorChar == '\\')
            {
                if (fullPath.StartsWith(UncPrefix, StringComparison.Ordinal))
                {
                    var list = new List<string> { UncPrefix };
                    list.AddRange(result);
                    result = list.ToArray();
                }
            }
            else if (fullPath.StartsWith(UnixRoot, StringComparison.Ordinal))
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

        /// <summary>
        /// True if the path is an absolute path (rooted to drive or network share)
        /// </summary>
        public static bool IsAbsolute([NotNullWhen(true)] string? path)
        {
            if (path is not { Length: >0 })
            {
                return false;
            }

            if (IsUnixLikePlatform)
            {
                return path[0] == Path.DirectorySeparatorChar;
            }

            // "C:\"
            if (IsDriveRootedAbsolutePath(path))
            {
                // Including invalid paths (e.g. "*:\")
                return true;
            }

            // "\\machine\share"
            // Including invalid/incomplete UNC paths (e.g. "\\goo")
            return path.Length >= 2 &&
                IsDirectorySeparator(path[0]) &&
                IsDirectorySeparator(path[1]);
        }

        /// <summary>
        /// True if the character is the platform directory separator character or the alternate directory separator.
        /// </summary>
        public static bool IsDirectorySeparator(char c)
            => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        /// <summary>
        /// Returns true if given path is absolute and starts with a drive specification ("C:\").
        /// </summary>
        private static bool IsDriveRootedAbsolutePath(string path)
        {
            Debug.Assert(!IsUnixLikePlatform);
            return path.Length >= 3 && path[1] == Path.VolumeSeparatorChar && IsDirectorySeparator(path[2]);
        }
    }
}
