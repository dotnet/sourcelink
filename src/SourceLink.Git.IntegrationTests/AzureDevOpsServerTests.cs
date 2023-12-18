// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.IO;
using TestUtilities;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class AzureDevOpsServerTests : DotNetSdkTestBase
    {
        public AzureDevOpsServerTests() 
            : base("Microsoft.SourceLink.AzureDevOpsServer.Git")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Https()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"https://tfs.{TestStrings.DomainName}.local:8080/tfs/DefaultCollection/project/_git/test-%72epo{TestStrings.RepoName}";
            var repoName = $"test-repo{TestStrings.RepoNameEscaped}";

            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: $@"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkAzureDevOpsServerGitHost Include=""tfs.{TestStrings.DomainName}.local"" VirtualDirectory=""tfs""/>
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
                    $"https://tfs.{TestStrings.DomainName}.local:8080/tfs/DefaultCollection/project/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://tfs.{TestStrings.DomainName}.local:8080/tfs/DefaultCollection/project/_git/{repoName}",
                    $"https://tfs.{TestStrings.DomainName}.local:8080/tfs/DefaultCollection/project/_git/{repoName}",
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://tfs.{TestStrings.DomainName}.local:8080/tfs/DefaultCollection/project/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath), 
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git", 
                commit: commitSha,
                url: $"https://tfs.{TestStrings.DomainName}.local:8080/tfs/DefaultCollection/project/_git/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Ssh()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"ssh://tfs.{TestStrings.DomainName}.local:22/tfs/DefaultCollection/project/_ssh/test-%72epo{TestStrings.RepoName}";
            var repoName = $"test-repo{TestStrings.RepoNameEscaped}";

            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: $@"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkAzureDevOpsServerGitHost Include=""tfs.{TestStrings.DomainName}.local"" VirtualDirectory=""tfs""/>
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
                    $"https://tfs.{TestStrings.DomainName}.local/tfs/DefaultCollection/project/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://tfs.{TestStrings.DomainName}.local/tfs/DefaultCollection/project/_git/{repoName}",
                    $"https://tfs.{TestStrings.DomainName}.local/tfs/DefaultCollection/project/_git/{repoName}",
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://tfs.{TestStrings.DomainName}.local/tfs/DefaultCollection/project/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://tfs.{TestStrings.DomainName}.local/tfs/DefaultCollection/project/_git/{repoName}");
        }
    }
}
