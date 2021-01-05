// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using TestUtilities;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class GiteaTests : DotNetSdkTestBase
    {
        public GiteaTests() 
            : base("Microsoft.SourceLink.Gitea")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Https()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "https://噸.com/test-org/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGiteaHost Include='噸.com'/>
</ItemGroup>
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
                    $"https://噸.com/test-org/{repoName}/raw/commit/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://噸.com/test-org/{repoName}",
                    $"https://噸.com/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://噸.com/test-org/{repoName}/raw/commit/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath), 
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git", 
                commit: commitSha,
                url: $"https://噸.com/test-org/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Ssh()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "test-user@噸.com:test-org/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGiteaHost Include='噸.com'/>
</ItemGroup>
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
                    $"https://噸.com/test-org/{repoName}/raw/commit/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://噸.com/test-org/{repoName}",
                    $"https://噸.com/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://噸.com/test-org/{repoName}/raw/commit/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://噸.com/test-org/{repoName}");
        }


        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void ImplicitHost()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "http://噸.com/test-org/test-%72epo\u1234%24%2572%2F";
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
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(PrivateRepositoryUrl)",
                    "$(RepositoryUrl)"
                },
                expectedResults: new[]
                {
                    $"http://噸.com/test-org/{repoName}/raw/commit/{commitSha}/*",
                    $"http://噸.com/test-org/{repoName}",
                    $"http://噸.com/test-org/{repoName}"
                });
        }
    }
}
