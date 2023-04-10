// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.IO;
using System.Text;
using Xunit;
using TestUtilities;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class GenerateSourceLinkFileTests
    {
        private static string AdjustSeparatorsInJson(string json)
            => Path.DirectorySeparatorChar == '/' ? json.Replace(@"\\", "/") : json;

        [Theory]
        [CombinatorialData]
        public void Empty(bool noWarning)
        {
            var sourceLinkFilePath = Path.Combine(TempRoot.Root, Guid.NewGuid().ToString());

            var engine = new MockEngine();

            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                OutputFile = sourceLinkFilePath,
                SourceRoots = new MockItem[0],
                NoWarnOnMissingSourceControlInformation = noWarning,
            };

            Assert.True(task.Execute());

            var expectedOutput = 
                (noWarning ? "" : "WARNING : " + string.Format(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty) + Environment.NewLine) +
                string.Format(Resources.SourceLinkEmptyNoExistingFile, sourceLinkFilePath);

            AssertEx.AssertEqualToleratingWhitespaceDifferences(expectedOutput, engine.Log);

            Assert.Null(task.SourceLink);
        }

        [Theory]
        [CombinatorialData]
        public void NoRepositoryUrl(bool noWarning)
        {
            var sourceLinkFilePath = Path.Combine(TempRoot.Root, Guid.NewGuid().ToString());

            var engine = new MockEngine();

            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                OutputFile = sourceLinkFilePath,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("MappedPath", "/1/")),
                    new MockItem("/2/", KVP("MappedPath", "/2/"), KVP("RevisionId", "f3dbcdfdd5b1f75613c7692f969d8df121fc3731"), KVP("SourceControl", "git")),
                    new MockItem("/3/", KVP("MappedPath", "/3/"), KVP("RevisionId", "f3dbcdfdd5b1f75613c7692f969d8df121fc3731"), KVP("SourceControl", "git"), KVP("RepositoryUrl", "")),
                },
                NoWarnOnMissingSourceControlInformation = noWarning,
            };

            Assert.True(task.Execute());

            var expectedOutput = 
                (noWarning ? "" : "WARNING : " + string.Format(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty) + Environment.NewLine) +
                string.Format(Resources.SourceLinkEmptyNoExistingFile, sourceLinkFilePath);

            AssertEx.AssertEqualToleratingWhitespaceDifferences(expectedOutput, engine.Log);

            Assert.Null(task.SourceLink);
        }

        [Fact]
        public void Empty_DeleteExistingFile()
        {
            using var tempRoot = new TempRoot();

            var sourceLinkFile = tempRoot.CreateFile();
            sourceLinkFile.WriteAllText("XYZ");

            var engine = new MockEngine();

            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                OutputFile = sourceLinkFile.Path,
                SourceRoots = new MockItem[0],
                NoWarnOnMissingSourceControlInformation = true,
            };

            Assert.True(task.Execute());

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                string.Format(Resources.SourceLinkEmptyDeletingExistingFile, sourceLinkFile.Path), engine.Log);

            Assert.Null(task.SourceLink);
        }

        [Fact]
        public void Escape()
        {
            var engine = new MockEngine();
            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    new MockItem(@"/_""_/", KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo/*"), KVP("SourceControl", "git")),
                },
            };

            var content = task.GenerateSourceLinkContent();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual(@"{""documents"":{""/_\""_/*"":""https://raw.githubusercontent.com/repo/*""}}", content);
        }

        [Fact]
        public void WithoutMappedPaths()
        {
            var engine = new MockEngine();

            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    new MockItem(@"C:\src\", KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo1/*"), KVP("SourceControl", "git")),
                    new MockItem(@"C:\x\a\", KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo2/*"), KVP("SourceControl", "git")),
                    new MockItem(@"C:\x\b\", KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo3/*"), KVP("SourceControl", "git")),
                },
            };

            var content = task.GenerateSourceLinkContent();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual(AdjustSeparatorsInJson(
                @"{""documents"":{" +
                @"""C:\\src\\*"":""https://raw.githubusercontent.com/repo1/*""," +
                @"""C:\\x\\a\\*"":""https://raw.githubusercontent.com/repo2/*""," +
                @"""C:\\x\\b\\*"":""https://raw.githubusercontent.com/repo3/*""}}"), content);
        }

        [Fact]
        public void WithMappedPaths()
        {
            var engine = new MockEngine();

            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                SourceRoots = new[] 
                {
                    new MockItem(@"C:\src\", KVP("MappedPath", "/_/"), KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo1/*"), KVP("SourceControl", "git")),
                    new MockItem(@"C:\x\a\", KVP("MappedPath", "/_/1/"), KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo2/*"), KVP("SourceControl", "git")),
                    new MockItem(@"C:\x\b\", KVP("MappedPath", "/_/2/"), KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo3/*"), KVP("SourceControl", "git")),
                },
            };

            var content = task.GenerateSourceLinkContent();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual(
                @"{""documents"":{" +
                @"""/_/*"":""https://raw.githubusercontent.com/repo1/*""," + 
                @"""/_/1/*"":""https://raw.githubusercontent.com/repo2/*""," + 
                @"""/_/2/*"":""https://raw.githubusercontent.com/repo3/*""}}", content);
        }

        [Fact]
        public void Errors()
        {
            var engine = new MockEngine();

            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    // skipped: missing SourceLinkUrl in source-controlled root:
                    new MockItem(@"C:\src\", KVP("SourceControl", "git")),

                    // skipped: missing SourceLinkUrl in non-source-controlled root
                    new MockItem(@"C:\x\a\"),

                    // error: * in local path:
                    new MockItem(@"C:\x\b\*\"),

                    // error: local path must end with separator:
                    new MockItem(@"C:\x\c"),

                    // error: multiple *'s in url
                    new MockItem(@"C:\x\d\", KVP("SourceLinkUrl", "http://a/**")),
                },
            };

            var content = task.GenerateSourceLinkContent();
            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(Resources.MustNotContainWildcard, "SourceRoot", MockItem.AdjustSeparators(@"C:\x\b\*\")) + Environment.NewLine +
                "ERROR : " + string.Format(Resources.MustEndWithDirectorySeparator, "SourceRoot", MockItem.AdjustSeparators(@"C:\x\c")) + Environment.NewLine +
                "ERROR : " + string.Format(Resources.MustContainSingleWildcard, "SourceRoot.SourceLinkUrl", "http://a/**"), engine.Log);

            Assert.Null(content);
        }

        [Fact]
        public void DoesNotRewriteContentIfFileContentIsSame()
        {
            using var temp = new TempRoot();
            var tempFile = temp.CreateFile();

            tempFile.WriteAllText("XYZ");

            var engine = new MockEngine();
            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    new MockItem(@"/_""_/", KVP("SourceLinkUrl", "https://raw.githubusercontent.com/repo/*"), KVP("SourceControl", "git")),
                },
                OutputFile = tempFile.Path
            };

            var result = task.Execute();

            var beforeWriteTime = File.GetLastWriteTime(tempFile.Path);

            Assert.Equal(@"{""documents"":{""/_\""_/*"":""https://raw.githubusercontent.com/repo/*""}}", File.ReadAllText(tempFile.Path, Encoding.UTF8));
            Assert.Equal(tempFile.Path, task.SourceLink);

            result = task.Execute();

            var afterWriteTime = File.GetLastWriteTime(tempFile.Path);

            Assert.Equal(beforeWriteTime, afterWriteTime);
        }
    }
}
