// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections;
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
        /// Verifies historical behavior is preserved: a fully-qualified <see cref="LocateRepository.Path"/>
        /// pointing at a git repository is located and its outputs are populated.
        /// </summary>
        [Fact]
        public void AbsolutePath_LocatesRepository()
        {
            using var temp = new TempRoot();
            var (repoDir, gitDir) = CreateMinimalRepository(temp);

            var engine = new CachingMockEngine();
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
        /// Verifies that a non-fully-qualified (here: relative) <see cref="LocateRepository.Path"/> is rejected
        /// rather than resolved against the process current working directory.
        ///
        /// This is the multithreaded (MT) task model behavior: the process CWD is shared across projects, so it
        /// must never influence repository discovery. The test sets the CWD to a real git repository and passes a
        /// relative path (<c>"."</c>). The pre-migration implementation resolved the initial path against the CWD
        /// (via <c>Path.GetFullPath</c>) and would have located that repository; the migrated task rejects the
        /// relative path and reports the "missing repository" warning instead. Reverting the migration fails this
        /// test.
        /// </summary>
        [Fact]
        public void RelativePath_IsRejected_IndependentOfProcessCwd()
        {
            using var temp = new TempRoot();
            var (repoDir, _) = CreateMinimalRepository(temp);

            var originalCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                // A CWD-dependent (pre-migration) implementation would resolve "." against this repository.
                Directory.SetCurrentDirectory(repoDir.Path);

                var engine = new CachingMockEngine();
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
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }
        }

        private static (TempDirectory repoDir, TempDirectory gitDir) CreateMinimalRepository(TempRoot temp)
        {
            var repoDir = temp.CreateDirectory();
            var gitDir = repoDir.CreateDirectory(".git");
            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/main\n");
            gitDir.CreateFile("config").WriteAllText("");
            return (repoDir, gitDir);
        }

        /// <summary>
        /// Minimal <see cref="IBuildEngine4"/> used only because <see cref="RepositoryTask"/> caches the located
        /// repository via <c>BuildEngine4.Register/GetRegisteredTaskObject</c> (pre-existing behavior). The cache
        /// misses on every lookup here, so each task instance performs a fresh lookup.
        /// </summary>
        private sealed class CachingMockEngine : IBuildEngine4
        {
            private readonly System.Text.StringBuilder _log = new();
            private readonly Dictionary<object, object?> _registeredTaskObjects = new();

            public string Log => _log.ToString();

            public void LogErrorEvent(BuildErrorEventArgs e) => _log.AppendLine("ERROR: " + e.Message);
            public void LogWarningEvent(BuildWarningEventArgs e) => _log.AppendLine("WARNING: " + e.Message);
            public void LogMessageEvent(BuildMessageEventArgs e) => _log.AppendLine(e.Message);
            public void LogCustomEvent(CustomBuildEventArgs e) => _log.AppendLine(e.Message);

            public string ProjectFileOfTaskNode => "";
            public int ColumnNumberOfTaskNode => 0;
            public int LineNumberOfTaskNode => 0;
            public bool ContinueOnError => true;
            public bool IsRunningMultipleNodes => false;

            public void RegisterTaskObject(object key, object? obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
                => _registeredTaskObjects[key] = obj;

            public object? GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
                => _registeredTaskObjects.TryGetValue(key, out var value) ? value : null;

            public object? UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            {
                _registeredTaskObjects.TryGetValue(key, out var value);
                _registeredTaskObjects.Remove(key);
                return value;
            }

            public void Yield() { }
            public void Reacquire() { }

            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
                => throw new NotImplementedException();
            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
                => throw new NotImplementedException();
            public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
                => throw new NotImplementedException();
            public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
                => throw new NotImplementedException();
        }
    }
}
