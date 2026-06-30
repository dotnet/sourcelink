// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class LocateRepositoryTests
    {
        /// <summary>
        /// Verifies that <see cref="LocateRepository"/> resolves a relative <see cref="LocateRepository.Path"/>
        /// against the task's <see cref="TaskEnvironment"/> project directory rather than the process current
        /// working directory. This is the multithreaded (MT) task model behavior: the original implementation
        /// resolved the initial path with <c>Path.GetFullPath</c>, which is process-CWD-dependent.
        ///
        /// The test sets the process CWD to a decoy directory that contains no git repository, points the task's
        /// project directory at a real (minimal) git repository, and passes a relative <c>Path</c>. If the
        /// migration is reverted (relative path resolved against the decoy CWD), the repository is not found and
        /// the outputs would not point at the project-directory repository, failing the assertions below.
        /// </summary>
        [Fact]
        public void RelativePath_ResolvesAgainstTaskEnvironmentProjectDirectory_NotProcessCwd()
        {
            using var temp = new TempRoot();

            // A real, minimal git repository whose working directory is repoDir.
            var repoDir = temp.CreateDirectory();
            var gitDir = repoDir.CreateDirectory(".git");
            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/main\n");
            gitDir.CreateFile("config").WriteAllText("");

            // A decoy directory that is NOT inside any git repository.
            var decoyDir = temp.CreateDirectory();

            var originalCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                // Point the process CWD at the decoy. A CWD-dependent (pre-migration) implementation would
                // resolve the relative path against this directory and fail to locate the repository.
                Directory.SetCurrentDirectory(decoyDir.Path);

                var engine = new MockEngine();
                var task = new LocateRepository
                {
                    BuildEngine = engine,
                    NoWarnOnMissingInfo = true,

                    // Resolve relative paths against the repository directory, emulating how MSBuild supplies
                    // the task environment in the multithreaded model.
                    TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(
                        repoDir.Path,
                        new Dictionary<string, string>()),

                    // Relative path: only resolves to the repository when combined with the project directory.
                    Path = ".",
                };

                Assert.True(task.Execute(), engine.Log);

                Assert.Equal(repoDir.Path, task.WorkingDirectory);
                Assert.Equal(gitDir.Path, task.RepositoryId);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }
        }
    }
}
