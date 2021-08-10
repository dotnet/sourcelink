// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Bitbucket.Git.UnitTests
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
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkBitbucketGitHost", "Bitbucket.Git"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("a/b", "", "a", "b")]
        [InlineData("/a/b", "", "a", "b")]
        [InlineData("/a/b/", "", "a", "b")]
        [InlineData("scm/a", "", "scm", "a")]
        [InlineData("scm/a/b", "", "a", "b")]
        [InlineData("/r/scm/a/b", "r", "a", "b")]
        [InlineData("/r/s/scm/a/b", "r/s", "a", "b")]
        [InlineData("/r/s/a/b", "r/s", "a", "b")]
        [InlineData("/r/s/scm/b", "r/s", "scm", "b")]
        public void TryParseEnterpriseUrl(string relativeUrl, string expectedBaseUrl, string expectedProjectName, string expectedRepositoryName)
        {
            Assert.True(GetSourceLinkUrl.TryParseEnterpriseUrl(relativeUrl, out var baseUrl, out var projectName, out var repositoryName));
            Assert.Equal(expectedBaseUrl, baseUrl);
            Assert.Equal(expectedProjectName, projectName);
            Assert.Equal(expectedRepositoryName, repositoryName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("x")]
        public void TryParseEnterpriseUrl_Errors(string relativeUrl)
        {
            Assert.False(GetSourceLinkUrl.TryParseEnterpriseUrl(relativeUrl, out _, out _, out _));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "/")]
        [InlineData("/", "")]
        [InlineData("/", "/")]
        public void BuildSourceLinkUrl_BitbucketCloud(string s1, string s2)
        {
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mybitbucket.org:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mybitbucket.org", KVP("ContentUrl", "https://domain.com/x/y" + s2), KVP("EnterpriseEdition", "false")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://api.domain.com/x/y/2.0/repositories/a/b/src/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void BuildSourceLinkUrl_BitbucketEnterprise_PersonalToken()
        {
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", ProjectCollection.Escape("https://user_name%40domain.com:Bitbucket_personaltoken@bitbucket.domain.tools/scm/abc/project1.git")), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("bitbucket.domain.tools", KVP("ContentUrl", "https://bitbucket.domain.tools")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://bitbucket.domain.tools/projects/abc/repos/project1/raw/*?at=0123456789abcdefABCDEF000000000000000000", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void BuildSourceLinkUrl_BitbucketEnterprise_InvalidUrl()
        {
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mybitbucket.org/a"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mybitbucket.org", KVP("ContentUrl", "https://domain.com/x/y")),
                }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsInvalid, "SourceRoot.RepositoryUrl", "/src/", "http://subdomain.mybitbucket.org/a"), engine.Log);

            Assert.False(result);
        }

        [Fact]
        public void BuildSourceLinkUrl_MetadataWithEnterpriseEditionButWithoutVersion_UseNewVersionAsDefauld()
        {
            var isEnterpriseEditionSetting = KVP("EnterpriseEdition", "true");
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.domain.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://bitbucket.domain.com"), isEnterpriseEditionSetting),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://bitbucket.domain.com/projects/a/repos/b/raw/*?at=0123456789abcdefABCDEF000000000000000000", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "", "4.4")]
        [InlineData("", "/", "4.4")]
        [InlineData("/", "", "4.4")]
        [InlineData("/", "/", "4.4")]
        [InlineData("", "", "4.6")]
        [InlineData("", "/", "4.6")]
        [InlineData("/", "", "4.6")]
        [InlineData("/", "/", "4.6")]
        public void BuildSourceLinkUrl_BitbucketEnterpriseOldVersionSsh(string s1, string s2, string bitbucketVersion)
        {
            var isEnterpriseEditionSetting = KVP("EnterpriseEdition", "true");
            var version = KVP("Version", bitbucketVersion);
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.domain.com:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://bitbucket.domain.com" + s2), isEnterpriseEditionSetting, version),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://bitbucket.domain.com/projects/a/repos/b/browse/*?at=0123456789abcdefABCDEF000000000000000000&raw", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "", "4.4")]
        [InlineData("", "/", "4.4")]
        [InlineData("/", "", "4.4")]
        [InlineData("/", "/", "4.4")]
        [InlineData("", "", "4.6")]
        [InlineData("", "/", "4.6")]
        [InlineData("/", "", "4.6")]
        [InlineData("/", "/", "4.6")]
        public void BuildSourceLinkUrl_BitbucketEnterpriseOldVersionHttps(string s1, string s2, string bitbucketVersion)
        {
            var isEnterpriseEditionSetting = KVP("EnterpriseEdition", "true");
            var version = KVP("Version", bitbucketVersion);
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.domain.com:100/base/scm/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://bitbucket.domain.com" + s2), isEnterpriseEditionSetting, version),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://bitbucket.domain.com/base/projects/a/repos/b/browse/*?at=0123456789abcdefABCDEF000000000000000000&raw", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "", "")]
        [InlineData("", "", "4.7")]
        [InlineData("", "/", "4.7")]
        [InlineData("/", "", "4.7")]
        [InlineData("/", "/", "4.7")]
        [InlineData("", "", "5.6")]
        [InlineData("", "/", "5.6")]
        [InlineData("/", "", "5.6")]
        [InlineData("/", "/", "5.6")]
        public void BuildSourceLinkUrl_BitbucketEnterpriseNewVersionSsh(string s1, string s2, string bitbucketVersion)
        {
            var isEnterpriseEditionSetting = KVP("EnterpriseEdition", "true");
            var version = KVP("Version", bitbucketVersion);
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.domain.com:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://bitbucket.domain.com" + s2), isEnterpriseEditionSetting, version),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://bitbucket.domain.com/projects/a/repos/b/raw/*?at=0123456789abcdefABCDEF000000000000000000", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "", "")]
        [InlineData("", "", "4.7")]
        [InlineData("", "/", "4.7")]
        [InlineData("/", "", "4.7")]
        [InlineData("/", "/", "4.7")]
        [InlineData("", "", "5.6")]
        [InlineData("", "/", "5.6")]
        [InlineData("/", "", "5.6")]
        [InlineData("/", "/", "5.6")]
        public void BuildSourceLinkUrl_BitbucketEnterpriseNewVersionHttps(string s1, string s2, string bitbucketVersion)
        {
            var isEnterpriseEditionSetting = KVP("EnterpriseEdition", "true");
            var version = KVP("Version", bitbucketVersion);
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.domain.com:100/scm/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://bitbucket.domain.com" + s2), isEnterpriseEditionSetting, version),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://bitbucket.domain.com/projects/a/repos/b/raw/*?at=0123456789abcdefABCDEF000000000000000000", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void BuildSourceLinkUrl_IncorrectVersionForEnterpriseEdition_ERROR()
        {
            var isEnterpriseEditionSetting = KVP("EnterpriseEdition", "true");
            var version = KVP("Version", "incorrect_version");
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.domain.com:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://bitbucket.domain.com"), isEnterpriseEditionSetting, version),
                }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ItemOfItemGroupMustSpecifyMetadata, "domain.com", "SourceLinkBitbucketGitHost", "Version"), engine.Log);
            Assert.False(result);
        }
    }
}
