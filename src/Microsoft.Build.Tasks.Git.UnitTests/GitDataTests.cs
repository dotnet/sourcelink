// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitDataTests
    {
        private static readonly char s = Path.DirectorySeparatorChar;

        [Fact]
        public void MinimalGitData()
        {
            using var temp = new TempRoot();
            var repoDir = temp.CreateDirectory();

            var gitDir = repoDir.CreateDirectory(".git");
            gitDir.CreateFile("HEAD").WriteAllText("1111111111111111111111111111111111111111");
            gitDir.CreateFile("config").WriteAllText(@"
[remote ""origin""]
url=http://github.com/test-org/test-repo
[submodule ""my submodule""]
url=https://github.com/test-org/test-sub
");
            gitDir.CreateDirectory("objects");
            gitDir.CreateDirectory("refs");
            repoDir.CreateFile(".gitignore").WriteAllText("ignore_this_*");

            // submodule:
            var gitModules = repoDir.CreateFile(".gitmodules").WriteAllText(@"
[submodule ""my submodule""]
  path = sub
  url = xyz
");
            
            var subDir = repoDir.CreateDirectory("sub");
            subDir.CreateFile(".git").WriteAllText("gitdir: ../.git/modules/sub");
            subDir.CreateFile(".gitignore").WriteAllText("ignore_in_submodule_*");

            var gitDirSub = gitDir.CreateDirectory("modules").CreateDirectory("sub");
            gitDirSub.CreateFile("HEAD").WriteAllText("2222222222222222222222222222222222222222");
            gitDirSub.CreateDirectory("objects");
            gitDirSub.CreateDirectory("refs");

            var repository = GitRepository.OpenRepository(repoDir.Path, GitEnvironment.Empty)!;

            Assert.Equal("http://github.com/test-org/test-repo", GitOperations.GetRepositoryUrl(repository, remoteName: null));
            Assert.Equal("1111111111111111111111111111111111111111", repository.GetHeadCommitSha());

            var warnings = new List<(string, object?[])>();
            var sourceRoots = GitOperations.GetSourceRoots(repository, remoteName: null, warnOnMissingCommitOrUnsupportedUri: true, (message, args) => warnings.Add((message, args)));
            AssertEx.Equal(new[]
            {
                $@"'{repoDir.Path}{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' ScmRepositoryUrl='http://github.com/test-org/test-repo'",
                $@"'{repoDir.Path}{s}sub{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/' ContainingRoot='{repoDir.Path}{s}' ScmRepositoryUrl='https://github.com/test-org/test-sub'",
            }, sourceRoots.Select(TestUtilities.InspectSourceRoot));

            AssertEx.Equal(new string[0], warnings.Select(TestUtilities.InspectDiagnostic));

            var files = new[] 
            {
                new MockItem(@"ignore_this_a"),
                new MockItem(@"b"),
                new MockItem(@"ignore_this_c"),
                new MockItem(@"sub\ignore_in_submodule_d"),
            };

            var untrackedFiles = GitOperations.GetUntrackedFiles(repository, files, repoDir.Path);

            AssertEx.Equal(new[]
            {
                "ignore_this_a",
                "ignore_this_c",
                MockItem.AdjustSeparators(@"sub\ignore_in_submodule_d"),
            }, untrackedFiles.Select(item => item.ItemSpec));
        }
    }
}
