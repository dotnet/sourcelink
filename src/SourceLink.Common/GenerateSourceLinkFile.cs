// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.Common
{
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
            WriteSourceLinkFile(GenerateSourceLinkContent());

            return !Log.HasLoggedErrors;
        }

        internal string? GenerateSourceLinkContent()
        {
            static string jsonEscape(string str)
                => str.Replace(@"\", @"\\").Replace("\"", "\\\"");

            var result = new StringBuilder();
            result.Append("{\"documents\":{");

            bool success = true;
            bool isEmpty = true;
            foreach (var root in SourceRoots)
            {
                string mappedPath = root.GetMetadata(Names.SourceRoot.MappedPath);
                bool isMapped = !string.IsNullOrEmpty(mappedPath);
                string localPath = isMapped ? mappedPath : root.ItemSpec;

                if (!localPath.EndsWithSeparator())
                {
                    Log.LogError(Resources.MustEndWithDirectorySeparator, isMapped ? Names.SourceRoot.MappedPathFullName : Names.SourceRoot.Name, localPath);
                    success = false;
                    continue;
                }

                if (localPath.Contains('*'))
                {
                    Log.LogError(Resources.MustNotContainWildcard, isMapped ? Names.SourceRoot.MappedPathFullName : Names.SourceRoot.Name, localPath);
                    success = false;
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
                    success = false;
                    continue;
                }

                if (isEmpty)
                {
                    isEmpty = false;
                }
                else
                {
                    result.Append(',');
                }

                result.Append('"');
                result.Append(jsonEscape(localPath));
                result.Append('*');
                result.Append('"');
                result.Append(':');
                result.Append('"');
                result.Append(jsonEscape(url));
                result.Append('"');
            }

            result.Append("}}");

            return success && !isEmpty ? result.ToString() : null;
        }

        private void WriteSourceLinkFile(string? content)
        {
            if (content == null && !NoWarnOnMissingSourceControlInformation)
            {
                Log.LogWarning(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty);
            }

            try
            {
                if (File.Exists(OutputFile))
                {
                    if (content == null)
                    {
                        Log.LogMessage(Resources.SourceLinkEmptyDeletingExistingFile, OutputFile);

                        File.Delete(OutputFile);
                        return;
                    }

                    var originalContent = File.ReadAllText(OutputFile);
                    if (originalContent == content)
                    {
                        // Don't rewrite the file if the contents is the same, just pass it to the compiler.
                        Log.LogMessage(Resources.SourceLinkFileUpToDate, OutputFile);

                        SourceLink = OutputFile;
                        return;
                    }
                }
                else if (content == null)
                {
                    // File doesn't exist and the output is empty:
                    // Do not write the file and don't pass it to the compiler.
                    Log.LogMessage(Resources.SourceLinkEmptyNoExistingFile, OutputFile);
                    return;
                }

                Log.LogMessage(Resources.SourceLinkFileUpdated, OutputFile);
                File.WriteAllText(OutputFile, content);
                SourceLink = OutputFile;
            }
            catch (Exception e)
            {
                Log.LogError(Resources.ErrorWritingToSourceLinkFile, OutputFile, e.Message);
            }
        }
    }
}
