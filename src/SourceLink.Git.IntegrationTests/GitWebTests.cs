// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using Microsoft.Build.Tasks.SourceControl;
using System;
using System.IO;
using TestUtilities;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class GitWebTests : DotNetSdkTestBase
    {
        public GitWebTests()
            : base("Microsoft.SourceLink.GitWeb")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Ssh()
        {
            // Test non-ascii characters and escapes in the URL. Escaped URI reserved characters
            // should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = $"ssh://git@噸.com/test-%72epo\u1234%24%2572%2F.git";
            var repoName = "test-repo\u1234%24%2572%2F.git";

            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGitWebHost Include='噸.com' ContentUrl='https://噸.com/gitweb'/>
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
                    $"https://噸.com/gitweb/?p={repoName};a=blob_plain;hb={commitSha};f=*",
                    s_relativeSourceLinkJsonPath,
                    $"ssh://git@噸.com/{repoName}",
                    $"ssh://git@噸.com/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://噸.com/gitweb/?p={repoName};a=blob_plain;hb={commitSha};f=*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"ssh://git@噸.com/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void Issues_error_on_git_url()
        {
            var repoUrl = "git://噸.com/invalid_url_protocol.git";
            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGitWebHost Include='噸.com' ContentUrl='https://噸.com/gitweb'/>
</ItemGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build", "Pack"
                },
                expressions: Array.Empty<string>(),
                expectedErrors: new[]{
                    string.Format(GitWeb.Resources.RepositoryUrlIsNotSupportedByProvider, "GIT")
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void Issues_error_on_https_url()
        {
            var repoUrl = "https://噸.com/invalid_url_protocol.git";
            var repo = GitUtilities.CreateGitRepository(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGitWebHost Include='噸.com' ContentUrl='https://噸.com/gitweb'/>
</ItemGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build", "Pack"
                },
                expressions: Array.Empty<string>(),
                expectedErrors: new[]
                {
                    string.Format(GitWeb.Resources.RepositoryUrlIsNotSupportedByProvider, "HTTP")
                });
        }
    }
}
