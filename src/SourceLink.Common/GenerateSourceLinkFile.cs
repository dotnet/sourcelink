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
        /// Fully qualified path of the placeholder replacement dictionary.
        /// When non-empty, emit stable revision markers in the compiler's Source Link input.
        /// </summary>
        public string? PlaceholderReplacementsOutputFile { get; set; }

        public override bool Execute()
        {
            if (!ValidateOutputPaths())
            {
                return false;
            }

            var succeeded = false;
            try
            {
                var mappings = GetSourceLinkMappings();
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                SourceLink = WriteOutputFile(OutputFile, GenerateSourceLinkContent(mappings));
                if (!NullableString.IsNullOrEmpty(PlaceholderReplacementsOutputFile) && !Log.HasLoggedErrors)
                {
                    WriteOutputFile(PlaceholderReplacementsOutputFile, GenerateRevisionFileContent(mappings));
                }

                succeeded = !Log.HasLoggedErrors;
                return succeeded;
            }
            finally
            {
                if (!succeeded)
                {
                    DeleteOutputFiles();
                    SourceLink = null;
                }
            }
        }

        private bool ValidateOutputPaths()
        {
            var path = OutputFile;
            try
            {
                // Relative paths must not be resolved against MSBuild's shared process working directory.
                if (path == null || !PathUtilities.IsPathFullyQualified(path))
                {
                    Log.LogError(Resources.ErrorWritingToSourceLinkFile, path, $"The path '{path}' must be fully qualified.");
                    return false;
                }

                var outputPath = Path.GetFullPath(path);
                if (!NullableString.IsNullOrEmpty(PlaceholderReplacementsOutputFile))
                {
                    path = PlaceholderReplacementsOutputFile;
                    if (!PathUtilities.IsPathFullyQualified(path))
                    {
                        Log.LogError(Resources.ErrorWritingToSourceLinkFile, path, $"The path '{path}' must be fully qualified.");
                        return false;
                    }

                    var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    if (string.Equals(outputPath, Path.GetFullPath(path), comparison))
                    {
                        Log.LogError(Resources.ErrorWritingToSourceLinkFile, path, Resources.SourceLinkRevisionFilesMustDiffer);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Log.LogError(Resources.ErrorWritingToSourceLinkFile, path, e.Message);
                return false;
            }
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
                if (!NullableString.IsNullOrEmpty(PlaceholderReplacementsOutputFile))
                {
                    if (TryGetRevision(root, mappedPath, url, out revision, out placeholder))
                    {
                        url = url.Replace(revision, placeholder);
                    }
                }

                mappings.Add(new SourceLinkMapping(root, localPath, url, revision, placeholder));
            }

            return mappings;
        }

        private bool TryGetRevision(ITaskItem root, string mappedPath, string url, out string revision, [NotNullWhen(true)] out string? placeholder)
        {
            revision = "";
            placeholder = null;

            if (string.IsNullOrEmpty(mappedPath))
            {
                Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, root.ItemSpec);
                return false;
            }

            revision = root.GetMetadata(Names.SourceRoot.RevisionId);

            // A mapping without source control may omit its revision; there is nothing to replace.
            if (string.IsNullOrEmpty(revision) && string.IsNullOrEmpty(root.GetMetadata(Names.SourceRoot.SourceControl)))
            {
                return false;
            }

            var revisionStart = string.IsNullOrEmpty(revision) ? -1 : url.IndexOf(revision, StringComparison.Ordinal);

            // Fixed-width revisions allow in-place substitution in uncompressed metadata.
            // A unique occurrence avoids replacing unrelated URL text.
            if (!GitUtilities.IsValidObjectId(revision) ||
                revision.Any(c => c is >= 'A' and <= 'F') ||
                revisionStart < 0 ||
                url.IndexOf(revision, revisionStart + revision.Length, StringComparison.Ordinal) >= 0)
            {
                Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, root.ItemSpec);
                return false;
            }

            var template = url.Substring(0, revisionStart) + "<revision>" + url.Substring(revisionStart + revision.Length);

            // The placeholder depends on the mapped path and revision-independent URL, so changes to either
            // update the Source Link compiler input. SHA256 is truncated to the revision width for substitution;
            // NUL separates the path and URL because it is not a valid path character.
            using (var sha = SHA256.Create())
            {
                placeholder = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(mappedPath + "\0" + template)))
                    .Replace("-", "").ToLowerInvariant().Substring(0, revision.Length);
            }

            if (placeholder == revision)
            {
                Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, root.ItemSpec);
                return false;
            }

            return true;
        }

        internal string? GenerateRevisionFileContent(List<SourceLinkMapping> mappings)
        {
            var revisions = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var mappedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in mappings)
            {
                if (mapping.Placeholder == null)
                {
                    continue;
                }

                if (!mappedPaths.Add(mapping.LocalPath))
                {
                    Log.LogError(Resources.InvalidSourceLinkPlaceholderRoot, mapping.Root.ItemSpec);
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

            // Both keys and values contain only the validated hexadecimal revision and a fixed ASCII prefix.
            return !Log.HasLoggedErrors ? "{" + string.Join(",", revisions.Select(pair => "\"" + pair.Key + "\":\"" + pair.Value + "\"")) + "}" : null;
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

        private string? WriteOutputFile(string path, string? content)
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

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
            void deleteFile(string path)
            {
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
            if (!NullableString.IsNullOrEmpty(PlaceholderReplacementsOutputFile))
            {
                deleteFile(PlaceholderReplacementsOutputFile);
            }
        }

        internal readonly struct SourceLinkMapping(ITaskItem root, string localPath, string url, string revision, string? placeholder)
        {
            public ITaskItem Root { get; } = root;
            public string LocalPath { get; } = localPath;
            public string Url { get; } = url;
            public string Revision { get; } = revision;
            public string? Placeholder { get; } = placeholder;
        }
    }
}
