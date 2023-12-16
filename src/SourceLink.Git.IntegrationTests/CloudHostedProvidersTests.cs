// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using LibGit2Sharp;
using Microsoft.Build.Tasks.Git;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.IntegrationTests
{
    /// <summary>
    /// Tests projects with all providers that have cloud-hosted repos.
    /// These providers are included in the SDK.
    /// </summary>
    public class CloudHostedProvidersTests : DotNetSdkTestBase
    {
        public CloudHostedProvidersTests() 
            : base("Microsoft.SourceLink.AzureRepos.Git",
                   "Microsoft.SourceLink.GitHub",
                   "Microsoft.SourceLink.GitLab",
                   "Microsoft.SourceLink.Bitbucket.Git")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void NoRepository_Warnings()
        {
            var sourceLinkFilePath = Path.Combine(ProjectObjDir.Path, Configuration, TargetFramework, "test.sourcelink.json");

            VerifyValues(
                customProps: "",
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(SourceLink)",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    "",
                },
                expectedWarnings: new[]
                {
                    string.Format(Resources.UnableToLocateRepository, ProjectDir.Path),
                    string.Format(Common.Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty),
                });

            Assert.False(File.Exists(sourceLinkFilePath));
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void NoRepository_NoWarnings()
        {
            var sourceLinkFilePath = Path.Combine(ProjectObjDir.Path, Configuration, TargetFramework, "test.sourcelink.json");

            VerifyValues(
                customProps: """
                <PropertyGroup>
                  <PkgMicrosoft_Build_Tasks_Git></PkgMicrosoft_Build_Tasks_Git>
                  <PkgMicrosoft_SourceLink_Common></PkgMicrosoft_SourceLink_Common>
                </PropertyGroup>
                """,
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(SourceLink)",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    "",
                });

            Assert.False(File.Exists(sourceLinkFilePath));
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void NoCommit_NoRemote_Warnings()
        {
            Repository.Init(workingDirectoryPath: ProjectDir.Path, gitDirectoryPath: Path.Combine(ProjectDir.Path, ".git"));

            VerifyValues(
                customProps: "",
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders
                },
                expectedWarnings: new[]
                {
                    // Repository has no remote.
                    string.Format(Resources.RepositoryHasNoRemote, ProjectDir.Path),

                    // Repository doesn't have any commit.
                    string.Format(Resources.RepositoryHasNoCommit, ProjectDir.Path),

                    // No SourceRoot items specified - the generated source link is empty.
                    string.Format(Common.Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty),
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void NoCommit_NoRemote_NoWarnings()
        {
            Repository.Init(workingDirectoryPath: ProjectDir.Path, gitDirectoryPath: Path.Combine(ProjectDir.Path, ".git"));

            VerifyValues(
                customProps: """
                <PropertyGroup>
                  <PkgMicrosoft_Build_Tasks_Git></PkgMicrosoft_Build_Tasks_Git>
                  <PkgMicrosoft_SourceLink_Common></PkgMicrosoft_SourceLink_Common>
                </PropertyGroup>
                <Target Name="_CaptureFileWrites" DependsOnTargets="GenerateSourceLinkFile" BeforeTargets="AfterBuild">
                  <ItemGroup>
                    <_SourceLinkFileWrites Include="@(FileWrites)" Condition="$([MSBuild]::ValueOrDefault('%(Identity)', '').EndsWith('sourcelink.json'))"/>
                  </ItemGroup>
                </Target>
                """,
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(SourceLink)",
                    "@(_SourceLinkFileWrites)",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    "",
                    "",
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void Commit_NoRemote_NoWarnings()
        {
            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, originUrl: null);

            VerifyValues(
                customProps: """
                <PropertyGroup>
                  <PkgMicrosoft_Build_Tasks_Git></PkgMicrosoft_Build_Tasks_Git>
                  <PkgMicrosoft_SourceLink_Common></PkgMicrosoft_SourceLink_Common>
                </PropertyGroup>
                <Target Name="_CaptureFileWrites" DependsOnTargets="GenerateSourceLinkFile" BeforeTargets="AfterBuild">
                  <ItemGroup>
                    <_SourceLinkFileWrites Include="@(FileWrites)" Condition="$([MSBuild]::ValueOrDefault('%(Identity)', '').EndsWith('sourcelink.json'))"/>
                  </ItemGroup>
                </Target>
                """,
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(SourceLink)",
                    "@(_SourceLinkFileWrites)",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    ProjectSourceRoot,
                    "",
                    "",
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void NoCommit_Remote_NoWarnings()
        {
            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, commitFileNames: null, originUrl: "https://github.com/org/repo");

            VerifyValues(
                customProps: """
                <PropertyGroup>
                  <PkgMicrosoft_Build_Tasks_Git></PkgMicrosoft_Build_Tasks_Git>
                  <PkgMicrosoft_SourceLink_Common></PkgMicrosoft_SourceLink_Common>
                </PropertyGroup>
                <Target Name="_CaptureFileWrites" DependsOnTargets="GenerateSourceLinkFile" BeforeTargets="AfterBuild">
                  <ItemGroup>
                    <_SourceLinkFileWrites Include="@(FileWrites)" Condition="$([MSBuild]::ValueOrDefault('%(Identity)', '').EndsWith('sourcelink.json'))"/>
                  </ItemGroup>
                </Target>
                """,
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(SourceLink)",
                    "@(_SourceLinkFileWrites)",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    "",
                    "",
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void CustomTranslation()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"ssh://test@vs-ssh.visualstudio.com:22/test-org/_ssh/test-%72epo{TestStrings.RepoName}";
            var repoName = $"test-repo{TestStrings.RepoNameEscaped}";

            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
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

<Target Name=""_CaptureFileWrites"" DependsOnTargets=""GenerateSourceLinkFile"" BeforeTargets=""AfterBuild"">
  <ItemGroup>
    <_SourceLinkFileWrites Include=""@(FileWrites)"" Condition=""$([MSBuild]::ValueOrDefault('%(Identity)', '').EndsWith('sourcelink.json'))""/>
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
                    "$(RepositoryUrl)",
                    "@(_SourceLinkFileWrites)",
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    ProjectSourceRoot,
                    $"https://raw.githubusercontent.com/test-org/{repoName}/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://github.com/test-org/{repoName}",
                    $"https://github.com/test-org/{repoName}",
                    s_relativeSourceLinkJsonPath
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
            var repoUrl = $"https://test.{host}/test-org/_git/test-%72epo{TestStrings.RepoName}";
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
            var repoUrl = $"https://{host}/test/test-org/_git/test-%72epo{TestStrings.RepoName}";
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

            GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);

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
                    string.Format(Common.Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty)
                });
        }
    }
}
