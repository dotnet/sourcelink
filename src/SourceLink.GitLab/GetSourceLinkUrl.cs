// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.GitLab
{
    /// <summary>
    /// The task calculates SourceLink URL for a given SourceRoot.
    /// If the SourceRoot is associated with a git repository with a recognized domain the <see cref="SourceLinkUrl"/>
    /// output property is set to the content URL corresponding to the domain, otherwise it is set to string "N/A".
    /// </summary>
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkGitLabHost";
        protected override string ProviderDisplayName => "GitLab";

        private const string VersionMetadataName = "Version";

        // see https://gitlab.com/gitlab-org/gitlab/-/issues/28848
        private static readonly Version s_versionWithNewUrlFormat = new Version(12, 0);

        protected override string? BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem)
        {
            var path = GetVersion(hostItem) >= s_versionWithNewUrlFormat
                ? "-/raw/" + revisionId + "/*"
                : "raw/" + revisionId + "/*";
            return UriUtilities.Combine(UriUtilities.Combine(contentUri.ToString(), relativeUrl), path);
        }

        private Version GetVersion(ITaskItem? hostItem)
        {
            var versionAsString = hostItem?.GetMetadata(VersionMetadataName);
            if (!NullableString.IsNullOrEmpty(versionAsString))
            {
                if (Version.TryParse(versionAsString, out var version))
                {
                    return version;
                }

                Log.LogError(CommonResources.ItemOfItemGroupMustSpecifyMetadata, hostItem!.ItemSpec, HostsItemGroupName, VersionMetadataName);
            }

            return s_versionWithNewUrlFormat;
        }
    }
}
