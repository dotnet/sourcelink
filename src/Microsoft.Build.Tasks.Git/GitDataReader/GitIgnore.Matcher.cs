// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Tasks.Git
{
    partial class GitIgnore
    {
        internal sealed class Matcher
        {
            public GitIgnore Ignore { get; }

            /// <summary>
            /// Maps full posix slash-terminated directory name to a pattern group.
            /// </summary>
            private readonly Dictionary<string, PatternGroup?> _patternGroups;

            /// <summary>
            /// The result of "is ignored" for directories.
            /// </summary>
            private readonly Dictionary<string, bool> _directoryIgnoreStateCache;

            private readonly List<PatternGroup> _reusableGroupList;

            internal Matcher(GitIgnore ignore)
            {
                Ignore = ignore;
                _patternGroups = new Dictionary<string, PatternGroup?>(StringComparer.Ordinal);
                _directoryIgnoreStateCache = new Dictionary<string, bool>(Ignore.PathComparer);
                _reusableGroupList = new List<PatternGroup>();
            }

            // test only:
            internal IReadOnlyDictionary<string, bool> DirectoryIgnoreStateCache
                => _directoryIgnoreStateCache;

            private PatternGroup? GetPatternGroup(string directory)
            {
                Debug.Assert(PathUtils.HasTrailingSlash(directory));

                if (_patternGroups.TryGetValue(directory, out var group))
                {
                    return group;
                }

                PatternGroup? parent;
                if (directory.Equals(Ignore.WorkingDirectory, Ignore.PathComparison))
                {
                    parent = Ignore.Root;
                }
                else
                {
                    var parentDirectory = directory.Substring(0, directory.LastIndexOf('/', directory.Length - 2, directory.Length - 1) + 1);
                    parent = GetPatternGroup(parentDirectory);
                }

                group = LoadFromFile(directory + GitIgnoreFileName, parent) ?? parent;

                _patternGroups.Add(directory, group);
                return group;
            }

            /// <summary>
            /// Checks if the specified file path is ignored.
            /// </summary>
            /// <param name="fullPath">Normalized path.</param>
            /// <returns>True if the path is ignored, fale if it is not, null if it is outside of the working directory.</returns>
            public bool? IsNormalizedFilePathIgnored(string fullPath)
            {
                if (!PathUtils.IsAbsolute(fullPath))
                {
                    throw new ArgumentException(Resources.PathMustBeAbsolute, nameof(fullPath));
                }

                if (PathUtils.HasTrailingDirectorySeparator(fullPath))
                {
                    throw new ArgumentException(Resources.PathMustBeFilePath, nameof(fullPath));
                }

                return IsPathIgnored(PathUtils.ToPosixPath(fullPath), isDirectoryPath: false);
            }

            /// <summary>
            /// Checks if the specified path is ignored.
            /// </summary>
            /// <param name="fullPath">Full path.</param>
            /// <returns>True if the path is ignored, fale if it is not, null if it is outside of the working directory.</returns>
            public bool? IsPathIgnored(string fullPath)
            {
                if (!PathUtils.IsAbsolute(fullPath))
                {
                    throw new ArgumentException(Resources.PathMustBeAbsolute, nameof(fullPath));
                }

                // git uses the FS case-sensitivity for checking directory existence:
                bool isDirectoryPath = PathUtils.HasTrailingDirectorySeparator(fullPath) || Directory.Exists(fullPath);

                var fullPathNoSlash = PathUtils.TrimTrailingSlash(PathUtils.ToPosixPath(Path.GetFullPath(fullPath)));
                if (isDirectoryPath && fullPathNoSlash.Equals(Ignore._workingDirectoryNoSlash, Ignore.PathComparison))
                {
                    return false;
                }

                return IsPathIgnored(fullPathNoSlash, isDirectoryPath);
            }

            private bool? IsPathIgnored(string normalizedPosixPath, bool isDirectoryPath)
            {
                Debug.Assert(PathUtils.IsAbsolute(normalizedPosixPath));
                Debug.Assert(PathUtils.IsPosixPath(normalizedPosixPath));
                Debug.Assert(!PathUtils.HasTrailingSlash(normalizedPosixPath));

                // paths outside of working directory:
                if (!normalizedPosixPath.StartsWith(Ignore.WorkingDirectory, Ignore.PathComparison))
                {
                    return null;
                }

                if (isDirectoryPath && _directoryIgnoreStateCache.TryGetValue(normalizedPosixPath, out var isIgnored))
                {
                    return isIgnored;
                }

                isIgnored = IsIgnoredRecursive(normalizedPosixPath, isDirectoryPath);
                if (isDirectoryPath)
                {
                    _directoryIgnoreStateCache.Add(normalizedPosixPath, isIgnored);
                }

                return isIgnored;
            }

            private bool IsIgnoredRecursive(string normalizedPosixPath, bool isDirectoryPath)
            {
                SplitPath(normalizedPosixPath, out var directory, out var fileName);
                if (directory == null || !directory.StartsWith(Ignore.WorkingDirectory, Ignore.PathComparison))
                {
                    return false;
                }

                var isIgnored = IsIgnored(normalizedPosixPath, directory, fileName, isDirectoryPath);
                if (isIgnored)
                {
                    return true;
                }

                // The target file/directory itself is not ignored, but its containing directory might be.
                normalizedPosixPath = PathUtils.TrimTrailingSlash(directory);
                if (_directoryIgnoreStateCache.TryGetValue(normalizedPosixPath, out isIgnored))
                {
                    return isIgnored;
                }

                isIgnored = IsIgnoredRecursive(normalizedPosixPath, isDirectoryPath: true);
                _directoryIgnoreStateCache.Add(normalizedPosixPath, isIgnored);
                return isIgnored;
            }

            private static void SplitPath(string fullPath, out string? directoryWithSlash, out string fileName)
            {
                Debug.Assert(!PathUtils.HasTrailingSlash(fullPath));
                int i = fullPath.LastIndexOf('/');
                if (i < 0)
                {
                    directoryWithSlash = null;
                    fileName = fullPath;
                }
                else
                {
                    directoryWithSlash = fullPath.Substring(0, i + 1);
                    fileName = fullPath.Substring(i + 1);
                }
            }

            private bool IsIgnored(string normalizedPosixPath, string directory, string fileName, bool isDirectoryPath)
            {
                // Default patterns can't be overriden by a negative pattern:
                if (fileName.Equals(".git", Ignore.PathComparison))
                {
                    return true;
                }

                bool isIgnored = false;
                
                // Visit groups in reverse order.
                // Patterns specified closer to the target file override those specified above.
                _reusableGroupList.Clear();
                var groups = _reusableGroupList;
                for (PatternGroup? patternGroup = GetPatternGroup(directory); patternGroup != null; patternGroup = patternGroup.Parent)
                {
                    groups.Add(patternGroup);
                }

                for (int i = groups.Count - 1; i >= 0; i--)
                {
                    var patternGroup = groups[i];

                    if (!normalizedPosixPath.StartsWith(patternGroup.ContainingDirectory, Ignore.PathComparison))
                    {
                        continue;
                    }

                    string? lazyRelativePath = null;

                    foreach (var pattern in patternGroup.Patterns)
                    {
                        // If a pattern is matched as ignored only look for a negative pattern that matches as well.
                        // If a pattern is not matched then skip negative patterns.
                        if (isIgnored != pattern.IsNegative)
                        {
                            continue;
                        }

                        if (pattern.IsDirectoryPattern && !isDirectoryPath)
                        {
                            continue;
                        }

                        string matchPath = pattern.IsFullPathPattern ?
                            lazyRelativePath ??= normalizedPosixPath.Substring(patternGroup.ContainingDirectory.Length) :
                            fileName;

                        if (Glob.IsMatch(pattern.Glob, matchPath, Ignore.IgnoreCase, matchWildCardWithDirectorySeparator: false))
                        {
                            // TODO: optimize negative pattern lookup (once we match, do we need to continue matching?)
                            isIgnored = !pattern.IsNegative;
                        }
                    }
                }

                return isIgnored;
            }
        }
    }
}
