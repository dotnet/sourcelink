// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LibGit2Sharp;
using TestUtilities;

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
                    // Repository doesn't have any commit, the source code won't be available via source link.
                    string.Format(Build.Tasks.Git.Resources.RepositoryWithoutCommit_SourceLink),

                    // No SourceRoot items specified - the generated source link is empty.
                    string.Format(Common.Resources.NoItemsSpecifiedSourceLinkEmpty, "SourceRoot"),
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void FullValidation()
        {
            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, "http://github.com/test-org/test-repo");
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
                    "$(SourceLink)"
                },
                expectedResults: new[]
                {
                    ProjectSourceRoot,
                    $"https://raw.githubusercontent.com/test-org/test-repo/{commitSha}/*",
                    s_relativeSourceLinkJsonPath
                });

            // SourceLink file:
            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://raw.githubusercontent.com/test-org/test-repo/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath), 
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath), 
                type: "git", 
                commit: commitSha,
                url: "http://github.com/test-org/test-repo");
        }
    }
}
