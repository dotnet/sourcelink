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
            var repositoryUrl = sourceRoot.GetMetadata("RepositoryUrl");
            var sourceLinkUrl = sourceRoot.GetMetadata("SourceLinkUrl");

            return $"'{sourceRoot.ItemSpec}'" +
              (string.IsNullOrEmpty(sourceControl) ? "" : $" SourceControl='{sourceControl}'") +
              (string.IsNullOrEmpty(revisionId) ? "" : $" RevisionId='{revisionId}'") +
              (string.IsNullOrEmpty(nestedRoot) ? "" : $" NestedRoot='{nestedRoot}'") +
              (string.IsNullOrEmpty(containingRoot) ? "" : $" ContainingRoot='{containingRoot}'") +
              (string.IsNullOrEmpty(repositoryUrl) ? "" : $" RepositoryUrl='{repositoryUrl}'") +
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
        [InlineData(@"C:org/repo", @"https://c/org/repo")]
        [InlineData(@"/xyz/src", @"file:///xyz/src")]
        [InlineData(@"\path\a\b", @"file:///path/a/b")]
        [InlineData(@"relative/./path", @"file:///usr/src/a/b/relative/path")]
        [InlineData(@"../relative/path", @"file:///usr/src/a/relative/path")]
        [InlineData(@"../relative/path?a=b", @"file:///usr/src/a/relative/path%3Fa=b")]
        [InlineData(@"../relative/path*<>|\0%00", @"file:///usr/src/a/relative/path*<>|\0%00")]
        [InlineData(@"../../../../relative/path", @"file:///relative/path")]
        [InlineData(@"../.://../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"../.:./../../relative/path", "file:///usr/src/relative/path")]
        [InlineData(@".:/../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"..:/../../relative/path", "file:///usr/src/a/relative/path")]
        [InlineData(@"@:org/repo", @"file:///usr/src/a/b/@:org/repo")]
        public void GetRepositoryUrl_Unix(string originUrl, string expectedUrl)
        {
            var repo = new TestRepository(workingDir: "/usr/src/a/b", commitSha: "1111111111111111111111111111111111111111",
                remotes: new[] { new TestRemote("origin", originUrl) });

            Assert.Equal(expectedUrl, GitOperations.GetRepositoryUrl(repo));
        }

        [Theory]
        [InlineData("abc:org/repo", "https://abc/org/repo")]
        [InlineData("ABC:ORG/REPO/X/Y", "https://abc/ORG/REPO/X/Y")]
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
                $@"'{s_root}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{s_root}{s}' RepositoryUrl='http://1.com/'",
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}' RepositoryUrl='http://2.com/'",
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
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}' RepositoryUrl='http://2.com/'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] { "SubmoduleWithoutCommit_SourceLink: 1" }, warnings.Select(InspectDiagnostic));
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

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'C:\src\' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'C:\src\sub\1\' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='C:\src\' RepositoryUrl='file:///C:/src/a/b'",
                $@"'C:\src\sub\2\' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='C:\src\' RepositoryUrl='file:///C:/a'",
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

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'/src/' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'/src/sub/1/' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='/src/' RepositoryUrl='file:///src/a/b'",
                $@"'/src/sub/2/' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='/src/' RepositoryUrl='file:///a'",
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

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{s_root}{s}sub{s}3{s}' SourceControl='git' RevisionId='3333333333333333333333333333333333333333' NestedRoot='sub/3/' ContainingRoot='{s_root}{s}' RepositoryUrl='http://3.com/'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] 
            {
                "InvalidSubmoduleUrl_SourceLink: 1,http:///",
                "InvalidSubmodulePath_SourceLink: 2,sub/\0*<>|:"
            }, warnings.Select(InspectDiagnostic));
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

            Assert.Equal(@"{C:{src!{a!{a{a{a!{}}},z!{}},c!{x!{}},e!{}}}}", inspect(root));
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

            var actual = repo.GetUntrackedFiles(
                new[]
                {
                    new MockItem(@"c.cs"),                // not ignored
                    new MockItem(@"..\sub\1\x.cs"),       // ignored in the main repository, but not in the submodule (which has a priority)
                    new MockItem(@"../sub/2/obj/b.cs"),   // ignored in submodule #2
                    new MockItem(@"d.cs"),                // not ignored
                    new MockItem(@"..\..\w.cs"),          // outside of repo
                    new MockItem(@"D:\w.cs"),             // outside of repo
                },
                projectDirectory: Path.Combine(s_root, "p"),
                root => subRepos[root]);

            AssertEx.Equal(new[] 
            {
                MockItem.AdjustSeparators("../sub/2/obj/b.cs"),
                MockItem.AdjustSeparators("d.cs"),
                MockItem.AdjustSeparators(@"..\..\w.cs"),
                MockItem.AdjustSeparators(@"D:\w.cs")
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

            var actual = repo.GetUntrackedFiles(
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
