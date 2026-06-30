// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GetUntrackedFilesTests
    {
        /// <summary>
        /// Minimal <see cref="IBuildEngine4"/> implementation. <see cref="RepositoryTask"/> caches the
        /// resolved repository via <c>BuildEngine4.RegisterTaskObject</c>/<c>GetRegisteredTaskObject</c>,
        /// so the shared <see cref="TestUtilities.MockEngine"/> (which only implements <see cref="IBuildEngine"/>)
        /// is insufficient for running the full task.
        /// </summary>
        private sealed class MockEngine4 : IBuildEngine4
        {
            private readonly Dictionary<object, object?> _taskObjects = new();

            public List<string> Warnings { get; } = new();
            public List<string> Errors { get; } = new();

            public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e.Message ?? "");
            public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e.Message ?? "");
            public void LogMessageEvent(BuildMessageEventArgs e) { }
            public void LogCustomEvent(CustomBuildEventArgs e) { }

            public bool ContinueOnError => false;
            public int LineNumberOfTaskNode => 0;
            public int ColumnNumberOfTaskNode => 0;
            public string ProjectFileOfTaskNode => "";
            public bool IsRunningMultipleNodes => false;

            public void RegisterTaskObject(object key, object? obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
                => _taskObjects[key] = obj;
            public object? GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
                => _taskObjects.TryGetValue(key, out var value) ? value : null;
            public object? UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            {
                _taskObjects.TryGetValue(key, out var value);
                _taskObjects.Remove(key);
                return value;
            }

            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => throw new NotImplementedException();
            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => throw new NotImplementedException();
            public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) => throw new NotImplementedException();
            public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs) => throw new NotImplementedException();
            public void Yield() { }
            public void Reacquire() { }
        }

        /// <summary>
        /// Sets the process current working directory and restores it on dispose. The current working
        /// directory is process-global, so this test mutates shared state; keeping the mutation window
        /// tightly scoped (and restoring in a finally/using) keeps it robust.
        /// </summary>
        private readonly struct CurrentDirectorySwitcher : IDisposable
        {
            private readonly string _previous;
            public CurrentDirectorySwitcher(string directory)
            {
                _previous = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(directory);
            }
            public void Dispose() => Directory.SetCurrentDirectory(_previous);
        }

        /// <summary>
        /// MT migration regression test (decoy-CWD pattern). The task's <see cref="GetUntrackedFiles.ProjectDirectory"/>
        /// and its <see cref="GetUntrackedFiles.Files"/> are specified relative; the process current working
        /// directory points at an unrelated "decoy" directory that is NOT a git repository. With the
        /// multithreaded migration in place, both repository discovery (in the base <see cref="RepositoryTask"/>)
        /// and the project-directory used to resolve the file item specs are resolved against the task's
        /// <see cref="TaskEnvironment"/> project directory rather than the process CWD.
        ///
        /// This single assertion catches reverting either half of the migration:
        ///  - revert the base <see cref="RepositoryTask"/> change: discovery resolves "." against the decoy CWD,
        ///    no repository is found, and <see cref="GetUntrackedFiles.UntrackedFiles"/> stays null.
        ///  - revert the call-site absolutization in <see cref="GetUntrackedFiles"/>: the (now relative)
        ///    project directory combines with the file item specs against the decoy CWD, placing every file
        ///    outside the repository working tree so both files are reported as untracked.
        /// Only the fully migrated code reports exactly the ignored file.
        /// </summary>
        [Fact]
        public void Execute_ResolvesPathsAgainstTaskEnvironmentProjectDirectoryNotCurrentDirectory()
        {
            using var temp = new TempRoot();

            // A real on-disk git repository that ignores 'ignored_file.cs' but not 'included_file.cs'.
            var repoDir = temp.CreateDirectory();
            var gitDir = repoDir.CreateDirectory(".git");
            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master");
            gitDir.CreateFile("config").WriteAllText("");
            gitDir.CreateDirectory("objects");
            gitDir.CreateDirectory("refs").CreateDirectory("heads").CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");

            repoDir.CreateFile(".gitignore").WriteAllText("ignored_file.cs\n");
            repoDir.CreateFile("ignored_file.cs").WriteAllText("");
            repoDir.CreateFile("included_file.cs").WriteAllText("");

            // A separate decoy directory that is NOT a git repository.
            var decoyDir = temp.CreateDirectory();
            decoyDir.CreateFile("ignored_file.cs").WriteAllText("");
            decoyDir.CreateFile("included_file.cs").WriteAllText("");

            var engine = new MockEngine4();
            var task = new GetUntrackedFiles
            {
                BuildEngine = engine,
                // 'local' scope avoids reading the machine's global/system git configuration, keeping the test deterministic.
                ConfigurationScope = "local",
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(repoDir.Path, new Dictionary<string, string>()),
                // Relative on purpose: resolves to repoDir against the TaskEnvironment project dir, but to the decoy against the CWD.
                ProjectDirectory = ".",
                Files = new ITaskItem[]
                {
                    new MockItem("ignored_file.cs"),
                    new MockItem("included_file.cs"),
                },
            };

            bool result;
            using (new CurrentDirectorySwitcher(decoyDir.Path))
            {
                result = task.Execute();
            }

            Assert.True(result);
            Assert.Empty(engine.Errors);
            // The repository was located via the TaskEnvironment project directory (repoDir). Had resolution
            // used the decoy current working directory, no repository would be found and this would be null.
            Assert.NotNull(task.UntrackedFiles);

            // Only the file ignored by the repo at the TaskEnvironment project directory is untracked.
            // The original (relative) ItemSpec is preserved on the output item.
            AssertEx.Equal(
                new[] { MockItem.AdjustSeparators("ignored_file.cs") },
                task.UntrackedFiles.Select(item => item.ItemSpec));
        }
    }
}
