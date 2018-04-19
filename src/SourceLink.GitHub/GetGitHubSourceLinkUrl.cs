// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SourceLink.GitHub
{
    public sealed class GetGitHubSourceLinkUrl : Task
    {
        private static readonly Uri s_rawGitHub = new Uri("https://raw.githubusercontent.com");

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

            if (!repoUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
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

            var relativeUrl = repoUri.LocalPath.TrimEnd('/');

            // The URL may or may not end with '.git', but raw.githubusercontent.com does not accept '.git' suffix:
            const string gitUrlSuffix = ".git";
            if (relativeUrl.EndsWith(gitUrlSuffix))
            {
                relativeUrl = relativeUrl.Substring(0, relativeUrl.Length - gitUrlSuffix.Length);
            }

            SourceLinkUrl = new Uri(s_rawGitHub, relativeUrl).ToString() + "/" + revisionId + "/*";
            return true;
        }
    }
}
