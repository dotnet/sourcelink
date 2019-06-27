// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
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
        public void LocateRepository_Worktree()
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
            var worktreeGitFile = worktreeDir.CreateFile(".git").WriteAllText("gitdir: " + worktreeGitDir);

            worktreeGitDir.CreateFile("HEAD");
            worktreeGitDir.CreateFile("commondir").WriteAllText(mainGitDir.Path);
            worktreeGitDir.CreateFile("gitdir").WriteAllText(worktreeGitFile.Path);

            // start under main repository directory:
            Assert.True(GitRepository.LocateRepository(
                mainWorkingSubDir.Path,
                out var locatedGitDirectory,
                out var locatedCommonDirectory,
                out var locatedWorkingDirectory));

            Assert.Equal(mainGitDir.Path, locatedGitDirectory);
            Assert.Equal(mainGitDir.Path, locatedCommonDirectory);
            Assert.Equal(mainWorkingDir.Path, locatedWorkingDirectory);

            // start at main git directory (git config works from this dir, but git status requires work dir):
            Assert.True(GitRepository.LocateRepository(
                mainGitDir.Path,
                out locatedGitDirectory,
                out locatedCommonDirectory,
                out locatedWorkingDirectory));

            Assert.Equal(mainGitDir.Path, locatedGitDirectory);
            Assert.Equal(mainGitDir.Path, locatedCommonDirectory);
            Assert.Null(locatedWorkingDirectory);

            // start under worktree directory:
            Assert.True(GitRepository.LocateRepository(
                worktreeSubDir.Path,
                out locatedGitDirectory,
                out locatedCommonDirectory,
                out locatedWorkingDirectory));

            Assert.Equal(worktreeGitDir.Path, locatedGitDirectory);
            Assert.Equal(mainGitDir.Path, locatedCommonDirectory);
            Assert.Equal(worktreeDir.Path, locatedWorkingDirectory);

            // start under worktree git directory (git config works from this dir, but git status requires work dir):
            Assert.True(GitRepository.LocateRepository(
                worktreeGitSubDir.Path,
                out locatedGitDirectory,
                out locatedCommonDirectory,
                out locatedWorkingDirectory));

            Assert.Equal(worktreeGitDir.Path, locatedGitDirectory);
            Assert.Equal(mainGitDir.Path, locatedCommonDirectory);
            Assert.Null(locatedWorkingDirectory);
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
            Assert.True(GitRepository.LocateRepository(
                submoduleWorkDir.Path,
                out var locatedGitDirectory,
                out var locatedCommonDirectory,
                out var locatedWorkingDirectory));

            Assert.Equal(submoduleGitDir.Path, locatedGitDirectory);
            Assert.Equal(submoduleGitDir.Path, locatedCommonDirectory);
            Assert.Equal(submoduleWorkDir.Path, locatedWorkingDirectory);

            // start under submodule git directory:
            Assert.True(GitRepository.LocateRepository(
                submoduleGitDir.Path,
                out locatedGitDirectory,
                out locatedCommonDirectory,
                out locatedWorkingDirectory));

            Assert.Equal(submoduleGitDir.Path, locatedGitDirectory);
            Assert.Equal(submoduleGitDir.Path, locatedCommonDirectory);
            Assert.Null(locatedWorkingDirectory);
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

            var repository = GitRepository.OpenRepository(src.Path, new GitEnvironment(homeDir.Path));

            Assert.Equal(gitDir.Path, repository.CommonDirectory);
            Assert.Equal(gitDir.Path, repository.GitDirectory);
            Assert.Equal("1", repository.Config.GetVariableValue("x", "a"));
            Assert.Empty(repository.GetSubmodules());
            Assert.Equal("0000000000000000000000000000000000000000", repository.GetHeadCommitSha());
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

            gitDir.CreateFile("config").WriteAllText("[core]repositoryformatversion = 1");

            var src = workingDir.CreateDirectory("src");

            Assert.Throws<NotSupportedException>(() => GitRepository.OpenRepository(src.Path, new GitEnvironment(homeDir.Path)));
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

            var submodules = GitRepository.EnumerateSubmoduleConfig(repository.ReadSubmoduleConfig());
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
            workingDir.CreateDirectory("sub9").CreateFile(".git").WriteAllText("gitdir: ../.git/modules/sub9");
            workingDir.CreateDirectory("sub10").CreateFile(".git").WriteAllText("gitdir: ../.git/modules/sub10");

            workingDir.CreateFile(".gitmodules").WriteAllText(@"
[submodule ""S1""]             # whitespace-only path
  path = ""  ""
  url = http://github.com

[submodule ""S2""]             # empty path
  path =                  
  url = http://github.com

[submodule ""S4""]             # invalid path
  path = sub<>
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
");
            var repository = new GitRepository(GitEnvironment.Empty, GitConfig.Empty, gitDir.Path, gitDir.Path, workingDir.Path);

            var submodules = repository.GetSubmodules();
            AssertEx.Equal(new[]
            {
                "S10: 'sub10' 'http://github.com'",
                "S9: 'sub9' 'http://github.com'"
            }, submodules.Select(s => $"{s.Name}: '{s.WorkingDirectoryRelativePath}' '{s.Url}'"));

            var diagnostics = repository.GetSubmoduleDiagnostics();
            AssertEx.Equal(new[]
            {
              // The path of submodule 'S1' is missing or invalid: '  '
              string.Format(Resources.InvalidSubmodulePath, "S1", "  "),
              // The path of submodule 'S2' is missing or invalid: ''
              string.Format(Resources.InvalidSubmodulePath, "S2", ""),
              // Could not find a part of the path 'sub3\.git'.
              TestUtilities.GetExceptionMessage(() => File.ReadAllText(Path.Combine(workingDir.Path, "sub3", ".git"))),
              // The URL of submodule 'S4' is missing or invalid: '   '
              string.Format(Resources.InvalidSubmoduleUrl, "S4", "   "),
              // Could not find a part of the path 'sub5\.git'.
              TestUtilities.GetExceptionMessage(() => File.ReadAllText(Path.Combine(workingDir.Path, "sub5", ".git"))),
              // Access to the path 'sub6\.git' is denied
              TestUtilities.GetExceptionMessage(() => File.ReadAllText(Path.Combine(workingDir.Path, "sub6", ".git"))),
              // The format of the file 'sub7\.git' is invalid.
              string.Format(Resources.FormatOfFileIsInvalid, Path.Combine(workingDir.Path, "sub7", ".git")),
              // Path specified in file 'sub8\.git' is invalid.
              string.Format(Resources.PathSpecifiedInFileIsInvalid, Path.Combine(workingDir.Path, "sub8", ".git"))
            }, diagnostics);
        }

        [Fact]
        public void ResolveReference()
        {
            using var temp = new TempRoot();

            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");

            refsHeadsDir.CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");
            refsHeadsDir.CreateFile("br1").WriteAllText("ref: refs/heads/br2");
            refsHeadsDir.CreateFile("br2").WriteAllText("ref: refs/heads/master");

            Assert.Equal("0123456789ABCDEFabcdef000000000000000000", GitRepository.ResolveReference("0123456789ABCDEFabcdef000000000000000000", commonDir.Path));

            Assert.Equal("0000000000000000000000000000000000000000", GitRepository.ResolveReference("ref: refs/heads/master", commonDir.Path));
            Assert.Equal("0000000000000000000000000000000000000000", GitRepository.ResolveReference("ref: refs/heads/br1", commonDir.Path));
            Assert.Equal("0000000000000000000000000000000000000000", GitRepository.ResolveReference("ref: refs/heads/br2", commonDir.Path));

            // branch without commits (emtpy repository) will have not file in refs/heads:
            Assert.Null(GitRepository.ResolveReference("ref: refs/heads/none", commonDir.Path));

            Assert.Null(GitRepository.ResolveReference("ref: refs/heads/rec1   ", commonDir.Path));
            Assert.Null(GitRepository.ResolveReference("ref: refs/heads/none" + string.Join("/", Path.GetInvalidPathChars()), commonDir.Path));
        }

        [Fact]
        public void ResolveReference_Errors()
        {
            using var temp = new TempRoot();

            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");

            refsHeadsDir.CreateFile("rec1").WriteAllText("ref: refs/heads/rec2");
            refsHeadsDir.CreateFile("rec2").WriteAllText("ref: refs/heads/rec1");

            Assert.Throws<InvalidDataException>(() => GitRepository.ResolveReference("ref: refs/heads/rec1", commonDir.Path));
            Assert.Throws<InvalidDataException>(() => GitRepository.ResolveReference("ref: xyz/heads/rec1", commonDir.Path));
            Assert.Throws<InvalidDataException>(() => GitRepository.ResolveReference("ref:refs/heads/rec1", commonDir.Path));
            Assert.Throws<InvalidDataException>(() => GitRepository.ResolveReference("refs/heads/rec1", commonDir.Path));
            Assert.Throws<InvalidDataException>(() => GitRepository.ResolveReference(new string('0', 39), commonDir.Path));
            Assert.Throws<InvalidDataException>(() => GitRepository.ResolveReference(new string('0', 41), commonDir.Path));
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
            submoduleWorkingDir.CreateFile(".git").WriteAllText("gitdir: " + submoduleGitDir.Path);

            var submoduleRefsHeadsDir = submoduleGitDir.CreateDirectory("refs").CreateDirectory("heads");
            submoduleRefsHeadsDir.CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");
            submoduleGitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master");

            var repository = new GitRepository(GitEnvironment.Empty, GitConfig.Empty, gitDir.Path, gitDir.Path, workingDir.Path);
            Assert.Equal("0000000000000000000000000000000000000000", repository.GetSubmoduleHeadCommitSha(submoduleWorkingDir.Path));
        }
    }
}
