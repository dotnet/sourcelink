// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Collections.Generic;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class LocateRepositoryTests
    {
        /// <summary>
        /// Verifies historical behavior is preserved: a fully-qualified <see cref="LocateRepository.Path"/>
        /// pointing at a git repository is located and its outputs are populated.
        /// </summary>
        [Fact]
        public void AbsolutePath_LocatesRepository()
        {
            using var temp = new TempRoot();
            var (repoDir, gitDir) = CreateMinimalRepository(temp);

            var engine = new MockEngine();
            var task = new LocateRepository
            {
                BuildEngine = engine,
                NoWarnOnMissingInfo = true,
                Path = repoDir.Path,
            };

            Assert.True(task.Execute(), engine.Log);
            Assert.Equal(repoDir.Path, task.WorkingDirectory);
            Assert.Equal(gitDir.Path, task.RepositoryId);
        }

        /// <summary>
        /// Verifies that a non-fully-qualified (here: relative) <see cref="LocateRepository.Path"/> is rejected.
        ///
        /// This is the multithreaded (MT) task model behavior: the process CWD is shared across projects, so it
        /// must never influence repository discovery. The migrated task requires a fully-qualified path; a relative
        /// path (<c>"."</c>) is rejected and reported as the "missing repository" warning instead of being resolved
        /// against the process current working directory.
        /// </summary>
        [Fact]
        public void RelativePath_IsRejected()
        {
            var engine = new MockEngine();
            var task = new LocateRepository
            {
                BuildEngine = engine,
                Path = ".",
            };

            // Not an error: repository discovery degrades to a warning.
            Assert.True(task.Execute(), engine.Log);
            Assert.Null(task.WorkingDirectory);
            Assert.Contains("WARNING", engine.Log);
        }

        [Fact]
        public void RevisionTimestamp_IsUtcRfc3339()
        {
            using var temp = new TempRoot();
            var repoDir = temp.CreateDirectory();

            var init = ProcessUtilities.Run("git", "init --quiet", repoDir.Path);
            Assert.Equal(0, init.ExitCode);

            var commit = ProcessUtilities.Run(
                "git",
                "commit --quiet --allow-empty -m timestamp",
                repoDir.Path,
                new[]
                {
                    new KeyValuePair<string, string>("GIT_AUTHOR_NAME", "Test"),
                    new KeyValuePair<string, string>("GIT_AUTHOR_EMAIL", "test@example.com"),
                    new KeyValuePair<string, string>("GIT_COMMITTER_NAME", "Test"),
                    new KeyValuePair<string, string>("GIT_COMMITTER_EMAIL", "test@example.com"),
                    new KeyValuePair<string, string>("GIT_COMMITTER_DATE", "2020-01-02T03:04:05+02:30"),
                });
            Assert.Equal(0, commit.ExitCode);

            var engine = new MockEngine();
            var task = new LocateRepository
            {
                BuildEngine = engine,
                NoWarnOnMissingInfo = true,
                Path = repoDir.Path,
            };

            Assert.True(task.Execute(), engine.Log);
            Assert.Equal("2020-01-02T00:34:05Z", task.RevisionTimestamp);
        }

        private static (TempDirectory repoDir, TempDirectory gitDir) CreateMinimalRepository(TempRoot temp)
        {
            var repoDir = temp.CreateDirectory();
            var gitDir = repoDir.CreateDirectory(".git");
            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/main\n");
            gitDir.CreateFile("config").WriteAllText("");
            return (repoDir, gitDir);
        }
    }
}
