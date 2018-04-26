// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace SourceLink.VSTS.Git
{
    public sealed class GetVstsGitSourceLinkUrl : Task
    {
        private const string UrlMapEnvironmentVariableName = "STANDARD_CI_REPOSITORY_URL_MAP";
        private const string DefaultDomain = "visualstudio.com";
        private const string SourceControlName = "git";
        private const string NotApplicableValue = "N/A";

        [Required]
        public ITaskItem SourceRoot { get; set; }

        public string Domain { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        public GetVstsGitSourceLinkUrl()
        {
            TaskResources = Resources.ResourceManager;
        }

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

            var map = TryGetStandardUriMap();
            if (map != null && map.TryGetValue(repoUri, out var mappedUri))
            {
                repoUri = mappedUri;
            }

            string domain = string.IsNullOrEmpty(Domain) ? DefaultDomain : Domain;
            if (!TryParseRepositoryUrl(repoUri, domain, out var projectName, out var repositoryName))
            {
                return false;
            }

            var query = GetSourceLinkQuery();
            if (query == null)
            {
                return false;
            }

            SourceLinkUrl = $"{repoUri.Scheme}://{repoUri.Host}/{projectName}/_apis/git/repositories/{repositoryName}/items?" + query;
            return true;
        }

        private Dictionary<Uri, Uri> TryGetStandardUriMap()
        {
            var urlSeparators = new[] { Path.PathSeparator };
            Dictionary<Uri, Uri> map = null;

            bool parse(string urls)
            {
                var items = urls.Split(urlSeparators, StringSplitOptions.None);
                if (items.Length % 2 != 0)
                {
                    return false;
                }

                for (int i = 0; i < items.Length; i += 2)
                {
                    string originalUrl = items[i];
                    string mappedUrl = items[i + 1];

                    if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var originalUri) || originalUri.Query != "")
                    {
                        return false;
                    }

                    if (!Uri.TryCreate(mappedUrl, UriKind.Absolute, out var mappedUri) || mappedUri.Query != "")
                    {
                        return false;
                    }

                    if (map == null)
                    {
                        map = new Dictionary<Uri, Uri>();
                    }

                    map[originalUri] = mappedUri;
                }

                return true;
            }

            IEnumerable<KeyValuePair<string, string>> enumerateVariables()
            {
                int i = 0;
                while (true)
                {
                    var name = UrlMapEnvironmentVariableName + (i == 0 ? "" : i.ToString());
                    var value = Environment.GetEnvironmentVariable(name);
                    if (string.IsNullOrEmpty(value))
                    {
                        yield break;
                    }

                    yield return new KeyValuePair<string, string>(name, value);
                    i++;
                }
            }

            foreach (var variable in enumerateVariables())
            {
                if (!parse(variable.Value))
                {
                    Log.LogErrorFromResources("EnvironmentVariableIsNotAlistOfUrlPairs", variable.Key, variable.Value);
                    return null;
                }
            }

            return map;
        }

        private static bool TryParseRepositoryUrl(Uri repoUri, string domain, out string projectName, out string repositoryName)
        {
            // URL format pattern:
            // https://{account}.{domain}/[DefaultCollection/]?{project}/_git/{repository-name}

            projectName = null;
            repositoryName = null;

            if (!repoUri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) && 
                !repoUri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase))
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

        private string GetSourceLinkQuery()
        {
            bool IsHexDigit(char c)
                => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

            string revisionId = SourceRoot.GetMetadata(Names.SourceRoot.RevisionId);
            if (revisionId == null || revisionId.Length != 40 || !revisionId.All(IsHexDigit))
            {
                Log.LogErrorFromResources("ValueOfWithIdentityIsNotValidCommitHash", Names.SourceRoot.RevisionIdFullName, SourceRoot.ItemSpec, revisionId);
                return null;
            }

            return $"api-version=1.0&versionType=commit&version={revisionId}&path=/*";
        }
    }
}
