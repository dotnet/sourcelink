// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using Xunit;

namespace Microsoft.SourceLink.Tfs.Git.UnitTests
{
    public class TeamFoundationUrlParserOnPremTests
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
        [InlineData("project/_git/repo")]
        [InlineData("/project/_git/a/b")]
        [InlineData("/project/_ssh/repo")]
        public void TryParseOnPremHttp_Error(string relativeUrl)
        {
            Assert.False(TeamFoundationUrlParser.TryParseOnPremHttp(relativeUrl, out var _, out var _));
        }

        [Theory]
        [InlineData("/project/_git/repo", "project", "repo")]
        [InlineData("/project/_git/repo/", "project", "repo")]
        [InlineData("/collection/project/_git/repo", "collection/project", "repo")]
        [InlineData("/collection/project/team/_git/repo", "collection/project/team", "repo")]
        [InlineData("/collection/_git/repo", "collection", "repo")]
        [InlineData("/virtual/iis/path/collection/project/team/_git/repo", "virtual/iis/path/collection/project/team", "repo")]
        public void TryParseOnPremHttp_Success(string relativeUrl, string repositoryPath, string repositoryName)
        {
            Assert.True(TeamFoundationUrlParser.TryParseOnPremHttp(relativeUrl, out var actualRepositoryPath, out var actualRepositoryName));
            Assert.Equal(repositoryPath, actualRepositoryPath);
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
            Assert.False(TeamFoundationUrlParser.TryParseOnPremSsh(new Uri(url, UriKind.Absolute), out var _, out var _));
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
            Assert.True(TeamFoundationUrlParser.TryParseOnPremSsh(new Uri(url, UriKind.Absolute), out var actualRepositoryPath, out var actualRepositoryName));
            Assert.Equal(repositoryPath, actualRepositoryPath);
            Assert.Equal(repositoryName, actualRepositoryName);
        }
    }
}
