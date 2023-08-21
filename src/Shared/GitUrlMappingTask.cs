// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.SourceControl
{
    public abstract class GitUrlMappingTask : Task
    {
        private const string SourceControlName = "git";
        private const string NotApplicableValue = "N/A";
        private const string ContentUrlMetadataName = "ContentUrl";

        [Required]
        public ITaskItem SourceRoot { get; set; }

        /// <summary>
        /// List of additional repository hosts for which the task produces SourceLink URLs.
        /// Each item maps a domain of a repository host (stored in the item identity) to a URL of the server that provides source file content (stored in <c>ContentUrl</c> metadata).
        /// <c>ContentUrl</c> is optional. If not specified it defaults to "https://{domain}/raw".
        /// </summary>
        public ITaskItem[] Hosts { get; set; }

        public string ImplicitHost { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        internal GitUrlMappingTask() { }

        protected abstract string ProviderDisplayName { get; }
        protected abstract string HostsItemGroupName { get; }
        protected abstract Uri GetDefaultContentUri(Uri uri);
        protected abstract string BuildSourceLinkUrl(string contentUrl, string relativeUrl, string revisionId);

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
                Log.LogError(CommonResources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, repoUrl);
                return;
            }

            var mappings = GetUrlMappings().ToArray();
            if (Log.HasLoggedErrors)
            {
                return;
            }

            if (mappings.Length == 0)
            {
                Log.LogError(CommonResources.AtLeastOneRepositoryHostIsRequired, HostsItemGroupName, ProviderDisplayName);
                return;
            }

            var contentUri = GetMatchingContentUri(mappings, repoUri);
            if (contentUri == null)
            {
                SourceLinkUrl = NotApplicableValue;
                return;
            }

            bool IsHexDigit(char c)
                => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

            string revisionId = SourceRoot.GetMetadata(Names.SourceRoot.RevisionId);
            if (revisionId == null || revisionId.Length != 40 || !revisionId.All(IsHexDigit))
            {
                Log.LogError(CommonResources.ValueOfWithIdentityIsNotValidCommitHash, Names.SourceRoot.RevisionIdFullName, SourceRoot.ItemSpec, revisionId);
                return;
            }

            var relativeUrl = repoUri.LocalPath.TrimEnd('/');

            // The URL may or may not end with '.git' (case-sensitive), but content URLs do not include '.git' suffix:
            const string gitUrlSuffix = ".git";
            if (relativeUrl.EndsWith(gitUrlSuffix, StringComparison.Ordinal))
            {
                relativeUrl = relativeUrl.Substring(0, relativeUrl.Length - gitUrlSuffix.Length);
            }

            SourceLinkUrl = BuildSourceLinkUrl(contentUri.ToString(), relativeUrl, revisionId);
        }

        private struct UrlMapping
        {
            public readonly Uri Host;
            public readonly Uri ContentUri;
            public readonly bool HasDefaultContentUri;

            public UrlMapping(Uri host, Uri contentUri, bool hasDefaultContentUri)
            {
                Host = host;
                ContentUri = contentUri;
                HasDefaultContentUri = hasDefaultContentUri;
            }
        }

        private IEnumerable<UrlMapping> GetUrlMappings()
        {
            bool isValidContentUri(Uri uri)
                => uri.Query == "" && uri.UserInfo == "";

            bool tryParseAuthority(string value, out Uri uri)
                => Uri.TryCreate("unknown://" + value, UriKind.Absolute, out uri) && IsAuthorityUri(uri);

            Uri getDefaultUri(string authority)
                => GetDefaultContentUri(new Uri("https://" + authority, UriKind.Absolute));

            if (Hosts != null)
            {
                foreach (var item in Hosts)
                {
                    string authority = item.ItemSpec;

                    if (!tryParseAuthority(authority, out var authorityUri))
                    {
                        Log.LogError(CommonResources.ValuePassedToTaskParameterNotValidDomainName, nameof(Hosts), item.ItemSpec);
                        continue;
                    }

                    Uri contentUri;
                    string contentUrl = item.GetMetadata(ContentUrlMetadataName);
                    bool hasDefaultContentUri = string.IsNullOrEmpty(contentUrl);
                    if (hasDefaultContentUri)
                    {
                        contentUri = getDefaultUri(authority);
                    }
                    else if (!Uri.TryCreate(contentUrl, UriKind.Absolute, out contentUri) || !isValidContentUri(contentUri))
                    {
                        Log.LogError(CommonResources.ValuePassedToTaskParameterNotValidHostUri, nameof(Hosts), contentUrl);
                        continue;
                    }

                    yield return new UrlMapping(authorityUri, contentUri, hasDefaultContentUri);
                }
            }

            // Add implicit host last, so that matching prefers explicitly listed hosts over the implicit one.
            if (!string.IsNullOrEmpty(ImplicitHost))
            {
                if (tryParseAuthority(ImplicitHost, out var authorityUri))
                {
                    yield return new UrlMapping(authorityUri, getDefaultUri(ImplicitHost), hasDefaultContentUri: true);
                }
                else
                {
                    Log.LogError(CommonResources.ValuePassedToTaskParameterNotValidDomainName, nameof(ImplicitHost), ImplicitHost);
                }
            }
        }

        private static Uri GetMatchingContentUri(UrlMapping[] mappings, Uri repoUri)
        {
            UrlMapping? findMatch(bool exactHost)
            {
                UrlMapping? candidate = null;

                foreach (var mapping in mappings)
                {
                    var host = mapping.Host.Host;
                    var port = mapping.Host.Port;
                    var contentUri = mapping.ContentUri;

                    if (exactHost && repoUri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                        !exactHost && repoUri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase))
                    {
                        // Port matches exactly:
                        if (repoUri.Port == port)
                        {
                            return mapping;
                        }

                        // Port not specified:
                        if (candidate == null && port == -1)
                        {
                            candidate = mapping;
                        }
                    }
                }

                return candidate;
            }

            var result = findMatch(exactHost: true) ?? findMatch(exactHost: false);
            if (result == null)
            {
                return null;
            }

            var value = result.Value;

            // If the mapping did not specify ContentUrl and did not specify port,
            // use the port from the RepositoryUrl, if a non-default is specified.
            if (value.HasDefaultContentUri && value.Host.Port == -1 && !repoUri.IsDefaultPort && value.ContentUri.Port != repoUri.Port)
            {
                return new Uri($"{value.ContentUri.Scheme}://{value.ContentUri.Host}:{repoUri.Port}{value.ContentUri.PathAndQuery}");
            }

            return value.ContentUri;
        }
    }
}
