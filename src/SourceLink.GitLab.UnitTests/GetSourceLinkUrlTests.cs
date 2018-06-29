// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.GitLab.UnitTests
{
    public class GetSourceLinkUrlTests
    {
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
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkGitLabHosts", "GitLab"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("mygitlab*.com")]
        [InlineData("mygitlab.com/a")]
        [InlineData("mygitlab.com/a?x=2")]
        [InlineData("http://mygitlab.com")]
        [InlineData("http://a@mygitlab.com")]
        [InlineData("a@mygitlab.com")]
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
        [InlineData("mygitlab*.com")]
        [InlineData("mygitlab.com/a")]
        [InlineData("mygitlab.com/a?x=2")]
        [InlineData("http://mygitlab.com")]
        [InlineData("http://a@mygitlab.com")]
        [InlineData("a@mygitlab.com")]
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
        [InlineData("mygitlab.com")]
        [InlineData("mygitlab.com/a")]
        [InlineData("mygitlab.com/a?x=2")]
        [InlineData("http://a@mygitlab.com")]
        [InlineData("a@mygitlab.com")]
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://gitlab.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", revisionId)),
                Hosts = new[] { new MockItem("gitlab.com") }
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://gitlab.com/a/b"), KVP("SourceControl", "tfvc"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("gitlab.com") }
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://gitlab.com/a/b"), KVP("SourceControl", "git"), KVP("SourceLinkUrl", "x"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("gitlab.com") }
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://mygitlab.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "12345")),
                Hosts = new[] { new MockItem("gitlab.com") }
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
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "http://mycontent.com"))
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com"),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://mygitlab.com:1234/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_ImplicitHost_PortWithDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                ImplicitHost = "mygitlab.com",
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://mygitlab.com:1234/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_PortWithNonDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://mygitlab.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://mygitlab.com/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching1()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                ImplicitHost = "abc.com",
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://subdomain.rawmygitlab1.com:777")),
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://subdomain.rawmygitlab2.com"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://subdomain.rawmygitlab1.com:777/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching2()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:123/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                ImplicitHost = "subdomain.mygitlab.com:123",
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.mygitlab.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.mygitlab.com:123", KVP("ContentUrl", "https://domain.com:5")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            // explicit host is preferred over the implicit one 
            AssertEx.AreEqual("https://domain.com:5/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching3()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("subdomain.mygitlab.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("subdomain.mygitlab.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("subdomain.mygitlab.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:4/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching4()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:123/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.mygitlab.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.mygitlab.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.mygitlab.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_Matching5()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygitlab.com:123", KVP("ContentUrl", "https://domain.com:3")),
                    new MockItem("z.mygitlab.com", KVP("ContentUrl", "https://domain.com:4")),
                    new MockItem("z.mygitlab.com:123", KVP("ContentUrl", "https://domain.com:5")),
                    new MockItem("z.mygitlab.com:123", KVP("ContentUrl", "https://domain.com:6")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com/x/y" + s2)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_DefaultPortHttp()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygitlab.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygitlab.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:1/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_CustomHosts_DefaultPortHttps()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://subdomain.mygitlab.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("mygitlab.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("mygitlab.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com:2/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_TrimDotGit()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://gitlab.com/a/b.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("gitlab.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://gitlab.com/a/b/raw/0000000000000000000000000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void GetSourceLinkUrl_TrimmingGitIsCaseSensitive()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://gitlab.com/a/b.GIT"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("gitlab.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://gitlab.com/a/b.GIT/raw/0000000000000000000000000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
