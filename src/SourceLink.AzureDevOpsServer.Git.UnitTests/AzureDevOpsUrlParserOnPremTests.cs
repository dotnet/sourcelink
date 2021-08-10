// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using Xunit;

namespace Microsoft.SourceLink.AzureDevOpsServer.Git.UnitTests
{
    public class AzureDevOpsUrlParserOnPremTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("/a")]
        [InlineData("/a/")]
        [InlineData("/a/b")]
        [InlineData("/a/b/")]
        [InlineData("/a/b/c")]
        [InlineData("/a/b/c/d")]
        [InlineData("/a//c")]
        [InlineData("/a/_git")]
        [InlineData("/a/_git/")]
        [InlineData("//_git/b")]
        [InlineData("/a/_git/b//")]
        [InlineData("/a/b/_git/")]
        [InlineData("//b/_git/c")]
        [InlineData("/project/_git/a/b")]
        [InlineData("/project/_ssh/repo")]
        [InlineData("/virtual/dir/project/_git/repo", "/virtual/dir2")]
        [InlineData("/virtual/dir/project/_git/repo", "/virtual/dir/dir3/dir4")]
        public void TryParseOnPremHttp_Error(string relativeUrl, string virtualDir = "/")
        {
            Assert.False(AzureDevOpsUrlParser.TryParseOnPremHttp(relativeUrl, virtualDir, out _, out _));
        }

        [Theory]
        [InlineData("/collection/project/team/_git/repo", "/", "/collection/project", "repo")]
        [InlineData("/collection/project/_git/repo", "/", "/collection/project", "repo")]
        [InlineData("/collection/_git/repo/", "/", "/collection/repo", "repo")]
        [InlineData("/collection/_git/repo", "/", "/collection/repo", "repo")]
        [InlineData("/virtual/iis/path/collection/project/team/_git/repo", "/virtual/iis/path", "virtual/iis/path/collection/project", "repo")]
        public void TryParseOnPremHttp_Success(string relativeUrl, string virtualDirectory, string projectPath, string repositoryName)
        {
            Assert.True(AzureDevOpsUrlParser.TryParseOnPremHttp(relativeUrl, virtualDirectory, out var actualProjectPath, out var actualRepositoryName));
            Assert.Equal(projectPath, actualProjectPath);
            Assert.Equal(repositoryName, actualRepositoryName);
        }

        [Theory]
        [InlineData("ssh://user@host")]
        [InlineData("ssh://user@host:22")]
        [InlineData("ssh://user@host/")]
        [InlineData("ssh://user@host:22/")]
        [InlineData("ssh://user@host/a")]
        [InlineData("ssh://user@host/a/")]
        [InlineData("ssh://user@host/a/b")]
        [InlineData("ssh://user@host/a/b/")]
        [InlineData("ssh://user@host/a/b/c")]
        [InlineData("ssh://user@host/a/b/c/d")]
        [InlineData("ssh://user@host/a//c")]
        [InlineData("ssh://user@host/a/_ssh")]
        [InlineData("ssh://user@host/a/_ssh/")]
        [InlineData("ssh://user@host//_ssh/b")]
        [InlineData("ssh://user@host/a/_ssh/b//")]
        [InlineData("ssh://user@host/a/b/_ssh/")]
        [InlineData("ssh://user@host//b/_ssh/c")]
        [InlineData("ssh://user@host/project/_ssh/a/b")]
        [InlineData("ssh://user@host/project/_git/repo")]
        public void TryParseOnPremSsh_Error(string url)
        {
            Assert.False(AzureDevOpsUrlParser.TryParseOnPremSsh(new Uri(url, UriKind.Absolute), out var _, out var _));
        }

        [Theory]
        [InlineData("ssh://user@host/project/_ssh/repo", "project", "repo")]
        [InlineData("ssh://user@host/project/_ssh/repo/", "project", "repo")]
        [InlineData("ssh://user@host:123/project/_ssh/repo", "project", "repo")]
        [InlineData("ssh://user@host/collection/project/_ssh/repo", "collection/project", "repo")]
        [InlineData("ssh://user@host/collection/project/team/_ssh/repo", "collection/project/team", "repo")]
        [InlineData("ssh://user@host/collection/_ssh/repo", "collection", "repo")]
        [InlineData("ssh://user@host/virtual/iis/path/collection/project/team/_ssh/repo", "virtual/iis/path/collection/project/team", "repo")]
        public void TryParseOnPremSsh_Success(string url, string repositoryPath, string repositoryName)
        {
            Assert.True(AzureDevOpsUrlParser.TryParseOnPremSsh(new Uri(url, UriKind.Absolute), out var actualRepositoryPath, out var actualRepositoryName));
            Assert.Equal(repositoryPath, actualRepositoryPath);
            Assert.Equal(repositoryName, actualRepositoryName);
        }
    }
}
