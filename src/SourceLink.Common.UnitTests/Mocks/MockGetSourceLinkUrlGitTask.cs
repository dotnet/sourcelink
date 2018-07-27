// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.IO;
using Xunit;
using TestUtilities;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Framework;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class MockGetSourceLinkUrlGitTask : GetSourceLinkUrlGitTask
    {
        protected override string ProviderDisplayName
            => "Mock";

        protected override string HostsItemGroupName
            => "SourceLinkMockHost";

        protected override string BuildSourceLinkUrl(Uri contentUrl, Uri gitUri, string relativeUrl, string revisionId, ITaskItem hostItem)
            => $"ContentUrl='{contentUrl}' GitUrl='{gitUri}' RelativeUrl='{relativeUrl}' RevisionId='{revisionId}'";

        protected override Uri GetDefaultContentUriFromHostUri(string authority, Uri gitUri)
            => new Uri($"{gitUri.Scheme}://{authority}/host-default", UriKind.Absolute);

        protected override Uri GetDefaultContentUriFromRepositoryUri(Uri repositoryUri)
            => new Uri(UriUtilities.Combine(repositoryUri.ToString(), "repo-default"), UriKind.Absolute);
    }
}
