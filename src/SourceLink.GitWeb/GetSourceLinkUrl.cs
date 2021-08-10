// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.GitWeb
{
    /// <summary>
    /// The task calculates SourceLink URL for a given SourceRoot. If the SourceRoot is associated
    /// with a git repository with a recognized domain the <see cref="SourceLinkUrl"/> output
    /// property is set to the content URL corresponding to the domain, otherwise it is set to
    /// string "N/A".
    /// </summary>
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkGitWebHost";
        protected override string ProviderDisplayName => "GitWeb";

        protected override Uri GetDefaultContentUriFromHostUri(string authority, Uri gitUri)
            => new Uri($"https://{authority}/gitweb", UriKind.Absolute);

        protected override string BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem)
        {
            var trimLeadingSlash = relativeUrl.TrimStart('/');
            var trimmedContentUrl = contentUri.ToString().TrimEnd('/', '\\');

            // p = project/path
            // a = action
            // hb = SHA/revision
            // f = repo file path
            var gitwebRawUrl = UriUtilities.Combine(trimmedContentUrl, $"?p={trimLeadingSlash}.git;a=blob_plain;hb={revisionId};f=*");
            return gitwebRawUrl;
        }
    }
}
