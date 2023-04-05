// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        private static readonly Version s_versionWithNewUrlFormat = new Version(4, 7);

        protected override string? BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem)
        {
            // The SourceLinkBitbucketGitHost item for bitbucket.org specifies EnterpriseEdition="false".
            // Other items that may be specified by the project default to EnterpriseEdition="true" without specifying it.
            bool isCloud = bool.TryParse(hostItem?.GetMetadata(IsEnterpriseEditionMetadataName), out var isEnterpriseEdition) && !isEnterpriseEdition;

            if (isCloud)
            {
                return BuildSourceLinkUrlForCloudEdition(contentUri, relativeUrl, revisionId);
            }

            if (TryParseEnterpriseUrl(relativeUrl, out var relativeBaseUrl, out var projectName, out var repositoryName))
            {
                var version = GetBitbucketEnterpriseVersion(hostItem);
                return BuildSourceLinkUrlForEnterpriseEdition(contentUri, relativeBaseUrl, projectName, repositoryName, revisionId, version);
            }

            Log.LogError(CommonResources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot!.ItemSpec, gitUri);
            return null;
        }

        internal static string BuildSourceLinkUrlForEnterpriseEdition(
            Uri contentUri,
            string relativeBaseUrl,
            string projectName,
            string repositoryName,
            string commitSha,
            Version version)
        {
            var relativeUrl = (version >= s_versionWithNewUrlFormat) ? 
                $"projects/{projectName}/repos/{repositoryName}/raw/*?at={commitSha}" :
                $"projects/{projectName}/repos/{repositoryName}/browse/*?at={commitSha}&raw";

            return UriUtilities.Combine(contentUri.ToString(), UriUtilities.Combine(relativeBaseUrl, relativeUrl));
        }

        internal static bool TryParseEnterpriseUrl(string relativeUrl, [NotNullWhen(true)]out string? relativeBaseUrl, [NotNullWhen(true)]out string? projectName, [NotNullWhen(true)]out string? repositoryName)
        {
            // HTTP: {baseUrl}/scm/{projectName}/{repositoryName}
            // SSH: {baseUrl}/{projectName}/{repositoryName}

            if (!UriUtilities.TrySplitRelativeUrl(relativeUrl, out var parts) || parts.Length < 2)
            {
                relativeBaseUrl = projectName = repositoryName = null;
                return false;
            }

            var i = parts.Length - 1;

            repositoryName = parts[i--];
            projectName = parts[i--];

            if (i >= 0 && parts[i] == "scm")
            {
                i--;
            }

            Debug.Assert(i >= -1);
            relativeBaseUrl = string.Join("/", parts, 0, i + 1);
            return true;
        }

        private Version GetBitbucketEnterpriseVersion(ITaskItem? hostItem)
        {
            var bitbucketEnterpriseVersionAsString = hostItem?.GetMetadata(VersionMetadataName);
            if (!NullableString.IsNullOrEmpty(bitbucketEnterpriseVersionAsString))
            {
                if (Version.TryParse(bitbucketEnterpriseVersionAsString, out var version))
                {
                    return version;
                }

                Log.LogError(CommonResources.ItemOfItemGroupMustSpecifyMetadata, hostItem!.ItemSpec,
                    HostsItemGroupName, VersionMetadataName);
            }
            
            return s_versionWithNewUrlFormat;
        }

        private static string BuildSourceLinkUrlForCloudEdition(Uri contentUri, string relativeUrl, string revisionId)
        {
            // change bitbucket.org to api.bitbucket.org
            UriBuilder apiUriBuilder = new UriBuilder(contentUri);
            apiUriBuilder.Host = $"api.{apiUriBuilder.Host}";

            string relativeApiUrl = UriUtilities.Combine(UriUtilities.Combine("2.0/repositories", relativeUrl), $"src/{revisionId}/*");

            return UriUtilities.Combine(apiUriBuilder.Uri.ToString(), relativeApiUrl);
        }
    }
}
