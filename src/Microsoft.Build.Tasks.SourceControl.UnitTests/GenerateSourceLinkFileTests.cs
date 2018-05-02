// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.Git.UnitTests;
using System;
using System.IO;
using Xunit;

namespace Microsoft.Build.Tasks.SourceControl.UnitTests
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
                "WARNING : " + string.Format(Resources.NoItemsSpecifiedSourceLinkEmpty, "SourceRoot"), engine.Log);

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
                    new MockItem(@"/_\_""_/", ("SourceLinkUrl", "https://raw.githubusercontent.com/repo/*"), ("SourceControl", "git")),
                },
            };

            var content = task.GenerateSourceLinkContent();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual(@"{""documents"":{""/_\\_\""_/*"":""https://raw.githubusercontent.com/repo/*""}}", content);
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
                    new MockItem(@"C:\src\", ("SourceLinkUrl", "https://raw.githubusercontent.com/repo1/*"), ("SourceControl", "git")),
                    new MockItem(@"C:\x\a\", ("SourceLinkUrl", "https://raw.githubusercontent.com/repo2/*"), ("SourceControl", "git")),
                    new MockItem(@"C:\x\b\", ("SourceLinkUrl", "https://raw.githubusercontent.com/repo3/*"), ("SourceControl", "git")),
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
                    new MockItem(@"C:\src\", ("MappedPath", "/_/"), ("SourceLinkUrl", "https://raw.githubusercontent.com/repo1/*"), ("SourceControl", "git")),
                    new MockItem(@"C:\x\a\", ("MappedPath", "/_/1/"), ("SourceLinkUrl", "https://raw.githubusercontent.com/repo2/*"), ("SourceControl", "git")),
                    new MockItem(@"C:\x\b\", ("MappedPath", "/_/2/"), ("SourceLinkUrl", "https://raw.githubusercontent.com/repo3/*"), ("SourceControl", "git")),
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
                    // error: missing SourceLinkUrl in source-controlled root:
                    new MockItem(@"C:\src\", ("SourceControl", "git")),

                    // skipped: missing SourceLinkUrl in non-source-controlled root
                    new MockItem(@"C:\x\a\"),

                    // error: * in local path:
                    new MockItem(@"C:\x\b\*\"),

                    // error: local path must end with separator:
                    new MockItem(@"C:\x\c"),

                    // error: multiple *'s in url
                    new MockItem(@"C:\x\d\", ("SourceLinkUrl", "http://a/**")),
                },
            };

            var content = task.GenerateSourceLinkContent();
            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(Resources.IsEmpty, "SourceRoot.SourceLinkUrl", @"C:\src\") + Environment.NewLine +
                "ERROR : " + string.Format(Resources.MustNotContainWildcard, "SourceRoot", @"C:\x\b\*\") + Environment.NewLine +
                "ERROR : " + string.Format(Resources.MustEndWithDirectorySeparator, "SourceRoot", @"C:\x\c") + Environment.NewLine +
                "ERROR : " + string.Format(Resources.MustContainSingleWildcard, "SourceRoot.SourceLinkUrl", "http://a/**"), engine.Log);

            Assert.Null(content);
        }
    }
}
