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
        [Fact]
        public void EmptyHosts()
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
        [InlineData("contoso.com/a")]
        [InlineData("contoso.com/a?x=2")]
        [InlineData("http://contoso.com")]
        [InlineData("http://a@contoso.com")]
        [InlineData("a@contoso.com")]
        public void HostsDomain_Errors(string domain)
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
        [InlineData("http://mytfs*.com")]
        // TODO (https://github.com/dotnet/sourcelink/issues/120): fails on Linux [InlineData("/a")]
        public void ImplicitHost_Errors(string repositoryUrl)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
                RepositoryUrl = repositoryUrl,
                IsSingleProvider = true,
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValuePassedToTaskParameterNotValidUri, "RepositoryUrl", repositoryUrl), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("contoso.com")]
        [InlineData("contoso.com/a")]
        [InlineData("contoso.com/a?x=2")]
        [InlineData("http://a@contoso.com")]
        [InlineData("a@contoso.com")]
        public void HostsContentUrl_Errors(string url)
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
        public void RepositoryUrl_Errors(string url)
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
        public void RevisionId_Errors(string revisionId)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", revisionId)),
                Hosts = new[] { new MockItem("contoso.com", KVP("ContentUrl", "https://contoso.com")) }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsNotValidCommitHash, "SourceRoot.RevisionId", "/src/", revisionId), engine.Log);

            Assert.False(result);
        }

        [Fact]
        public void SourceRootNotApplicable_SourceControlNotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com/a/b"), KVP("SourceControl", "tfvc"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("contoso.com", KVP("ContentUrl", "https://contoso.com")) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void SourceRootNotApplicable_SourceLinkUrlSet()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com/a/b"), KVP("SourceControl", "git"), KVP("SourceLinkUrl", "x"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("github.com", KVP("ContentUrl", "https://contoso.com")) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void SourceRootNotApplicable_RepositoryUrlNotMatchingHost()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://abc.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "12345")),
                Hosts = new[]
                {
                    new MockItem("visualstudio.com", KVP("ContentUrl", "https://visualstudio.com")),
                    new MockItem("contoso.com", KVP("ContentUrl", "http://contoso.com"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_PortWithDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com"),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://contoso.com:1234/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void ImplicitHost_PortWithDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "http://contoso.com",
                IsSingleProvider = true,
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://contoso.com:1234/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_PortWithNonDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://othercontoso.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://othercontoso.com/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching1()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "http://abc.com",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("visualstudio.com", KVP("ContentUrl", "https://visualstudio.com")),
                    new MockItem("contoso.com", KVP("ContentUrl", "https://subdomain.contoso.com1:777")),
                    new MockItem("contoso.com", KVP("ContentUrl", "https://subdomain.contoso.com2"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://subdomain.contoso.com1:777/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching2()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:123/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "http://subdomain.contoso.com:123",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.contoso.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.contoso.com:123", KVP("ContentUrl", "https://domain.com:5")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            // explicit host is preferred over the implicit one 
            AssertEx.AreEqual("https://domain.com:5/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching3()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:100/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.contoso.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.contoso.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("subdomain.contoso.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:4/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching4()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:123/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.contoso.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.contoso.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.contoso.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching5()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:100/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.contoso.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.contoso.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.contoso.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching6()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "https://contoso.com/collection/project/_git/repo",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://zzz.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://zzz.com/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "/")]
        [InlineData("/", "")]
        [InlineData("/", "/")]
        public void CustomHosts_WithPath1(string s1, string s2)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:100/collection/project/_git/repo" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://domain.com/x/y" + s2)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_DefaultPortHttp()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_DefaultPortHttps()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://subdomain.contoso.com/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimDotGit()
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
            AssertEx.AreEqual("https://account.visualstudio.com/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0000000000000000000000000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimmingGitIsCaseSensitive()
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
            AssertEx.AreEqual("https://account.visualstudio.com/collection/project/_apis/git/repositories/repo.GIT/items?api-version=1.0&versionType=commit&version=0000000000000000000000000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimmingGitOnlyWhenSuffix()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://account.visualstudio.com/collection/project/_git/.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("visualstudio.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://account.visualstudio.com/collection/project/_apis/git/repositories/.git/items?api-version=1.0&versionType=commit&version=0000000000000000000000000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("visualstudio.com")]
        [InlineData("vsts.me")]
        public void VisualStudioHost_Explicit(string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"http://account.{host}/DefaultCollection/project/team/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://account.{host}/project/team/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("visualstudio.com")]
        [InlineData("vsts.me")]
        public void VisualStudioHost_Implicit(string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"http://account.{host}/DefaultCollection/project/team/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                IsSingleProvider = true,
                RepositoryUrl = $"http://account.{host}/DefaultCollection/project/team/_git/repo"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://account.{host}/project/team/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
