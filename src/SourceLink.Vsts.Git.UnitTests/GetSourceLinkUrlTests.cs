// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using Microsoft.SourceLink.Common.UnitTests;
using Xunit;
using static Microsoft.SourceLink.Common.UnitTests.KeyValuePairUtils;

namespace Microsoft.SourceLink.Vsts.Git.UnitTests
{
    public class GetSourceLinkUrlTests
    {

        [Theory]
        [InlineData("mytfs*.com")]
        [InlineData("mytfs.com/a")]
        [InlineData("mytfs.com/a?x=2")]
        [InlineData("http://mytfs.com")]
        [InlineData("http://a@mytfs.com")]
        [InlineData("a@mytfs.com")]
        public void GetSourceLinkUrl_Domain_Errors(string domain)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
                Domain = domain
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(Resources.ValuePassedToTaskParameterNotValidDomainName, "Domain", domain), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("a/b")]
        [InlineData("")]
        [InlineData("http://")]
        public void GetSourceLinkUrl_RepositoryUrl_Errors(string url)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", url), KVP("SourceControl", "git")),
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(Resources.ValueOfWithIdentityIsInvalid, "SourceRoot.RepositoryUrl", "/src/", url), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("123")]
        [InlineData(" 00000000000000000000000000000000000000")]
        [InlineData("000000000000000000000000000000000000000G")]
        [InlineData("000000000000000000000000000000000000000g")]
        [InlineData("00000000000000000000000000000000000000001")]
        [InlineData("")]
        public void GetSourceLinkUrl_RevisionId_Errors(string revisionId)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://x.visualstudio.com/a/_git/b"), KVP("SourceControl", "git"), KVP("RevisionId", revisionId)),
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(Resources.ValueOfWithIdentityIsNotValidCommitHash, "SourceRoot.RevisionId", "/src/", revisionId), engine.Log);

            Assert.False(result);
        }

        [Fact]
        public void GetSourceLinkUrl_SourceRootNotApplicable_SourceControlNotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://x.visualstudio.com/a/_git/b"), KVP("SourceControl", "tfvc"), KVP("RevisionId", "12345")),
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_SourceRootNotApplicable_SourceLinkUrlSet()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://x.visualstudio.com/a/_git/b"), KVP("SourceControl", "git"), KVP("SourceLinkUrl", "x"), KVP("RevisionId", "12345")),
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("http://x.visualstudio.com")]
        [InlineData("http://x.visualstudio.com/")]
        [InlineData("http://x.visualstudio.com/a")]
        [InlineData("http://x.visualstudio.com/a/")]
        [InlineData("http://x.visualstudio.com/a/b")]
        [InlineData("http://x.visualstudio.com/a/b/")]
        [InlineData("http://x.visualstudio.com/a/b/c")]
        [InlineData("http://x.visualstudio.com/a/b/c/d")]
        [InlineData("http://x.visualstudio.com/a//c")]
        [InlineData("http://x.visualstudio.com/a/_git")]
        [InlineData("http://x.visualstudio.com/a/_git/")]
        [InlineData("http://x.visualstudio.com//_git/b")]
        [InlineData("http://x.visualstudio.com/a/_git/b//")]
        [InlineData("http://x.visualstudio.com/a/b/_git/")]
        [InlineData("http://x.visualstudio.com//b/_git/c")]
        public void TryParseRepositoryUrl_Error(string url)
        {
            Assert.False(GetSourceLinkUrl.TryParseRepositoryUrl(new Uri(url), "visualstudio.com", out _, out _, out _));
        }

        [Theory]
        [InlineData("http://account.visualstudio.com/project/_git/repo", "visualstudio.com", "project", "repo", null)]
        [InlineData("http://account.visualstudio.com/project/_git/repo/", "visualstudio.com", "project", "repo", null)]
        [InlineData("http://account.visualstudio.com/collection/project/_git/repo", "visualstudio.com", "project", "repo", "collection")]
        [InlineData("http://account.visualstudio.com/collection/project/_git/repo/", "visualstudio.com", "project", "repo", "collection")]
        [InlineData("http://visualstudio.com/collection/project/_git/repo", "visualstudio.com", "project", "repo", "collection")]
        [InlineData("http://d.tfs/collection/project/_git/repo", "d.tfs", "project", "repo", "collection")]
        [InlineData("http://d.tfs/collection/project/_git/repo", "tfs", "project", "repo", "collection")]
        [InlineData("http://tfs/collection/project/_git/repo", "tfs", "project", "repo", "collection")]
        [InlineData("http://tfs:123/collection/project/_git/repo", "tfs", "project", "repo", "collection")]
        public void TryParseRepositoryUrl_Success(string url, string domain, string project, string repository, string collection)
        {
            Assert.True(GetSourceLinkUrl.TryParseRepositoryUrl(new Uri(url), domain, out var actualProject, out var actualRepository, out var actualCollection));
            Assert.Equal(project, actualProject);
            Assert.Equal(repository, actualRepository);
            Assert.Equal(collection, actualCollection);
        }

        [Fact]
        public void GetSourceLinkUrl_SourceRootNotApplicable_RepositoryUrlNotDomain_Custom()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://abc.com/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "12345")),
                Domain = "visualstudio.com"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomDomain()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs:1234/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Domain = "tfs"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("http://subdomain.tfs:1234/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomCollection()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Domain = "tfs"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("http://subdomain.tfs:1234/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        /// <summary>
        /// The hosts currently map domains, not ports.
        /// </summary>
        [Fact]
        public void GetSourceLinkUrl_CustomHosts_WithPort()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mytfs.com:1234/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Domain = "mytfs.com:1234"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_DoNotTrimDotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.visualstudio.com/project/_git/repo.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("http://subdomain.visualstudio.com/project/_apis/git/repositories/repo.git/items?api-version=1.0&versionType=commit&version=0000000000000000000000000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        // TODO: test TryGetStandardUriMap: https://github.com/dotnet/sourcelink/issues/2
    }
}
