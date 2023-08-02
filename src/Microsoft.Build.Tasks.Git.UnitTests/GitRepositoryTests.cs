// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitRepositoryTests
    {
        [Fact]
        public void TryFindRepository_Worktree()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainWorkingSubDir = mainWorkingDir.CreateDirectory("A");
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");

            var worktreeGitDir = temp.CreateDirectory();
            var worktreeGitSubDir = worktreeGitDir.CreateDirectory("B");
            var worktreeDir = temp.CreateDirectory();
            var worktreeSubDir = worktreeDir.CreateDirectory("C");
            var worktreeGitFile = worktreeDir.CreateFile(".git").WriteAllText("gitdir: " + worktreeGitDir + " \r\n\t\v");

            worktreeGitDir.CreateFile("HEAD");
            worktreeGitDir.CreateFile("commondir").WriteAllText(mainGitDir.Path + " \r\n\t\v");
            worktreeGitDir.CreateFile("gitdir").WriteAllText(worktreeGitFile.Path + " \r\n\t\v");

            // start under main repository directory:
            Assert.True(GitRepository.TryFindRepository(mainWorkingSubDir.Path, out var location));

            Assert.Equal(mainGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Equal(mainWorkingDir.Path, location.WorkingDirectory);

            // start at main git directory (git config works from this dir, but git status requires work dir):
            Assert.True(GitRepository.TryFindRepository(mainGitDir.Path, out location));

            Assert.Equal(mainGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Null(location.WorkingDirectory);

            // start under worktree directory:
            Assert.True(GitRepository.TryFindRepository(worktreeSubDir.Path, out location));

            Assert.Equal(worktreeGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Equal(worktreeDir.Path, location.WorkingDirectory);

            // start under worktree git directory (git config works from this dir, but git status requires work dir):
            Assert.True(GitRepository.TryFindRepository(worktreeGitSubDir.Path, out location));

            Assert.Equal(worktreeGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Null(location.WorkingDirectory);
        }

        [Fact]
        public void TryFindRepository_Worktree_Realistic()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainWorkingSubDir = mainWorkingDir.CreateDirectory("A");
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");

            var worktreesDir = mainGitDir.CreateDirectory("worktrees");
            var worktreeGitDir = worktreesDir.CreateDirectory("myworktree");
            var worktreeGitSubDir = worktreeGitDir.CreateDirectory("B");
            var worktreeDir = temp.CreateDirectory();
            var worktreeSubDir = worktreeDir.CreateDirectory("C");
            var worktreeGitFile = worktreeDir.CreateFile(".git").WriteAllText("gitdir: " + worktreeGitDir + " \r\n\t\v");

            worktreeGitDir.CreateFile("HEAD");
            worktreeGitDir.CreateFile("commondir").WriteAllText("../..\n");
            worktreeGitDir.CreateFile("gitdir").WriteAllText(worktreeGitFile.Path + " \r\n\t\v");

            // start under main repository directory:
            Assert.True(GitRepository.TryFindRepository(mainWorkingSubDir.Path, out var location));

            Assert.Equal(mainGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Equal(mainWorkingDir.Path, location.WorkingDirectory);

            // start at main git directory (git config works from this dir, but git status requires work dir):
            Assert.True(GitRepository.TryFindRepository(mainGitDir.Path, out location));

            Assert.Equal(mainGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Null(location.WorkingDirectory);

            var repository = GitRepository.OpenRepository(location, GitEnvironment.Empty);
            Assert.Equal(location.GitDirectory, repository.GitDirectory);
            Assert.Equal(location.CommonDirectory, repository.CommonDirectory);
            Assert.Null(repository.WorkingDirectory);

            // start under worktree directory:
            Assert.True(GitRepository.TryFindRepository(worktreeSubDir.Path, out location));
            Assert.Equal(worktreeGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Equal(worktreeDir.Path, location.WorkingDirectory);

            repository = GitRepository.OpenRepository(location, GitEnvironment.Empty);
            Assert.Equal(location.GitDirectory, repository.GitDirectory);
            Assert.Equal(location.WorkingDirectory, repository.WorkingDirectory);
            Assert.Equal(location.CommonDirectory, repository.CommonDirectory);

            // start under worktree git directory (git config works from this dir, but git status requires work dir):
            Assert.True(GitRepository.TryFindRepository(worktreeGitSubDir.Path, out location));

            Assert.Equal(worktreeGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Null(location.WorkingDirectory);

            repository = GitRepository.OpenRepository(location, GitEnvironment.Empty);
            Assert.Equal(location.GitDirectory, repository.GitDirectory);
            Assert.Equal(location.CommonDirectory, repository.CommonDirectory);
            Assert.Null(repository.WorkingDirectory);
        }

        [Fact]
        public void LocateRepository_Submodule()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");

            var submoduleGitDir = mainGitDir.CreateDirectory("modules").CreateDirectory("sub");

            var submoduleWorkDir = temp.CreateDirectory();
            submoduleWorkDir.CreateFile(".git").WriteAllText("gitdir: " + submoduleGitDir.Path);

            submoduleGitDir.CreateFile("HEAD");
            submoduleGitDir.CreateDirectory("objects");
            submoduleGitDir.CreateDirectory("refs");

            // start under submodule working directory:
            Assert.True(GitRepository.TryFindRepository(submoduleWorkDir.Path, out var location));

            Assert.Equal(submoduleGitDir.Path, location.GitDirectory);
            Assert.Equal(submoduleGitDir.Path, location.CommonDirectory);
            Assert.Equal(submoduleWorkDir.Path, location.WorkingDirectory);

            // start under submodule git directory:
            Assert.True(GitRepository.TryFindRepository(submoduleGitDir.Path, out location));

            Assert.Equal(submoduleGitDir.Path, location.GitDirectory);
            Assert.Equal(submoduleGitDir.Path, location.CommonDirectory);
            Assert.Null(location.WorkingDirectory);
        }

        [Fact]
        public void OpenRepository()
        {
            using var temp = new TempRoot();

            var homeDir = temp.CreateDirectory();

            var workingDir = temp.CreateDirectory();
            var gitDir = workingDir.CreateDirectory(".git");

            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master");
            gitDir.CreateDirectory("refs").CreateDirectory("heads").CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");
            gitDir.CreateDirectory("objects");

            gitDir.CreateFile("config").WriteAllText("[x]a = 1");

            var src = workingDir.CreateDirectory("src");

            var repository = GitRepository.OpenRepository(src.Path, new GitEnvironment(homeDir.Path))!;

            Assert.Equal(gitDir.Path, repository.CommonDirectory);
            Assert.Equal(gitDir.Path, repository.GitDirectory);
            Assert.Equal("1", repository.Config.GetVariableValue("x", "a"));
            Assert.Empty(repository.GetSubmodules());
            Assert.Equal("0000000000000000000000000000000000000000", repository.GetHeadCommitSha());
        }

        [Fact]
        public void OpenRepository_WorkingDirectorySpecifiedInConfig()
        {
            using var temp = new TempRoot();

            var homeDir = temp.CreateDirectory();

            var workingDir = temp.CreateDirectory();
            var workingDir2 = temp.CreateDirectory();
            var gitDir = workingDir.CreateDirectory(".git");

            gitDir.CreateFile("HEAD");
            gitDir.CreateFile("config").WriteAllText("[core]worktree = " + workingDir2.Path.Replace(@"\", @"\\"));

            Assert.True(GitRepository.TryFindRepository(gitDir.Path, out var location));
            Assert.Equal(gitDir.Path, location.CommonDirectory);
            Assert.Equal(gitDir.Path, location.GitDirectory);
            Assert.Null(location.WorkingDirectory);

            var repository = GitRepository.OpenRepository(location, GitEnvironment.Empty);
            Assert.Equal(gitDir.Path, repository.CommonDirectory);
            Assert.Equal(gitDir.Path, repository.GitDirectory);
            Assert.Equal(workingDir2.Path, repository.WorkingDirectory);
        }

        [Fact]
        public void OpenRepository_Version1_Extensions()
        {
            using var temp = new TempRoot();

            var workingDir = temp.CreateDirectory();
            var gitDir = workingDir.CreateDirectory(".git");

            gitDir.CreateFile("HEAD");
            gitDir.CreateFile("config").WriteAllText(@"
[core]
	repositoryformatversion = 1
[extensions]
    noop = 1
    preciousObjects = true
    partialClone = promisor_remote
    worktreeConfig = true
");

            Assert.True(GitRepository.TryFindRepository(gitDir.Path, out var location));
            Assert.Equal(gitDir.Path, location.CommonDirectory);
            Assert.Equal(gitDir.Path, location.GitDirectory);
            Assert.Null(location.WorkingDirectory);

            var repository = GitRepository.OpenRepository(location, GitEnvironment.Empty);
            Assert.Equal(gitDir.Path, repository.CommonDirectory);
            Assert.Equal(gitDir.Path, repository.GitDirectory);
            Assert.Null(repository.WorkingDirectory);
        }

        [Fact]
        public void OpenRepository_Version1_UnknownExtension()
        {
            using var temp = new TempRoot();

            var homeDir = temp.CreateDirectory();

            var workingDir = temp.CreateDirectory();
            var gitDir = workingDir.CreateDirectory(".git");

            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master");
            gitDir.CreateDirectory("refs").CreateDirectory("heads").CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");
            gitDir.CreateDirectory("objects");

            gitDir.CreateFile("config").WriteAllText(@"
[core]
	repositoryformatversion = 1
[extensions]
	newExtension = true");

            var src = workingDir.CreateDirectory("src");

            Assert.Throws<NotSupportedException>(() => GitRepository.OpenRepository(src.Path, new GitEnvironment(homeDir.Path)));
        }

        [Fact]
        public void OpenRepository_VersionNotSupported()
        {
            using var temp = new TempRoot();

            var homeDir = temp.CreateDirectory();

            var workingDir = temp.CreateDirectory();
            var gitDir = workingDir.CreateDirectory(".git");

            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master");
            gitDir.CreateDirectory("refs").CreateDirectory("heads").CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");
            gitDir.CreateDirectory("objects");

            gitDir.CreateFile("config").WriteAllText("[core]repositoryformatversion = 2");

            var src = workingDir.CreateDirectory("src");

            Assert.Throws<NotSupportedException>(() => GitRepository.OpenRepository(src.Path, new GitEnvironment(homeDir.Path)));
        }

        [Fact]
        public void OpenRepository_Worktree_GitdirFileMissing()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");

            var worktreesDir = mainGitDir.CreateDirectory("worktrees");
            var worktreeGitDir = worktreesDir.CreateDirectory("myworktree");
            var worktreeDir = temp.CreateDirectory();
            var worktreeGitFile = worktreeDir.CreateFile(".git").WriteAllText("gitdir: " + worktreeGitDir + " \r\n\t\v");

            worktreeGitDir.CreateFile("HEAD");
            worktreeGitDir.CreateFile("commondir").WriteAllText("../..\n");
            // gitdir file that links back to the worktree working directory is missing from worktreeGitDir

            Assert.True(GitRepository.TryFindRepository(worktreeDir.Path, out var location));
            Assert.Equal(worktreeGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Equal(worktreeDir.Path, location.WorkingDirectory);
            
            var repository = GitRepository.OpenRepository(location, GitEnvironment.Empty);
            Assert.Equal(repository.GitDirectory, location.GitDirectory);
            Assert.Equal(repository.CommonDirectory, location.CommonDirectory);
            Assert.Equal(repository.WorkingDirectory, location.WorkingDirectory);
        }

        /// <summary>
        /// The directory in gitdir file is ignored for the purposes of determining repository working directory.
        /// </summary>
        [Fact]
        public void OpenRepository_Worktree_GitdirFileDifferentPath()
        {
            using var temp = new TempRoot();

            var mainWorkingDir = temp.CreateDirectory();
            var mainGitDir = mainWorkingDir.CreateDirectory(".git");
            mainGitDir.CreateFile("HEAD");

            var worktreesDir = mainGitDir.CreateDirectory("worktrees");
            var worktreeGitDir = worktreesDir.CreateDirectory("myworktree");
            var worktreeDir = temp.CreateDirectory();
            var worktreeGitFile = worktreeDir.CreateFile(".git").WriteAllText("gitdir: " + worktreeGitDir + " \r\n\t\v");

            var worktreeDir2 = temp.CreateDirectory();
            var worktreeGitFile2 = worktreeDir2.CreateFile(".git").WriteAllText("gitdir: " + worktreeGitDir + " \r\n\t\v");

            worktreeGitDir.CreateFile("HEAD");
            worktreeGitDir.CreateFile("commondir").WriteAllText("../..\n");
            worktreeGitDir.CreateFile("gitdir").WriteAllText(worktreeGitFile2.Path + " \r\n\t\v");

            Assert.True(GitRepository.TryFindRepository(worktreeDir.Path, out var location));
            Assert.Equal(worktreeGitDir.Path, location.GitDirectory);
            Assert.Equal(mainGitDir.Path, location.CommonDirectory);
            Assert.Equal(worktreeDir.Path, location.WorkingDirectory);

            var repository = GitRepository.OpenRepository(location, GitEnvironment.Empty);
            Assert.Equal(repository.GitDirectory, location.GitDirectory);
            Assert.Equal(repository.CommonDirectory, location.CommonDirectory);
            
            // actual working dir is not affected:
            Assert.Equal(worktreeDir.Path, location.WorkingDirectory);
        }

        [Fact]
        public void Submodules()
        {
            using var temp = new TempRoot();

            var workingDir = temp.CreateDirectory();
            var gitDir = workingDir.CreateDirectory(".git");
            workingDir.CreateFile(".gitmodules").WriteAllText(@"
[submodule ""S1""]
	path = subs/s1
	url = http://github.com/test1
[submodule ""S2""]
	path = s2
	url = http://github.com/test2
[submodule ""S3""]
	path = s3
	url = ../repo2
[abc ""S3""]               # ignore other sections
	path = s3  
	url = ../repo2 
[submodule ""S2""]         # use the latest
	url = http://github.com/test3
[submodule ""S4""]         # ignore if path unspecified
	url = http://github.com/test3
[submodule ""S5""]         # ignore if url unspecified
	path = s4
");
            var repository = new GitRepository(GitEnvironment.Empty, GitConfig.Empty, gitDir.Path, gitDir.Path, workingDir.Path);

            var submodules = GitRepository.EnumerateSubmoduleConfig(repository.ReadSubmoduleConfig()!);
            AssertEx.Equal(new[]
            {
                "S1: 'subs/s1' 'http://github.com/test1'",
                "S2: 's2' 'http://github.com/test3'",
                "S3: 's3' '../repo2'",
            }, submodules.Where(s => s.Url != null && s.Path != null).Select(s => $"{s.Name}: '{s.Path}' '{s.Url}'"));
        }

        [Fact]
        public void Submodules_Errors()
        {
            using var temp = new TempRoot();

            var workingDir = temp.CreateDirectory();
            var gitDir = workingDir.CreateDirectory(".git");
            gitDir.CreateDirectory("modules").CreateDirectory("sub10").CreateDirectory("commondir");

            workingDir.CreateDirectory("sub6").CreateDirectory(".git");
            workingDir.CreateDirectory("sub7").CreateFile(".git").WriteAllText("xyz");
            workingDir.CreateDirectory("sub8").CreateFile(".git").WriteAllText("gitdir: \0<>");
            workingDir.CreateDirectory("sub9").CreateFile(".git").WriteAllText("gitdir: ../.git/modules/sub9\r\n");
            workingDir.CreateDirectory("sub10").CreateFile(".git").WriteAllText("gitdir: ../.git/modules/sub10");
            workingDir.CreateDirectory("sub11").CreateFile(".git").WriteAllText("gitdir: ../.git/modules/sub11 \t\v\r\n");

            workingDir.CreateFile(".gitmodules").WriteAllText(@"
[submodule ""S1""]             # whitespace-only path
  path = ""  ""
  url = http://github.com

[submodule ""S2""]             # empty path
  path =                  
  url = http://github.com

[submodule ""S3""]             # invalid url
  path = sub3
  url = http://?

[submodule ""S4""]             # whitespace-only url
  path = sub4
  url = ""   ""             

[submodule ""S5""]             # path does not exist
  path = sub5
  url = http://github.com

[submodule ""S6""]             # sub6/.git is a directory, but should be a file
  path = sub6
  url = http://github.com

[submodule ""S7""]             # sub7/.git has invalid format
  path = sub7
  url = http://github.com

[submodule ""S8""]             # sub8/.git contains invalid path
  path = sub8
  url = http://github.com

[submodule ""S9""]             # sub9/.git points to directory that does not exist
  path = sub9
  url = http://github.com

[submodule ""S10""]            # sub10/.git points to directory that has commondir directory (it should be a file)
  path = sub10
  url = http://github.com

[submodule ""S11""]            # trailing whitespace in path
  path = sub11
  url = http://github.com
");
            var repository = new GitRepository(GitEnvironment.Empty, GitConfig.Empty, gitDir.Path, gitDir.Path, workingDir.Path);

            var submodules = repository.GetSubmodules();
            Assert.Empty(submodules);

            var diagnostics = repository.GetSubmoduleDiagnostics();
            AssertEx.Equal(new[]
            {
              // The path of submodule 'S1' is missing or invalid: '  '
              string.Format(Resources.InvalidSubmodulePath, "S1", "  "),
              // The path of submodule 'S2' is missing or invalid: ''
              string.Format(Resources.InvalidSubmodulePath, "S2", ""),
              // The format of the file 'sub7\.git' is invalid.
              string.Format(Resources.FormatOfFileIsInvalid, Path.Combine(workingDir.Path, "sub7", ".git")),
              // Path specified in file 'sub8\.git' is invalid.
              string.Format(Resources.PathSpecifiedInFileIsInvalid, Path.Combine(workingDir.Path, "sub8", ".git"), "\0<>")
            }, diagnostics);
        }

        [Fact]
        public void GetHeadCommitSha()
        {
            using var temp = new TempRoot();

            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");
            refsHeadsDir.CreateFile("master").WriteAllText("0000000000000000000000000000000000000000 \t\v\r\n");

            var gitDir = temp.CreateDirectory();
            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master \t\v\r\n");

            var repository = new GitRepository(GitEnvironment.Empty, GitConfig.Empty, gitDir.Path, commonDir.Path, workingDirectory: null);
            Assert.Equal("0000000000000000000000000000000000000000", repository.GetHeadCommitSha());
        }

        [Fact]
        public void GetSubmoduleHeadCommitSha()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();
            var workingDir = temp.CreateDirectory();

            var submoduleGitDir = temp.CreateDirectory();

            var submoduleWorkingDir = workingDir.CreateDirectory("sub").CreateDirectory("abc");
            submoduleWorkingDir.CreateFile(".git").WriteAllText("gitdir: " + submoduleGitDir.Path + "\t \v\f\r\n\n\r");

            var submoduleRefsHeadsDir = submoduleGitDir.CreateDirectory("refs").CreateDirectory("heads");
            submoduleRefsHeadsDir.CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");
            submoduleGitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master");

            Assert.Equal("0000000000000000000000000000000000000000",
                GitRepository.GetSubmoduleReferenceResolver(submoduleWorkingDir.Path)?.ResolveHeadReference());
        }

        [Fact]
        public void GetOldStyleSubmoduleHeadCommitSha()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();
            var workingDir = temp.CreateDirectory();

            // this is a unusual but legal case which can occur with older versions of Git or other tools.
            // see https://git-scm.com/docs/gitsubmodules#_forms for more details.
            var oldStyleSubmoduleWorkingDir = workingDir.CreateDirectory("old-style-submodule");
            var oldStyleSubmoduleGitDir = oldStyleSubmoduleWorkingDir.CreateDirectory(".git");
            var oldStyleSubmoduleRefsHeadDir = oldStyleSubmoduleGitDir.CreateDirectory("refs").CreateDirectory("heads");
            oldStyleSubmoduleRefsHeadDir.CreateFile("branch1").WriteAllText("1111111111111111111111111111111111111111");
            oldStyleSubmoduleGitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/branch1");

            Assert.Equal("1111111111111111111111111111111111111111",
                GitRepository.GetSubmoduleReferenceResolver(oldStyleSubmoduleWorkingDir.Path)?.ResolveHeadReference());
        }

        [Fact]
        public void GetSubmoduleHeadCommitSha_NoGitFile()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();
            var workingDir = temp.CreateDirectory();

            var submoduleGitDir = temp.CreateDirectory();
            var submoduleWorkingDir = workingDir.CreateDirectory("sub").CreateDirectory("abc");

            Assert.Null(GitRepository.GetSubmoduleReferenceResolver(submoduleWorkingDir.Path)?.ResolveHeadReference());
        }
    }
}
