// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
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
        [Required]
        public ITaskItem[] SourceRoots { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            var content = GenerateSourceLinkContent();
            if (content != null)
            {
                WriteSourceLinkFile(content);
            }

            return !Log.HasLoggedErrors;
        }

        internal string GenerateSourceLinkContent()
        {
            string JsonEscape(string str)
                => str.Replace(@"\", @"\\").Replace("\"", "\\\"");

            var result = new StringBuilder();
            result.Append("{\"documents\":{");

            bool success = true;
            bool first = true;
            foreach (var root in SourceRoots)
            {
                string mappedPath = root.GetMetadata(Names.SourceRoot.MappedPath);
                bool isMapped = !string.IsNullOrEmpty(mappedPath);
                string localPath = isMapped ? mappedPath : root.ItemSpec;

                if (!localPath.EndsWithSeparator())
                {
                    Log.LogError(Resources.MustEndWithDirectorySeparator, (isMapped ? Names.SourceRoot.MappedPathFullName : Names.SourceRoot.Name), localPath);
                    success = false;
                    continue;
                }

                if (localPath.Contains('*'))
                {
                    Log.LogError(Resources.MustNotContainWildcard, (isMapped ? Names.SourceRoot.MappedPathFullName : Names.SourceRoot.Name), localPath);
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

                if (first)
                {
                    first = false;
                }
                else
                {
                    result.Append(',');
                }

                result.Append('"');
                result.Append(JsonEscape(localPath));
                result.Append('*');
                result.Append('"');
                result.Append(':');
                result.Append('"');
                result.Append(JsonEscape(url));
                result.Append('"');
            }

            result.Append("}}");

            if (!success)
            {
                return null;
            }

            if (first)
            {
                Log.LogWarning(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty);
            }

            return result.ToString();
        }

        private void WriteSourceLinkFile(string content)
        {
            try
            {
                if (File.Exists(OutputFile))
                {
                    var originalContent = File.ReadAllText(OutputFile);
                    if (originalContent == content)
                    {
                        // Don't rewrite the file if the contents are the same
                        return;
                    }
                }

                File.WriteAllText(OutputFile, content);
            }
            catch (Exception e)
            {
                Log.LogError(Resources.ErrorWritingToSourceLinkFile, OutputFile, e.Message);
            }
        }
    }
}
