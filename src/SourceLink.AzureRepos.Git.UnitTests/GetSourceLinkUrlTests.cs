// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.AzureRepos.Git.UnitTests
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
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkAzureReposGitHost", "AzureRepos.Git"), engine.Log);

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
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.contoso.com:100/account/project/_git/repo" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("contoso.com", KVP("ContentUrl", "https://domain.com/x/y" + s2)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/account/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com", "visualstudio.com")]
        [InlineData("account.vsts.me", "vsts.me")]
        [InlineData("contoso.com/account", "contoso.com")]
        public void BadUrl(string domainAndAccount, string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"http://{domainAndAccount}/_git"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();

            // ERROR : The value of SourceRoot.RepositoryUrl with identity '/src/' is invalid: 'http://account.visualstudio.com/_git'""
            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsInvalid, "SourceRoot.RepositoryUrl", "/src/", $"http://{domainAndAccount}/_git"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com", "visualstudio.com")]
        [InlineData("account.vsts.me", "vsts.me")]
        [InlineData("contoso.com/account", "contoso.com")]
        public void RepoOnly(string domainAndAccount, string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"http://{domainAndAccount}/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"http://{domainAndAccount}/repo/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com", "visualstudio.com")]
        [InlineData("account.vsts.me", "vsts.me")]
        [InlineData("contoso.com/account", "contoso.com")]
        public void Project(string domainAndAccount, string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domainAndAccount}/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domainAndAccount}/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com", "visualstudio.com")]
        [InlineData("account.vsts.me", "vsts.me")]
        public void Project_Team(string domainAndAccount, string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domainAndAccount}/project/team/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domainAndAccount}/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void Project_Team_NonVisualStudioHost()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://contoso.com/account/project/team/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem("contoso.com") }
            };

            bool result = task.Execute();

            // ERROR : The value of SourceRoot.RepositoryUrl with identity '/src/' is invalid: 'http://contoso.com/account/project/team/_git/repo'""
            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.ValueOfWithIdentityIsInvalid, "SourceRoot.RepositoryUrl", "/src/", "http://contoso.com/account/project/team/_git/repo"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com", "visualstudio.com")]
        [InlineData("account.vsts.me", "vsts.me")]
        public void VisualStudioHost_DefaultCollection(string domain, string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domain}/DefaultCollection/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domain}/repo/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);

            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com", "visualstudio.com")]
        [InlineData("account.vsts.me", "vsts.me")]
        public void VisualStudioHost_DefaultCollection_Project(string domain, string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domain}/DefaultCollection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domain}/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com", "visualstudio.com")]
        [InlineData("account.vsts.me", "vsts.me")]
        public void VisualStudioHost_DefaultCollection_Project_Team(string domain, string host)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domain}/DefaultCollection/project/team/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[] { new MockItem(host) }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domain}/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com")]
        [InlineData("account.vsts.me")]
        public void VisualStudioHost_ImplicitHost_DeafultCollection(string domain)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domain}/DefaultCollection/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                IsSingleProvider = true,
                RepositoryUrl = $"https://{domain}/DefaultCollection/project/_git/repo"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domain}/repo/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com")]
        [InlineData("account.vsts.me")]
        public void VisualStudioHost_ImplicitHost_DeafultCollection_Project(string domain)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domain}/DefaultCollection/project/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                IsSingleProvider = true,
                RepositoryUrl = $"https://{domain}/DefaultCollection/project/_git/repo"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domain}/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("account.visualstudio.com")]
        [InlineData("account.vsts.me")]
        public void VisualStudioHost_ImplicitHost_DeafultCollection_Project_Team(string domain)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", $"https://{domain}/DefaultCollection/project/team/_git/repo"), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                IsSingleProvider = true,
                RepositoryUrl = $"https://{domain}/DefaultCollection/project/_git/repo"
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domain}/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
