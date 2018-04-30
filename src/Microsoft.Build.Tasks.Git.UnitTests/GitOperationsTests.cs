// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl.UnitTests;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitOperationsTests
    {
        private static readonly char s = Path.DirectorySeparatorChar;
        private static readonly string s_root = (s == '/') ? "/usr/src" : @"C:\src";

        private static string InspectSourceRoot(ITaskItem sourceRoot)
        {
            var sourceControl = sourceRoot.GetMetadata("SourceControl");
            var revisionId = sourceRoot.GetMetadata("RevisionId");
            var nestedRoot = sourceRoot.GetMetadata("NestedRoot");
            var containingRoot = sourceRoot.GetMetadata("ContainingRoot");
            var sourceLinkUrl = sourceRoot.GetMetadata("SourceLinkUrl");

            return $"'{sourceRoot.ItemSpec}'" +
              (string.IsNullOrEmpty(sourceControl) ? "" : $" SourceControl='{sourceControl}'") +
              (string.IsNullOrEmpty(revisionId) ? "" : $" RevisionId='{revisionId}'") +
              (string.IsNullOrEmpty(nestedRoot) ? "" : $" NestedRoot='{nestedRoot}'") +
              (string.IsNullOrEmpty(containingRoot) ? "" : $" ContainingRoot='{containingRoot}'") +
              (string.IsNullOrEmpty(sourceLinkUrl) ? "" : $" SourceLinkUrl='{sourceLinkUrl}'");
        }

        private static string InspectDiagnostic((string resourceName, string[] args) warning)
            => (warning.args.Length == 0) ? warning.resourceName : warning.resourceName + ": " + string.Join(",", warning.args);

        [Fact]
        public void GetRevisionId_RepoWithoutCommits()
        {
            var repo = new TestRepository(workingDir: "", commitSha: null);
            Assert.Null(GitOperations.GetRevisionId(repo));
        }

        [Fact]
        public void GetRevisionId_RepoWithCommit()
        {
            var repo = new TestRepository(workingDir: "", commitSha: "8398cdcd9043724b9bef1efda8a703dfaa336c0f");
            Assert.Equal("8398cdcd9043724b9bef1efda8a703dfaa336c0f", GitOperations.GetRevisionId(repo));
        }

        [Fact]
        public void GetRepositoryUrl_NoRemotes()
        {
            var repo = new TestRepository(workingDir: s_root, commitSha: "1111111111111111111111111111111111111111");
            Assert.Null(GitOperations.GetRepositoryUrl(repo));
        }

        [Theory]
        [InlineData("https://github.com/org/repo")]
        [InlineData("http://github.com/org/repo")]
        [InlineData("http://github.com:102/org/repo")]
        [InlineData("ssh://user@github.com/org/repo")]
        [InlineData("abc://user@github.com/org/repo")]
        public void GetRepositoryUrl_Agnostic1(string url)
        {
            var repo = new TestRepository(workingDir: s_root, commitSha: "1111111111111111111111111111111111111111",
                remotes: new[] { new TestRemote("origin", url) });

            Assert.Equal(url, GitOperations.GetRepositoryUrl(repo));
        }

        [Theory]
        [InlineData("https://github.com/org/repo/./.", "https://github.com/org/repo/")]
        [InlineData("ssh://github.com/org/../repo", "ssh://github.com/repo")]
        [InlineData("ssh://github.com/%32/repo", "ssh://github.com/2/repo")]
        [InlineData("ssh://github.com/%3F/repo", "ssh://github.com/%3F/repo")]
        public void GetRepositoryUrl_Agnostic2(string originUrl, string expectedUrl)
        {
            var repo = new TestRepository(workingDir: s_root, commitSha: "1111111111111111111111111111111111111111",
                remotes: new[] { new TestRemote("origin", originUrl) });

            Assert.Equal(expectedUrl, GitOperations.GetRepositoryUrl(repo));
        }

        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData(@"C:", "file:///C:/")]
        [InlineData(@"C:\", "file:///C:/")]
        [InlineData(@"C:x", null)]
        [InlineData(@"C:x\y\..\z", null)]
        [InlineData(@"C:org/repo", null)]
        [InlineData(@"D:\src", "file:///D:/src")]
        [InlineData(@"\\", null)]
        [InlineData(@"\\server", "file://server/")]
        [InlineData(@"\\server\dir", "file://server/dir")]
        [InlineData(@"relative/./path", "file:///C:/src/a/b/relative/path")]
        [InlineData(@"../relative/path", "file:///C:/src/a/relative/path")]
        [InlineData(@"..\relative\path", "file:///C:/src/a/relative/path")]
        [InlineData(@"../relative/path?a=b", "file:///C:/src/a/relative/path%3Fa=b")]
        [InlineData(@"../relative/path*<>|\0%00", "file:///C:/src/a/relative/path*<>|/0%00")]
        [InlineData(@"../../../../relative/path", "file:///C:/relative/path")]
        [InlineData(@"a:/../../relative/path", "file:///a:/relative/path")]
        [InlineData(@"Z:/a/b/../../relative/path", "file:///Z:/relative/path")]
        [InlineData(@"../.://../../relative/path", "file:///C:/src/a/relative/path")]
        [InlineData(@"../.:./../../relative/path", "file:///C:/src/relative/path")]
        [InlineData(@".:/../../relative/path", "file:///C:/src/a/relative/path")]
        [InlineData(@"..:/../../relative/path", "file:///C:/src/a/relative/path")]
        [InlineData(@"@:org/repo", "file:///C:/src/a/b/@:org/repo")]
        public void GetRepositoryUrl_Windows(string originUrl, string expectedUrl)
        {
            var repo = new TestRepository(workingDir: @"C:\src\a\b", commitSha: "1111111111111111111111111111111111111111",
                remotes: new[] { new TestRemote("origin", originUrl) });

            Assert.Equal(expectedUrl, GitOperations.GetRepositoryUrl(repo));
        }

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(@"C:org/repo", @"https://C/org/repo")]
        [InlineData(@"/xyz/src", @"file:///xyz/src")]
        [InlineData(@"\path", @"file:///usr/src/a/b/\path")]
        [InlineData(@"relative/./path", @"file:///usr/src/a/b/relative/path")]
        [InlineData(@"../relative/path", @"file:///usr/src/a/relative/path")]
        [InlineData(@"../relative/path?a=b", @"file:///usr/src/a/relative/path?a=b")]
        [InlineData(@"../relative/path*<>|\0%00", @"file:///usr/src/a/relative/path*<>|\0%00")]
        [InlineData(@"../../../../relative/path", @"file:///relative/path")]
        [InlineData(@"../.://../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"../.:./../../relative/path", "file:///usr/src/relative/path")]
        [InlineData(@".:/../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"..:/../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"@:org/repo", @"file:///usr/src/a/b/@:org\repo")]
        public void GetRepositoryUrl_Unix(string originUrl, string expectedUrl)
        {
            var repo = new TestRepository(workingDir: "/usr/src/a/b", commitSha: "1111111111111111111111111111111111111111",
                remotes: new[] { new TestRemote("origin", originUrl) });

            Assert.Equal(expectedUrl, GitOperations.GetRepositoryUrl(repo));
        }

        [Theory]
        [InlineData("abc:org/repo", "https://abc/org/repo")]
        [InlineData("github.com:org/repo", "https://github.com/org/repo")]
        [InlineData("git@github.com:org/repo", "https://github.com/org/repo")]
        [InlineData("@github.com:org/repo", "https://github.com/org/repo")]
        [InlineData("http:x//y", "https://http/x//y")]
        public void GetRepositoryUrl_ScpSyntax(string originUrl, string expectedUrl)
        {
            var repo = new TestRepository(workingDir: s_root, commitSha: "1111111111111111111111111111111111111111",
                remotes: new[] { new TestRemote("origin", originUrl) });

            Assert.Equal(expectedUrl, GitOperations.GetRepositoryUrl(repo));
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommits()
        {
            var repo = new TestRepository(workingDir: s_root, commitSha: null);

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            Assert.Empty(items);
            AssertEx.Equal(new[] { "RepositoryWithoutCommit_SourceLink" }, warnings.Select(InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommitsWithSubmodules()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: null,
                submodules: new[] 
                {
                    new TestSubmodule("1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("1", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")
                });

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{s_root}{s}'",
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] { "RepositoryWithoutCommit_SourceLink" }, warnings.Select(InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RepoWithCommitsWithSubmodules()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "http://1.com", workDirCommitSha: null),
                    new TestSubmodule("1", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")
                });

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] { "SubmoduleWithoutCommit_SourceLink: 1" }, warnings.Select(InspectDiagnostic));
        }
    }
}
