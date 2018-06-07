// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        public void FullValidation()
        {
            var repo = GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, "http://mygitlab.com/test-org/test-repo");
            var commitSha = repo.Head.Tip.Sha;

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
<ItemGroup>
  <SourceLinkGitLabHosts Include='mygitlab.com'/>
</ItemGroup>
",
                customTargets: "",
                targets: new[]
                {
                    "Build", "Pack"
                },
                expressions: new[]
                {
                    "@(SourceRoot->'%(Identity): %(SourceLinkUrl)')",
                    "$(SourceLink)"
                },
                expectedResults: new[]
                {
                    $@"{ProjectSourceRoot}: https://mygitlab.com/test-org/test-repo/raw/{commitSha}/*",
                    s_relativeSourceLinkJsonPath
                });

            AssertEx.AreEqual(
                $@"{{""documents"":{{""{ProjectSourceRoot.Replace(@"\", @"\\")}*"":""https://mygitlab.com/test-org/test-repo/raw/{commitSha}/*""}}}}",
                File.ReadAllText(Path.Combine(ProjectDir.Path, s_relativeSourceLinkJsonPath)));

            TestUtilities.ValidateAssemblyInformationalVersion(
                Path.Combine(ProjectDir.Path, s_relativeOutputFilePath), 
                "1.0.0+" + commitSha);

            TestUtilities.ValidateNuSpecRepository(
                Path.Combine(ProjectDir.Path, s_relativePackagePath),
                type: "git", 
                commit: commitSha,
                url: "http://mygitlab.com/test-org/test-repo");
        }
    }
}
