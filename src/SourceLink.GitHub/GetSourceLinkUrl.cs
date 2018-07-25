// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.GitHub
{
    /// <summary>
    /// The task calculates SourceLink URL for a given SourceRoot.
    /// If the SourceRoot is associated with a git repository with a recognized domain the <see cref="SourceLinkUrl"/>
    /// output property is set to the content URL corresponding to the domain, otherwise it is set to string "N/A".
    /// </summary>
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkGitHubHost";
        protected override string ProviderDisplayName => "GitHub";

        protected override Uri GetDefaultContentUriFromHostUri(Uri hostUri, Uri gitUri)
            => new Uri(hostUri, "raw");

        protected override string BuildSourceLinkUrl(Uri contentUri, string host, string relativeUrl, string revisionId)
            => UriUtilities.Combine(UriUtilities.Combine(contentUri.ToString(), relativeUrl), revisionId + "/*");
    }
}
