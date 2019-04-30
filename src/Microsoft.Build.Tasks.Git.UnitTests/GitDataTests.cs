// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using LibGit2Sharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitDataTests
    {
        public readonly TempRoot Temp = new TempRoot();
        private static readonly char s = Path.DirectorySeparatorChar;

        [Fact]
        public void MinimalGitData()
        {
            var repoDir = Temp.CreateDirectory();

            var gitDir = repoDir.CreateDirectory(".git");
            gitDir.CreateFile("HEAD").WriteAllText("1111111111111111111111111111111111111111");
            gitDir.CreateFile("config").WriteAllText(@"[remote ""origin""]url=""http://github.com/test-org/test-repo""");
            gitDir.CreateDirectory("objects");
            gitDir.CreateDirectory("refs");
            repoDir.CreateFile(".gitignore").WriteAllText("ignore_this_*");

            // submodule:
            var gitModules = repoDir.CreateFile(".gitmodules").WriteAllText(@"
[submodule ""my submodule""]
  path = sub
  url = https://github.com/test-org/test-sub
");
            
            var subDir = repoDir.CreateDirectory("sub");
            subDir.CreateFile(".git").WriteAllText("gitdir: ../.git/modules/sub");
            subDir.CreateFile(".gitignore").WriteAllText("ignore_in_submodule_*");

            var gitDirSub = gitDir.CreateDirectory("modules").CreateDirectory("sub");
            gitDirSub.CreateFile("HEAD").WriteAllText("2222222222222222222222222222222222222222");
            gitDirSub.CreateDirectory("objects");
            gitDirSub.CreateDirectory("refs");

            var repository = new Repository(gitDir.Path);

            Assert.Equal("http://github.com/test-org/test-repo", GitOperations.GetRepositoryUrl(repository));
            Assert.Equal("1111111111111111111111111111111111111111", GitOperations.GetRevisionId(repository));

            var warnings = new List<(string, object[])>();
            var sourceRoots = GitOperations.GetSourceRoots(repository, (message, args) => warnings.Add((message, args)), File.Exists);
            AssertEx.Equal(new[]
            {
                $@"'{repoDir.Path}{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' ScmRepositoryUrl='http://github.com/test-org/test-repo'",
                $@"'{repoDir.Path}{s}sub{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/' ContainingRoot='{repoDir.Path}{s}' ScmRepositoryUrl='https://github.com/test-org/test-sub'",
            }, sourceRoots.Select(GitOperationsTests.InspectSourceRoot));

            AssertEx.Equal(new string[0], warnings.Select(GitOperationsTests.InspectDiagnostic));

            var files = new[] 
            {
                new MockItem(@"ignore_this_a"),
                new MockItem(@"b"),
                new MockItem(@"ignore_this_c"),
                new MockItem(@"sub\ignore_in_submodule_d"),
            };

            var untrackedFiles = GitOperations.GetUntrackedFiles(repository, files, repoDir.Path, path => new Repository(path));

            AssertEx.Equal(new[]
            {
                "ignore_this_a",
                "ignore_this_c",
                MockItem.AdjustSeparators(@"sub\ignore_in_submodule_d"),
            }, untrackedFiles.Select(item => item.ItemSpec));
        }
    }
}
