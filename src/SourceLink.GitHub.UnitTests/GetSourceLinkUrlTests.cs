// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.SourceControl.UnitTests;
using Xunit;
using static Microsoft.Build.Tasks.SourceControl.UnitTests.KeyValuePairUtils;

namespace Microsoft.SourceLink.GitHub.UnitTests
{
    public class GetSourceLinkUrlTests
    {
        [Theory]
        [InlineData("mygithub*.com")]
        [InlineData("mygithub.com/a")]
        [InlineData("mygithub.com/a?x=2")]
        [InlineData("http://mygithub.com")]
        [InlineData("http://a@mygithub.com")]
        [InlineData("a@mygithub.com")]
        public void GetSourceLinkUrl_HostsDomain_Errors(string domain)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
                Hosts = new[] { new MockItem(domain) }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(Resources.ValuePassedToTaskParameterNotValidDomainName, "Hosts", domain), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("myrawgithub.com")]
        [InlineData("myrawgithub.com/a")]
        [InlineData("myrawgithub.com/a?x=2")]
        [InlineData("http://a@myrawgithub.com")]
        [InlineData("a@myrawgithub.com")]
        public void GetSourceLinkUrl_HostsContentUrl_Errors(string url)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
                Hosts = new[] { new MockItem("abc.com", KVP("ContentUrl", url)) }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(Resources.ValuePassedToTaskParameterNotValidHostUri, "Hosts", url), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("a/b")]
        [InlineData("/a/b")]
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", revisionId)),
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b"), KVP("SourceControl", "tfvc"), KVP("RevisionId", "12345")),
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b"), KVP("SourceControl", "git"), KVP("SourceLinkUrl", "x"), KVP("RevisionId", "12345")),
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_SourceRootNotApplicable_RepositoryUrlNotDomain_Default()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://mygithub.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "12345")),
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_SourceRootNotApplicable_RepositoryUrlNotDomain_Custom()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://abc.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("mygithub.com", KVP("ContentUrl", "http://mycontent.com")) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("abc.com", KVP("ContentUrl", "https://abc.com")),
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://subdomain.rawmygithub1.com")),
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://subdomain.rawmygithub2.com"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://subdomain.rawmygithub1.com/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_TrimDotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://raw.githubusercontent.com/a/b/0000000000000000000000000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
