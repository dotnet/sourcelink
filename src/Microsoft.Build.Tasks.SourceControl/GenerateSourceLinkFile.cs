// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    // TODO: move to Microsoft.Build.Tasks
    public class GenerateSourceLinkFile : Task
    {
        [Required]
        public ITaskItem[] SourceRoots { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(OutputFile))
            {
                Log.LogError("OutputFile not specified");
                return false;
            }

            if (SourceRoots.Length == 0)
            {
                Log.LogError("No SourceRoots specified");
                return false;
            }

            string JsonEscape(string str)
                => str.Replace(@"\", @"\\").Replace("\"", "\\\"");

            var result = new StringBuilder();
            result.Append("{\"documents\":{");

            bool success = true;
            bool first = true;
            foreach (var root in SourceRoots)
            {
                string mappedPath = root.GetMetadata("MappedPath");
                bool isMapped = !string.IsNullOrEmpty(mappedPath);
                string localPath = isMapped ? mappedPath : root.ItemSpec;

                if (!localPath.EndsWithSeparator())
                {
                    Log.LogError($"{(isMapped ? "SourceLink.MappedPath" : "SourceLink")} must end with a directory separator: '{localPath}'");
                    success = false;
                    continue;
                }

                if (localPath.Contains('*'))
                {
                    Log.LogError($"{(isMapped ? "SourceLink.MappedPath" : "SourceLink")} must not contain wildcard '*': '{localPath}'");
                    success = false;
                    continue;
                }

                var url = root.GetMetadata("SourceLinkUrl");
                if (string.IsNullOrEmpty(url))
                {
                    Log.LogError($"SourceRoot.SourceLinkUrl is empty: '{root.ItemSpec}'");
                    success = false;
                    continue;
                }

                if (url.Count(c => c == '*') != 1)
                {
                    Log.LogError($"SourceRoot.SourceLinkUrl must contain a single wildcard '*': '{url}'");
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
                return false;
            }

            try
            {
                File.WriteAllText(OutputFile, result.ToString());
            }
            catch (Exception e)
            {
                Log.LogError($"Error writing to source link file '{OutputFile}': {e.Message}");
                return false;
            }

            return true;
        }
    }
}
