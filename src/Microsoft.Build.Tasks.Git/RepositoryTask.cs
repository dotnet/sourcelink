// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    public abstract class RepositoryTask : Task
    {
#if NET461
        static RepositoryTask() => AssemblyResolver.Initialize();
#endif
        private static readonly string s_cacheKeyPrefix = "3AE29AB7-AE6B-48BA-9851-98A15ED51C94:";

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
            var repository = GetOrCreateRepositoryInstance();
            if (repository == null)
            {
                // error has already been reported
                return;
            }

            try
            {
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
            try
            {
                if (!GitRepository.TryFindRepository(initialPath, out location))
                {
                    Log.LogWarning(Resources.UnableToLocateRepository, initialPath);
                    return null;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is NotSupportedException)
            {
                Log.LogError(Resources.ErrorReadingGitRepositoryInformation, e.Message);
                return null;
            }

            var cacheKey = GetCacheKey(location.GitDirectory);
            if (TryGetCachedRepositoryInstance(cacheKey, requireCached: false, out repository))
            {
                return repository;
            }

            try
            {
                // TODO: configure environment
                repository = GitRepository.OpenRepository(location, GitEnvironment.CreateFromProcessEnvironment());
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
            => s_cacheKeyPrefix + repositoryId;

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
