// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class SourceLinkPlaceholderTests
    {
        private static readonly DateTime s_oldWriteTime = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static MockItem Root(string revision, string mappedPath = "/_/", string host = "raw.githubusercontent.com", string directory = @"C:\checkout\")
            => new(directory, KVP("MappedPath", mappedPath), KVP("RevisionId", revision), KVP("SourceControl", "git"),
                KVP("SourceLinkUrl", $"https://{host}/org%20name/repo/{revision}/*"));

        private static MockItem FixedRoot(string mappedPath = "/package/")
            => new(@"C:\package\", KVP("MappedPath", mappedPath), KVP("SourceLinkUrl", "https://example.com/fixed/*"));

        private static GenerateSourceLinkFile CreateTask(TempRoot temp, params MockItem[] roots)
            => CreateTask(temp.CreateFile().Path, temp.CreateFile().Path, roots);

        private static GenerateSourceLinkFile CreateTask(string outputFile, string revisionOutputFile, params MockItem[] roots)
        {
            var task = CreateTask(roots);
            task.OutputFile = outputFile;
            task.RevisionOutputFile = revisionOutputFile;
            return task;
        }

        private static GenerateSourceLinkFile CreateTask(params MockItem[] roots)
            => new()
            {
                BuildEngine = new MockEngine(),
                SourceRoots = roots,
                RevisionOutputFile = "unused.revisions.json",
                NoWarnOnMissingSourceControlInformation = true,
            };

        private static string ReadMarker(GenerateSourceLinkFile task)
            => File.ReadAllText(task.RevisionOutputFile!).Split('"')[1].Substring("SourceLinkRevision_".Length);

        [Theory]
        [InlineData(40)]
        [InlineData(64)]
        public void RevisionsUsePlaceholdersWithoutChangingSourceRootMetadata(int width)
        {
            var revision = new string('a', width);
            var root = Root(revision);
            var realUrl = root.GetMetadata("SourceLinkUrl");
            var task = CreateTask(root);

            var mappings = task.GetSourceLinkMappings();
            var marker = Assert.Single(mappings).Placeholder;

            Assert.NotNull(marker);
            Assert.Equal(width, marker.Length);
            Assert.All(marker, c => Assert.True(c is >= '0' and <= '9' or >= 'a' and <= 'f'));
            Assert.NotEqual(revision, marker);
            Assert.Equal("{\"SourceLinkRevision_" + marker + "\":\"" + revision + "\"}", task.GenerateRevisionFileContent(mappings));
            Assert.Equal("{\"documents\":{\"/_/*\":\"" + realUrl.Replace(revision, marker) + "\"}}", GenerateSourceLinkFile.GenerateSourceLinkContent(mappings));
            Assert.Equal(realUrl, root.GetMetadata("SourceLinkUrl"));
            Assert.Equal(revision, root.GetMetadata("RevisionId"));
            Assert.Empty(((MockEngine)task.BuildEngine).Log);
        }

        [Theory]
        [InlineData(40, "d0efab0058fb9849f56a9c0d7073dbdf295a764c")]
        [InlineData(64, "d0efab0058fb9849f56a9c0d7073dbdf295a764cbf0aa1c1e14cef60715551e2")]
        public void MarkerMatchesExpectedHash(int width, string marker)
        {
            var revision = new string('a', width);
            var root = Root(revision);
            var task = CreateTask(root);

            var mappings = task.GetSourceLinkMappings();

            Assert.Equal("{\"SourceLinkRevision_" + marker + "\":\"" + revision + "\"}", task.GenerateRevisionFileContent(mappings));
            Assert.Equal("{\"documents\":{\"/_/*\":\"" + root.GetMetadata("SourceLinkUrl").Replace(revision, marker) + "\"}}", GenerateSourceLinkFile.GenerateSourceLinkContent(mappings));
            Assert.Empty(((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void RevisionEqualToMarkerFailsAndDeletesOutputs()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            Assert.True(task.Execute());
            task = CreateTask(task.OutputFile, task.RevisionOutputFile!, Root(ReadMarker(task)));

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.False(File.Exists(task.RevisionOutputFile));
        }

        [Theory]
        [InlineData(40)]
        [InlineData(64)]
        public void CommitOnlyChangeRefreshesDictionaryWithoutRewritingCompilerInput(int width)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', width)));
            Assert.True(task.Execute());
            var original = File.ReadAllText(task.OutputFile);
            var marker = ReadMarker(task);
            File.SetLastWriteTimeUtc(task.OutputFile, s_oldWriteTime);
            File.SetLastWriteTimeUtc(task.RevisionOutputFile!, s_oldWriteTime);
            task = CreateTask(task.OutputFile, task.RevisionOutputFile!, Root(new string('b', width), directory: @"D:\other-checkout\"));

            Assert.True(task.Execute());

            Assert.Equal(marker, ReadMarker(task));
            Assert.Equal(original, File.ReadAllText(task.OutputFile));
            Assert.Equal(s_oldWriteTime, File.GetLastWriteTimeUtc(task.OutputFile));
            Assert.NotEqual(s_oldWriteTime, File.GetLastWriteTimeUtc(task.RevisionOutputFile!));
            Assert.Contains(new string('b', width), File.ReadAllText(task.RevisionOutputFile!));
            Assert.DoesNotContain(new string('a', width), File.ReadAllText(task.RevisionOutputFile!));
        }

        [Fact]
        public void UnchangedDictionaryRetainsItsTimestamp()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            Assert.True(task.Execute());
            File.SetLastWriteTimeUtc(task.OutputFile, s_oldWriteTime);
            File.SetLastWriteTimeUtc(task.RevisionOutputFile!, s_oldWriteTime);
            task = CreateTask(task.OutputFile, task.RevisionOutputFile!, Root(new string('a', 40)));

            Assert.True(task.Execute());

            Assert.Equal(s_oldWriteTime, File.GetLastWriteTimeUtc(task.OutputFile));
            Assert.Equal(s_oldWriteTime, File.GetLastWriteTimeUtc(task.RevisionOutputFile!));
        }

        [Theory]
        [InlineData("/_/nested/", "raw.githubusercontent.com")]
        [InlineData("/_/", "other.example")]
        public void SemanticMappingChangesChangeCompilerInput(string mappedPath, string host)
        {
            using var temp = new TempRoot();
            var revision = new string('a', 40);
            var task = CreateTask(temp, Root(revision));
            Assert.True(task.Execute());
            var original = File.ReadAllText(task.OutputFile);
            var marker = ReadMarker(task);
            task = CreateTask(task.OutputFile, task.RevisionOutputFile!, Root(revision, mappedPath, host));

            Assert.True(task.Execute());

            Assert.NotEqual(marker, ReadMarker(task));
            Assert.NotEqual(original, File.ReadAllText(task.OutputFile));
        }

        [Fact]
        public void IndependentRootMarkersAndDictionaryDoNotDependOnEnumerationOrder()
        {
            var roots = new[] { Root(new string('a', 40)), Root(new string('b', 64), "/_/submodule/") };
            var task = CreateTask(roots);
            var original = task.GenerateRevisionFileContent(task.GetSourceLinkMappings());
            task = CreateTask(Enumerable.Reverse(roots).ToArray());

            var reversed = task.GenerateRevisionFileContent(task.GetSourceLinkMappings());

            Assert.NotNull(original);
            Assert.Equal(original, reversed);
            Assert.Empty(((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void FixedNonSourceControlledMappingIsPreserved()
        {
            var task = CreateTask(FixedRoot());

            var mappings = task.GetSourceLinkMappings();

            Assert.Equal("{}", task.GenerateRevisionFileContent(mappings));
            Assert.Equal("{\"documents\":{\"/package/*\":\"https://example.com/fixed/*\"}}", GenerateSourceLinkFile.GenerateSourceLinkContent(mappings));
            Assert.Empty(((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void MixedMappingsOnlyWritePlaceholderRevisionsToDictionary()
        {
            var revision = new string('a', 40);
            var root = Root(revision);
            var task = CreateTask(FixedRoot(), root);

            var mappings = task.GetSourceLinkMappings();

            var marker = mappings[1].Placeholder;
            Assert.NotNull(marker);
            Assert.Equal("{\"SourceLinkRevision_" + marker + "\":\"" + revision + "\"}", task.GenerateRevisionFileContent(mappings));
            Assert.Equal("{\"documents\":{\"/package/*\":\"https://example.com/fixed/*\",\"/_/*\":\"" +
                root.GetMetadata("SourceLinkUrl").Replace(revision, marker) + "\"}}", GenerateSourceLinkFile.GenerateSourceLinkContent(mappings));
            Assert.Empty(((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void EmptyContentDoesNotRequireValidOutputPaths()
        {
            var task = CreateTask();
            var mappings = task.GetSourceLinkMappings();

            Assert.Null(GenerateSourceLinkFile.GenerateSourceLinkContent(mappings));
            Assert.Equal("{}", task.GenerateRevisionFileContent(mappings));
            Assert.Empty(((MockEngine)task.BuildEngine).Log);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void DuplicateMappedRootsOnlyRejectPlaceholderPairsInRevisionContent(bool firstFixed, bool secondFixed)
        {
            var task = CreateTask(
                firstFixed ? FixedRoot("/_/") : Root(new string('a', 40)),
                secondFixed ? FixedRoot("/_/") : Root(new string('a', 40)));
            var mappings = task.GetSourceLinkMappings();

            var content = task.GenerateRevisionFileContent(mappings);

            if (firstFixed || secondFixed)
            {
                var expected = "{}";
                if (!firstFixed || !secondFixed)
                {
                    var mapping = Assert.Single(mappings, mapping => mapping.Placeholder != null);
                    expected = "{\"SourceLinkRevision_" + mapping.Placeholder + "\":\"" + mapping.Revision + "\"}";
                }

                Assert.Equal(expected, content);
                Assert.Empty(((MockEngine)task.BuildEngine).Log);
            }
            else
            {
                Assert.Null(content);
                Assert.True(task.Log.HasLoggedErrors);
                Assert.Contains("ERROR", ((MockEngine)task.BuildEngine).Log);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RevisionContentRejectsConflictingPlaceholderReplacements(bool conflicting)
        {
            var revision = new string('a', 40);
            var task = CreateTask(Root(revision), Root(new string(conflicting ? 'b' : 'a', 40), "/_/submodule/"));
            var mappings = task.GetSourceLinkMappings();
            var first = mappings[0];
            var second = mappings[1];
            mappings[1] = new GenerateSourceLinkFile.SourceLinkMapping(second.Root, second.LocalPath, second.Url, second.Revision, first.Placeholder);

            var content = task.GenerateRevisionFileContent(mappings);

            if (conflicting)
            {
                Assert.Null(content);
                Assert.True(task.Log.HasLoggedErrors);
            }
            else
            {
                Assert.Equal("{\"SourceLinkRevision_" + first.Placeholder + "\":\"" + revision + "\"}", content);
                Assert.Empty(((MockEngine)task.BuildEngine).Log);
            }
        }

        [Fact]
        public void EmptyDiscoveryClearsStaleRevisions()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            Assert.True(task.Execute());
            task = CreateTask(task.OutputFile, task.RevisionOutputFile!);

            Assert.True(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.Equal("{}", File.ReadAllText(task.RevisionOutputFile!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("abcd")]
        [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void UnsupportedRevisionFailsAndDeletesOutputs(string revision)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(revision));
            File.WriteAllText(task.OutputFile, "original input");
            File.WriteAllText(task.RevisionOutputFile!, "original dictionary");

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.False(File.Exists(task.RevisionOutputFile));
            Assert.Contains("ERROR", ((MockEngine)task.BuildEngine).Log);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MappingAndPlaceholderErrorsAreReportedTogetherAndDeleteOutputs(bool reverse)
        {
            using var temp = new TempRoot();
            var roots = new[] { Root(new string('A', 40)), Root(new string('b', 40), mappedPath: "/_/*/") };
            var task = CreateTask(temp, reverse ? Enumerable.Reverse(roots).ToArray() : roots);
            File.WriteAllText(task.OutputFile, "original input");
            File.WriteAllText(task.RevisionOutputFile!, "original dictionary");

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.False(File.Exists(task.RevisionOutputFile));
            var log = ((MockEngine)task.BuildEngine).Log;
            Assert.Contains(string.Format(Resources.InvalidSourceLinkPlaceholderRoot, roots[0].ItemSpec), log);
            Assert.Contains(string.Format(Resources.MustNotContainWildcard, "SourceRoot.MappedPath", "/_/*/"), log);
        }

        [Fact]
        public void RevisionErrorsDoNotRemoveMappings()
        {
            var roots = new[] { Root(new string('A', 40)), FixedRoot(), Root(new string('b', 40), "/_/other/") };
            var task = CreateTask(roots);

            var mappings = task.GetSourceLinkMappings();

            Assert.Equal(roots, mappings.Select(mapping => mapping.Root));
            Assert.Null(mappings[0].Placeholder);
            Assert.Equal(roots[0].GetMetadata("SourceLinkUrl"), mappings[0].Url);
            Assert.Null(mappings[1].Placeholder);
            var placeholder = mappings[2].Placeholder;
            Assert.NotNull(placeholder);
            Assert.Equal(roots[2].GetMetadata("SourceLinkUrl").Replace(mappings[2].Revision, placeholder), mappings[2].Url);
            Assert.True(task.Log.HasLoggedErrors);
            Assert.Contains(string.Format(Resources.InvalidSourceLinkPlaceholderRoot, roots[0].ItemSpec), ((MockEngine)task.BuildEngine).Log);
            Assert.Null(task.GenerateRevisionFileContent(mappings));
        }

        [Fact]
        public void RevisionErrorsDoNotCreateDictionaryDirectory()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('A', 40)));
            var directory = Path.Combine(temp.CreateDirectory().Path, "revisions");
            task.RevisionOutputFile = Path.Combine(directory, "dictionary.json");

            Assert.False(task.Execute());

            Assert.False(File.Exists(task.OutputFile));
            Assert.False(Directory.Exists(directory));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MissingOrRepeatedUrlRevisionFails(bool repeated)
        {
            using var temp = new TempRoot();
            var revision = new string('a', 40);
            var root = Root(revision);
            root.SetMetadataValueLiteral("SourceLinkUrl", repeated ? $"https://example.com/{revision}/{revision}/*" : "https://example.com/main/*");
            var task = CreateTask(temp, root);

            Assert.False(task.Execute());
            Assert.Null(task.SourceLink);
        }

        [Fact]
        public void UnmappedSourceRootFails()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40), mappedPath: ""));

            Assert.False(task.Execute());
            Assert.Null(task.SourceLink);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void DuplicateMappedRootsOnlyRejectPlaceholderPairsDuringExecution(bool firstFixed, bool secondFixed)
        {
            using var temp = new TempRoot();
            var roots = new[]
            {
                firstFixed ? FixedRoot("/_/") : Root(new string('a', 40)),
                secondFixed ? FixedRoot("/_/") : Root(new string('b', 40))
            };
            var task = CreateTask(temp, roots);
            var directory = Path.Combine(temp.CreateDirectory().Path, "revisions");
            task.RevisionOutputFile = Path.Combine(directory, "dictionary.json");

            var succeeded = task.Execute();

            if (firstFixed || secondFixed)
            {
                Assert.True(succeeded);
                Assert.Equal(task.OutputFile, task.SourceLink);
                var marker = firstFixed && secondFixed ? "" : ReadMarker(task);
                var expectedRevisions = firstFixed && secondFixed
                    ? "{}"
                    : "{\"SourceLinkRevision_" + marker + "\":\"" + new string(firstFixed ? 'b' : 'a', 40) + "\"}";
                Assert.Equal(expectedRevisions, File.ReadAllText(task.RevisionOutputFile));
                var firstUrl = roots[0].GetMetadata("SourceLinkUrl");
                var secondUrl = roots[1].GetMetadata("SourceLinkUrl");
                if (!firstFixed)
                {
                    firstUrl = firstUrl.Replace(new string('a', 40), marker);
                }

                if (!secondFixed)
                {
                    secondUrl = secondUrl.Replace(new string('b', 40), marker);
                }

                Assert.Equal("{\"documents\":{\"/_/*\":\"" + firstUrl + "\",\"/_/*\":\"" + secondUrl + "\"}}",
                    File.ReadAllText(task.OutputFile));
            }
            else
            {
                Assert.False(succeeded);
                Assert.Null(task.SourceLink);
                Assert.False(File.Exists(task.OutputFile));
                Assert.False(Directory.Exists(directory));
                Assert.Contains("ERROR", ((MockEngine)task.BuildEngine).Log);
            }
        }

        [Theory]
        [CombinatorialData]
        public void InvalidOutputPathPreservesExistingFiles(bool invalidRevisionPath, bool invalidCharacter)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40), mappedPath: "/_/*/"));
            var sourceLinkFile = task.OutputFile;
            var revisionFile = task.RevisionOutputFile!;
            File.WriteAllText(sourceLinkFile, "original input");
            File.WriteAllText(revisionFile, "original dictionary");
            var path = invalidCharacter ? Path.Combine(Path.GetDirectoryName(sourceLinkFile)!, "invalid.json") + '\0' : "relative.json";
            if (invalidRevisionPath)
            {
                task.RevisionOutputFile = path;
            }
            else
            {
                task.OutputFile = path;
            }

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.Equal("original input", File.ReadAllText(sourceLinkFile));
            Assert.Equal("original dictionary", File.ReadAllText(revisionFile));
            var log = ((MockEngine)task.BuildEngine).Log;
            Assert.Contains("ERROR", log);
            Assert.DoesNotContain(string.Format(Resources.MustNotContainWildcard, "SourceRoot.MappedPath", "/_/*/"), log);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void MissingSourceLinkOutputPreservesRevisionDictionary(string? path)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            File.WriteAllText(task.RevisionOutputFile!, "original dictionary");
            task.OutputFile = path;

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.Equal("original dictionary", File.ReadAllText(task.RevisionOutputFile!));
            Assert.Contains("ERROR", ((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void RevisionOutputCreatesMissingDirectory()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            task.RevisionOutputFile = Path.Combine(temp.CreateDirectory().Path, "revisions", "dictionary.json");

            Assert.True(task.Execute());

            Assert.True(File.Exists(task.RevisionOutputFile));
            Assert.Equal(task.OutputFile, task.SourceLink);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SourceLinkOutputCreatesMissingDirectory(bool usePlaceholderValues)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            if (!usePlaceholderValues)
            {
                task.RevisionOutputFile = null;
            }

            var directory = Path.Combine(temp.CreateDirectory().Path, "missing");
            task.OutputFile = Path.Combine(directory, "sourcelink.json");

            Assert.True(task.Execute());

            Assert.Equal(task.OutputFile, task.SourceLink);
            Assert.True(File.Exists(task.OutputFile));
            Assert.False(task.Log.HasLoggedErrors);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EmptyDiscoveryDoesNotCreateSourceLinkDirectory(bool usePlaceholderValues)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp);
            if (!usePlaceholderValues)
            {
                task.RevisionOutputFile = null;
            }

            var directory = Path.Combine(temp.CreateDirectory().Path, "missing");
            task.OutputFile = Path.Combine(directory, "sourcelink.json");

            Assert.True(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(Directory.Exists(directory));
            Assert.False(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void CompilerInputAndDictionaryCannotAlias()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            File.WriteAllText(task.OutputFile, "original input");
            task.RevisionOutputFile = Path.Combine(Path.GetDirectoryName(task.OutputFile)!, "subdirectory", "..", Path.GetFileName(task.OutputFile));

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.Equal("original input", File.ReadAllText(task.OutputFile));
            Assert.Contains(Resources.SourceLinkRevisionFilesMustDiffer, ((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void DictionaryWriteFailureDeletesExistingCompilerInput()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            task.RevisionOutputFile = temp.CreateDirectory().Path;

            Assert.False(task.Execute());
            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.True(Directory.Exists(task.RevisionOutputFile));
            Assert.Contains("ERROR", ((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void CompilerInputWriteFailureDeletesRevisionDictionary()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)));
            task.OutputFile = temp.CreateDirectory().Path;

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.RevisionOutputFile));
            Assert.True(Directory.Exists(task.OutputFile));
            Assert.Contains(task.OutputFile, ((MockEngine)task.BuildEngine).Log);
        }

        [Fact]
        public void CompilerInputWriteFailureSkipsRevisionGeneration()
        {
            using var temp = new TempRoot();
            var roots = new[] { Root(new string('a', 40)), Root(new string('b', 40)) };
            var task = CreateTask(temp, roots);
            task.OutputFile = temp.CreateDirectory().Path;
            var directory = Path.Combine(temp.CreateDirectory().Path, "revisions");
            task.RevisionOutputFile = Path.Combine(directory, "dictionary.json");

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(Directory.Exists(directory));
            var log = ((MockEngine)task.BuildEngine).Log;
            Assert.Contains(task.OutputFile, log);
            Assert.DoesNotContain(string.Format(Resources.InvalidSourceLinkPlaceholderRoot, roots[0].ItemSpec), log);
        }

        [Fact]
        public void RevisionGenerationFailureSkipsDictionaryWriter()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40)), Root(new string('b', 40)));
            task.NoWarnOnMissingSourceControlInformation = false;
            File.WriteAllText(task.RevisionOutputFile!, "original dictionary");

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.False(File.Exists(task.RevisionOutputFile));
            var log = ((MockEngine)task.BuildEngine).Log;
            Assert.Contains("ERROR", log);
            Assert.DoesNotContain(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty, log);
            Assert.DoesNotContain(string.Format(Resources.SourceLinkEmptyDeletingExistingFile, task.RevisionOutputFile), log);
        }

        [Fact]
        public void CleanupFailureIsReportedAndDoesNotSkipOtherOutput()
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40), mappedPath: "/_/*/"));
            task.OutputFile = temp.CreateDirectory().Path;

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.True(Directory.Exists(task.OutputFile));
            Assert.False(File.Exists(task.RevisionOutputFile));
            Assert.Contains(task.OutputFile, ((MockEngine)task.BuildEngine).Log);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnexpectedMappingExceptionCleansUpOutputs(bool usePlaceholderValues)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp);
            var revisionFile = task.RevisionOutputFile!;
            File.WriteAllText(task.OutputFile, "original input");
            File.WriteAllText(revisionFile, "original dictionary");
            task.SourceRoots = [new ThrowingSourceRoot()];
            if (!usePlaceholderValues)
            {
                task.RevisionOutputFile = null;
            }

            var exception = Assert.Throws<InvalidOperationException>(() => task.Execute());

            Assert.Equal("Metadata read failed.", exception.Message);
            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.Equal(!usePlaceholderValues, File.Exists(revisionFile));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void EmptyRevisionPathUsesResolvedSourceLinkWithoutWritingDictionary(string? revisionPath)
        {
            using var temp = new TempRoot();
            var root = Root(new string('a', 40));
            var task = CreateTask(temp, root);
            var revisionFile = task.RevisionOutputFile!;
            task.RevisionOutputFile = revisionPath;
            File.WriteAllText(revisionFile, "unchanged");

            Assert.True(task.Execute());

            Assert.Equal("unchanged", File.ReadAllText(revisionFile));
            Assert.Contains(root.GetMetadata("SourceLinkUrl"), File.ReadAllText(task.OutputFile));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void DefaultModeMappingErrorsDeleteExistingCompilerInputWithoutChangingRevisionDictionary(string? revisionPath)
        {
            using var temp = new TempRoot();
            var task = CreateTask(temp, Root(new string('a', 40), mappedPath: "/_/*/"));
            var revisionFile = task.RevisionOutputFile!;
            task.RevisionOutputFile = revisionPath;
            File.WriteAllText(task.OutputFile, "original input");
            File.WriteAllText(revisionFile, "original dictionary");

            Assert.False(task.Execute());

            Assert.Null(task.SourceLink);
            Assert.False(File.Exists(task.OutputFile));
            Assert.Equal("original dictionary", File.ReadAllText(revisionFile));
            Assert.Contains("ERROR", ((MockEngine)task.BuildEngine).Log);
        }

        private sealed class ThrowingSourceRoot : ITaskItem
        {
            public string ItemSpec { get; set; } = "unused";
            public ICollection MetadataNames => throw new NotImplementedException();
            public int MetadataCount => throw new NotImplementedException();
            public string GetMetadata(string metadataName) => throw new InvalidOperationException("Metadata read failed.");
            public void SetMetadata(string metadataName, string metadataValue) => throw new NotImplementedException();
            public void RemoveMetadata(string metadataName) => throw new NotImplementedException();
            public void CopyMetadataTo(ITaskItem destinationItem) => throw new NotImplementedException();
            public IDictionary CloneCustomMetadata() => throw new NotImplementedException();
        }
    }
}
