﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class AzureReposTests : DotNetSdkTestBase
    {
        public AzureReposTests() 
            : base("Microsoft.SourceLink.AzureRepos.Git")
        {
        }

        [ConditionalTheory(typeof(DotNetSdkAvailable))]
        [InlineData("visualstudio.com")]
        [InlineData("vsts.me")]
        public void FullValidation_Https(string host)
        {
            // Test non - ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"https://test.{host}/test-org/_git/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build", "Pack"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(SourceLink)",
                    "$(PrivateRepositoryUrl)",
                    "$(RepositoryUrl)"
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    ProjectSourceRoot,
                    $"https://test.{host}/test-org/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://test.{host}/test-org/_git/{repoName}",
                    $"https://test.{host}/test-org/_git/{repoName}",
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://test.{host}/test-org/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath), 
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git", 
                commit: commitSha,
                url: $"https://test.{host}/test-org/_git/{repoName}");
        }

        [ConditionalTheory(typeof(DotNetSdkAvailable))]
        [InlineData("visualstudio.com")]
        [InlineData("vsts.me")]
        public void FullValidation_Ssh(string host)
        {
            // Test non - ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"ssh://test@vs-ssh.{host}:22/test-org/_ssh/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build", "Pack"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(SourceLink)",
                    "$(PrivateRepositoryUrl)",
                    "$(RepositoryUrl)"
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    ProjectSourceRoot,
                    $"https://test.{host}/test-org/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://test.{host}/test-org/_git/{repoName}",
                    $"https://test.{host}/test-org/_git/{repoName}",
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://test.{host}/test-org/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://test.{host}/test-org/_git/{repoName}");
        }
    }
}
