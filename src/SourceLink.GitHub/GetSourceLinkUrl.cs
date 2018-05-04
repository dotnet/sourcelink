// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.GitHub
{
    /// <summary>
    /// The task calculates SourceLink URL for a given SourceRoot.
    /// If the SourceRoot is associated with a git repository with a recognized domain (e.g. github.com) the <see cref="SourceLinkUrl"/>
    /// output property is set to the content URL corresponding to the domain (e.g. https://raw.githubusercontent.com), otherwise it is set to string "N/A".
    /// The caller may specify additional domains and content URLs in <see cref="Host"/> optional task parameter.
    /// </summary>
    public sealed class GetSourceLinkUrl : Task
    {
        private static readonly Uri s_defaultContentUri = new Uri("https://raw.githubusercontent.com");
        private const string SourceControlName = "git";
        private const string DefaultDomain = "github.com";
        private const string NotApplicableValue = "N/A";
        private const string ContentUrlMetadataName = "ContentUrl";

        [Required]
        public ITaskItem SourceRoot { get; set; }

        /// <summary>
        /// List of additional repository hosts for which the task produces SourceLink URLs.
        /// Each item maps a domain of a repository host (stored in the item identity) to a URL of the server that provides source file content (stored in <c>ContentUrl</c> metadata).
        /// </summary>
        public ITaskItem[] Hosts { get; set; }

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

            var mappings = GetUrlMappings().ToArray();
            var contentUri = GetMatchingContentUri(mappings, repoUri.Host);
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
                Log.LogError(Resources.ValueOfWithIdentityIsNotValidCommitHash, Names.SourceRoot.RevisionIdFullName, SourceRoot.ItemSpec, revisionId);
                return;
            }

            var relativeUrl = repoUri.LocalPath.TrimEnd('/');

            // The URL may or may not end with '.git', but raw.githubusercontent.com does not accept '.git' suffix:
            const string gitUrlSuffix = ".git";
            if (relativeUrl.EndsWith(gitUrlSuffix))
            {
                relativeUrl = relativeUrl.Substring(0, relativeUrl.Length - gitUrlSuffix.Length);
            }

            SourceLinkUrl = new Uri(contentUri, relativeUrl).ToString() + "/" + revisionId + "/*";
        }

        private Uri GetMatchingContentUri(KeyValuePair<string, Uri>[] mappings, string host)
        {
            foreach (var mapping in mappings)
            {
                var domain = mapping.Key;
                var contentUri = mapping.Value;

                if (host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) ||
                    host.Equals(domain, StringComparison.OrdinalIgnoreCase))
                {
                    return contentUri;
                }
            }

            return null;
        }

        private IEnumerable<KeyValuePair<string, Uri>> GetUrlMappings()
        {
            bool isHostUri(Uri uri) => uri.PathAndQuery == "/" && uri.UserInfo == "";

            yield return new KeyValuePair<string, Uri>(DefaultDomain, s_defaultContentUri);

            if (Hosts == null)
            {
                yield break;
            }

            foreach (var item in Hosts)
            {
                string domain = item.ItemSpec;
                string contentUrl = item.GetMetadata(ContentUrlMetadataName);

                if (!Uri.TryCreate("http://" + domain, UriKind.Absolute, out var domainUri) || !isHostUri(domainUri))
                {
                    Log.LogError(Resources.ValuePassedToTaskParameterNotValidDomainName, nameof(Hosts), item.ItemSpec);
                    continue;
                }

                if (!Uri.TryCreate(contentUrl, UriKind.Absolute, out var contentUri) || !isHostUri(contentUri))
                {
                    Log.LogError(Resources.ValuePassedToTaskParameterNotValidHostUri, nameof(Hosts), contentUrl);
                    continue;
                }

                yield return new KeyValuePair<string, Uri>(domain, contentUri);
            }
        }
    }
}
