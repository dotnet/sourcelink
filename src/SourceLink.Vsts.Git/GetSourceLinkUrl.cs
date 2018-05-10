﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.Vsts.Git
{
    public sealed class GetSourceLinkUrl : Task
    {
        private const string UrlMapEnvironmentVariableName = "BUILD_REPOSITORY_URL_MAP";
        private const string DefaultDomain = "visualstudio.com";
        private const string SourceControlName = "git";
        private const string NotApplicableValue = "N/A";

        [Required]
        public ITaskItem SourceRoot { get; set; }

        public string Domain { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            // skip SourceRoot that already has SourceLinkUrl set, or its SourceControl is not "git":
            if (!string.IsNullOrEmpty(SourceRoot.GetMetadata(Names.SourceRoot.SourceLinkUrl)) ||
                !string.Equals(SourceRoot.GetMetadata(Names.SourceRoot.SourceControl), SourceControlName, StringComparison.OrdinalIgnoreCase))
            {
                SourceLinkUrl = NotApplicableValue;
                return;
            }

            var repoUrl = SourceRoot.GetMetadata(Names.SourceRoot.RepositoryUrl);
            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var repoUri))
            {
                Log.LogError(Resources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, repoUrl);
                return;
            }

            var map = TryGetStandardUriMap();
            if (map != null && map.TryGetValue(repoUri, out var mappedUri))
            {
                repoUri = mappedUri;
            }

            string domain;
            if (string.IsNullOrEmpty(Domain))
            {
                domain = DefaultDomain;
            }
            else
            {
                bool isHostUri(Uri uri) => uri.PathAndQuery == "/" && uri.UserInfo == "";

                domain = Domain;
                if (!Uri.TryCreate("http://" + domain, UriKind.Absolute, out var domainUri) || !isHostUri(domainUri))
                {
                    Log.LogError(Resources.ValuePassedToTaskParameterNotValidDomainName, nameof(Domain), domain);
                    return;
                }
            }

            if (!TryParseRepositoryUrl(repoUri, domain, out var projectName, out var repositoryName, out var collectionName))
            {
                SourceLinkUrl = NotApplicableValue;
                return;
            }

            var query = GetSourceLinkQuery();
            if (query == null)
            {
                return;
            }

            // Although VSTS does not have non-default collections, TFS does. 
            // This package can be used for both VSTS and TFS.
            string collectionPath = (collectionName == null || StringComparer.OrdinalIgnoreCase.Equals(collectionName, "DefaultCollection")) ? "" : "/" + collectionName;

            SourceLinkUrl = $"{repoUri.Scheme}://{repoUri.Authority}{collectionPath}/{projectName}/_apis/git/repositories/{repositoryName}/items?" + query;
        }

        // TODO: confirm design and test https://github.com/dotnet/sourcelink/issues/2
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
                    Log.LogError(Resources.EnvironmentVariableIsNotAlistOfUrlPairs, variable.Key, variable.Value);
                    return null;
                }
            }

            return map;
        }

        internal static bool TryParseRepositoryUrl(Uri repoUri, string domain, out string projectName, out string repositoryName, out string collectionName)
        {
            // URL format pattern:
            // https://{domain}/[DefaultCollection/]?{project}/_git/{repository-name}[.git]

            projectName = null;
            repositoryName = null;
            collectionName = null;

            if (!repoUri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) && 
                !repoUri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string localPath = repoUri.LocalPath;
            if (localPath.Length <= 1 || localPath[0] != '/')
            {
                return false;
            }

            // trim leading and optional trailing slash:
            localPath = localPath.Substring(1, (localPath[localPath.Length - 1] == '/') ? localPath.Length - 2 : localPath.Length - 1);

            var parts = localPath.Split('/');
            if (parts.Length < 3 || parts.Length > 4)
            {
                return false;
            }

            if (parts.Length == 4)
            {
                collectionName = parts[0];
                if (collectionName.Length == 0)
                {
                    return false;
                }
            }

            if (!parts[parts.Length - 2].Equals("_git", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            repositoryName = parts[parts.Length - 1];
            projectName = parts[parts.Length - 3];

            if (repositoryName.Length == 0 || projectName.Length == 0)
            {
                return false;
            }

            return true;
        }

        private string GetSourceLinkQuery()
        {
            bool IsHexDigit(char c)
                => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

            string revisionId = SourceRoot.GetMetadata(Names.SourceRoot.RevisionId);
            if (revisionId == null || revisionId.Length != 40 || !revisionId.All(IsHexDigit))
            {
                Log.LogError(Resources.ValueOfWithIdentityIsNotValidCommitHash, Names.SourceRoot.RevisionIdFullName, SourceRoot.ItemSpec, revisionId);
                return null;
            }

            return $"api-version=1.0&versionType=commit&version={revisionId}&path=/*";
        }
    }
}
