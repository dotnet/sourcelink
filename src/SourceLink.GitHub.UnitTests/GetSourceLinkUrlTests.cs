// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.GitHub.UnitTests
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
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkGitHubHost", "GitHub"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("mygithub*.com")]
        [InlineData("mygithub.com/a")]
        [InlineData("mygithub.com/a?x=2")]
        [InlineData("http://mygithub.com")]
        [InlineData("http://a@mygithub.com")]
        [InlineData("a@mygithub.com")]
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
        [InlineData("http://mygithub*.com")]
        // TODO (https://github.com/dotnet/sourcelink/issues/120): fails on Linux [InlineData("/a")]
        public void ImplicitHost_Errors(string repositoryUrl)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
                RepositoryUrl = repositoryUrl,
                IsSingleProvider = true
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValuePassedToTaskParameterNotValidUri, "RepositoryUrl", repositoryUrl), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("myrawgithub.com")]
        [InlineData("myrawgithub.com/a")]
        [InlineData("myrawgithub.com/a?x=2")]
        [InlineData("http://a@myrawgithub.com")]
        [InlineData("a@myrawgithub.com")]
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", revisionId)),
                Hosts = new[] { new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")) }
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b"), KVP("SourceControl", "tfvc"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")) }
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b"), KVP("SourceControl", "git"), KVP("SourceLinkUrl", "x"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")) }
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
                    new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")),
                    new MockItem("mygithub.com", KVP("ContentUrl", "http://mycontent.com"))
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com"),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://mygithub.com:1234/raw/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void ImplicitHost_PortWithDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "https://mygithub.com",
                IsSingleProvider = true,
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://mygithub.com:1234/raw/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_PortWithNonDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://mygithub.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://mygithub.com/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching1()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "http://abc.com",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")),
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://subdomain.rawmygithub1.com:777")),
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://subdomain.rawmygithub2.com"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://subdomain.rawmygithub1.com:777/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching2()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:123/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "http://subdomain.mygithub.com:123",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.mygithub.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.mygithub.com:123", KVP("ContentUrl", "https://domain.com:5")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            // explicit host is preferred over the implicit one 
            AssertEx.AreEqual("https://domain.com:5/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching3()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.mygithub.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.mygithub.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("subdomain.mygithub.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:4/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching4()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:123/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.mygithub.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.mygithub.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.mygithub.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching5()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygithub.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.mygithub.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.mygithub.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.mygithub.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching6()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://github.com/test-org/test-repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "https://github.com/test-org/test-repo",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://raw.githubusercontent.com/test-org/test-repo/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com", KVP("ContentUrl", "https://domain.com/x/y" + s2)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_DefaultPortHttp()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygithub.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygithub.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygithub.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_DefaultPortHttps()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://subdomain.mygithub.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygithub.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygithub.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygithub.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/a/b/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimDotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://raw.githubusercontent.com/a/b/0000000000000000000000000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimmingGitIsCaseSensitive()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://github.com/a/b.GIT"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("github.com", KVP("ContentUrl", "https://raw.githubusercontent.com")) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://raw.githubusercontent.com/a/b.GIT/0000000000000000000000000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
