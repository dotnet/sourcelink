// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.AzureDevOpsServer.Git.UnitTests
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
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkAzureDevOpsServerGitHost", "AzureDevOpsServer.Git"), engine.Log);

            Assert.False(result);
        }

        [Fact]
        public void BadUrl()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://tfs.com/tfs/zzz"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("VirtualDirectory", "tfs"))
                }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsInvalid, "SourceRoot.RepositoryUrl", "/src/", "http://tfs.com/tfs/zzz"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("http://contoso.com/tfs2/collection/project/_git/repo", "tfs")]
        [InlineData("http://contoso.com/tfs/collection/project/_git/repo", "tfs2")]
        [InlineData("http://contoso.com/x/y/collection/project/_git/repo", "x/z")]
        [InlineData("http://contoso.com/x/y/collection/project/_git/repo", "x/z/")]
        [InlineData("http://contoso.com/x/y/collection/project/_git/repo", "/x/z")]
        public void BadUrl_NotMatchingToVirtualDirectory(string url, string virtualDirectory)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", url), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("VirtualDirectory", virtualDirectory))
                }
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsInvalid, "SourceRoot.RepositoryUrl", "/src/", url), engine.Log);

            Assert.False(result);
        }

        [Fact]
        public void ImplicitHostNotSupported()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com:100/tfs/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                IsSingleProvider = true,
                RepositoryUrl = "http://subdomain.tfs.com:100/tfs/collection/project/_git/repo"
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkAzureDevOpsServerGitHost", "AzureDevOpsServer.Git"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "/")]
        [InlineData("/", "")]
        [InlineData("/", "/")]
        public void BuildSourceLinkUrl(string s1, string s2)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.tfs.com:100/collection/project/_git/repo" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("tfs.com", KVP("VirtualDirectory", "/"), KVP("ContentUrl", "https://domain.com/x/y" + s2)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "/")]
        [InlineData("/", "/")]
        [InlineData("a", "/a/")]
        [InlineData("a/", "/a/")]
        [InlineData("/a", "/a/")]
        [InlineData("/a/", "/a/")]
        [InlineData("/A/", "/a/")]
        [InlineData("/A/b/C", "/a/B/c/")]
        public void DefaultContentUrl(string virtualDirectory, string localPath)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"http://contoso.com{localPath}collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("VirtualDirectory", virtualDirectory)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"http://contoso.com{localPath}collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void VirtualDirectory_Collection()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://contoso.com:8080/tfs/collection/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("VirtualDirectory", "tfs")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://contoso.com:8080/tfs/collection/repo/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void VirtualDirectory_Collection_Project()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://contoso.com:8080/tfs/collection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("VirtualDirectory", "tfs")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://contoso.com:8080/tfs/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void VirtualDirectory_Collection_Project_Team()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://contoso.com:8080/tfs/collection/project/team/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("VirtualDirectory", "tfs")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://contoso.com:8080/tfs/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void VirtualDirectory_Collection_Project_Team_Option()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "https://contoso.com:8080/tfs/collection/project/team/_git/_optimized/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("VirtualDirectory", "tfs")),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://contoso.com:8080/tfs/collection/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
