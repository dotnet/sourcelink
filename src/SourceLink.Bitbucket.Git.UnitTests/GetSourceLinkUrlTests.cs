// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Bitbucket.Git.UnitTests
{
    public class GetSourceLinkUrlTests
    {
        private const string ExpectedUrlForCloudEdition =
            "https://domain.com/x/y/a/b/raw/0123456789abcdefABCDEF000000000000000000/*";
        private const string ExpectedUrlForEnterpriseEditionOldVersion = "https://bitbucket.domain.com/projects/a/repos/b/browse/*?at=0123456789abcdefABCDEF000000000000000000&raw";
        private const string ExpectedUrlForEnterpriseEditionNewVersion = "https://bitbucket.domain.com/projects/a/repos/b/raw/*?at=0123456789abcdefABCDEF000000000000000000";

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

        [Fact]
        public void BuildSourceLinkUrl_bitbucketorgIsHost_UseCloudEditionAsDefault()
        {
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.org:100/a/b"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("bitbucket.org", KVP("ContentUrl", "https://domain.com/x/y"))
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual(ExpectedUrlForCloudEdition, task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "/")]
        [InlineData("/", "")]
        [InlineData("/", "/")]
        public void BuildSourceLinkUrl_BitbucketCloud(string s1, string s2)
        {
            var isEnterpriseEditionSetting = KVP("EnterpriseEdition", "false");
            var engine = new MockEngine();
            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mybitbucket.org:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mybitbucket.org", KVP("ContentUrl", "https://domain.com/x/y" + s2), isEnterpriseEditionSetting),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual(ExpectedUrlForCloudEdition, task.SourceLinkUrl);
            Assert.True(result);
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
            AssertEx.AreEqual(ExpectedUrlForEnterpriseEditionNewVersion, task.SourceLinkUrl);
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
            AssertEx.AreEqual(ExpectedUrlForEnterpriseEditionOldVersion, task.SourceLinkUrl);
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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://bitbucket.domain.com:100/scm/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("domain.com", KVP("ContentUrl", "https://bitbucket.domain.com" + s2), isEnterpriseEditionSetting, version),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual(ExpectedUrlForEnterpriseEditionOldVersion, task.SourceLinkUrl);
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
            AssertEx.AreEqual(ExpectedUrlForEnterpriseEditionNewVersion, task.SourceLinkUrl);
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
            AssertEx.AreEqual(ExpectedUrlForEnterpriseEditionNewVersion, task.SourceLinkUrl);
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
