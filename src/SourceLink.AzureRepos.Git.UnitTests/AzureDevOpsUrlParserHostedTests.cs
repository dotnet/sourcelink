// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using Xunit;

namespace Microsoft.SourceLink.AzureRepos.Git.UnitTests
{
    public class AzureDevOpsUrlParserHostedTests
    {
        [Theory]
        [InlineData("account.visualstudio.com", "")]
        [InlineData("account.visualstudio.com", "/")]
        [InlineData("account.visualstudio.com", "/a")]
        [InlineData("account.visualstudio.com", "/a/")]
        [InlineData("account.visualstudio.com", "/a/b")]
        [InlineData("account.visualstudio.com", "/a/b/")]
        [InlineData("account.visualstudio.com", "/a/b/c")]
        [InlineData("account.visualstudio.com", "/a/b/c/d")]
        [InlineData("account.visualstudio.com", "/a//c")]
        [InlineData("account.visualstudio.com", "/a/_git")]
        [InlineData("account.visualstudio.com", "/a/_git/")]
        [InlineData("account.visualstudio.com", "//_git/b")]
        [InlineData("account.visualstudio.com", "/a/_git/b//")]
        [InlineData("account.visualstudio.com", "/a/b/_git/")]
        [InlineData("account.visualstudio.com", "//b/_git/c")]
        [InlineData("account.visualstudio.com", "/project/_ssh/repo")]
        [InlineData("account.visualstudio.com", "/DefaultCollection/project/_ssh/repo")]
        [InlineData("contoso.com", "/e/_git/repo")]
        [InlineData("contoso.com", "/e/enterprise/_git/repo")]
        [InlineData("contoso.com", "/account/project/team/_git/repo")]
        public void TryParseHostedHttp_Error(string host, string relativeUrl)
        {
            Assert.False(AzureDevOpsUrlParser.TryParseHostedHttp(host, relativeUrl, out _, out _));
        }

        [Theory]
        [InlineData("account.visualstudio.com", "/_git/repo", "repo", "repo")]
        [InlineData("account.visualstudio.com", "/project/_git/repo", "project", "repo")]
        [InlineData("account.visualstudio.com", "/project/team/_git/repo", "project", "repo")]
        [InlineData("account.visualstudio.com", "/DefaultCollection/project/_git/repo", "project", "repo")]
        [InlineData("account.visualstudio.com", "/DefaultCollection/project/team/_git/repo", "project", "repo")]
        [InlineData("account.visualstudio.com", "/DefaultCollection/project/team/_git/_full/repo", "project", "repo")]
        [InlineData("account.visualstudio.com", "/DefaultCollection/project/team/_git/_optimized/repo", "project", "repo")]
        [InlineData("account.visualstudio.com", "/DefaultCollection/_git/repo", "repo", "repo")]
        [InlineData("account.vsts.me", "/_git/repo", "repo", "repo")]
        [InlineData("account.vsts.me", "/project/_git/repo", "project", "repo")]
        [InlineData("account.vsts.me", "/project/team/_git/repo", "project", "repo")]
        [InlineData("account.vsts.me", "/DefaultCollection/project/_git/repo", "project", "repo")]
        [InlineData("account.vsts.me", "/DefaultCollection/project/team/_git/repo", "project", "repo")]
        [InlineData("account.vsts.me", "/DefaultCollection/project/team/_git/_full/repo", "project", "repo")]
        [InlineData("account.vsts.me", "/DefaultCollection/project/team/_git/_optimized/repo", "project", "repo")]
        [InlineData("account.vsts.me", "/DefaultCollection/_git/repo", "repo", "repo")]
        [InlineData("contoso.com", "/account/_git/repo", "account/repo", "repo")]
        [InlineData("contoso.com", "/account/project/_git/repo", "account/project", "repo")]
        [InlineData("contoso.com", "/account/project/_git/_full/repo", "account/project", "repo")]
        [InlineData("contoso.com", "/account/project/_git/_optimized/repo", "account/project", "repo")]
        public void TryParseHostedHttp_Success(string host, string relativeUrl, string repositoryPath, string repositoryName)
        {
            Assert.True(AzureDevOpsUrlParser.TryParseHostedHttp(host, relativeUrl, out var actualRepositoryPath, out var actualRepositoryName));
            Assert.Equal(repositoryPath, actualRepositoryPath);
            Assert.Equal(repositoryName, actualRepositoryName);
        }

