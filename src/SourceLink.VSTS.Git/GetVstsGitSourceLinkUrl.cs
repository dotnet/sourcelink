// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace SourceLink.VSTS.Git
{
    public sealed class GetVstsGitSourceLinkUrl : Task
    {
        private const string SourceControlName = "git";
        private const string NotApplicableValue = "N/A";

        [Required]
        public ITaskItem SourceRoot { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        public override bool Execute()
        {
            if (!string.IsNullOrEmpty(SourceRoot.GetMetadata(Names.SourceRoot.SourceLinkUrl)) ||
                !string.Equals(SourceRoot.GetMetadata(Names.SourceRoot.SourceControl), SourceControlName, StringComparison.OrdinalIgnoreCase))
            {
                SourceLinkUrl = NotApplicableValue;
                return true;
            }

            var repoUrl = SourceRoot.GetMetadata(Names.SourceRoot.RepositoryUrl);
            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var repoUri))
            {
                Log.LogErrorFromResources("ValueOfOWithIdentityIsInvalid", Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, repoUrl);
                return false;
            }

            if (!TryParseRepositoryUrl(repoUri, out var projectName, out var repositoryName))
            {
                SourceLinkUrl = NotApplicableValue;
                return true;
            }

            bool IsHexDigit(char c)
                => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

            string revisionId = SourceRoot.GetMetadata(Names.SourceRoot.RevisionId);
            if (revisionId == null || revisionId.Length != 40 || !revisionId.All(IsHexDigit))
            {
                Log.LogError("ValueOfWithIdentityIsNotValidCommitHash", Names.SourceRoot.RevisionIdFullName, SourceRoot.ItemSpec, revisionId);
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
