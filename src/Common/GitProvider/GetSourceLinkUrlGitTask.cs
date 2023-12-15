// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.SourceControl
{
    public abstract class GetSourceLinkUrlGitTask : Task
    {
        private const string SourceControlName = "git";
        protected const string NotApplicableValue = "N/A";
        private const string ContentUrlMetadataName = "ContentUrl";

        /// <summary>
        /// Optional, but null is elimated when the task starts executing.
        /// </summary>
        public ITaskItem? SourceRoot { get; set; }

        /// <summary>
        /// List of additional repository hosts for which the task produces SourceLink URLs.
        /// Each item maps a domain of a repository host (stored in the item identity) to a URL of the server that provides source file content (stored in <c>ContentUrl</c> metadata).
        /// <c>ContentUrl</c> is optional.
        /// </summary>
        public ITaskItem[]? Hosts { get; set; }

        public string? RepositoryUrl { get; set; }

        public bool IsSingleProvider { get; set; }

        [Output]
        public string? SourceLinkUrl { get; set; }

        internal GetSourceLinkUrlGitTask() { }

        protected abstract string ProviderDisplayName { get; }
        protected abstract string HostsItemGroupName { get; }
        protected virtual bool SupportsImplicitHost => true;

        /// <summary>
        /// Get the default content URL for given host and git URL.
        /// </summary>
        /// <param name="authority">The host authority.</param>
        /// <param name="gitUri">Remote or submodule URL translated by <see cref="TranslateRepositoryUrlsGitTask"/>.</param>
        /// <remarks>
        /// Use the <paramref name="gitUri"/> scheme. Some servers might not support https, so we can't default to https.
        /// </remarks>
        protected virtual Uri GetDefaultContentUriFromHostUri(string authority, Uri gitUri)
            => new Uri($"{gitUri.Scheme}://{authority}", UriKind.Absolute);

        protected virtual Uri GetDefaultContentUriFromRepositoryUri(Uri repositoryUri)
            => GetDefaultContentUriFromHostUri(repositoryUri.GetAuthority(), repositoryUri);

        protected abstract string? BuildSourceLinkUrl(Uri contentUrl, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem);

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            // Avoid errors when no SourceRoot is specified, _InitializeXyzGitSourceLinkUrl target will simply not update any SourceRoots.
            if (SourceRoot == null)
            {
                return;
            }

            // skip SourceRoot that already has SourceLinkUrl set, or its SourceControl is not "git":
            if (!string.IsNullOrEmpty(SourceRoot.GetMetadata(Names.SourceRoot.SourceLinkUrl)) ||
                !string.Equals(SourceRoot.GetMetadata(Names.SourceRoot.SourceControl), SourceControlName, StringComparison.OrdinalIgnoreCase))
            {
                SourceLinkUrl = NotApplicableValue;
                return;
            }

            var gitUrl = SourceRoot.GetMetadata(Names.SourceRoot.RepositoryUrl);
            if (string.IsNullOrEmpty(gitUrl))
            {
                SourceLinkUrl = NotApplicableValue;

                // If SourceRoot has commit sha but not repository URL the source control info is available,
                // but the remote for the repo has not been defined yet. We already reported missing remote in that case
                // (unless suppressed).
                if (string.IsNullOrEmpty(SourceRoot.GetMetadata(Names.SourceRoot.RevisionId)))
                {
                    Log.LogWarning(CommonResources.UnableToDetermineRepositoryUrl);
                }

                return;
            }

            if (!Uri.TryCreate(gitUrl, UriKind.Absolute, out var gitUri))
            {
                Log.LogError(CommonResources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, gitUrl);
                return;
            }

            var mappings = GetUrlMappings(gitUri).ToArray();
            if (Log.HasLoggedErrors)
            {
                return;
            }

            if (mappings.Length == 0)
            {
                Log.LogError(CommonResources.AtLeastOneRepositoryHostIsRequired, HostsItemGroupName, ProviderDisplayName);
                return;
            }

            if (!TryGetMatchingContentUri(mappings, gitUri, out var contentUri, out var hostItem))
            {
                SourceLinkUrl = NotApplicableValue;
                return;
            }

            static bool isHexDigit(char c)
                => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

            string revisionId = SourceRoot.GetMetadata(Names.SourceRoot.RevisionId);
            if (revisionId == null || revisionId.Length != 40 || !revisionId.All(isHexDigit))
            {
                Log.LogError(CommonResources.ValueOfWithIdentityIsNotValidCommitHash, Names.SourceRoot.RevisionIdFullName, SourceRoot.ItemSpec, revisionId);
                return;
            }

            var relativeUrl = gitUri.GetPath().TrimEnd('/');

            // The URL may or may not end with '.git' (case-sensitive), but content URLs do not include '.git' suffix:
            const string gitUrlSuffix = ".git";
            if (relativeUrl.EndsWith(gitUrlSuffix, StringComparison.Ordinal) && !relativeUrl.EndsWith("/" + gitUrlSuffix, StringComparison.Ordinal))
            {
                relativeUrl = relativeUrl.Substring(0, relativeUrl.Length - gitUrlSuffix.Length);
            }

            SourceLinkUrl = BuildSourceLinkUrl(contentUri, gitUri, relativeUrl, revisionId, hostItem);
        }

        private readonly struct UrlMapping
        {
            public readonly string Host;
            public readonly ITaskItem? HostItem;
            public readonly int Port;
            public readonly Uri ContentUri;
            public readonly bool HasDefaultContentUri;

            public UrlMapping(string host, ITaskItem? hostItem, int port, Uri contentUri, bool hasDefaultContentUri)
            {
                NullableDebug.Assert(port >= -1);
                NullableDebug.Assert(!string.IsNullOrEmpty(host));
                NullableDebug.Assert(contentUri != null);

                Host = host;
                Port = port;
                HostItem = hostItem;
                ContentUri = contentUri;
                HasDefaultContentUri = hasDefaultContentUri;
            }
        }

        private IEnumerable<UrlMapping> GetUrlMappings(Uri gitUri)
        {
            static bool isValidContentUri(Uri uri)
                => uri.GetHost() != "" && uri.Query == "" && uri.UserInfo == "";

            if (Hosts != null)
            {
                foreach (var item in Hosts)
                {
                    string hostUrl = item.ItemSpec;

                    if (!UriUtilities.TryParseAuthority(hostUrl, out var hostUri))
                    {
                        Log.LogError(CommonResources.ValuePassedToTaskParameterNotValidDomainName, nameof(Hosts), item.ItemSpec);
                        continue;
                    }

                    Uri? contentUri;
                    string contentUrl = item.GetMetadata(ContentUrlMetadataName);
                    bool hasDefaultContentUri = string.IsNullOrEmpty(contentUrl);
                    if (hasDefaultContentUri)
                    {
                        contentUri = GetDefaultContentUriFromHostUri(hostUri.GetAuthority(), gitUri);
                    }
                    else if (!Uri.TryCreate(contentUrl, UriKind.Absolute, out contentUri) || !isValidContentUri(contentUri))
                    {
                        Log.LogError(CommonResources.ValuePassedToTaskParameterNotValidHostUri, nameof(Hosts), contentUrl);
                        continue;
                    }

                    yield return new UrlMapping(hostUri.GetHost(), item, hostUri.Port, contentUri, hasDefaultContentUri);
                }
            }

            // Add implicit host last, so that matching prefers explicitly listed hosts over the implicit one.
            if (SupportsImplicitHost && IsSingleProvider)
            {
                if (Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri))
                {
                    // If the URL is a local path the host will be empty.
                    var host = uri.GetHost();
                    if (host != "")
                    {
                        yield return new UrlMapping(host, hostItem: null, uri.GetExplicitPort(), GetDefaultContentUriFromRepositoryUri(uri), hasDefaultContentUri: true);
                    }
                    else
                    {
                        Log.LogError(CommonResources.ValuePassedToTaskParameterNotValidHostUri, nameof(RepositoryUrl), RepositoryUrl);
                    }
                }
                else
                {
                    Log.LogError(CommonResources.ValuePassedToTaskParameterNotValidUri, nameof(RepositoryUrl), RepositoryUrl);
                }
            }
        }

        private static bool TryGetMatchingContentUri(UrlMapping[] mappings, Uri repoUri, [NotNullWhen(true)]out Uri? contentUri, out ITaskItem? hostItem)
        {
            UrlMapping? findMatch(bool exactHost)
            {
                UrlMapping? candidate = null;

                foreach (var mapping in mappings)
                {
                    if (exactHost && repoUri.GetHost().Equals(mapping.Host, StringComparison.OrdinalIgnoreCase) ||
                        !exactHost && repoUri.GetHost().EndsWith("." + mapping.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        // Port matches exactly:
                        if (repoUri.Port == mapping.Port)
                        {
                            return mapping;
                        }

                        // Port not specified:
                        if (candidate == null && mapping.Port == -1)
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
                contentUri = null;
                hostItem = null;
                return false;
            }

            var value = result.Value;
            contentUri = value.ContentUri;
            hostItem = value.HostItem;

            // If the mapping did not specify ContentUrl and did not specify port,
            // use the port from the RepositoryUrl, if a non-default is specified.
            if (value.HasDefaultContentUri && value.Port == -1 && !repoUri.IsDefaultPort && contentUri.Port != repoUri.Port)
            {
                contentUri = new Uri($"{contentUri.Scheme}://{contentUri.Host}:{repoUri.Port}{contentUri.PathAndQuery}");
            }

            return true;
        }
    }
}
