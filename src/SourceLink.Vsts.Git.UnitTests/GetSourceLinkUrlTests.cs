// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Vsts.Git.UnitTests
{
    public class GetSourceLinkUrlTests
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
        public void TryParseRepositoryUrl_Error(string relativeUrl)
        {
            Assert.False(GetSourceLinkUrl.TryParseRelativeRepositoryUrl(relativeUrl, out _, out _, out _));
        }

        [Theory]
        [InlineData("/project/_git/repo", "project", "repo", null)]
        [InlineData("/project/_git/repo/", "project", "repo", null)]
        [InlineData("/collection/project/_git/repo", "project", "repo", "collection")]
        [InlineData("/collection/project/_git/repo/", "project", "repo", "collection")]
        public void TryParseRepositoryUrl_Success(string relativeUrl, string project, string repository, string collection)
        {
            Assert.True(GetSourceLinkUrl.TryParseRelativeRepositoryUrl(relativeUrl, out var actualProject, out var actualRepository, out var actualCollection));
            Assert.Equal(project, actualProject);
            Assert.Equal(repository, actualRepository);
            Assert.Equal(collection, actualCollection);
        }

        [Fact]
        public void GetSourceLinkUrl_EmptyHosts()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkVstsGitHost", "Vsts.Git"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("mytfs*.com")]
        [InlineData("mytfs.com/a")]
        [InlineData("mytfs.com/a?x=2")]
        [InlineData("http://mytfs.com")]
        [InlineData("http://a@mytfs.com")]
        [InlineData("a@mytfs.com")]
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
                "ERROR : " + string.Format(CommonResources.ValuePassedToTaskParameterNotValidDomainName, "Hosts", domain), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("mytfs*.com")]
        [InlineData("mytfs.com/a")]
        [InlineData("mytfs.com/a?x=2")]
        [InlineData("http://mytfs.com")]
        [InlineData("http://a@mytfs.com")]
        [InlineData("a@mytfs.com")]
        public void GetSourceLinkUrl_ImplicitHost_Errors(string domain)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
                ImplicitHost = domain
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValuePassedToTaskParameterNotValidDomainName, "ImplicitHost", domain), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("mytfs.com")]
        [InlineData("mytfs.com/a")]
        [InlineData("mytfs.com/a?x=2")]
        [InlineData("http://a@mytfs.com")]
        [InlineData("a@mytfs.com")]
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
                "ERROR : " + string.Format(CommonResources.ValuePassedToTaskParameterNotValidHostUri, "Hosts", url), engine.Log);

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
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsInvalid, "SourceRoot.RepositoryUrl", "/src/", url), engine.Log);

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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://tfs.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", revisionId)),
                Hosts = new[] { new MockItem("tfs.com", KVP("ContentUrl", "https://tfs.com")) }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsNotValidCommitHash, "SourceRoot.RevisionId", "/src/", revisionId), engine.Log);

            Assert.False(result);
        }

        [Fact]
        public void GetSourceLinkUrl_SourceRootNotApplicable_SourceControlNotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://tfs.com/a/b"), KVP("SourceControl", "tfvc"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("tfs.com", KVP("ContentUrl", "https://tfs.com")) }
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://tfs.com/a/b"), KVP("SourceControl", "git"), KVP("SourceLinkUrl", "x"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("github.com", KVP("ContentUrl", "https://tfs.com")) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_SourceRootNotApplicable_RepositoryUrlNotMatchingHost()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://abc.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "12345")),
                Hosts = new[]
                {
                    new MockItem("visualstudio.com", KVP("ContentUrl", "https://visualstudio.com")),
                    new MockItem("mytfs.com", KVP("ContentUrl", "http://mytfs.com"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_PortWithDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com"),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://tfs.com:1234/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_ImplicitHost_PortWithDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                ImplicitHost = "tfs.com",
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://tfs.com:1234/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_PortWithNonDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("ContentUrl", "https://othertfs.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://othertfs.com/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching1()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                ImplicitHost = "abc.com",
                Hosts = new[]
                {
                    new MockItem("visualstudio.com", KVP("ContentUrl", "https://visualstudio.com")),
                    new MockItem("tfs.com", KVP("ContentUrl", "https://subdomain.tfs.com1:777")),
                    new MockItem("tfs.com", KVP("ContentUrl", "https://subdomain.tfs.com2"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://subdomain.tfs.com1:777/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching2()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:123/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                ImplicitHost = "subdomain.tfs.com:123",
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.tfs.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.tfs.com:123", KVP("ContentUrl", "https://domain.com:5")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            // explicit host is preferred over the implicit one 
            AssertEx.AreEqual("https://domain.com:5/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching3()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:100/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.tfs.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.tfs.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("subdomain.tfs.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:4/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching4()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:123/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.tfs.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.tfs.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.tfs.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching5()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:100/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("tfs.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.tfs.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.tfs.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.tfs.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "/")]
        [InlineData("/", "")]
        [InlineData("/", "/")]
        public void GetSourceLinkUrl_CustomHosts_WithPath1(string s1, string s2)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:100/collection/project/_git/repo" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("ContentUrl", "https://domain.com/x/y" + s2)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_DefaultPortHttp()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("tfs.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("tfs.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_DefaultPortHttps()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://subdomain.tfs.com/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("tfs.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("tfs.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_TrimDotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://account.visualstudio.com/collection/project/_git/repo.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("visualstudio.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://visualstudio.com/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0000000000000000000000000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_TrimmingGitIsCaseSensitive()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://account.visualstudio.com/collection/project/_git/repo.GIT"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("visualstudio.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://visualstudio.com/collection/project/_apis/git/repositories/repo.GIT/items?api-version=1.0&versionType=commit&version=0000000000000000000000000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
