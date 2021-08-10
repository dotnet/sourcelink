// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.IO;
using Xunit;
using TestUtilities;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class GenerateSourceLinkFileTests
    {
        private static string AdjustSeparatorsInJson(string json)
            => Path.DirectorySeparatorChar == '/' ? json.Replace(@"\\", "/") : json;

        [Fact]
        public void Empty()
        {
            var engine = new MockEngine();

            var task = new GenerateSourceLinkFile()
            {
                BuildEngine = engine,
                SourceRoots = new MockItem[0],
            };

            var content = task.GenerateSourceLinkContent();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "WARNING : " + string.Format(Resources.SourceControlInformationIsNotAvailableGeneratedSourceLinkEmpty), engine.Log);

            AssertEx.AreEqual(@"{""documents"":{}}", content);
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

            result = task.Execute();

            var afterWriteTime = File.GetLastWriteTime(tempFile.Path);

            Assert.Equal(beforeWriteTime, afterWriteTime);
        }
    }
}
