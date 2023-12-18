// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.IO;
using TestUtilities;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class GitLabTests : DotNetSdkTestBase
    {
        public GitLabTests() 
            : base("Microsoft.SourceLink.GitLab")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Https()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"https://{TestStrings.DomainName}.com/test-org/test-%72epo{TestStrings.RepoName}";
            var repoName = $"test-repo{TestStrings.RepoNameEscaped}";

            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: $@"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGitLabHost Include='{TestStrings.DomainName}.com'/>
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
                    $"https://{TestStrings.DomainName}.com/test-org/{repoName}/-/raw/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://{TestStrings.DomainName}.com/test-org/{repoName}",
                    $"https://{TestStrings.DomainName}.com/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://{TestStrings.DomainName}.com/test-org/{repoName}/-/raw/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath), 
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git", 
                commit: commitSha,
                url: $"https://{TestStrings.DomainName}.com/test-org/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Ssh()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"test-user@{TestStrings.DomainName}.com:test-org/test-%72epo{TestStrings.RepoName}";
            var repoName = $"test-repo{TestStrings.RepoNameEscaped}";

            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: $@"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGitLabHost Include='{TestStrings.DomainName}.com'/>
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
                    $"https://{TestStrings.DomainName}.com/test-org/{repoName}/-/raw/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://{TestStrings.DomainName}.com/test-org/{repoName}",
                    $"https://{TestStrings.DomainName}.com/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://{TestStrings.DomainName}.com/test-org/{repoName}/-/raw/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://{TestStrings.DomainName}.com/test-org/{repoName}");
        }


        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void ImplicitHost()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"http://{TestStrings.DomainName}.com/test-org/test-%72epo{TestStrings.RepoName}";
            var repoName = $"test-repo{TestStrings.RepoNameEscaped}";

            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
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
                    $"http://{TestStrings.DomainName}.com/test-org/{repoName}/-/raw/{commitSha}/*",
                    $"http://{TestStrings.DomainName}.com/test-org/{repoName}",
                    $"http://{TestStrings.DomainName}.com/test-org/{repoName}"
                });
        }
    }
}
