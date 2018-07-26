// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.IO;
using Xunit;
using TestUtilities;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class MockGetSourceLinkUrlGitTask : GetSourceLinkUrlGitTask
    {
        protected override string ProviderDisplayName
            => "Mock";

        protected override string HostsItemGroupName
            => "SourceLinkMockHost";

        protected override string BuildSourceLinkUrl(Uri contentUrl, string host, string relativeUrl, string revisionId)
            => $"ContentUrl='{contentUrl}' Host='{host}' RelativeUrl='{relativeUrl}' RevisionId='{revisionId}'";

        protected override Uri GetDefaultContentUriFromHostUri(Uri hostUri, Uri gitUri)
            => new Uri(hostUri, "host-default");

        protected override Uri GetDefaultContentUriFromRepositoryUri(Uri repositoryUri)
            => new Uri(repositoryUri, "repo-default");
    }
}
