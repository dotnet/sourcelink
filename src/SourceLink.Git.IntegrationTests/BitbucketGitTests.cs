// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using TestUtilities;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class BitBucketGitTests : DotNetSdkTestBase
    {
        public BitBucketGitTests()
            : base("Microsoft.SourceLink.BitBucket.Git")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_CloudHttps()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "https://test-user@bitbucket.org/test-org/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>",
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
                    ProjectSourceRoot,
                    $"https://bitbucket.org/test-org/{repoName}/raw/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://bitbucket.org/test-org/{repoName}",
                    $"https://bitbucket.org/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://bitbucket.org/test-org/{repoName}/raw/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://bitbucket.org/test-org/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_EnterpriseNewHttps()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "https://bitbucket.domain.com/scm/test-org/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] {ProjectFileName},
                repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkBitbucketGitHost Include=""bitbucket.domain.com"" EnterpriseEdition=""true"" Version=""4.7""/>
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
                    ProjectSourceRoot,
                    $"https://bitbucket.domain.com/projects/test-org/repos/{repoName}/raw/*?at={commitSha}",
                    s_relativeSourceLinkJsonPath,
                    $"https://bitbucket.domain.com/scm/test-org/{repoName}",
                    $"https://bitbucket.domain.com/scm/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://bitbucket.domain.com/projects/test-org/repos/{repoName}/raw/*?at={commitSha}""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://bitbucket.domain.com/scm/test-org/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_EnterpriseNewHttpsWithDefaultFlags()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "https://bitbucket.domain.com/scm/test-org/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName },
                repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkBitbucketGitHost Include=""bitbucket.domain.com""/>
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
                    ProjectSourceRoot,
                    $"https://bitbucket.domain.com/projects/test-org/repos/{repoName}/raw/*?at={commitSha}",
                    s_relativeSourceLinkJsonPath,
                    $"https://bitbucket.domain.com/scm/test-org/{repoName}",
                    $"https://bitbucket.domain.com/scm/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://bitbucket.domain.com/projects/test-org/repos/{repoName}/raw/*?at={commitSha}""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://bitbucket.domain.com/scm/test-org/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_EnterpriseOldHttps()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "https://bitbucket.domain.com/scm/test-org/test-%72epo\u1234%24%2572%2F";
            var repoName = "test-repo\u1234%24%2572%2F";

            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, repoUrl);
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
        <PropertyGroup>
          <PublishRepositoryUrl>true</PublishRepositoryUrl>
        </PropertyGroup>
        <ItemGroup>
          <SourceLinkBitbucketGitHost Include=""bitbucket.domain.com"" EnterpriseEdition=""true"" Version=""4.5""/>
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
                    ProjectSourceRoot,
                    $"https://bitbucket.domain.com/projects/test-org/repos/{repoName}/browse/*?at={commitSha}&raw",
                    s_relativeSourceLinkJsonPath,
                    $"https://bitbucket.domain.com/scm/test-org/{repoName}",
                    $"https://bitbucket.domain.com/scm/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://bitbucket.domain.com/projects/test-org/repos/{repoName}/browse/*?at={commitSha}&raw""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath),
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git",
                commit: commitSha,
                url: $"https://bitbucket.domain.com/scm/test-org/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_CloudSsh()
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
  <SourceLinkBitBucketGitHost Include='噸.com' EnterpriseEdition=""false""/>
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
                    ProjectSourceRoot,
                    $"https://噸.com/test-org/{repoName}/raw/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://噸.com/test-org/{repoName}",
                    $"https://噸.com/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://噸.com/test-org/{repoName}/raw/{commitSha}/*""}}}}",
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
        public void FullValidation_EnterpriseOldSsh()
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
  <SourceLinkBitBucketGitHost Include='噸.com' EnterpriseEdition=""true"" Version=""4.5""/>
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
                    ProjectSourceRoot,
                    $"https://噸.com/projects/test-org/repos/{repoName}/browse/*?at={commitSha}&raw",
                    s_relativeSourceLinkJsonPath,
                    $"https://噸.com/test-org/{repoName}",
                    $"https://噸.com/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://噸.com/projects/test-org/repos/{repoName}/browse/*?at={commitSha}&raw""}}}}",
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
        public void FullValidation_EnterpriseNewSsh()
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
  <SourceLinkBitBucketGitHost Include='噸.com' EnterpriseEdition=""true"" Version=""4.7""/>
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
                    ProjectSourceRoot,
                    $"https://噸.com/projects/test-org/repos/{repoName}/raw/*?at={commitSha}",
                    s_relativeSourceLinkJsonPath,
                    $"https://噸.com/test-org/{repoName}",
                    $"https://噸.com/test-org/{repoName}"
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://噸.com/projects/test-org/repos/{repoName}/raw/*?at={commitSha}""}}}}",
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
    }
}
