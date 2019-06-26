// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitOperationsTests
    {
        private static readonly bool IsUnix = Path.DirectorySeparatorChar == '/';
        private static readonly char s = Path.DirectorySeparatorChar;
        private static readonly string s_root = (s == '/') ? "/usr/src" : @"C:\src";

        private static readonly GitEnvironment s_environment = new GitEnvironment("/home");

        private string _workingDir = s_root;

        private GitRepository CreateRepository(
            string workingDir = null,
            GitConfig config = null,
            string commitSha = null,
            ImmutableArray<GitSubmodule> submodules = default,
            GitIgnore ignore = null)
        {
            workingDir ??= _workingDir;
            var gitDir = Path.Combine(workingDir, ".git");
            return new GitRepository(
                s_environment,
                config ?? GitConfig.Empty,
                gitDir, 
                gitDir,
                _workingDir,
                submodules.IsDefault ? ImmutableArray<GitSubmodule>.Empty : submodules,
                submoduleDiagnostics: ImmutableArray<string>.Empty,
                ignore ?? new GitIgnore(root: null, workingDir, ignoreCase: false),
                commitSha);
        }

        private GitSubmodule CreateSubmodule(string name, string relativePath, string url, string headCommitSha)
            => new GitSubmodule(name, relativePath, Path.GetFullPath(Path.Combine(_workingDir, relativePath)), url, headCommitSha);

        internal static GitIgnore CreateIgnore(string workingDirectory, string[] filePathsRelativeToWorkingDirectory)
        {
            var patterns = filePathsRelativeToWorkingDirectory.Select(p => new GitIgnore.Pattern(p, GitIgnore.PatternFlags.FullPath));

            return new GitIgnore(
                new GitIgnore.PatternGroup(parent: null, PathUtils.ToPosixDirectoryPath(workingDirectory), patterns.ToImmutableArray()),
                workingDirectory,
                ignoreCase: false);
        }

        private static GitVariableName CreateVariableName(string str)
        {
            var parts = str.Split(new[] { '.' }, 3);
            return parts.Length switch
            {
                2 => new GitVariableName(parts[0], "", parts[1]),
                3 => new GitVariableName(parts[0], parts[1], parts[2]),
                _ => throw new InvalidOperationException()
            };
        }

        private static GitConfig CreateConfig(params (string Name, string Value)[] variables)
            => new GitConfig(ImmutableDictionary.CreateRange(
                variables.Select(v => new KeyValuePair<GitVariableName, ImmutableArray<string>>(CreateVariableName(v.Name), ImmutableArray.Create(v.Value)))));

        private static GitConfig CreateConfig(params (string Name, string[] Values)[] variables)
            => new GitConfig(ImmutableDictionary.CreateRange(
                variables.Select(v => new KeyValuePair<GitVariableName, ImmutableArray<string>>(CreateVariableName(v.Name), ImmutableArray.CreateRange(v.Values)))));

        [Fact]
        public void GetRepositoryUrl_NoRemotes()
        {
            var repo = CreateRepository();
            var warnings = new List<(string, object[])>();
            Assert.Null(GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[] { Resources.RepositoryHasNoRemote }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_Origin()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.origin.url", "http://github.com/origin")));

            var warnings = new List<(string, object[])>();

            Assert.Equal("http://github.com/origin", GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args))));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetRepositoryUrl_NoOrigin()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.def.url", "http://github.com/def")));

            var warnings = new List<(string, object[])>();

            Assert.Equal("http://github.com/abc", GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args))));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetRepositoryUrl_Specified()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.origin.url", "http://github.com/origin")));

            var warnings = new List<(string, object[])>();

            Assert.Equal("http://github.com/abc",
                GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args)),
                remoteName: "abc"));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetRepositoryUrl_SpecifiedNotFound_OriginFallback()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.origin.url", "http://github.com/origin")));

            var warnings = new List<(string, object[])>();

            Assert.Equal("http://github.com/origin", 
                GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args)),
                remoteName: "myremote"));

            AssertEx.Equal(new[]
            {
                string.Format(Resources.RepositoryDoesNotHaveSpecifiedRemote, "myremote", "origin")
            }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_SpecifiedNotFound_FirstFallback()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.def.url", "http://github.com/def")));

            var warnings = new List<(string, object[])>();

            Assert.Equal("http://github.com/abc",
                GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args)),
                remoteName: "myremote"));

            AssertEx.Equal(new[]
            {
                string.Format(Resources.RepositoryDoesNotHaveSpecifiedRemote, "myremote", "abc")
            }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_BadUrl()
        {
            var repo = CreateRepository(config: CreateConfig(("remote.origin.url", "http://?")));

            var warnings = new List<(string, object[])>();
            Assert.Null(GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[]
            {
                string.Format(Resources.InvalidRepositoryRemoteUrl, "origin", "http://?")
            }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_InsteadOf()
        {
            var repo = CreateRepository(config: new GitConfig(ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<GitVariableName, ImmutableArray<string>>(new GitVariableName("remote", "origin", "url"), ImmutableArray.Create("http://?")),
                new KeyValuePair<GitVariableName, ImmutableArray<string>>(new GitVariableName("url", "git@github.com:org/repo", "insteadOf"), ImmutableArray.Create("http://?"))
            })));

            var warnings = new List<(string, object[])>();
            Assert.Equal("ssh://git@github.com/org/repo", GitOperations.GetRepositoryUrl(repo, (message, args) => warnings.Add((message, args))));
            Assert.Empty(warnings);
        }

        [Theory]
        [InlineData("https://github.com/org/repo")]
        [InlineData("http://github.com/org/repo")]
        [InlineData("http://github.com:102/org/repo")]
        [InlineData("ssh://user@github.com/org/repo")]
        [InlineData("abc://user@github.com/org/repo")]
        public void NormalizeUrl_PlatformAgnostic1(string url)
        {
            Assert.Equal(url, GitOperations.NormalizeUrl(url, s_root));
        }

        [Theory]
        [InlineData("http://?", null)]
        [InlineData("https://github.com/org/repo/./.", "https://github.com/org/repo/")]
        [InlineData("http://github.com/org/\u1234", "http://github.com/org/\u1234")]
        [InlineData("ssh://github.com/org/../repo", "ssh://github.com/repo")]
        [InlineData("ssh://github.com/%32/repo", "ssh://github.com/2/repo")]
        [InlineData("ssh://github.com/%3F/repo", "ssh://github.com/%3F/repo")]
        public void NormalizeUrl_PlatformAgnostic2(string url, string expectedUrl)
        {
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, s_root));
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
        public void NormalizeUrl_Windows(string url, string expectedUrl)
        {
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, @"C:\src\a\b"));
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
        public void NormalizeUrl_Unix(string url, string expectedUrl)
        {
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, "/usr/src/a/b"));
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
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, s_root));
        }

        [Theory]
        [InlineData("http://test.com/test-repo", "http", "ssh", "ssh://test.com/test-repo")]
        [InlineData("http://test.com/test-repo", "", "pre-", "pre-http://test.com/test-repo")]
        [InlineData("http://test.com/test-repo", "http", "", "://test.com/test-repo")]
        [InlineData("http://test.com/test-repo", "Http://", "xxx", "http://test.com/test-repo")]
        public void ApplyInsteadOfUrlMapping_Single(string url, string prefix, string replacement, string mappedUrl)
        {
            var config = CreateConfig(($"url.{replacement}.insteadOf", prefix));
            var actualMappedUrl = GitOperations.ApplyInsteadOfUrlMapping(config, url);
            Assert.Equal(mappedUrl, actualMappedUrl);
        }

        [Fact]
        public void ApplyInsteadOfUrlMapping_Multiple()
        {
            var config = CreateConfig(
                ("url.A.insteadOf", new[] { "http://github", "http:" }),
                ("url.B.insteadOf", new[] { "http://" }),
                ("url.C.insteadOf", new[] { "http:/" }));

            var actualMappedUrl = GitOperations.ApplyInsteadOfUrlMapping(config, "http://github.com");
            Assert.Equal("A.com", actualMappedUrl);
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommits()
        {
            var repo = CreateRepository();

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            Assert.Empty(items);
            AssertEx.Equal(new[] { Resources.RepositoryHasNoCommit }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommitsWithSubmodules()
        {
            var repo = CreateRepository(
                commitSha: null,
                config: CreateConfig(("url.ssh://.insteadOf", "http://")),
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("1", "sub/2", "http://2.com", "2222222222222222222222222222222222222222"))); ;

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{_workingDir}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{_workingDir}{s}' ScmRepositoryUrl='ssh://1.com/'",
                $@"'{_workingDir}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{_workingDir}{s}' ScmRepositoryUrl='ssh://2.com/'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            AssertEx.Equal(new[] { Resources.RepositoryHasNoCommit }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RepoWithCommitsWithSubmodules()
        {
            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "http://1.com", headCommitSha: null),
                    CreateSubmodule("1", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")));

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{_workingDir}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{_workingDir}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{_workingDir}{s}' ScmRepositoryUrl='http://2.com/'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            AssertEx.Equal(new[] { string.Format(Resources.SourceCodeWontBeAvailableViaSourceLink, string.Format(Resources.SubmoduleWithoutCommit, "1")) }, 
                warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [ConditionalFact(typeof(WindowsOnly))]
        public void GetSourceRoots_RelativeSubmodulePaths_Windows()
        {
            _workingDir = @"C:\src";

            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "./a/b", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("2", "sub/2", "../a", "2222222222222222222222222222222222222222")));

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'C:\src\' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'C:\src\sub\1\' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='C:\src\' ScmRepositoryUrl='file:///C:/src/a/b'",
                $@"'C:\src\sub\2\' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='C:\src\' ScmRepositoryUrl='file:///C:/a'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [ConditionalFact(typeof(WindowsOnly))]
        public void GetSourceRoots_RelativeSubmodulePaths_Windows_UnicodeAndEscapes()
        {
            _workingDir = @"C:\%25@噸";

            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("%25ሴ", "sub/%25ሴ", "./a/b", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("%25ለ", "sub/%25ለ", "../a", "2222222222222222222222222222222222222222")));

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'C:\%25@噸\' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'C:\%25@噸\sub\%25ሴ\' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/%25ሴ/' ContainingRoot='C:\%25@噸\' ScmRepositoryUrl='file:///C:/%25@噸/a/b'",
                $@"'C:\%25@噸\sub\%25ለ\' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/%25ለ/' ContainingRoot='C:\%25@噸\' ScmRepositoryUrl='file:///C:/a'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [ConditionalFact(typeof(UnixOnly))]
        public void GetSourceRoots_RelativeSubmodulePaths_Unix()
        {
            _workingDir = @"/src";

            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "./a/b", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("2", "sub/2", "../a", "2222222222222222222222222222222222222222")));

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'/src/' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'/src/sub/1/' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='/src/' ScmRepositoryUrl='file:///src/a/b'",
                $@"'/src/sub/2/' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='/src/' ScmRepositoryUrl='file:///a'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [ConditionalFact(typeof(UnixOnly), Skip = "https://github.com/dotnet/corefx/issues/34227")]
        public void GetSourceRoots_RelativeSubmodulePaths_Unix_UnicodeAndEscapes()
        {
            _workingDir = @"/%25@噸";

            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("%25ሴ", "sub/%25ሴ", "./a/b", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("%25ለ", "sub/%25ለ", "../a", "2222222222222222222222222222222222222222")));

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'/%25@噸/' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'/%25@噸/sub/%25ሴ/' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/%25ሴ/' ContainingRoot='/%25@噸/' ScmRepositoryUrl='file:///%25@噸/a/b'",
                $@"'/%25@噸/sub/%25ለ/' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/%25ለ/' ContainingRoot='/%25@噸/' ScmRepositoryUrl='file:///a'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetSourceRoots_InvalidSubmoduleUrl()
        {
            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "http:///", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("3", "sub/3", "http://3.com", "3333333333333333333333333333333333333333")));

            var warnings = new List<(string, object[])>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{s_root}{s}sub{s}3{s}' SourceControl='git' RevisionId='3333333333333333333333333333333333333333' NestedRoot='sub/3/' ContainingRoot='{s_root}{s}' ScmRepositoryUrl='http://3.com/'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            AssertEx.Equal(new[] 
            {
                string.Format(Resources.SourceCodeWontBeAvailableViaSourceLink, string.Format(Resources.InvalidSubmoduleUrl, "1", "http:///")),
            }, warnings.Select(TestUtilities.InspectDiagnostic));
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
                new GitOperations.DirectoryNode("", null,
                    new List<GitOperations.DirectoryNode>
                    {
                        new GitOperations.DirectoryNode("C:", null, new List<GitOperations.DirectoryNode>
                        {
                            new GitOperations.DirectoryNode("src", @"C:\src", new List<GitOperations.DirectoryNode>
                            {
                                new GitOperations.DirectoryNode("a", @"C:\src\a"),
                                new GitOperations.DirectoryNode("c", @"C:\src\c", new List<GitOperations.DirectoryNode>
                                {
                                    new GitOperations.DirectoryNode("x", @"C:\src\c\x")
                                }),
                                new GitOperations.DirectoryNode("e", @"C:\src\e")
                            }),
                        })
                    }));

            Assert.Equal(expectedDirectory, actual?.WorkingDirectoryFullPath);
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
                new GitOperations.DirectoryNode("", null,
                    new List<GitOperations.DirectoryNode>
                    {
                        new GitOperations.DirectoryNode("/", null, new List<GitOperations.DirectoryNode>
                        {
                            new GitOperations.DirectoryNode("src", "/src", new List<GitOperations.DirectoryNode>
                            {
                                new GitOperations.DirectoryNode("a", "/src/a"),
                                new GitOperations.DirectoryNode("c", "/src/c", new List<GitOperations.DirectoryNode>
                                {
                                    new GitOperations.DirectoryNode("x", "/src/c/x"),
                                }),
                                new GitOperations.DirectoryNode("e", "/src/e"),
                            }),
                        })
                    }));

            Assert.Equal(expectedDirectory, actual?.WorkingDirectoryFullPath);
        }

        [Fact]
        public void BuildDirectoryTree()
        {
            var repo = CreateRepository(
                commitSha: null,
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "c/x", "http://github.com/1", null),
                    CreateSubmodule("2", "e", "http://github.com/2", null),
                    CreateSubmodule("3", "a", "http://github.com/3", null),
                    CreateSubmodule("4", "a/a/a/a/", "http://github.com/4", null),
                    CreateSubmodule("5", "c", "http://github.com/5", null),
                    CreateSubmodule("6", "a/z", "http://github.com/6", null)));

            var root = GitOperations.BuildDirectoryTree(repo);

            string inspect(GitOperations.DirectoryNode node)
                => node.Name + (node.WorkingDirectoryFullPath != null ? $"!" : "") + "{" + string.Join(",", node.OrderedChildren.Select(inspect)) + "}";

            var expected = IsUnix ?
                "{/{usr{src!{a!{a{a{a!{}}},z!{}},c!{x!{}},e!{}}}}}" :
                "{C:{src!{a!{a{a{a!{}}},z!{}},c!{x!{}},e!{}}}}";

            Assert.Equal(expected, inspect(root));
        }

        [Fact]
        public void GetUntrackedFiles_ProjectInMainRepoIncludesFilesInSubmodules()
        {
            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("2", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")),
                ignore: CreateIgnore(_workingDir, new[] { "c.cs", "p/d.cs", "sub/1/x.cs" }));

            var subRoot1 = Path.Combine(_workingDir, "sub", "1");
            var subRoot2 = Path.Combine(_workingDir, "sub", "2");

            var subRepos = new Dictionary<string, GitRepository>()
            {
                { subRoot1, CreateRepository(workingDir: subRoot1, commitSha: null, ignore: CreateIgnore(subRoot1, new[] { "obj/a.cs" })) },
                { subRoot2, CreateRepository(workingDir: subRoot2, commitSha: null, ignore: CreateIgnore(subRoot2, new[] { "obj/b.cs" })) },
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
                projectDirectory: Path.Combine(_workingDir, "p"),
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
            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("2", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")),
                ignore: CreateIgnore(_workingDir, new[] { "c.cs", "sub/1/x.cs" }));

            var subRoot1 = Path.Combine(s_root, "sub", "1");
            var subRoot2 = Path.Combine(s_root, "sub", "2");

            var subRepos = new Dictionary<string, GitRepository>()
            {
                { subRoot1, CreateRepository(subRoot1, commitSha: null, ignore: CreateIgnore(subRoot1, new[] { "obj/a.cs" })) },
                { subRoot2, CreateRepository(subRoot2, commitSha: null, ignore: CreateIgnore(subRoot2, new[] { "obj/b.cs" })) },
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