        [Theory]
        [InlineData("ssh://user@vs-ssh.visualstudio.com")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com:22")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com:22/")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/b")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/b/")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/b/c")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/b/c/d")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a//c")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/_ssh")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/_ssh/")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com//_ssh/b")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/_ssh/b//")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/a/b/_ssh/")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com//b/_ssh/c")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/project/_ssh/a/b")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/project/_git/repo")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/v3")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/v3/account")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/v3/account/repo")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/v3/account/project/repo")]
        [InlineData("ssh://user@vs-ssh.visualstudio.com/v3//project/repo")]
        [InlineData("ssh://account1@vs-ssh.visualstudio.com/v3/account2/repo")]
        [InlineData("ssh://vs-ssh.vsts.me/v3/account2/repo")]
        public void TryParseHostedSsh_Error(string url)
        {
            Assert.False(AzureDevOpsUrlParser.TryParseOnPremSsh(new Uri(url, UriKind.Absolute), out var _, out var _));
        }

        [Theory]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/project/_ssh/repo", "account", "project", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/project/team/_ssh/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/DefaultCollection/project/_ssh/repo", "account", "project", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/DefaultCollection/project/team/_ssh/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/DefaultCollection/project/team/_ssh/_full/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/DefaultCollection/project/team/_ssh/_optimized/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/DefaultCollection/_ssh/repo", "account", "", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/_ssh/repo", "account", "", "repo")]

        [InlineData("ssh://account@vs-ssh.vsts.me/project/_ssh/repo", "account", "project", "repo")]
        [InlineData("ssh://account@vs-ssh.vsts.me/project/team/_ssh/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.vsts.me/DefaultCollection/project/_ssh/repo", "account", "project", "repo")]
        [InlineData("ssh://account@vs-ssh.vsts.me/DefaultCollection/project/team/_ssh/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.vsts.me/DefaultCollection/project/team/_ssh/_full/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.vsts.me/DefaultCollection/project/team/_ssh/_optimized/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@vs-ssh.vsts.me/DefaultCollection/_ssh/repo", "account", "", "repo")]
        [InlineData("ssh://account@vs-ssh.vsts.me/_ssh/repo", "account", "", "repo")]

        [InlineData("ssh://account@ssh.contoso.com/project/_ssh/repo", "account", "project", "repo")]
        [InlineData("ssh://account@ssh.contoso.com/project/team/_ssh/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@ssh.contoso.com/project/team/_ssh/_full/repo", "account", "project/team", "repo")]
        [InlineData("ssh://account@ssh.contoso.com/project/team/_ssh/_optimized/repo", "account", "project/team", "repo")]

        [InlineData("ssh://account@vs-ssh.visualstudio.com/v3/_ssh/repo", "account", "v3", "repo")]
        [InlineData("ssh://account@vs-ssh.visualstudio.com/v3/team/_ssh/repo", "account", "v3/team", "repo")]
        public void TryParseHostedSshV1V2_Success(string url, string account, string repositoryPath, string repositoryName)
        {
            Assert.True(AzureDevOpsUrlParser.TryParseHostedSsh(new Uri(url, UriKind.Absolute), out var actualAccount, out var actualRepositoryPath, out var actualRepositoryName));
            Assert.Equal(account, actualAccount);
            Assert.Equal(repositoryPath, actualRepositoryPath);
            Assert.Equal(repositoryName, actualRepositoryName);
        }

        [Theory]
        [InlineData("ssh://account1@vs-ssh.visualstudio.com/v3/account2/project/repo", "account2", "project", "repo")]
        [InlineData("ssh://account1@vs-ssh.visualstudio.com/v3/account2/project/team/repo", "account2", "project/team", "repo")]
        [InlineData("ssh://account1@vs-ssh.visualstudio.com/v3/account2/project/team/_full/repo", "account2", "project/team", "repo")]
        [InlineData("ssh://account1@vs-ssh.visualstudio.com/v3/account2/project/team/_optimized/repo", "account2", "project/team", "repo")]

        [InlineData("ssh://account1@vs-ssh.vsts.me/v3/account2/project/repo", "account2", "project", "repo")]
        [InlineData("ssh://account1@vs-ssh.vsts.me/v3/account2/project/team/repo", "account2", "project/team", "repo")]
        [InlineData("ssh://account1@vs-ssh.vsts.me/v3/account2/project/team/_full/repo", "account2", "project/team", "repo")]
        [InlineData("ssh://account1@vs-ssh.vsts.me/v3/account2/project/team/_optimized/repo", "account2", "project/team", "repo")]

        [InlineData("ssh://account1@ssh.contoso.com/v3/account2/project/repo", "account2", "project", "repo")]
        [InlineData("ssh://account1@ssh.contoso.com/v3/account2/project/team/repo", "account2", "project/team", "repo")]
        [InlineData("ssh://account1@ssh.contoso.com/v3/account2/project/team/_full/repo", "account2", "project/team", "repo")]
        [InlineData("ssh://account1@ssh.contoso.com/v3/account2/project/team/_optimized/repo", "account2", "project/team", "repo")]
        public void TryParseHostedSshV3_Success(string url, string account, string repositoryPath, string repositoryName)
        {
            Assert.True(AzureDevOpsUrlParser.TryParseHostedSsh(new Uri(url, UriKind.Absolute), out var actualAccount, out var actualRepositoryPath, out var actualRepositoryName));
            Assert.Equal(account, actualAccount);
            Assert.Equal(repositoryPath, actualRepositoryPath);
            Assert.Equal(repositoryName, actualRepositoryName);
        }
    }
}
