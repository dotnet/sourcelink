// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.AzureRepos.Git
{
    [MSBuildMultiThreadableTask]
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkAzureReposGitHost";
        protected override string ProviderDisplayName => "AzureRepos.Git";

        protected override Uri GetDefaultContentUriFromHostUri(string authority, Uri gitUri)
        {
            var gitHost = gitUri.GetHost();
            return AzureDevOpsUrlParser.IsVisualStudioHostedServer(gitHost) ?
                new Uri($"{gitUri.Scheme}://{gitHost[..gitHost.IndexOf('.')]}.{authority}", UriKind.Absolute) :
                new Uri($"{gitUri.Scheme}://{authority}", UriKind.Absolute);
        }

        // Repository URL already contains account in case of VS host. Don't add it like we do when the content URL is inferred from host name.
        protected override Uri GetDefaultContentUriFromRepositoryUri(Uri repositoryUri)
            => new Uri($"{repositoryUri.Scheme}://{repositoryUri.GetAuthority()}", UriKind.Absolute);

        protected override string? BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem? hostItem)
        {
            // Azure DevOps does not support optional ".git" suffix in repository URLs and adding it may result in 404.
            // Unlike other providers (GitHub, GitLab, etc.), relativeUrl may include the ".git" suffix,
            // so use gitUri.GetPath() here instead.
            if (!AzureDevOpsUrlParser.TryParseHostedHttp(gitUri.GetHost(), gitUri.GetPath(), out var projectPath, out var repositoryName))
            {
                Log.LogError(CommonResources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot!.ItemSpec, gitUri);
                return null;
            }

            return
                UriUtilities.Combine(
                UriUtilities.Combine(contentUri.ToString(), projectPath), $"_apis/git/repositories/{repositoryName}/items") +
                $"?api-version=1.0&versionType=commit&version={revisionId}&path=/*";
        }
    }
}
