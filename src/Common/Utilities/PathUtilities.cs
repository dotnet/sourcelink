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
            var last = path[path.Length - 1];
            return last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar;
        }

        public static string EndWithSeparator(this string path)
            => path.EndsWithSeparator() ? path : path + Path.DirectorySeparatorChar;

        public static string EndWithSeparator(this string path, char separator)
            => path.EndsWithSeparator() ? path : path + separator;

        /// <summary>
        /// Determines whether <paramref name="path"/> is fully qualified (independent of the process current
        /// directory and drive). Unlike <see cref="Path.IsPathRooted"/>, rejects Windows drive-relative
        /// (<c>C:foo</c>) and root-relative (<c>\foo</c>) paths. Polyfills <c>Path.IsPathFullyQualified</c>,
        /// which is unavailable on .NET Framework.
        /// </summary>
        public static bool IsPathFullyQualified(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

#if NETFRAMEWORK
            // Mirrors System.IO.PathInternal.IsPartiallyQualified (Windows).
            if (path.Length < 2)
            {
                // A single character (or empty) can't be fixed; it is relative.
                return false;
            }

            if (IsDirectorySeparator(path[0]))
            {
                // A leading separator is only fully qualified for UNC (\\server) or device (\\?\) paths.
                return path[1] == '?' || IsDirectorySeparator(path[1]);
            }

            // The only other fixed form is drive-qualified: "X:\" or "X:/".
            return path.Length >= 3
                && path[1] == Path.VolumeSeparatorChar
                && IsDirectorySeparator(path[2])
                && IsValidDriveChar(path[0]);
#else
            return Path.IsPathFullyQualified(path);
#endif
        }

#if NETFRAMEWORK
        private static bool IsDirectorySeparator(char c)
            => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        private static bool IsValidDriveChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
#endif
    }
}
