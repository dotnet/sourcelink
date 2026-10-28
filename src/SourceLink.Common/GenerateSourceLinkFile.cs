// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.Common
{
    [MSBuildMultiThreadableTask]
    public sealed class GenerateSourceLinkFile : Task
    {
        [Required, NotNull]
        public ITaskItem[]? SourceRoots { get; set; }

        [Required, NotNull]
        public string? OutputFile { get; set; }

        /// <summary>
        /// Set to <see cref="OutputFile"/> if the output Source Link file should be passed to the compiler.
        /// </summary>
        [Output]
        public string? SourceLink { get; set; }

        public bool NoWarnOnMissingSourceControlInformation { get; set; }

        /// <summary>
        /// Emit stable revision markers instead of resolved revisions in the compiler's Source Link input.
        /// </summary>
        public bool UsePlaceholderValues { get; set; }

        /// <summary>
        /// Fully qualified path of the marker-to-revision dictionary. Required when using placeholder values.
        /// </summary>
        public string? RevisionOutputFile { get; set; }

        public override bool Execute()
        {
            var mappings = GetSourceLinkMappings();
            if (Log.HasLoggedErrors)
            {
                DeleteOutputFiles();
                return false;
            }

            SourceLink = WriteOutputFile(OutputFile, GenerateSourceLinkContent(mappings));
            if (UsePlaceholderValues && !Log.HasLoggedErrors)
            {
                WriteOutputFile(RevisionOutputFile, GenerateRevisionFileContent(mappings));
            }

            if (Log.HasLoggedErrors)
            {
                DeleteOutputFiles();
                return false;
            }

            return true;
        }

        internal List<SourceLinkMapping> GetSourceLinkMappings()
        {
            var mappings = new List<SourceLinkMapping>();
            foreach (var root in SourceRoots)
            {
                var mappedPath = root.GetMetadata(Names.SourceRoot.MappedPath);
                var isMapped = !string.IsNullOrEmpty(mappedPath);
                var localPath = isMapped ? mappedPath : root.ItemSpec;

                if (!localPath.EndsWithSeparator())
                {
                    Log.LogError(Resources.MustEndWithDirectorySeparator, isMapped ? Names.SourceRoot.MappedPathFullName : Names.SourceRoot.Name, localPath);
                    continue;
                }

                if (localPath.Contains('*'))
                {
                    Log.LogError(Resources.MustNotContainWildcard, isMapped ? Names.SourceRoot.MappedPathFullName : Names.SourceRoot.Name, localPath);
                    continue;
                }

                var url = root.GetMetadata(Names.SourceRoot.SourceLinkUrl);
                if (string.IsNullOrEmpty(url))
                {
                    // Do not report any diagnostic. If the source root comes from source control a warning has already been reported.
                    // SourceRoots can be specified by the project to make other features like deterministic paths, and they don't need source link URL.
                    continue;
                }

                if (url.Count(c => c == '*') != 1)
                {
                    Log.LogError(Resources.MustContainSingleWildcard, Names.SourceRoot.SourceLinkUrlFullName, url);
                    continue;
                }

                var revision = "";
                string? placeholder = null;
                if (UsePlaceholderValues)
                {
                    // Stable mapped roots give placeholders checkout-independent identities.
                    if (!isMapped)
                    {
                        Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, root.ItemSpec);
                        continue;
                    }

                    revision = root.GetMetadata(Names.SourceRoot.RevisionId);

                    // Fixed mappings are not tied to source-control revisions and need no placeholder.
                    if (!string.IsNullOrEmpty(revision) || !string.IsNullOrEmpty(root.GetMetadata("SourceControl")))
                    {
                        var revisionStart = string.IsNullOrEmpty(revision) ? -1 : url.IndexOf(revision, StringComparison.Ordinal);

                        // Fixed-width revisions allow in-place substitution in uncompressed metadata.
                        // A unique occurrence avoids replacing unrelated URL text.
                        if ((revision.Length != 40 && revision.Length != 64) ||
                            revision.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')) ||
                            revisionStart < 0 ||
                            url.IndexOf(revision, revisionStart + revision.Length, StringComparison.Ordinal) >= 0)
                        {
                            Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, root.ItemSpec);
                            continue;
                        }

                        var template = url.Substring(0, revisionStart) + "<revision>" + url.Substring(revisionStart + revision.Length);
                        using (var sha = SHA256.Create())
                        {
                            placeholder = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(mappedPath + "\n" + template)))
                                .Replace("-", "").ToLowerInvariant().Substring(0, revision.Length);
                        }

                        if (placeholder == revision)
                        {
                            Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, root.ItemSpec);
                            continue;
                        }

                        url = url.Substring(0, revisionStart) + placeholder + url.Substring(revisionStart + revision.Length);
                    }
                }

                mappings.Add(new SourceLinkMapping(root, localPath, url, revision, placeholder));
            }

            return mappings;
        }

        internal string? GenerateRevisionFileContent(List<SourceLinkMapping> mappings)
        {
            var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var mappedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in mappings)
            {
                // Duplicate roots make document mappings ambiguous, including fixed mappings.
                if (!mappedPaths.Add(mapping.LocalPath))
                {
                    Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, mapping.Root.ItemSpec);
                    continue;
                }

                if (mapping.Placeholder == null)
                {
                    continue;
                }

                var key = "SourceLinkRevision_" + mapping.Placeholder;

                // A placeholder must identify one replacement across all mappings.
                if (revisions.TryGetValue(key, out var existing) && existing != mapping.Revision)
                {
                    Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, mapping.Root.ItemSpec);
                    continue;
                }

                revisions[key] = mapping.Revision;
            }

            if (Log.HasLoggedErrors)
            {
                return null;
            }

            // Both keys and values contain only the validated hexadecimal revision and a fixed ASCII prefix.
            return "{" + string.Join(",", revisions.Select(pair => "\"" + pair.Key + "\":\"" + pair.Value + "\"")) + "}";
        }

        internal static string? GenerateSourceLinkContent(List<SourceLinkMapping> mappings)
        {
            static string jsonEscape(string str)
                => str.Replace(@"\", @"\\").Replace("\"", "\\\"");

            if (mappings.Count == 0)
            {
                return null;
            }

            var result = new StringBuilder();
            result.Append("{\"documents\":{");
            for (var i = 0; i < mappings.Count; i++)
            {
                if (i > 0)
                {
                    result.Append(',');
                }

                var mapping = mappings[i];
                result.Append('"');
                result.Append(jsonEscape(mapping.LocalPath));
                result.Append('*');
                result.Append('"');
                result.Append(':');
                result.Append('"');
                result.Append(jsonEscape(mapping.Url));
                result.Append('"');
            }

            result.Append("}}");
            return result.ToString();
        }

        private string? WriteOutputFile(string? path, string? content)
        {
            if (Log.HasLoggedErrors)
            {
                return null;
            }

            try
            {
                if (content == null && !NoWarnOnMissingSourceControlInformation)
                {
                    Log.LogWarning(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty);
                }

                // Relative paths must not be resolved against MSBuild's shared process working directory.
                if (path == null || path.Length == 0 || !PathUtilities.IsPathFullyQualified(path))
                {
                    throw new ArgumentException($"The path '{path}' must be fully qualified.", nameof(path));
                }

                var isRevisionFile = UsePlaceholderValues && path == RevisionOutputFile;
                if (UsePlaceholderValues)
                {
                    // Validate both destinations before either output is written.
                    var otherPath = isRevisionFile ? OutputFile : RevisionOutputFile;
                    if (otherPath == null || otherPath.Length == 0 || !PathUtilities.IsPathFullyQualified(otherPath))
                    {
                        throw new ArgumentException($"The path '{otherPath}' must be fully qualified.",
                            isRevisionFile ? nameof(OutputFile) : nameof(RevisionOutputFile));
                    }

                    var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(otherPath), comparison))
                    {
                        throw new ArgumentException(Resources.SourceLinkRevisionFilesMustDiffer, nameof(RevisionOutputFile));
                    }
                }

                if (isRevisionFile)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                }

                if (File.Exists(path))
                {
                    if (content == null)
                    {
                        Log.LogMessage(Resources.SourceLinkEmptyDeletingExistingFile, path);
                        File.Delete(path);
                        return null;
                    }

                    if (File.ReadAllText(path) == content)
                    {
                        Log.LogMessage(Resources.SourceLinkFileUpToDate, path);
                        return path;
                    }
                }
                else if (content == null)
                {
                    Log.LogMessage(Resources.SourceLinkEmptyNoExistingFile, path);
                    return null;
                }

                Log.LogMessage(Resources.SourceLinkFileUpdated, path);
                File.WriteAllText(path, content);
                return path;
            }
            catch (Exception e)
            {
                Log.LogError(Resources.ErrorWritingToSourceLinkFile, path, e.Message);
                return null;
            }
        }

        private void DeleteOutputFiles()
        {
            void deleteFile(string? path)
            {
                // Invalid paths must not be resolved against MSBuild's shared process working directory.
                if (path == null || path.Length == 0 || !PathUtilities.IsPathFullyQualified(path))
                {
                    return;
                }

                try
                {
                    File.Delete(path);
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e);
                }
            }

            deleteFile(OutputFile);
            if (UsePlaceholderValues)
            {
                deleteFile(RevisionOutputFile);
            }

            SourceLink = null;
        }

        internal sealed class SourceLinkMapping(ITaskItem root, string localPath, string url, string revision, string? placeholder)
        {
            public ITaskItem Root { get; } = root;
            public string LocalPath { get; } = localPath;
            public string Url { get; } = url;
            public string Revision { get; } = revision;
            public string? Placeholder { get; } = placeholder;
        }
    }
}
