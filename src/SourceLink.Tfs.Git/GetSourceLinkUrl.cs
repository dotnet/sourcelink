// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Tfs.Git
{
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        protected override string HostsItemGroupName => "SourceLinkTfsGitHost";
        protected override string ProviderDisplayName => "Tfs.Git";

        protected override Uri GetDefaultContentUriFromHostUri(Uri hostUri, Uri gitUri)
            => hostUri;

        protected override string BuildSourceLinkUrl(Uri contentUri, string host, string relativeUrl, string revisionId)
        {
            if (!TeamFoundationUrlParser.TryParseOnPremHttp(relativeUrl, out var repositoryPath, out var repositoryName))
            {
                // TODO: Log.LogError(CommonResources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, repoUrl);
                return null;
            }

            return
                UriUtilities.Combine(
                UriUtilities.Combine(contentUri.ToString(), repositoryPath), $"_apis/git/repositories/{repositoryName}/items") +
                $"?api-version=1.0&versionType=commit&version={revisionId}&path=/*";
        }
    }
}
