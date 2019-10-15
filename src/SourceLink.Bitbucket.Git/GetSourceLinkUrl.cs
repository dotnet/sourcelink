// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Bitbucket.Git
{
    /// <summary>
    /// The task calculates SourceLink URL for a given SourceRoot.
    /// If the SourceRoot is associated with a git repository with a recognized domain the <see cref="SourceLinkUrl"/>
    /// output property is set to the content URL corresponding to the domain, otherwise it is set to string "N/A".
    /// </summary>
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkBitbucketGitHost";
        protected override string ProviderDisplayName => "Bitbucket.Git";

        private const string IsEnterpriseEditionMetadataName = "EnterpriseEdition";
        private const string VersionMetadataName = "Version";
        private const string VersionWithNewUrlFormat = "4.7";

        protected override string BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem hostItem)
        {
            return
                bool.TryParse(hostItem?.GetMetadata(IsEnterpriseEditionMetadataName), out var isEnterpriseEdition) && !isEnterpriseEdition
                    ? BuildSourceLinkUrlForCloudEdition(contentUri, relativeUrl, revisionId)
                    : BuildSourceLinkUrlForEnterpriseEdition(contentUri, relativeUrl, revisionId, hostItem);
        }

        private string BuildSourceLinkUrlForEnterpriseEdition(Uri contentUri, string relativeUrl, string revisionId,
            ITaskItem hostItem)
        {
            var bitbucketEnterpriseVersion = GetBitbucketEnterpriseVersion(hostItem);

            var splits = relativeUrl.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            var isSshRepoUri = !(splits.Length == 3 && splits[0] == "scm");
            var projectName = isSshRepoUri ? splits[0] : splits[1];
            var repositoryName = isSshRepoUri ? splits[1] : splits[2];

            var relativeUrlForBitbucketEnterprise =
                GetRelativeUrlForBitbucketEnterprise(projectName, repositoryName, revisionId,
                    bitbucketEnterpriseVersion);

            var result = UriUtilities.Combine(contentUri.ToString(), relativeUrlForBitbucketEnterprise);

            return result;
        }

        private Version GetBitbucketEnterpriseVersion(ITaskItem hostItem)
        {
            var bitbucketEnterpriseVersionAsString = hostItem?.GetMetadata(VersionMetadataName);
            Version bitbucketEnterpriseVersion;
            if (!string.IsNullOrEmpty(bitbucketEnterpriseVersionAsString))
            {
                if (!Version.TryParse(bitbucketEnterpriseVersionAsString, out bitbucketEnterpriseVersion))
                {
                    Log.LogError(CommonResources.ItemOfItemGroupMustSpecifyMetadata, hostItem.ItemSpec,
                        HostsItemGroupName, VersionMetadataName);

                    return null;
                }
            }
            else
            {
                bitbucketEnterpriseVersion = Version.Parse(VersionWithNewUrlFormat);
            }

            return bitbucketEnterpriseVersion;
        }

        private static string BuildSourceLinkUrlForCloudEdition(Uri contentUri, string relativeUrl, string revisionId)
        {
            // change bitbuket.org to api.bitbucket.org
            UriBuilder apiUriBuilder = new UriBuilder(contentUri);
            apiUriBuilder.Host = $"api.{apiUriBuilder.Host}";

            string relativeApiUrl = UriUtilities.Combine(UriUtilities.Combine("2.0/repositories", relativeUrl), $"src/{revisionId}/*");

            return UriUtilities.Combine(apiUriBuilder.Uri.ToString(), relativeApiUrl);
        }

        private static string GetRelativeUrlForBitbucketEnterprise(string projectName, string repositoryName, string commitId, Version bitbucketVersion)
        {
            string relativeUrl;
            if (bitbucketVersion >= Version.Parse(VersionWithNewUrlFormat))
            {
                relativeUrl = $"projects/{projectName}/repos/{repositoryName}/raw/*?at={commitId}";
            }
            else
            {
                relativeUrl = $"projects/{projectName}/repos/{repositoryName}/browse/*?at={commitId}&raw";
            }

            return relativeUrl;
        }
    }
}
