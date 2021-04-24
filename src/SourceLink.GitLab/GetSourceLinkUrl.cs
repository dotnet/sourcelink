// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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

        private const string VersionMetadataName = "Version"; // TODO rename to GitLabVersion? Or leave it as Version to be similar with Microsoft.SourceLink.Bitbucket.Git.GetSourceLinkUrl?
        private static readonly Version s_versionWithNewUrlFormat = new Version(13, 5);

        protected override string? BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem)
        {
            var path = GetVersion(hostItem) >= s_versionWithNewUrlFormat
                ? "-/raw/" + revisionId + "/*"
                : "raw/" + revisionId + "/*";
            return UriUtilities.Combine(UriUtilities.Combine(contentUri.ToString(), relativeUrl), path);
        }

        private Version GetVersion(ITaskItem? hostItem)
        {
            // TODO get GitLab version from the environment variable CI_SERVER_VERSION?
            //      see https://docs.gitlab.com/ce/ci/variables/predefined_variables.html

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
