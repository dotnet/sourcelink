// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SourceLink.VSO.Git
{
    public sealed class GetVsoGitSourceLinkUrl : Task
    {
        [Required]
        public ITaskItem SourceRoot { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        public override bool Execute()
        {
            if (!string.IsNullOrEmpty(SourceRoot.GetMetadata("SourceLinkUrl")) ||
                !string.Equals(SourceRoot.GetMetadata("SourceControl"), "git", StringComparison.OrdinalIgnoreCase))
            {
                SourceLinkUrl = "N/A";
                return true;
            }

            var repoUrl = SourceRoot.GetMetadata("RepositoryUrl");
            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var repoUri))
            {
                Log.LogError($"SourceRoot.RepositoryUrl of '{SourceRoot.ItemSpec}' is invalid: '{repoUrl}'");
                return false;
            }

            if (!TryParseRepositoryUrl(repoUri, out var projectName, out var repositoryName))
            {
                SourceLinkUrl = "N/A";
                return true;
            }

            bool IsHexDigit(char c)
                => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

            string revisionId = SourceRoot.GetMetadata("RevisionId");
            if (revisionId == null || revisionId.Length != 40 || !revisionId.All(IsHexDigit))
            {
                Log.LogError($"SourceRoot.RevisionId of '{SourceRoot.ItemSpec}' is not a valid commit hash: '{revisionId}'");
                return false;
            }

            SourceLinkUrl = $"{repoUri.Scheme}://{repoUri.Host}/{projectName}/_apis/git/repositories/{repositoryName}/items?api-version=1.0&versionType=commit&version={revisionId}&path=/*";
            return true;
        }

        private static bool TryParseRepositoryUrl(Uri repoUri, out string projectName, out string repositoryName)
        {
            // URL format pattern:
            // https://{account}.visualstudio.com/[DefaultCollection/]?{project}/_git/{repository-name}

            projectName = null;
            repositoryName = null;

            if (!repoUri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parts = repoUri.LocalPath.Trim('/').Split('/');
            if (parts.Length < 3 || parts.Length > 4)
            {
                return false;
            }

            if (parts.Length == 4 && !parts[0].Equals("DefaultCollection", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!parts[parts.Length - 2].Equals("_git", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            repositoryName = parts[parts.Length - 1];
            projectName = parts[parts.Length - 3];
            return true;
        }
    }
}
