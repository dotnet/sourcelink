// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    public abstract class RepositoryTask : Task, IMultiThreadableTask
    {
        /// <summary>
        /// Provides project-directory-relative path resolution and environment access that is safe
        /// to use when the task runs on the MSBuild multithreaded execution model (no reliance on the
        /// process current working directory). Defaults to <see cref="TaskEnvironment.Fallback"/> so that
        /// existing single-process callers and tests that do not set it preserve the original CWD-based
        /// semantics.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        private sealed class RepositoryContainer(GitRepository? repository) : IDisposable
        {
            public GitRepository? Repository
                => repository;

            public void Dispose()
                => repository?.Dispose();
        }

        /// <summary>
        /// Sets the scope of git repository configuration. By default (no scope specified) configuration is read from environment variables
        /// and system and global user git/ssh configuration files.
        /// 
        /// Supported values:
        /// If "local" is specified the configuration is only read from the configuration files local to the repository (or work tree).
        /// </summary>
        public string? ConfigurationScope { get; set; }

#if NET461
        static RepositoryTask() => AssemblyResolver.Initialize();
#endif

        /// <summary>
        /// True to report a warning when the repository can't be located, it's missing remote or a commit.
        /// </summary>
        public bool NoWarnOnMissingInfo { get; set; }

        public sealed override bool Execute()
        {
#if NET461
            bool logAssemblyLoadingErrors()
            {
                foreach (var message in AssemblyResolver.GetLog())
                {
                    Log.LogMessage(message);
                }
                return false;
            }

            try
            {
                ExecuteImpl();
            }
            catch when (logAssemblyLoadingErrors())
            {
            }
#else
            ExecuteImpl();
#endif

            return !Log.HasLoggedErrors;
        }

        private void ReportMissingRepositoryWarning(string initialPath)
        {
            if (!NoWarnOnMissingInfo)
            {
                Log.LogWarning(Resources.UnableToLocateRepository, initialPath);
            }
        }

        private protected abstract void Execute(GitRepository repository);

        protected abstract string? GetRepositoryId();
        protected abstract string GetInitialPath();

        private void ExecuteImpl()
        {
            try
            {
                var repository = GetOrCreateRepositoryInstance();
                if (repository == null)
                {
                    // error has already been reported
                    return;
                }

                Execute(repository);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException)
            {
                Log.LogError(Resources.ErrorReadingGitRepositoryInformation, e.Message);
            }
        }

        private GitRepository? GetOrCreateRepositoryInstance()
        {
            GitRepository? repository;

            var repositoryId = GetRepositoryId();
            if (repositoryId != null)
            {
                if (TryGetCachedRepositoryInstance(GetCacheKey(repositoryId), requireCached: true, out repository))
                {
                    return repository;
                }

                return null;
            }

            // 'initialPath' is kept as the ORIGINAL (possibly relative) string for diagnostics (Sin 2).
            // 'absoluteInitialPath' is resolved against the task's project directory (via TaskEnvironment)
            // rather than the process current working directory, making repository discovery MT-safe.
            // Passing an already-absolute path into TryFindRepository (which calls Path.GetFullPath
            // internally) preserves canonicalization and avoids any CWD dependency.
            // Note: for tasks whose initial path is [Required, NotNull] (e.g. ProjectDirectory), an empty
            // string causes GetAbsolutePath("") to throw ArgumentException, matching the prior
            // Path.GetFullPath("") behavior.
            var initialPath = GetInitialPath();
            AbsolutePath absoluteInitialPath = TaskEnvironment.GetAbsolutePath(initialPath);

            if (!GitRepository.TryFindRepository(absoluteInitialPath, out var location))
            {
                ReportMissingRepositoryWarning(initialPath);
                return null;
            }

            var cacheKey = GetCacheKey(location.GitDirectory);
            if (TryGetCachedRepositoryInstance(cacheKey, requireCached: false, out repository))
            {
                return repository;
            }

            try
            {
                repository = GitRepository.OpenRepository(location, GitEnvironment.Create(ConfigurationScope));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException)
            {
                Log.LogError(Resources.ErrorReadingGitRepositoryInformation, e.Message);
                repository = null;
            }

            if (repository?.WorkingDirectory == null)
            {
                ReportMissingRepositoryWarning(initialPath);
                repository = null;
            }

            CacheRepositoryInstance(cacheKey, repository);

            return repository;
        }

        private Tuple<Type, string> GetCacheKey(string repositoryId)
            => new(typeof(RepositoryTask), (string.IsNullOrEmpty(ConfigurationScope) ? "*" : ConfigurationScope) + ":" + repositoryId);

        private bool TryGetCachedRepositoryInstance(Tuple<Type, string> cacheKey, bool requireCached, [NotNullWhen(true)]out GitRepository? repository)
        {
            var entry = (RepositoryContainer?)BuildEngine4.GetRegisteredTaskObject(cacheKey, RegisteredTaskObjectLifetime.Build);
            if (entry != null)
            {
                Log.LogMessage(MessageImportance.Low, $"SourceLink: Reusing cached git repository information.");
                repository = entry.Repository;
                return repository != null;
            }

            var message = $"SourceLink: Repository instance not found in cache: '{cacheKey.Item2}'";
            if (requireCached)
            {
                Log.LogError(message);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, message);
            }

            repository = null;
            return false;
        }

        private void CacheRepositoryInstance(Tuple<Type, string> cacheKey, GitRepository? repository)
        {
            BuildEngine4.RegisterTaskObject(
                  cacheKey,
                  new RepositoryContainer(repository),
                  RegisteredTaskObjectLifetime.Build,
                  allowEarlyCollection: true);
        }
    }
}
