﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LibGit2Sharp;
using Microsoft.Build.Tasks.Git;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class GitHubTests : DotNetSdkTestBase
    {
        public GitHubTests() 
            : base("Microsoft.SourceLink.GitHub")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void EmptyRepository()
        {
            Repository.Init(workingDirectoryPath: ProjectDir.Path, gitDirectoryPath: Path.Combine(ProjectDir.Path, ".git"));

            VerifyValues(
                customProps:  "",
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
                    ""
                },
                expectedWarnings: new[]
                {
                    // Repository has no remote.
                    string.Format(Build.Tasks.Git.Resources.RepositoryHasNoRemote),

                    // Repository doesn't have any commit.
                    string.Format(Build.Tasks.Git.Resources.RepositoryHasNoCommit),

                    // No SourceRoot items specified - the generated source link is empty.
                    string.Format(Common.Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty),
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void MutlipleProjects()
        {
            var projectName2 = "Project2";
            var projectFileName2 = projectName2 + ".csproj";

            var project2 = RootDir.CreateDirectory(projectName2).CreateFile(projectFileName2).WriteAllText(@"
<Project Sdk='Microsoft.NET.Sdk'>
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
</Project>
");

            using var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(
                RootDir.Path, 
                new[] { Path.Combine(ProjectName, ProjectFileName), Path.Combine(projectName2, projectFileName2) },
                "http://github.com/test-org/test-repo1");

            repo.Network.Remotes.Add("origin2", "http://github.com/test-org/test-repo2");

            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: $@"
<ItemGroup>
  <ProjectReference Include='{project2.Path}'/>
</ItemGroup>
<PropertyGroup>
  <GitRepositoryRemoteName>origin2</GitRepositoryRemoteName>
</PropertyGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(SourceLink)",
                    "$(PrivateRepositoryUrl)",
                },
                expectedResults: new[]
                {
                    SourceRoot,
                    $"https://raw.githubusercontent.com/test-org/test-repo2/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"http://github.com/test-org/test-repo2",
                },
                // the second project should reuse the repository info cached by the first project:
                buildVerbosity: "detailed",
                expectedBuildOutputFilter: line => line.Contains("SourceLink: Reusing cached git repository information."));
        }

        private void PrepareTestEnvironment()
        {
            var homeDir = RootDir.CreateDirectory(".home");
            var xdgDir = RootDir.CreateDirectory(".xdg");

            EnvironmentVariables.Add("XDG_CONFIG_HOME", xdgDir.Path);
            EnvironmentVariables.Add("HOME", homeDir.Path);

            if (PathUtils.IsUnixLikePlatform)
            {
                EnvironmentVariables.Add("MICROSOFT_SOURCELINK_TEST_ENVIRONMENT_ETC_DIR", homeDir.Path);

                xdgDir.CreateDirectory("git").CreateFile("config").WriteAllText(@"[remote ""origin2""]url = http://github.com/test-org/test-repo2");
            }
            else
            {
                var gitInstallDir = RootDir.CreateDirectory(".gitinstall");
                var gitExeDir = gitInstallDir.CreateDirectory("bin");
                gitExeDir.CreateFile("git.exe");
                var etcDir = gitInstallDir.CreateDirectory("mingw64").CreateDirectory("etc");

                etcDir.CreateFile("gitconfig").WriteAllText(@"[remote ""origin2""]url = http://github.com/test-org/test-repo2");

                var programDataDir = RootDir.CreateDirectory(".programdata");

                EnvironmentVariables.Add("USERPROFILE", homeDir.Path);
                EnvironmentVariables.Add("PATH", gitExeDir.Path);
                EnvironmentVariables.Add("PROGRAMDATA", programDataDir.Path);
            }
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void Environment_Enabled()
        {
            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, "http://github.com/test-org/test-repo1");
            var commitSha = repo.Head.Tip.Sha;

            PrepareTestEnvironment();

            VerifyValues(
                customProps: $@"
<PropertyGroup>
  <ContinuousIntegrationBuild>false</ContinuousIntegrationBuild>
  <GitRepositoryRemoteName>origin2</GitRepositoryRemoteName>
</PropertyGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(PrivateRepositoryUrl)",
                },
                expectedResults: new[]
                {
                    $"https://raw.githubusercontent.com/test-org/test-repo2/{commitSha}/*",
                    $"http://github.com/test-org/test-repo2",
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void Environment_Disabled()
        {
            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, "http://github.com/test-org/test-repo1");
            var commitSha = repo.Head.Tip.Sha;

            PrepareTestEnvironment();

            VerifyValues(
                customProps: $@"
<PropertyGroup>
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  <GitRepositoryRemoteName>origin2</GitRepositoryRemoteName>
</PropertyGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(PrivateRepositoryUrl)",
                },
                expectedResults: new[]
                {
                    $"https://raw.githubusercontent.com/test-org/test-repo1/{commitSha}/*",
                    $"http://github.com/test-org/test-repo1",
                },
                expectedWarnings: new[]
                {
                    string.Format(Resources.RepositoryDoesNotHaveSpecifiedRemote, "origin2", "origin")
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Https()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "http://github.com/test-org/test-%72epo\u1234%24%2572%2F";
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
                    ProjectSourceRoot,
                    $"https://raw.githubusercontent.com/test-org/{repoName}/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"http://github.com/test-org/{repoName}",
                    $"http://github.com/test-org/{repoName}"
                });

            // SourceLink file:
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
                url: $"http://github.com/test-org/{repoName}");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation_Ssh()
        {
            // Test non-ascii characters and escapes in the URL.
            // Escaped URI reserved characters should remain escaped, non-reserved characters unescaped in the results.
            var repoUrl = "ssh://github.com/test-org/test-%72epo\u1234%24%2572%2F";
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
                    ProjectSourceRoot,
                    $"https://raw.githubusercontent.com/test-org/{repoName}/{commitSha}/*",
                    s_relativeSourceLinkJsonPath,
                    $"https://github.com/test-org/{repoName}",
                    $"https://github.com/test-org/{repoName}"
                });

            // SourceLink file:
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
    }
}
