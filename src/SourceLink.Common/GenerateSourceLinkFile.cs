// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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

        public override bool Execute()
        {
            var mappings = GetSourceLinkMappings();
            WriteOutputFile(OutputFile, Log.HasLoggedErrors ? null : GenerateSourceLinkContent(mappings));

            return !Log.HasLoggedErrors;
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

                mappings.Add(new SourceLinkMapping(localPath, url));
            }

            return mappings;
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

        private void WriteOutputFile(string? path, string? content)
        {
            if (content == null && !NoWarnOnMissingSourceControlInformation)
            {
                Log.LogWarning(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty);
            }

            try
            {
                // Relative paths must not be resolved against MSBuild's shared process working directory.
                if (path == null || path.Length == 0 || !PathUtilities.IsPathFullyQualified(path))
                {
                    throw new ArgumentException($"The path '{path}' must be fully qualified.", nameof(OutputFile));
                }

                if (File.Exists(path))
                {
                    if (content == null)
                    {
                        Log.LogMessage(Resources.SourceLinkEmptyDeletingExistingFile, path);
                        File.Delete(path);
                        return;
                    }

                    if (File.ReadAllText(path) == content)
                    {
                        Log.LogMessage(Resources.SourceLinkFileUpToDate, path);
                        SourceLink = path;
                        return;
                    }
                }
                else if (content == null)
                {
                    Log.LogMessage(Resources.SourceLinkEmptyNoExistingFile, path);
                    return;
                }

                Log.LogMessage(Resources.SourceLinkFileUpdated, path);
                File.WriteAllText(path, content);
                SourceLink = path;
            }
            catch (Exception e)
            {
                Log.LogError(Resources.ErrorWritingToSourceLinkFile, path, e.Message);
            }
        }

        internal sealed class SourceLinkMapping(string localPath, string url)
        {
            public string LocalPath { get; } = localPath;
            public string Url { get; } = url;
        }
    }
}
