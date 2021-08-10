// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.IO;
using Microsoft.SourceLink.Common;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class AzureReposAndGitHubTests : DotNetSdkTestBase
    {
        public AzureReposAndGitHubTests() 
            : base("Microsoft.SourceLink.AzureRepos.Git", "Microsoft.SourceLink.GitHub")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void CustomTranslation()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "ssh://test@vs-ssh.visualstudio.com:22/test-org/_ssh/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
",
                customTargets: @"
<Target Name=""TranslateAzureReposToGitHub""
        DependsOnTargets=""$(SourceControlManagerUrlTranslationTargets)""
        BeforeTargets=""SourceControlManagerPublishTranslatedUrls"">

    <PropertyGroup>
      <_Pattern>https://([^.]+)[.]visualstudio.com/([^/]+)/_git/([^/]+)</_Pattern>
      <_Replacement>https://github.com/$2/$3</_Replacement>
    </PropertyGroup>

    <PropertyGroup>
      <ScmRepositoryUrl>$([System.Text.RegularExpressions.Regex]::Replace($(ScmRepositoryUrl), $(_Pattern), $(_Replacement)))</ScmRepositoryUrl>
    </PropertyGroup>

    <ItemGroup>
      <SourceRoot Update=""@(SourceRoot)"">
        <ScmRepositoryUrl>$([System.Text.RegularExpressions.Regex]::Replace(%(SourceRoot.ScmRepositoryUrl), $(_Pattern), $(_Replacement)))</ScmRepositoryUrl>
      </SourceRoot>
    </ItemGroup>
  </Target>
",
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
                    $"https://raw.githubusercontent.com/test-org/{repoName}/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://github.com/test-org/{repoName}",
                    $"https://github.com/test-org/{repoName}",
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://raw.githubusercontent.com/test-org/{repoName}/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://github.com/test-org/{repoName}");
        }

        [ConditionalTheory(typeof(DotNetSdkAvailable))]
        [InlineData("visualstudio.com")]
        [InlineData("vsts.me")]
        public void Host_VisualStudio(string host)
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
        }

        [ConditionalTheory(typeof(DotNetSdkAvailable))]
        [InlineData("dev.azure.com")]
        public void Host_DevAzureCom(string host)
        {
            // Test non - ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"https://{host}/test/test-org/_git/test-%72epo\u1234%24%2572%2F";
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
                    $"https://{host}/test/test-org/_apis/git/repositories/{repoName}/items?api-version=1.0&versionType=commit&version={commitSha}&path=/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://{host}/test/test-org/_git/{repoName}",
                    $"https://{host}/test/test-org/_git/{repoName}",
                });
        }


        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void Host_Unknown()
        {
            var repoUrl = $"https://contoso.com/test/test-org/_git/test-repo";

            GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);

            VerifyValues(
                customProps: "",
                customTargets: "",
                targets: new[]
                {
                    "Build", "Pack"
                },
                expressions: new[]
                {
                    "@(SourceRoot->'%(Identity):%(SourceLinkUrl)')",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders + ":",
                    EnsureTrailingDirectorySeparator(ProjectDir.Path) + ":",
                },
                expectedWarnings: new[]
                {
                    string.Format(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty)
                });
        }
    }
}
