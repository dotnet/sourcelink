// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class GetSourceLinkUrlTests
    {
        [Fact]
        public void NoSourceRoot()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
            };

            Assert.True(task.Execute());
            Assert.Null(task.SourceLinkUrl);
        }

        [Theory]
        [InlineData("contoso*.com")]
        [InlineData("contoso.com/a?x=2")]
        [InlineData("contoso.com/x")]
        [InlineData("a@contoso.com")]
        [InlineData("file:///D:/contoso")]
        [InlineData("http://contoso.com")]
        [InlineData("http://contoso.com/a")]
        [InlineData("http://a@contoso.com")]
        [InlineData("http://contoso.com?x=2")]
        public void HostsDomain_Errors(string domain)
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
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
        [InlineData("http://contoso*.com")]
        // TODO (https://github.com/dotnet/sourcelink/issues/120): fails on Linux [InlineData("/a")]
        public void ImplicitHost_Errors(string repositoryUrl)
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
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
        [InlineData("file:///D:/a/b")]
        public void ImplicitHost_Local(string repositoryUrl)
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
                RepositoryUrl = repositoryUrl,
                IsSingleProvider = true,
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValuePassedToTaskParameterNotValidHostUri, "RepositoryUrl", repositoryUrl), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("contoso.com")]
        [InlineData("contoso.com/a")]
        [InlineData("contoso.com/a?x=2")]
        [InlineData("http://a@contoso.com")]
        [InlineData("http://contoso.com?x=2")]
        [InlineData("a@contoso.com")]
        public void HostsContentUrl_Errors(string url)
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
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
        [InlineData("http://")]
        public void RepositoryUrl_Errors(string url)
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
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

            var task = new MockGetSourceLinkUrlGitTask()
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

            var task = new MockGetSourceLinkUrlGitTask()
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

            var task = new MockGetSourceLinkUrlGitTask()
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
        public void SourceRootNotApplicable_SourceLinkUrlEmpty()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", ""), KVP("SourceControl", "git")),
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "WARNING : " + string.Format(CommonResources.UnableToDetermineRepositoryUrl), engine.Log);

            Assert.Equal("N/A", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void SourceRootNotApplicable_RepositoryUrlNotMatchingHost()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://abc.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "12345")),
                Hosts = new[]
                {
                    new MockItem("domain1.com", KVP("ContentUrl", "https://domain1.com")),
                    new MockItem("domain2.com", KVP("ContentUrl", "http://domain2.com"))
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

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com"),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='http://contoso.com:1234/host-default' GitUrl='http://subdomain.contoso.com:1234/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void ImplicitHost_PortWithDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "http://contoso.com",
                IsSingleProvider = true,
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='http://contoso.com:1234/repo-default' GitUrl='http://subdomain.contoso.com:1234/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_PortWithNonDefaultContentUrl()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://othercontoso.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='https://othercontoso.com/' GitUrl='http://subdomain.contoso.com:1234/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching1()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:1234/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "http://abc.com",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://domain.com")),
                    new MockItem("contoso.com", KVP("ContentUrl", "https://subdomain.contoso.com1:777")),
                    new MockItem("contoso.com", KVP("ContentUrl", "https://subdomain.contoso.com2"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='https://subdomain.contoso.com1:777/' GitUrl='http://subdomain.contoso.com:1234/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching2()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:123/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
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
            AssertEx.AreEqual("ContentUrl='https://domain.com:5/' GitUrl='http://subdomain.contoso.com:123/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching3()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
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
            AssertEx.AreEqual("ContentUrl='https://domain.com:4/' GitUrl='http://subdomain.contoso.com:100/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching4()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:123/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
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
            AssertEx.AreEqual("ContentUrl='https://domain.com:2/' GitUrl='http://subdomain.contoso.com:123/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching5()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
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
            AssertEx.AreEqual("ContentUrl='https://domain.com:1/' GitUrl='http://subdomain.contoso.com:100/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_Matching6()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                RepositoryUrl = "https://contoso.com/collection/project/_git/repo",
                IsSingleProvider = true,
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://zzz.com")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='https://zzz.com/' GitUrl='http://subdomain.contoso.com/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_DefaultPortHttp()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='https://domain.com:1/' GitUrl='http://subdomain.contoso.com/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void CustomHosts_DefaultPortHttps()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://subdomain.contoso.com/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com:80", KVP("ContentUrl", "https://domain.com:1")),
                    new MockItem("contoso.com:443", KVP("ContentUrl", "https://domain.com:2")),
                    new MockItem("contoso.com:1234", KVP("ContentUrl", "https://domain.com:3")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='https://domain.com:2/' GitUrl='https://subdomain.contoso.com/a/b' RelativeUrl='/a/b' RevisionId='0123456789abcdefABCDEF000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimDotGit()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com/a/b.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("contoso.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='http://contoso.com/host-default' GitUrl='http://contoso.com/a/b.git' RelativeUrl='/a/b' RevisionId='0000000000000000000000000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimmingGitIsCaseSensitive()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com/a/b.GIT"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("contoso.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='http://contoso.com/host-default' GitUrl='http://contoso.com/a/b.GIT' RelativeUrl='/a/b.GIT' RevisionId='0000000000000000000000000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void TrimmingGitOnlyWhenSuffix()
        {
            var engine = new MockEngine();

            var task = new MockGetSourceLinkUrlGitTask()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com/a/.git"), KVP("SourceControl", "git"), KVP("RevisionId", "0000000000000000000000000000000000000000")),
                Hosts = new[] { new MockItem("contoso.com") }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("ContentUrl='http://contoso.com/host-default' GitUrl='http://contoso.com/a/.git' RelativeUrl='/a/.git' RevisionId='0000000000000000000000000000000000000000'", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
