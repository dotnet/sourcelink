// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Gitea
{
    /// <summary>
    /// The task calculates SourceLink URL for a given SourceRoot.
    /// If the SourceRoot is associated with a git repository with a recognized domain the <see cref="SourceLinkUrl"/>
    /// output property is set to the content URL corresponding to the domain, otherwise it is set to string "N/A".
    /// </summary>
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkGiteaHost";
        protected override string ProviderDisplayName => "Gitea";

        protected override string? BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem)
            => UriUtilities.Combine(UriUtilities.Combine(contentUri.ToString(), relativeUrl), "raw/commit/" + revisionId + "/*");
    }
}
