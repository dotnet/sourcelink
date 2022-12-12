// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Gitee
{
    /// <summary>
    /// The task calculates SourceLink URL for a given SourceRoot.
    /// If the SourceRoot is associated with a git repository with a recognized domain the <see cref="SourceLinkUrl"/>
    /// output property is set to the content URL corresponding to the domain, otherwise it is set to string "N/A".
    /// </summary>
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkGiteeHost";
        protected override string ProviderDisplayName => "Gitee";

        protected override Uri GetDefaultContentUriFromHostUri(string authority, Uri gitUri)
            => new Uri($"{gitUri.Scheme}://{authority}", UriKind.Absolute);

        protected override string? BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem)
            => UriUtilities.Combine(UriUtilities.Combine(contentUri.ToString(), relativeUrl), "raw/"+ revisionId + "/*");
    }
}
