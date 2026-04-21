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

            var result = task.Execute();

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

            var result = task.Execute();
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

            var result = task.Execute();
            
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

            var result = task.Execute();
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

            var result = task.Execute();
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

            var result = task.Execute();
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

            var result = task.Execute();

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

            var result = task.Execute();
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

            var result = task.Execute();
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

            var result = task.Execute();
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

            var result = task.Execute();
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

            var result = task.Execute();
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

            var result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual($"https://{domain}/project/_apis/git/repositories/repo/items?api-version=1.0&versionType=commit&version=0123456789abcdefABCDEF000000000000000000&path=/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Fact]
        public void DevAzureCom_RepositoryName_WithDotGit_IsNotIgnored()
        {
            var urlWithoutDotGit = ExecuteDevAzureCom("https://dev.azure.com/org/project/_git/repo");
            var urlWithDotGit = ExecuteDevAzureCom("https://dev.azure.com/org/project/_git/repo.git");

            Assert.True(
                !string.Equals(urlWithoutDotGit, urlWithDotGit, StringComparison.Ordinal),
                $"Repository name is ignored: URLs are identical.\n" +
                $"Input without .git: https://dev.azure.com/org/project/_git/repo\n" +
                $"Input with .git:    https://dev.azure.com/org/project/_git/repo.git\n" +
                $"Output:             {urlWithoutDotGit}");
        }

        [Fact]
        public void DevAzureCom_RepositoryName_WithDotGit_IsPreservedInOutput()
        {
            var url = ExecuteDevAzureCom("https://dev.azure.com/org/project/_git/repo.git");

            // Ensure the '.git' suffix is preserved in the repository segment of the URL, not just anywhere.
            Assert.Contains(
                "project/_apis/git/repositories/repo.git/items",
                url);
        }

        private static string ExecuteDevAzureCom(string repositoryUrl)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/",
                    KVP("RepositoryUrl", repositoryUrl),
                    KVP("SourceControl", "git"),
                    KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("dev.azure.com")
                }
            };

            var result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            Assert.True(result);
            Assert.False(string.IsNullOrEmpty(task.SourceLinkUrl));

            return task.SourceLinkUrl!;
        }
    }
}
