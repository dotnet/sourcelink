// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.AzureDevOpsServer.Git
{
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkAzureDevOpsServerGitHost";
        protected override string ProviderDisplayName => "AzureDevOpsServer.Git";

        private const string VirtualDirectoryMetadataName = "VirtualDirectory";

        /// <summary>
        /// To constructs correct SourceLink URL we need to know the virtual directory of the AzureDevOps Server.
        /// We can't infer it from repository URL. The user needs to specify it explicitly in the host specification.
        /// </summary>
        protected override bool SupportsImplicitHost => false;

        protected override string BuildSourceLinkUrl(Uri contentUri, Uri gitUri, string relativeUrl, string revisionId, ITaskItem hostItem)
        {
            var virtualDirectory = hostItem.GetMetadata(VirtualDirectoryMetadataName);
            if (!AzureDevOpsUrlParser.TryParseOnPremHttp(relativeUrl, virtualDirectory, out var projectPath, out var repositoryName))
            {
                Log.LogError(CommonResources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, gitUri);
                return null;
            }

            return
                UriUtilities.Combine(
                UriUtilities.Combine(contentUri.ToString(), projectPath), $"_apis/git/repositories/{repositoryName}/items") +
                $"?api-version=1.0&versionType=commit&version={revisionId}&path=/*";
        }
    }
}
