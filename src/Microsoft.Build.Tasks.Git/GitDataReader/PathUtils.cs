// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Tasks.Git
{
    internal static class PathUtils
    {
        public static bool IsUnixLikePlatform => Path.DirectorySeparatorChar == '/';
        public const char VolumeSeparatorChar = ':';
        public static readonly string DirectorySeparatorStr = Path.DirectorySeparatorChar.ToString();
        private static readonly char[] s_slash = new char[] { '/' };
        private static readonly char[] s_directorySeparators = new char[] { '/' };

        public static string EnsureTrailingSlash(string path)
            => HasTrailingSlash(path) ? path : path + "/";

        public static string TrimTrailingSlash(string path)
            => path.TrimEnd(s_slash);

        public static string TrimTrailingDirectorySeparator(string path)
            => path.TrimEnd(s_directorySeparators);

        public static bool HasTrailingSlash(string path)
            => path.Length > 0 && path[path.Length - 1] == '/';

        public static bool HasTrailingDirectorySeparator(string path)
            => path.Length > 0 && (path[path.Length - 1] == '/' || path[path.Length - 1] == '\\');

        public static string ToPosixPath(string path)
            => (Path.DirectorySeparatorChar == '\\') ? path.Replace('\\', '/') : path;

        internal static string ToPosixDirectoryPath(string path)
            => EnsureTrailingSlash(ToPosixPath(path));

        internal static bool IsPosixPath(string path)
            => Path.DirectorySeparatorChar == '/' || path.IndexOf('\\') < 0;

        public static string CombinePosixPaths(string root, string relativePath)
            => CombinePaths(root, relativePath, "/");

        public static string CombinePaths(string root, string relativePath)
            => CombinePaths(root, relativePath, DirectorySeparatorStr);

        public static string CombinePaths(string root, string relativePath, string separator)
        {
            Debug.Assert(!string.IsNullOrEmpty(root));

            char c = root[root.Length - 1];
            if (!IsDirectorySeparator(c) && c != VolumeSeparatorChar)
            {
                return root + separator + relativePath;
            }

            return root + relativePath;
        }

        public static bool IsDirectorySeparator(char c)
            => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        public static bool IsNormalized(string path)
            => Path.GetFullPath(path) == path;

        /// <summary>
        /// True if the path is an absolute path (rooted to drive or network share)
        /// </summary>
        public static bool IsAbsolute(string path)
        {
            if (string.IsNullOrEmpty(path))
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
        /// Returns true if given path is absolute and starts with a drive specification ("C:\").
        /// </summary>
        private static bool IsDriveRootedAbsolutePath(string path)
        {
            Debug.Assert(!IsUnixLikePlatform);
            return path.Length >= 3 && path[1] == VolumeSeparatorChar && IsDirectorySeparator(path[2]);
        }
    }
}
