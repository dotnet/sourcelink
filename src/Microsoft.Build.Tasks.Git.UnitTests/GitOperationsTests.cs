﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    using static KeyValuePairUtils;

    public class GitOperationsTests
    {
        private static readonly bool IsUnix = Path.DirectorySeparatorChar == '/';
        private static readonly char s = Path.DirectorySeparatorChar;
        private static readonly string s_root = (s == '/') ? "/usr/src" : @"C:\src";

        private readonly string _workingDir = s_root;

        private GitRepository CreateRepository(
            string? workingDir = null,
            GitConfig? config = null,
            string? commitSha = null,
            ImmutableArray<GitSubmodule> submodules = default,
            GitIgnore? ignore = null)
        {
            workingDir ??= _workingDir;
            var gitDir = Path.Combine(workingDir, ".git");
            return new GitRepository(
                GitEnvironment.Empty,
                config ?? GitConfig.Empty,
                gitDir, 
                gitDir,
                workingDir,
                submodules.IsDefault ? ImmutableArray<GitSubmodule>.Empty : submodules,
                submoduleDiagnostics: ImmutableArray<string>.Empty,
                ignore ?? new GitIgnore(root: null, workingDir, ignoreCase: false),
                commitSha);
        }

        private GitSubmodule CreateSubmodule(string name, string relativePath, string url, string? headCommitSha, string? containingRepositoryWorkingDir = null)
            => new GitSubmodule(name, relativePath, Path.GetFullPath(Path.Combine(containingRepositoryWorkingDir ?? _workingDir, relativePath)), url, headCommitSha);

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
                variables.Select(v => KVP(CreateVariableName(v.Name), ImmutableArray.Create(v.Value)))));

        private static GitConfig CreateConfig(params (string Name, string[] Values)[] variables)
            => new GitConfig(ImmutableDictionary.CreateRange(
                variables.Select(v => KVP(CreateVariableName(v.Name), ImmutableArray.CreateRange(v.Values)))));

        [Fact]
        public void GetRepositoryUrl_NoRemotes()
        {
            var repo = CreateRepository();
            var warnings = new List<(string, object?[])>();
            Assert.Null(GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[] { string.Format(Resources.RepositoryHasNoRemote, repo.WorkingDirectory) }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_Origin()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.origin.url", "http://github.com/origin")));

            var warnings = new List<(string, object?[])>();

            Assert.Equal("http://github.com/origin", GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetRepositoryUrl_NoOrigin()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.def.url", "http://github.com/def")));

            var warnings = new List<(string, object?[])>();

            Assert.Equal("http://github.com/abc", GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetRepositoryUrl_Specified()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.origin.url", "http://github.com/origin")));

            var warnings = new List<(string, object?[])>();

            Assert.Equal("http://github.com/abc",
                GitOperations.GetRepositoryUrl(repo, remoteName: "abc",
                    logWarning: (message, args) => warnings.Add((message, args))));

            Assert.Empty(warnings);
        }

        [Fact]
        public void GetRepositoryUrl_SpecifiedNotFound_OriginFallback()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.origin.url", "http://github.com/origin")));

            var warnings = new List<(string, object?[])>();

            Assert.Equal("http://github.com/origin", 
                GitOperations.GetRepositoryUrl(repo, remoteName: "myremote",
                    logWarning: (message, args) => warnings.Add((message, args))));

            AssertEx.Equal(new[]
            {
                string.Format(Resources.RepositoryDoesNotHaveSpecifiedRemote, repo.WorkingDirectory, "myremote", "origin")
            }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_SpecifiedNotFound_FirstFallback()
        {
            var repo = CreateRepository(config: CreateConfig(
                ("remote.abc.url", "http://github.com/abc"),
                ("remote.def.url", "http://github.com/def")));

            var warnings = new List<(string, object?[])>();

            Assert.Equal("http://github.com/abc",
                GitOperations.GetRepositoryUrl(repo, remoteName: "myremote",
                    logWarning: (message, args) => warnings.Add((message, args))));

            AssertEx.Equal(new[]
            {
                string.Format(Resources.RepositoryDoesNotHaveSpecifiedRemote, repo.WorkingDirectory, "myremote", "abc")
            }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_BadUrl()
        {
            var repo = CreateRepository(config: CreateConfig(("remote.origin.url", "http://?")));

            var warnings = new List<(string, object?[])>();
            Assert.Null(GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
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
                KVP(new GitVariableName("remote", "origin", "url"), ImmutableArray.Create("http://?")),
                KVP(new GitVariableName("url", "git@github.com:org/repo", "insteadOf"), ImmutableArray.Create("http://?"))
            })));

            var warnings = new List<(string, object?[])>();
            Assert.Equal("ssh://git@github.com/org/repo", GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
            Assert.Empty(warnings);
        }

        /// <summary>
        /// Test scenario where a local repository is cloned from another local repository that was cloned from a remote repository.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetRepositoryUrl_Local(bool useFileUrl)
        {
            using var temp = new TempRoot();

            var dir = temp.CreateDirectory();
            var mainWorkingDir = dir.CreateDirectory("x \u1234");
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");
            mainGitDir.CreateFile("config").WriteAllText(@"[remote ""origin""] url = http://github.com/repo");

            var repo = CreateRepository(config: new GitConfig(ImmutableDictionary.CreateRange(new[]
            {
                KVP(new GitVariableName("remote", "origin", "url"),
                    ImmutableArray.Create(useFileUrl ? new Uri(mainWorkingDir.Path).AbsolutePath : mainWorkingDir.Path)),
            })));

            var warnings = new List<(string, object?[])>();
            Assert.Equal("http://github.com/repo", GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
            Assert.Empty(warnings);
        }

        /// <summary>
        /// Test scenario where a local repository is cloned from another local repository that was cloned from a remote repository.
        /// With custom remote name.
        /// </summary>
        [Fact]
        public void GetRepositoryUrl_Local_CustomRemoteName()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");
            mainGitDir.CreateFile("config").WriteAllText(@"[remote ""origin""] url = http://github.com/repo");

            var repo = CreateRepository(config: new GitConfig(ImmutableDictionary.CreateRange(new[]
            {
                KVP(new GitVariableName("remote", "origin", "url"), ImmutableArray.Create(mainWorkingDir.Path)),
            })));

            var warnings = new List<(string, object?[])>();
            Assert.Equal("http://github.com/repo", GitOperations.GetRepositoryUrl(repo, remoteName: "myremote", logWarning: (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[] { string.Format(Resources.RepositoryDoesNotHaveSpecifiedRemote, repo.WorkingDirectory, "myremote", "origin") }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        /// <summary>
        /// Test scenario where a local repository is cloned from another local repository that does not have remote URL specified.
        /// </summary>
        [Fact]
        public void GetRepositoryUrl_Local_NoRemoteOriginUrl()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");

            var repo = CreateRepository(config: new GitConfig(ImmutableDictionary.CreateRange(new[]
            {
                KVP(new GitVariableName("remote", "origin", "url"), ImmutableArray.Create(mainWorkingDir.Path)),
            })));

            var warnings = new List<(string, object?[])>();
            Assert.Equal(new Uri(mainWorkingDir.Path).AbsoluteUri, GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[] { string.Format(Resources.RepositoryHasNoRemote, mainWorkingDir.Path) }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        /// <summary>
        /// Test scenario where a local repository is cloned from another local repository that does not have a working directory.
        /// </summary>
        [Fact]
        public void GetRepositoryUrl_Local_NoWorkingDirectory()
        {
            using var temp = new TempRoot();

            var dir = temp.CreateDirectory();

            var gitDir1 = dir.CreateDirectory("1");
            gitDir1.CreateFile("HEAD");
            gitDir1.CreateFile("config").WriteAllText(@"[remote ""origin""] url = http://github.com/repo");

            var gitDir2 = dir.CreateDirectory("2");
            gitDir2.CreateFile("HEAD");
            gitDir2.CreateFile("config").WriteAllText(@"[remote ""origin""] url = ../1");

            var repo = CreateRepository(config: new GitConfig(ImmutableDictionary.CreateRange(new[]
            {
                KVP(new GitVariableName("remote", "origin", "url"), ImmutableArray.Create(gitDir2.Path)),
            })));

            var warnings = new List<(string, object?[])>();
            Assert.Null(GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[] { string.Format(Resources.UnableToLocateRepository, gitDir2.Path) }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        /// <summary>
        /// Test scenario where a local repository is cloned from another local repository that was cloned from a remote repository.
        /// </summary>
        [Fact]
        public void GetRepositoryUrl_Local_BadRepo()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");

            var repo = CreateRepository(config: new GitConfig(ImmutableDictionary.CreateRange(new[]
            {
                KVP(new GitVariableName("remote", "origin", "url"), ImmutableArray.Create(mainWorkingDir.Path)),
            })));

            var warnings = new List<(string, object?[])>();
            Assert.Equal(new Uri(mainWorkingDir.Path).AbsoluteUri, GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[] { string.Format(Resources.RepositoryHasNoRemote, mainWorkingDir.Path) }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetRepositoryUrl_LocalRecursion()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");
            mainGitDir.CreateFile("config").WriteAllText($@"[remote ""origin""] url = {mainWorkingDir.Path.Replace('\\', '/')}");

            var repo = CreateRepository(config: new GitConfig(ImmutableDictionary.CreateRange(new[]
            {
                KVP(new GitVariableName("remote", "origin", "url"),
                    ImmutableArray.Create(mainWorkingDir.Path)),
            })));

            var warnings = new List<(string, object?[])>();
            Assert.Equal(new Uri(mainWorkingDir.Path).AbsoluteUri, GitOperations.GetRepositoryUrl(repo, remoteName: null, logWarning: (message, args) => warnings.Add((message, args))));
            AssertEx.Equal(new[] { string.Format(Resources.RepositoryUrlEvaluationExceededMaximumAllowedDepth, "10") }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Theory]
        [InlineData("https://github.com/org/repo")]
        [InlineData("http://github.com/org/repo")]
        [InlineData("http://github.com:102/org/repo")]
        [InlineData("ssh://user@github.com/org/repo")]
        [InlineData("abc://user@github.com/org/repo")]
        public void NormalizeUrl_PlatformAgnostic1(string url)
        {
            Assert.Equal(url, GitOperations.NormalizeUrl(url, s_root)?.AbsoluteUri);
        }

        [Theory]
        [InlineData("http://?", null)]
        [InlineData("https://github.com/org/repo/./.", "https://github.com/org/repo/")]
        [InlineData("http://github.com/org/\u1234", "http://github.com/org/%E1%88%B4")]
        [InlineData("ssh://github.com/org/../repo", "ssh://github.com/repo")]
        [InlineData("ssh://github.com/%32/repo", "ssh://github.com/2/repo")]
        [InlineData("ssh://github.com/%3F/repo", "ssh://github.com/%3F/repo")]
        public void NormalizeUrl_PlatformAgnostic2(string url, string expectedUrl)
        {
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, s_root)?.AbsoluteUri);
        }

        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData(@"C:", "file:///C:/")]
        [InlineData(@"C:\", "file:///C:/")]
        [InlineData(@"C:x", null)]
        [InlineData(@"C:x\y\..\z", null)]
        [InlineData(@"C:org/repo", null)]
        [InlineData(@"D:\src", "file:///D:/src")]
        [InlineData(@"D:\a%20b", "file:///D:/a%2520b")]
        [InlineData(@"\\", null)]
        [InlineData(@"\\server", "file://server/")]
        [InlineData(@"\\server\dir", "file://server/dir")]
        [InlineData(@"relative/./path", "file:///C:/src/a/b/relative/path")]
        [InlineData(@"%20", "file:///C:/src/a/b/%2520")]
        [InlineData(@"..\%20", "file:///C:/src/a/%2520")]
        [InlineData(@"../relative/path", "file:///C:/src/a/relative/path")]
        [InlineData(@"..\relative\path", "file:///C:/src/a/relative/path")]
        [InlineData(@"../relative/path?a=b", "file:///C:/src/a/relative/path%3Fa=b")]
        [InlineData(@"../relative/path*<>|\0%00", "file:///C:/src/a/relative/path*%3C%3E%7C/0%2500")]
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
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, @"C:\src\a\b")?.AbsoluteUri);
        }

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(@"C:org/repo", @"ssh://c/org/repo")]
        [InlineData(@"/xyz/src", @"file:///xyz/src")]
        // [InlineData(@"/a%20b", @"file:///a%2520b")] // https://github.com/dotnet/sourcelink/issues/439
        [InlineData(@"\path\a\b", @"file:///path/a/b")]
        [InlineData(@"relative/./path", @"file:///usr/src/a/b/relative/path")]
        [InlineData(@"%20", "file:///usr/src/a/b/%2520")]
        [InlineData(@"../%20", "file:///usr/src/a/%2520")]
        [InlineData(@"../relative/path", @"file:///usr/src/a/relative/path")]
        [InlineData(@"../relative/path?a=b", @"file:///usr/src/a/relative/path%3Fa=b")]
        // [InlineData(@"../relative/path*<>|\0%00", @"file:///usr/src/a/relative/path*%3C%3E%7C/0%2500")] // https://github.com/dotnet/sourcelink/issues/439
        [InlineData(@"../../../../relative/path", @"file:///relative/path")]
        [InlineData(@"../.://../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"../.:./../../relative/path", "ssh://../relative/path")]
        [InlineData(@".:/../../relative/path", "ssh://./relative/path")]
        [InlineData(@"..:/../../relative/path", "ssh://../relative/path")]
        [InlineData(@"@:org/repo", @"file:///usr/src/a/b/@:org/repo")]
        public void NormalizeUrl_Unix(string url, string expectedUrl)
        {
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, "/usr/src/a/b")?.AbsoluteUri);
        }

        [Theory]
        [InlineData("abc:org/repo", "ssh://abc/org/repo")]
        [InlineData("abc:org/x%20y", "ssh://abc/org/x%20y")]
        [InlineData("ABC:ORG/REPO/X/Y", "ssh://abc/ORG/REPO/X/Y")]
        [InlineData("github.com:org/repo", "ssh://github.com/org/repo")]
        [InlineData("git@github.com:org/repo", "ssh://git@github.com/org/repo")]
        [InlineData("@github.com:org/repo", "ssh://@github.com/org/repo")]
        [InlineData("http:x//y", "ssh://http/x//y")]
        public void GetRepositoryUrl_ScpSyntax(string url, string expectedUrl)
        {
            Assert.Equal(expectedUrl, GitOperations.NormalizeUrl(url, s_root)?.AbsoluteUri);
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

            var warnings = new List<(string, object?[])>();
            var items = GitOperations.GetSourceRoots(repo, remoteName: null, (message, args) => warnings.Add((message, args)));

            Assert.Empty(items);
            AssertEx.Equal(new[] { Resources.RepositoryHasNoCommit }, warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommitsWithSubmodules()
        {
            var repo = CreateRepository(
                commitSha: null,
                config: CreateConfig(
                    ("url.ssh://.insteadOf", "http://"),
                    ("submodule.sub1.url", "http://github.com/sub-1"),
                    ("submodule.sub3.url", "https://github.com/sub-3"),
                    ("submodule.sub4.url", "https:///"),
                    ("submodule.sub6.url", "https://github.com/sub-6")),
                submodules: ImmutableArray.Create(
                    CreateSubmodule("sub1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    CreateSubmodule("sub2", "sub/2", "http://2.com", "2222222222222222222222222222222222222222"),
                    CreateSubmodule("sub3", "sub/3", "http://3.com", "3333333333333333333333333333333333333333"),
                    CreateSubmodule("sub4", "sub/4", "http://4.com", "4444444444444444444444444444444444444444"),
                    CreateSubmodule("sub5", "sub/5", "http:///", "5555555555555555555555555555555555555555"),
                    CreateSubmodule("sub6", "sub/6", "", "6666666666666666666666666666666666666666")));

            var warnings = new List<(string, object?[])>();
            var items = GitOperations.GetSourceRoots(repo, remoteName: null, (message, args) => warnings.Add((message, args)));

            // Module without a configuration entry is not initialized.
            // URLs listed in .submodules are ignored (they are used by git submodule initialize to generate URLs stored in config).
            AssertEx.Equal(new[]
            {
                $@"'{_workingDir}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{_workingDir}{s}' ScmRepositoryUrl='ssh://github.com/sub-1'",
                $@"'{_workingDir}{s}sub{s}3{s}' SourceControl='git' RevisionId='3333333333333333333333333333333333333333' NestedRoot='sub/3/' ContainingRoot='{_workingDir}{s}' ScmRepositoryUrl='https://github.com/sub-3'",
                $@"'{_workingDir}{s}sub{s}6{s}' SourceControl='git' RevisionId='6666666666666666666666666666666666666666' NestedRoot='sub/6/' ContainingRoot='{_workingDir}{s}' ScmRepositoryUrl='https://github.com/sub-6'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            AssertEx.Equal(new[]
            {
                Resources.RepositoryHasNoCommit,
                string.Format(Resources.SourceCodeWontBeAvailableViaSourceLink, string.Format(Resources.InvalidSubmoduleUrl, "sub4", "https:///"))
            }, warnings.Select(TestUtilities.InspectDiagnostic)); ;
        }

        [Fact]
        public void GetSourceRoots_RepoWithCommitsWithSubmodules()
        {
            var repo = CreateRepository(
                commitSha: "0000000000000000000000000000000000000000",
                config: CreateConfig(
                    ("submodule.1.url", "http://github.com/sub-1"),
                    ("submodule.2.url", "http://github.com/sub-2")),
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "http://1.com", headCommitSha: null),
                    CreateSubmodule("2", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")));

            var warnings = new List<(string, object?[])>();
            var items = GitOperations.GetSourceRoots(repo, remoteName: null, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{_workingDir}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{_workingDir}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{_workingDir}{s}' ScmRepositoryUrl='http://github.com/sub-2'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            AssertEx.Equal(new[] { string.Format(Resources.SourceCodeWontBeAvailableViaSourceLink, string.Format(Resources.SubmoduleWithoutCommit, "1")) }, 
                warnings.Select(TestUtilities.InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RelativeSubmodulePath()
        {
            using var temp = new TempRoot();

            var dir = temp.CreateDirectory();

            // TODO: test unicode chars on Linux as well https://github.com/dotnet/corefx/issues/34227
            var repoDir = dir.CreateDirectory("%25@" + (s == '\\' ? "噸" : ""));

            var repo1WorkingDir = dir.CreateDirectory("1");
            var repo1GitDir = repo1WorkingDir.CreateDirectory(".git");
            repo1GitDir.CreateFile("HEAD");
            repo1GitDir.CreateFile("config").WriteAllText(@"[remote ""origin""] url = http://github.com/repo1");

            var repo = CreateRepository(
                workingDir: repoDir.Path,
                commitSha: "0000000000000000000000000000000000000000",
                config: CreateConfig(
                    ("submodule.1.url", "../1")),
                submodules: ImmutableArray.Create(
                    CreateSubmodule("1", "sub/1", "---", "1111111111111111111111111111111111111111", containingRepositoryWorkingDir: repoDir.Path)));

            var warnings = new List<(string, object?[])>();
            var items = GitOperations.GetSourceRoots(repo, remoteName: null, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{repoDir.Path}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{repoDir.Path}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{repoDir.Path}{s}' ScmRepositoryUrl='http://github.com/repo1'",
            }, items.Select(TestUtilities.InspectSourceRoot));

            Assert.Empty(warnings);
        }
		
		private static GitOperations.DirectoryNode CreateNode(string name, string? submoduleWorkingDirectory, List<GitOperations.DirectoryNode>? children = null)
            => new GitOperations.DirectoryNode(name, children ?? new List<GitOperations.DirectoryNode>())
            {
                Matcher = (submoduleWorkingDirectory != null) ? new Lazy<GitIgnore.Matcher?>(() =>
                    new GitIgnore.Matcher(new GitIgnore(null, submoduleWorkingDirectory, ignoreCase: false))) : null
            };

        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData(@"C:\", null)]
        [InlineData(@"C:\x", null)]
        [InlineData(@"C:\x\y\z", null)]
        [InlineData(@"C:\src", null)]
        [InlineData(@"C:\src\", null)]
        [InlineData(@"C:\src\a\x.cs", "C:/src/a/")]
        [InlineData(@"C:\src\b\x.cs", "C:/src/")]
        [InlineData(@"C:\src\ab\x.cs", "C:/src/")]
        [InlineData(@"C:\src\a\b\x.cs", "C:/src/a/")]
        [InlineData(@"C:\src\c\x.cs", "C:/src/c/")]
        [InlineData(@"C:\src\c", "C:/src/")]
        [InlineData(@"C:\src\c\", "C:/src/")]
        [InlineData(@"C:\src\c.cs", "C:/src/")]
        [InlineData(@"C:\src\c\x\x.cs", "C:/src/c/x/")]
        [InlineData(@"C:\src\d\x.cs", "C:/src/")]
        [InlineData(@"C:\src\e\x.cs", "C:/src/e/")]
        public void GetContainingRepository_Windows(string path, string expectedDirectory)
        {
            var actual = GitOperations.GetContainingRepositoryMatcher(path,
                CreateNode("", null,
                    new List<GitOperations.DirectoryNode>
                    {
                        CreateNode("C:", null, new List<GitOperations.DirectoryNode>
                        {
                            CreateNode("src", @"C:\src", new List<GitOperations.DirectoryNode>
                            {
                                CreateNode("a", @"C:\src\a"),
                                CreateNode("c", @"C:\src\c", new List<GitOperations.DirectoryNode>
                                {
                                    CreateNode("x", @"C:\src\c\x")
                                }),
                                CreateNode("e", @"C:\src\e")
                            }),
                        })
                    }));

            Assert.Equal(expectedDirectory, actual?.Ignore.WorkingDirectory);
        }

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData("/", null)]
        [InlineData("/x", null)]
        [InlineData("/x/y/z", null)]
        [InlineData("/src", null)]
        [InlineData("/src/", null)]
        [InlineData("/src/a/x.cs", "/src/a/")]
        [InlineData("/src/b/x.cs", "/src/")]
        [InlineData("/src/ab/x.cs", "/src/")]
        [InlineData("/src/a/b/x.cs", "/src/a/")]
        [InlineData("/src/c/x.cs", "/src/c/")]
        [InlineData("/src/c", "/src/")]
        [InlineData("/src/c/", "/src/")]
        [InlineData("/src/c.cs", "/src/")]
        [InlineData("/src/c/x/x.cs", "/src/c/x/")]
        [InlineData("/src/d/x.cs", "/src/")]
        [InlineData("/src/e/x.cs", "/src/e/")]
        public void GetContainingRepository_Unix(string path, string expectedDirectory)
        {
            var actual = GitOperations.GetContainingRepositoryMatcher(path,
                CreateNode("", null,
                    new List<GitOperations.DirectoryNode>
                    {
                        CreateNode("/", null, new List<GitOperations.DirectoryNode>
                        {
                            CreateNode("src", "/src", new List<GitOperations.DirectoryNode>
                            {
                                CreateNode("a", "/src/a"),
                                CreateNode("c", "/src/c", new List<GitOperations.DirectoryNode>
                                {
                                    CreateNode("x", "/src/c/x"),
                                }),
                                CreateNode("e", "/src/e"),
                            }),
                        })
                    }));

            Assert.Equal(expectedDirectory, actual?.Ignore.WorkingDirectory);
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

            var root = GitOperations.BuildDirectoryTree(repo, (e, d) => null);

            static string inspect(GitOperations.DirectoryNode node)
                => node.Name + (node.Matcher != null ? $"!" : "") + "{" + string.Join(",", node.OrderedChildren.Select(inspect)) + "}";

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
                (_, root) => subRepos[root]);

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
                (_, root) => subRepos[root]);

            AssertEx.Equal(new[]
            {
                MockItem.AdjustSeparators(@"obj\a.cs"),
                MockItem.AdjustSeparators(@"..\2\obj\b.cs"),
                MockItem.AdjustSeparators(@"..\..\c.cs")
            }, actual.Select(item => item.ItemSpec));
        }
    }
}
