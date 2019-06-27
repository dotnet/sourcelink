// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    public abstract class RepositoryTask : Task
    {
        private static readonly string s_cacheKeyPrefix = "3AE29AB7-AE6B-48BA-9851-98A15ED51C94:";

        /// <summary>
        /// Sets the scope of git repository configuration. By default (no scope specified) configuration is read from environment variables
        /// and system and global user git/ssh configuration files.
        /// 
        /// Supported values:
        /// If "local" is specified the configuration is only read from the configuration files local to the repository (or work tree).
        /// </summary>
        public string ConfigurationScope { get; set; }

#if NET461
        static RepositoryTask() => AssemblyResolver.Initialize();
#endif

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

        private protected abstract void Execute(GitRepository repository);

        protected abstract string GetRepositoryId();
        protected abstract string GetInitialPath();

        [MethodImpl(MethodImplOptions.NoInlining)]
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
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is NotSupportedException)
            {
                Log.LogError(Resources.ErrorReadingGitRepositoryInformation, e.Message);
            }
        }

        private GitRepository GetOrCreateRepositoryInstance()
        {
            GitRepository repository;

            var repositoryId = GetRepositoryId();
            if (repositoryId != null)
            {
                if (TryGetCachedRepositoryInstance(GetCacheKey(repositoryId), requireCached: true, out repository))
                {
                    return repository;
                }

                return null;
            }

            var initialPath = GetInitialPath();

            GitRepositoryLocation location;
            if (!GitRepository.TryFindRepository(initialPath, out location))
            {
                Log.LogWarning(Resources.UnableToLocateRepository, initialPath);
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
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is NotSupportedException)
            {
                Log.LogError(Resources.ErrorReadingGitRepositoryInformation, e.Message);
                repository = null;
            }

            if (repository?.WorkingDirectory == null)
            {
                Log.LogWarning(Resources.UnableToLocateRepository, initialPath);
                repository = null;
            }

            CacheRepositoryInstance(cacheKey, repository);

            return repository;
        }

        private string GetCacheKey(string repositoryId)
            => s_cacheKeyPrefix + (string.IsNullOrEmpty(ConfigurationScope) ? "*" : ConfigurationScope) + ":" + repositoryId;

        private bool TryGetCachedRepositoryInstance(string cacheKey, bool requireCached, out GitRepository repository)
        {
            var entry = (StrongBox<GitRepository>)BuildEngine4.GetRegisteredTaskObject(cacheKey, RegisteredTaskObjectLifetime.Build);

            if (entry != null)
            {
                Log.LogMessage(MessageImportance.Low, $"SourceLink: Reusing cached git repository information.");
                repository = entry.Value;
                return repository != null;
            }

            if (requireCached)
            {
                Log.LogError($"SourceLink: Repository instance not found in cache: '{cacheKey.Substring(s_cacheKeyPrefix.Length)}'");
            }

            repository = null;
            return false;
        }

        private void CacheRepositoryInstance(string cacheKey, GitRepository repository)
        {
            BuildEngine4.RegisterTaskObject(
                  cacheKey,
                  new StrongBox<GitRepository>(repository),
                  RegisteredTaskObjectLifetime.Build,
                  allowEarlyCollection: true);
        }
    }
}
