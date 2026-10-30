// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Microsoft.Build.Logging;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class SourceLinkPlaceholderTests : DotNetSdkTestBase
    {
        public SourceLinkPlaceholderTests()
            : base("Microsoft.SourceLink.GitHub")
        {
        }

        [ConditionalTheory(typeof(DotNetSdkAvailable))]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public void CommitOnlyChangeRefreshesRevisionsWithoutCompiling(bool? absoluteRevisionFile)
        {
            using var repo = GitUtilities.CreateGitRepository(
                ProjectDir.Path, new[] { ProjectFileName, "TestClass.cs" }, "https://github.com/test-org/test-repo");
            var revision = repo.Head.Tip.Sha;
            var relativeRevisionFile = absoluteRevisionFile.HasValue
                ? Path.Combine("obj", "Debug", TargetFramework, "unused", "..", "custom.revisions.json")
                : Path.Combine("obj", "Debug", TargetFramework, "test.sourcelink.revisions.json");
            var revisionFile = Path.GetFullPath(Path.Combine(ProjectDir.Path, relativeRevisionFile));
            var revisionFileValue = absoluteRevisionFile == true ? Path.Combine(ProjectDir.Path, relativeRevisionFile) : relativeRevisionFile;
            var customProps = $@"
<PropertyGroup>
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
  <SourceLinkGeneratePlaceholders>true</SourceLinkGeneratePlaceholders>
  {(absoluteRevisionFile.HasValue ? $"<SourceLinkPlaceholderReplacementsFile>{revisionFileValue}</SourceLinkPlaceholderReplacementsFile>" : "")}
</PropertyGroup>
";

            VerifyValues(
                customProps: customProps,
                customTargets: "",
                targets: new[] { "Build" },
                expressions: new[]
                {
                    "@(SourceRoot->'%(SourceLinkUrl)')",
                    "$(SourceRevisionId)",
                    "$(_SourceLinkPlaceholderReplacementsFilePath)",
                    "@(FileWrites->WithMetadataValue('Identity', '$(_SourceLinkPlaceholderReplacementsFilePath)'))"
                },
                expectedResults: new[]
                {
                    $"https://raw.githubusercontent.com/test-org/test-repo/{revision}/*",
                    revision,
                    revisionFile,
                    revisionFile
                });

            Assert.Equal(1, CountTasks(Path.Combine(RootDir.Path, "build0.binlog"), "Csc"));
            var sourceLink = File.ReadAllText(SourceLinkFilePath);
            var sourceLinkWriteTime = File.GetLastWriteTimeUtc(SourceLinkFilePath);
            var outputFile = Path.Combine(ProjectDir.Path, s_relativeOutputFilePath);
            var output = File.ReadAllBytes(outputFile);
            var outputWriteTime = File.GetLastWriteTimeUtc(outputFile);
            var revisions = File.ReadAllText(revisionFile);
            Assert.Contains(revision, revisions);
            Assert.DoesNotContain(revision, sourceLink);

            var signature = new Signature("test", "test@test.com", DateTimeOffset.Now);
            revision = repo.Commit("Advance provenance", signature, signature, new CommitOptions { AllowEmptyCommit = true }).Sha;
            var warmLog = Build("warm");

            Assert.Equal(0, CountTasks(warmLog, "Csc"));
            Assert.Equal(1, CountTasks(warmLog, "Microsoft.SourceLink.Common.GenerateSourceLinkFile"));
            Assert.Equal(sourceLink, File.ReadAllText(SourceLinkFilePath));
            Assert.Equal(sourceLinkWriteTime, File.GetLastWriteTimeUtc(SourceLinkFilePath));
            Assert.Equal(output, File.ReadAllBytes(outputFile));
            Assert.Equal(outputWriteTime, File.GetLastWriteTimeUtc(outputFile));
            Assert.NotEqual(revisions, File.ReadAllText(revisionFile));
            Assert.Contains(revision, File.ReadAllText(revisionFile));
            var evaluatedValues = File.ReadAllLines(Path.Combine(ProjectOutDir.Path, "EvaluationResult.txt"));
            Assert.Equal($"https://raw.githubusercontent.com/test-org/test-repo/{revision}/*", evaluatedValues[0]);
            Assert.Equal(revision, evaluatedValues[1]);
            Assert.Equal(revisionFile, evaluatedValues[2]);
            Assert.Equal(revisionFile, evaluatedValues[3]);

            var sourceFile = Path.Combine(ProjectDir.Path, "TestClass.cs");
            File.WriteAllText(sourceFile, File.ReadAllText(sourceFile).Replace("123", "456"));
            Assert.Equal(1, CountTasks(Build("source-edit"), "Csc"));
            Assert.False(output.SequenceEqual(File.ReadAllBytes(outputFile)));

            repo.Network.Remotes.Update("origin", remote => remote.Url = "https://github.com/test-org/other-repo");
            Assert.Equal(1, CountTasks(Build("mapping-edit"), "Csc"));
            Assert.NotEqual(sourceLink, File.ReadAllText(SourceLinkFilePath));

            var clean = ProcessUtilities.Run(DotNetPath, $@"msbuild ""{Project.Path}"" /t:Clean /v:quiet",
                additionalEnvironmentVars: EnvironmentVariables);
            Assert.True(clean.ExitCode == 0, clean.Output + clean.Errors);
            Assert.False(File.Exists(revisionFile));
        }

        [ConditionalTheory(typeof(DotNetSdkAvailable))]
        [InlineData(null, false)]
        [InlineData(null, true)]
        [InlineData("false", false)]
        [InlineData("false", true)]
        public void DisabledPlaceholderGenerationUsesResolvedSourceLink(string? generatePlaceholders, bool customRevisionFile)
        {
            using var repo = GitUtilities.CreateGitRepository(
                ProjectDir.Path, new[] { ProjectFileName, "TestClass.cs" }, "https://github.com/test-org/test-repo");
            var revision = repo.Head.Tip.Sha;
            var relativeRevisionFile = Path.Combine("obj", "custom.revisions.json");
            var revisionFile = Path.GetFullPath(Path.Combine(ProjectDir.Path, relativeRevisionFile));
            var customProps = $@"
<PropertyGroup>
  <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  {(generatePlaceholders != null ? $"<SourceLinkGeneratePlaceholders>{generatePlaceholders}</SourceLinkGeneratePlaceholders>" : "")}
  {(customRevisionFile ? $"<SourceLinkPlaceholderReplacementsFile>{relativeRevisionFile}</SourceLinkPlaceholderReplacementsFile>" : "")}
</PropertyGroup>
";

            VerifyValues(
                customProps: customProps,
                customTargets: "",
                targets: new[] { "Build" },
                expressions: new[]
                {
                    "$(SourceLinkPlaceholderReplacementsFile)",
                    "$(_SourceLinkPlaceholderReplacementsFilePath)",
                    $"@(FileWrites->WithMetadataValue('Identity', '{revisionFile}'))"
                },
                expectedResults: new[] { customRevisionFile ? relativeRevisionFile : "", "", "" });

            Assert.Contains(revision, File.ReadAllText(SourceLinkFilePath));
            Assert.False(File.Exists(revisionFile));
            Assert.Empty(Directory.GetFiles(ProjectDir.Path, "*.sourcelink.revisions.json", SearchOption.AllDirectories));
            Assert.Equal(1, CountTasks(Path.Combine(RootDir.Path, "build0.binlog"), "Csc"));
        }

        private string Build(string name)
        {
            // Re-emitting the test helper imports would invalidate the compiler's incremental inputs.
            var log = Path.Combine(RootDir.Path, name + ".binlog");
            var result = ProcessUtilities.Run(DotNetPath,
                $@"msbuild ""{Project.Path}"" /t:Build;Test_EvaluateExpressions /bl:""{log}"" /v:quiet",
                additionalEnvironmentVars: EnvironmentVariables);
            Assert.True(result.ExitCode == 0, result.Output + result.Errors);
            return log;
        }

        private static int CountTasks(string binlog, string taskName)
        {
            var count = 0;
            var replay = new BinaryLogReplayEventSource { AllowForwardCompatibility = true };
            replay.RecoverableReadError += error =>
            {
                Assert.NotEqual("TaskStarted", error.RecordKind.ToString());
                Console.Error.WriteLine($"Binary log compatibility: {error.ErrorType}, {error.RecordKind}.");
            };
            replay.TaskStarted += (_, args) =>
            {
                if (args.TaskName == taskName)
                {
                    count++;
                }
            };
            using var stream = File.OpenRead(binlog);
            replay.Replay(stream, default);
            return count;
        }
    }
}
