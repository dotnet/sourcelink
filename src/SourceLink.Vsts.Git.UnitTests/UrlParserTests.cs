// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Xunit;

namespace Microsoft.SourceLink.Vsts.Git.UnitTests
{
    public class UrlParserTests
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
        [InlineData("/project/_ssh/repo")]
        public void TryParseRepositoryUrl_Error(string relativeUrl)
        {
            Assert.False(UrlParser.TryParseRelativeRepositoryUrl(relativeUrl, "_git", out _, out _, out _));
        }

        [Theory]
        [InlineData("/project/_git/repo", "project", "repo", null)]
        [InlineData("/project/_git/repo/", "project", "repo", null)]
        [InlineData("/collection/project/_git/repo", "project", "repo", "collection")]
        [InlineData("/collection/project/_git/repo/", "project", "repo", "collection")]
        public void TryParseRepositoryUrl_Git_Success(string relativeUrl, string project, string repository, string collection)
        {
            Assert.True(UrlParser.TryParseRelativeRepositoryUrl(relativeUrl, "_git", out var actualCollection, out var actualProject, out var actualRepository));
            Assert.Equal(project, actualProject);
            Assert.Equal(repository, actualRepository);
            Assert.Equal(collection, actualCollection);
        }

        [Theory]
        [InlineData("/project/_ssh/repo", "project", "repo", null)]
        [InlineData("/project/_ssh/repo/", "project", "repo", null)]
        [InlineData("/collection/project/_ssh/repo", "project", "repo", "collection")]
        [InlineData("/collection/project/_ssh/repo/", "project", "repo", "collection")]
        public void TryParseRepositoryUrl_Ssh_Success(string relativeUrl, string project, string repository, string collection)
        {
            Assert.True(UrlParser.TryParseRelativeRepositoryUrl(relativeUrl, "_ssh", out var actualCollection, out var actualProject, out var actualRepository));
            Assert.Equal(project, actualProject);
            Assert.Equal(repository, actualRepository);
            Assert.Equal(collection, actualCollection);
        }
    }
}
