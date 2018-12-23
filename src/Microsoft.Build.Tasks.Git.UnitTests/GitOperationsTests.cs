// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Microsoft.Build.Framework;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitOperationsTests
    {
        private static readonly bool IsUnix = Path.DirectorySeparatorChar == '/';
        private static readonly char s = Path.DirectorySeparatorChar;
        private static readonly string s_root = (s == '/') ? "/usr/src" : @"C:\src";

        private static string InspectSourceRoot(ITaskItem sourceRoot)
        {
            var sourceControl = sourceRoot.GetMetadata("SourceControl");
            var revisionId = sourceRoot.GetMetadata("RevisionId");
            var nestedRoot = sourceRoot.GetMetadata("NestedRoot");
            var containingRoot = sourceRoot.GetMetadata("ContainingRoot");
            var scmRepositoryUrl = sourceRoot.GetMetadata("ScmRepositoryUrl");
            var sourceLinkUrl = sourceRoot.GetMetadata("SourceLinkUrl");

            return $"'{sourceRoot.ItemSpec}'" +
              (string.IsNullOrEmpty(sourceControl) ? "" : $" SourceControl='{sourceControl}'") +
              (string.IsNullOrEmpty(revisionId) ? "" : $" RevisionId='{revisionId}'") +
              (string.IsNullOrEmpty(nestedRoot) ? "" : $" NestedRoot='{nestedRoot}'") +
              (string.IsNullOrEmpty(containingRoot) ? "" : $" ContainingRoot='{containingRoot}'") +
              (string.IsNullOrEmpty(scmRepositoryUrl) ? "" : $" ScmRepositoryUrl='{scmRepositoryUrl}'") +
              (string.IsNullOrEmpty(sourceLinkUrl) ? "" : $" SourceLinkUrl='{sourceLinkUrl}'");
        }

        private static string InspectDiagnostic(KeyValuePair<string, object[]> warning)
            => string.Format(warning.Key, warning.Value);

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

            var warnings = new List<KeyValuePair<string, object[]>>();
            Assert.Null(GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add(KVP(message, args))));
            AssertEx.Equal(new[] { Resources.RepositoryHasNoRemote }, warnings.Select(InspectDiagnostic));
        }

        private void ValidateGetRepositoryUrl(string workingDir, string actualUrl, string expectedUrl)
        {
            var testRemote = new TestRemote("origin", actualUrl);

            var repo = new TestRepository(workingDir, commitSha: "1111111111111111111111111111111111111111",
                remotes: new[] { testRemote });

            var expectedWarnings = (expectedUrl != null) ?
                Array.Empty<string>() :
                new[] { string.Format(Resources.InvalidRepositoryRemoteUrl, testRemote.Name, testRemote.Url) };

            var warnings = new List<KeyValuePair<string, object[]>>();
            Assert.Equal(expectedUrl, GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add(KVP(message, args))));
            AssertEx.Equal(expectedWarnings, warnings.Select(InspectDiagnostic));
        }

        [Theory]
        [InlineData("https://github.com/org/repo")]
        [InlineData("http://github.com/org/repo")]
        [InlineData("http://github.com:102/org/repo")]
        [InlineData("ssh://user@github.com/org/repo")]
        [InlineData("abc://user@github.com/org/repo")]
        public void GetRepositoryUrl_PlatformAgnostic1(string url)
        {
            ValidateGetRepositoryUrl(s_root, url, url);
        }

        [Theory]
        [InlineData("http://?", null)]
        [InlineData("https://github.com/org/repo/./.", "https://github.com/org/repo/")]
        [InlineData("http://github.com/org/\u1234", "http://github.com/org/\u1234")]
        [InlineData("ssh://github.com/org/../repo", "ssh://github.com/repo")]
        [InlineData("ssh://github.com/%32/repo", "ssh://github.com/2/repo")]
        [InlineData("ssh://github.com/%3F/repo", "ssh://github.com/%3F/repo")]
        public void GetRepositoryUrl_PlatformAgnostic2(string url, string expectedUrl)
        {
            ValidateGetRepositoryUrl(s_root, url, expectedUrl);
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
        [InlineData(@"../.:./../../relative/path", "ssh://../relative/path")]
        [InlineData(@".:/../../relative/path", "ssh://./relative/path")]
        [InlineData(@"..:/../../relative/path", "ssh://../relative/path")]
        [InlineData(@"@:org/repo", "file:///C:/src/a/b/@:org/repo")]
        public void GetRepositoryUrl_Windows(string url, string expectedUrl)
        {
            ValidateGetRepositoryUrl(@"C:\src\a\b", url, expectedUrl);
        }

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(@"C:org/repo", @"ssh://c/org/repo")]
        [InlineData(@"/xyz/src", @"file:///xyz/src")]
        [InlineData(@"\path\a\b", @"file:///path/a/b")]
        [InlineData(@"relative/./path", @"file:///usr/src/a/b/relative/path")]
        [InlineData(@"../relative/path", @"file:///usr/src/a/relative/path")]
        [InlineData(@"../relative/path?a=b", @"file:///usr/src/a/relative/path%3Fa=b")]
        [InlineData(@"../relative/path*<>|\0%00", @"file:///usr/src/a/relative/path*<>|\0%00")]
        [InlineData(@"../../../../relative/path", @"file:///relative/path")]
        [InlineData(@"../.://../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"../.:./../../relative/path", "ssh://../relative/path")]
        [InlineData(@".:/../../relative/path", "ssh://./relative/path")]
        [InlineData(@"..:/../../relative/path", "ssh://../relative/path")]
        [InlineData(@"@:org/repo", @"file:///usr/src/a/b/@:org/repo")]
        public void GetRepositoryUrl_Unix(string url, string expectedUrl)
        {
            ValidateGetRepositoryUrl("/usr/src/a/b", url, expectedUrl);
        }

        [Theory]
        [InlineData("abc:org/repo", "ssh://abc/org/repo")]
        [InlineData("ABC:ORG/REPO/X/Y", "ssh://abc/ORG/REPO/X/Y")]
        [InlineData("github.com:org/repo", "ssh://github.com/org/repo")]
        [InlineData("git@github.com:org/repo", "ssh://git@github.com/org/repo")]
        [InlineData("@github.com:org/repo", "ssh://@github.com/org/repo")]
        [InlineData("http:x//y", "ssh://http/x//y")]
        public void GetRepositoryUrl_ScpSyntax(string url, string expectedUrl)
        {
            ValidateGetRepositoryUrl(s_root, url, expectedUrl);
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommits()
        {
            var repo = new TestRepository(workingDir: s_root, commitSha: null);

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            Assert.Empty(items);
            AssertEx.Equal(new[] { Resources.RepositoryHasNoCommit }, warnings.Select(InspectDiagnostic));
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

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{s_root}{s}' ScmRepositoryUrl='http://1.com/'",
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}' ScmRepositoryUrl='http://2.com/'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] { Resources.RepositoryHasNoCommit }, warnings.Select(InspectDiagnostic));
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

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}' ScmRepositoryUrl='http://2.com/'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] { string.Format(Resources.SubmoduleWithoutCommit_SourceLink, "1") }, warnings.Select(InspectDiagnostic));
        }

        [ConditionalFact(typeof(WindowsOnly))]
        public void GetSourceRoots_RelativeSubmodulePaths_Windows()
        {
            var repo = new TestRepository(
                workingDir: @"C:\src",
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "./a/b", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("2", "sub/2", "../a", "2222222222222222222222222222222222222222"),
                });

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'C:\src\' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'C:\src\sub\1\' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='C:\src\' ScmRepositoryUrl='file:///C:/src/a/b'",
                $@"'C:\src\sub\2\' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='C:\src\' ScmRepositoryUrl='file:///C:/a'",
            }, items.Select(InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [ConditionalFact(typeof(WindowsOnly))]
        public void GetSourceRoots_RelativeSubmodulePaths_Windows_UnicodeAndEscapes()
        {
            var repo = new TestRepository(
                workingDir: @"C:\%25@噸",
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("%25ሴ", "sub/%25ሴ", "./a/b", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("%25ለ", "sub/%25ለ", "../a", "2222222222222222222222222222222222222222"),
                });

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'C:\%25@噸\' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'C:\%25@噸\sub\%25ሴ\' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/%25ሴ/' ContainingRoot='C:\%25@噸\' ScmRepositoryUrl='file:///C:/%25@噸/a/b'",
                $@"'C:\%25@噸\sub\%25ለ\' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/%25ለ/' ContainingRoot='C:\%25@噸\' ScmRepositoryUrl='file:///C:/a'",
            }, items.Select(InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [ConditionalFact(typeof(UnixOnly))]
        public void GetSourceRoots_RelativeSubmodulePaths_Unix()
        {
            var repo = new TestRepository(
                workingDir: @"/src",
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "./a/b", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("2", "sub/2", "../a", "2222222222222222222222222222222222222222"),
                });

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'/src/' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'/src/sub/1/' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='/src/' ScmRepositoryUrl='file:///src/a/b'",
                $@"'/src/sub/2/' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='/src/' ScmRepositoryUrl='file:///a'",
            }, items.Select(InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [ConditionalFact(typeof(UnixOnly))]
        public void GetSourceRoots_RelativeSubmodulePaths_Unix_UnicodeAndEscapes()
        {
            var repo = new TestRepository(
                workingDir: @"/%25@噸",
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("%25ሴ", "sub/%25ሴ", "./a/b", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("%25ለ", "sub/%25ለ", "../a", "2222222222222222222222222222222222222222"),
                });

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'/%25@噸/' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'/%25@噸/sub/%25ሴ/' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/%25ሴ/' ContainingRoot='/%25@噸/' ScmRepositoryUrl='file:///%25@噸/a/b'",
                $@"'/%25@噸/sub/%25ለ/' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/%25ለ/' ContainingRoot='/%25@噸/' ScmRepositoryUrl='file:///a'",
            }, items.Select(InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetSourceRoots_InvalidSubmoduleUrlOrPath()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "http:///", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("2", "sub/\0*<>|:", "http://2.com", "2222222222222222222222222222222222222222"),
                    new TestSubmodule("3", "sub/3", "http://3.com", "3333333333333333333333333333333333333333"),
                });

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{s_root}{s}sub{s}3{s}' SourceControl='git' RevisionId='3333333333333333333333333333333333333333' NestedRoot='sub/3/' ContainingRoot='{s_root}{s}' ScmRepositoryUrl='http://3.com/'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] 
            {
                string.Format(Resources.InvalidSubmoduleUrl_SourceLink, "1", "http:///"),
                string.Format(Resources.InvalidSubmodulePath_SourceLink, "2", "sub/\0*<>|:")
            }, warnings.Select(InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_GvfsWithoutModules()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                config: new Dictionary<string, object> { { "core.gvfs", true } },
                submodulesSupported: false);

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: _ => false);

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
            }, items.Select(InspectSourceRoot));
        }

        [Fact]
        public void GetSourceRoots_GvfsWithModules()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                config: new Dictionary<string, object> { { "core.gvfs", true } },
                submodulesSupported: false);

            Assert.Throws<LibGit2SharpException>(() => GitOperations.GetSourceRoots(repo, null, fileExists: _ => true));
        }

        [Fact]
        public void GetSourceRoots_GvfsBadOptionType()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                config: new Dictionary<string, object> { { "core.gvfs", 1 } },
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "http://1.com/", "1111111111111111111111111111111111111111"),
                });

            var warnings = new List<KeyValuePair<string, object[]>>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add(KVP(message, args)), fileExists: null);

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{s_root}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{s_root}{s}' ScmRepositoryUrl='http://1.com/'",
            }, items.Select(InspectSourceRoot));
        }

        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData(@"C:\", null)]
        [InlineData(@"C:\x", null)]
        [InlineData(@"C:\x\y\z", null)]
        [InlineData(@"C:\src", null)]
        [InlineData(@"C:\src\", null)]
        [InlineData(@"C:\src\a\x.cs", @"C:\src\a")]
        [InlineData(@"C:\src\b\x.cs", @"C:\src")]
        [InlineData(@"C:\src\ab\x.cs", @"C:\src")]
        [InlineData(@"C:\src\a\b\x.cs", @"C:\src\a")]
        [InlineData(@"C:\src\c\x.cs", @"C:\src\c")]
        [InlineData(@"C:\src\c", @"C:\src")]
        [InlineData(@"C:\src\c\", @"C:\src")]
        [InlineData(@"C:\src\c.cs", @"C:\src")]
        [InlineData(@"C:\src\c\x\x.cs", @"C:\src\c\x")]
        [InlineData(@"C:\src\d\x.cs", @"C:\src")]
        [InlineData(@"C:\src\e\x.cs", @"C:\src\e")]
        public void GetContainingRepository_Windows(string path, string expectedDirectory)
        {
            var actual = GitOperations.GetContainingRepository(path,
                new GitOperations.SourceControlDirectory("", null,
                    new List<GitOperations.SourceControlDirectory>
                    {
                        new GitOperations.SourceControlDirectory("C:", null, new List<GitOperations.SourceControlDirectory>
                        {
                            new GitOperations.SourceControlDirectory("src", @"C:\src", new List<GitOperations.SourceControlDirectory>
                            {
                                new GitOperations.SourceControlDirectory("a", @"C:\src\a"),
                                new GitOperations.SourceControlDirectory("c", @"C:\src\c", new List<GitOperations.SourceControlDirectory>
                                {
                                    new GitOperations.SourceControlDirectory("x", @"C:\src\c\x")
                                }),
                                new GitOperations.SourceControlDirectory("e", @"C:\src\e")
                            }),
                        })
                    }));

            Assert.Equal(expectedDirectory, actual?.RepositoryFullPath);
        }

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(@"/", null)]
        [InlineData(@"/x", null)]
        [InlineData(@"/x/y/z", null)]
        [InlineData(@"/src", null)]
        [InlineData(@"/src/", null)]
        [InlineData(@"/src/a/x.cs", @"/src/a")]
        [InlineData(@"/src/b/x.cs", @"/src")]
        [InlineData(@"/src/ab/x.cs", @"/src")]
        [InlineData(@"/src/a/b/x.cs", @"/src/a")]
        [InlineData(@"/src/c/x.cs", @"/src/c")]
        [InlineData(@"/src/c", @"/src")]
        [InlineData(@"/src/c/", @"/src")]
        [InlineData(@"/src/c.cs", @"/src")]
        [InlineData(@"/src/c/x/x.cs", @"/src/c/x")]
        [InlineData(@"/src/d/x.cs", @"/src")]
        [InlineData(@"/src/e/x.cs", @"/src/e")]
        public void GetContainingRepository_Unix(string path, string expectedDirectory)
        {
            var actual = GitOperations.GetContainingRepository(path,
                new GitOperations.SourceControlDirectory("", null,
                    new List<GitOperations.SourceControlDirectory>
                    {
                        new GitOperations.SourceControlDirectory("/", null, new List<GitOperations.SourceControlDirectory>
                        {
                            new GitOperations.SourceControlDirectory("src", "/src", new List<GitOperations.SourceControlDirectory>
                            {
                                new GitOperations.SourceControlDirectory("a", "/src/a"),
                                new GitOperations.SourceControlDirectory("c", "/src/c", new List<GitOperations.SourceControlDirectory>
                                {
                                    new GitOperations.SourceControlDirectory("x", "/src/c/x"),
                                }),
                                new GitOperations.SourceControlDirectory("e", "/src/e"),
                            }),
                        })
                    }));

            Assert.Equal(expectedDirectory, actual?.RepositoryFullPath);
        }

        [Fact]
        public void BuildDirectoryTree()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: null,
                submodules: new[]
                {
                    new TestSubmodule(null, "c/x", null, null),
                    new TestSubmodule(null, "e", null, null),
                    new TestSubmodule(null, "a", null, null),
                    new TestSubmodule(null, "a/a/a/a/", null, null),
                    new TestSubmodule(null, "c", null, null),
                    new TestSubmodule(null, "a/z", null, null),
                });

            var root = GitOperations.BuildDirectoryTree(repo);

            string inspect(GitOperations.SourceControlDirectory node)
                => node.Name + (node.RepositoryFullPath != null ? $"!" : "") + "{" + string.Join(",", node.OrderedChildren.Select(inspect)) + "}";

            var expected = IsUnix ?
                "{/{usr{src!{a!{a{a{a!{}}},z!{}},c!{x!{}},e!{}}}}}" :
                "{C:{src!{a!{a{a{a!{}}},z!{}},c!{x!{}},e!{}}}}";

            Assert.Equal(expected, inspect(root));
        }

        [Fact]
        public void GetUntrackedFiles_ProjectInMainRepoIncludesFilesInSubmodules()
        {
            string gitRoot = s_root.Replace('\\', '/');

            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("2", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")
                },
                ignoredPaths: new[] { gitRoot + @"/c.cs", gitRoot + @"/p/d.cs", gitRoot + @"/sub/1/x.cs" });

            var subRoot1 = Path.Combine(s_root, "sub", "1");
            var subRoot2 = Path.Combine(s_root, "sub", "2");

            var subRepos = new Dictionary<string, TestRepository>()
            {
                { subRoot1, new TestRepository(subRoot1, commitSha: null, ignoredPaths: new[] { gitRoot + @"/sub/1/obj/a.cs" }) },
                { subRoot2, new TestRepository(subRoot2, commitSha: null, ignoredPaths: new[] { gitRoot + @"/sub/2/obj/b.cs" }) },
            };

            var actual = GitOperations.GetUntrackedFiles(repo,
                new[]
                {
                    new MockItem(@"c.cs"),                         // not ignored
                    new MockItem(@"..\sub\1\x.cs"),                // ignored in the main repository, but not in the submodule (which has a priority)
                    new MockItem(@"../sub/2/obj/b.cs"),            // ignored in submodule #2
                    new MockItem(@"d.cs"),                         // not ignored
                    new MockItem(@"..\..\w.cs"),                   // outside of repo
                    new MockItem(IsUnix ? "/d/w.cs" : @"D:\w.cs"), // outside of repo
                },
                projectDirectory: Path.Combine(s_root, "p"),
                root => subRepos[root]);

            AssertEx.Equal(new[] 
            {
                MockItem.AdjustSeparators("../sub/2/obj/b.cs"),
                MockItem.AdjustSeparators("d.cs"),
                MockItem.AdjustSeparators(@"..\..\w.cs"),
                MockItem.AdjustSeparators(IsUnix ? "/d/w.cs" : @"D:\w.cs")
            }, actual.Select(item => item.ItemSpec));
        }

        [Fact]
        public void GetUntrackedFiles_ProjectInSubmodule()
        {
            string gitRoot = s_root.Replace('\\', '/');

            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("2", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")
                },
                ignoredPaths: new[] { gitRoot + "/c.cs", gitRoot + "/sub/1/x.cs" });

            var subRoot1 = Path.Combine(s_root, "sub", "1");
            var subRoot2 = Path.Combine(s_root, "sub", "2");

            var subRepos = new Dictionary<string, TestRepository>()
            {
                { subRoot1, new TestRepository(subRoot1, commitSha: null, ignoredPaths: new[] { gitRoot + "/sub/1/obj/a.cs" }) },
                { subRoot2, new TestRepository(subRoot2, commitSha: null, ignoredPaths: new[] { gitRoot + "/sub/2/obj/b.cs" }) },
            };

            var actual = GitOperations.GetUntrackedFiles(repo,
                new[]
                {
                    new MockItem(@"c.cs"),           // not ignored
                    new MockItem(@"x.cs"),           // ignored in the main repository, but not in the submodule (which has a priority)
                    new MockItem(@"obj\a.cs"),       // ignored in submodule #1
                    new MockItem(@"obj\b.cs"),       // not ignored
                    new MockItem(@"..\2\obj\b.cs"),  // ignored in submodule #2
                    new MockItem(@"..\..\c.cs"),     // ignored in main repo
                },
                projectDirectory: subRoot1,
                root => subRepos[root]);

            AssertEx.Equal(new[]
            {
                MockItem.AdjustSeparators(@"obj\a.cs"),
                MockItem.AdjustSeparators(@"..\2\obj\b.cs"),
                MockItem.AdjustSeparators(@"..\..\c.cs")
            }, actual.Select(item => item.ItemSpec));
        }
    }
}
